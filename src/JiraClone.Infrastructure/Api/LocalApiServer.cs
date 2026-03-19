using System.Net;
using System.Text;
using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Issues;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Api;

public sealed class LocalApiServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocalApiServer> _logger;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;
    private bool _started;

    public LocalApiServer(IServiceScopeFactory scopeFactory, ILogger<LocalApiServer>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger ?? NullLogger<LocalApiServer>.Instance;
        _listener.Prefixes.Add("http://127.0.0.1:47892/");
        _listener.Prefixes.Add("http://localhost:47892/");
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_started)
        {
            return Task.CompletedTask;
        }

        try
        {
            _listener.Start();
            _started = true;
            _serverTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            _logger.LogInformation("Local API server started on http://localhost:47892.");
        }
        catch (HttpListenerException exception)
        {
            _logger.LogWarning(exception, "Local API server could not start on port 47892. External API access is disabled for this session.");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _cts.Cancel();
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        if (_serverTask is null)
        {
            return;
        }

        try
        {
            await _serverTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestSafeAsync(context, cancellationToken), cancellationToken);
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug(exception, "Local API server listener loop stopped.");
                }

                break;
            }
        }
    }

    private async Task HandleRequestSafeAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            await HandleRequestAsync(context, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled error while processing local API request {Method} {Path}.", context.Request.HttpMethod, context.Request.Url?.AbsolutePath);
            await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new { error = "An unexpected server error occurred." }, cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;
        if (string.Equals(path, "/api/v1/issues", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            await HandleGetIssuesAsync(context, cancellationToken);
            return;
        }

        if (string.Equals(path, "/api/v1/issues", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await HandleCreateIssueAsync(context, cancellationToken);
            return;
        }

        await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "Endpoint not found." }, cancellationToken);
    }

    private async Task HandleGetIssuesAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var token = await AuthorizeAsync(context, ApiTokenScope.ReadIssues, cancellationToken);
        if (token is null)
        {
            return;
        }

        var projectKey = context.Request.QueryString["projectKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { error = "projectKey is required." }, cancellationToken);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await ResolveProjectAsync(services, token.UserId, projectKey, cancellationToken);
        if (project is null)
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "Project not found or not accessible." }, cancellationToken);
            return;
        }

        var permissions = services.GetRequiredService<IPermissionService>();
        if (!await permissions.HasPermissionAsync(token.UserId, project.Id, Permission.ViewProject, cancellationToken))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.Forbidden, new { error = "Token user does not have permission to view this project." }, cancellationToken);
            return;
        }

        var issues = await services.GetRequiredService<IIssueQueryService>().GetProjectIssuesAsync(project.Id, cancellationToken);
        var result = issues.Select(issue => new
        {
            issue.Id,
            issue.IssueKey,
            issue.Title,
            issue.Type,
            issue.Priority,
            issue.StatusId,
            issue.StatusName,
            issue.StatusColor,
            issue.StoryPoints,
            Assignees = issue.Assignees.Select(assignee => new { assignee.UserId, assignee.DisplayName })
        });
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, result, cancellationToken);
    }

    private async Task HandleCreateIssueAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var token = await AuthorizeAsync(context, ApiTokenScope.WriteIssues, cancellationToken);
        if (token is null)
        {
            return;
        }

        LocalApiCreateIssueRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<LocalApiCreateIssueRequest>(context.Request.InputStream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { error = "Request body must be valid JSON." }, cancellationToken);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ProjectKey) || string.IsNullOrWhiteSpace(request.Title))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { error = "projectKey and title are required." }, cancellationToken);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await ResolveProjectAsync(services, token.UserId, request.ProjectKey.Trim(), cancellationToken);
        if (project is null)
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { error = "Project not found or not accessible." }, cancellationToken);
            return;
        }

        var permissions = services.GetRequiredService<IPermissionService>();
        if (!await permissions.HasPermissionAsync(token.UserId, project.Id, Permission.CreateIssue, cancellationToken))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.Forbidden, new { error = "Token user does not have permission to create issues in this project." }, cancellationToken);
            return;
        }

        var issue = await services.GetRequiredService<IssueService>().CreateAsync(new IssueEditModel
        {
            ProjectId = project.Id,
            Title = request.Title.Trim(),
            DescriptionText = string.IsNullOrWhiteSpace(request.DescriptionText) ? null : request.DescriptionText.Trim(),
            Type = request.Type ?? IssueType.Task,
            Priority = request.Priority ?? IssuePriority.Medium,
            ReporterId = token.UserId,
            CreatedById = token.UserId,
            DueDate = request.DueDate,
            StoryPoints = request.StoryPoints,
            AssigneeIds = Array.Empty<int>()
        }, cancellationToken);

        await WriteJsonAsync(context.Response, HttpStatusCode.Created, new
        {
            issue.Id,
            issue.IssueKey,
            issue.Title,
            issue.ProjectId,
            issue.WorkflowStatusId,
            issue.Priority,
            issue.Type,
        }, cancellationToken);
    }

    private async Task<ApiToken?> AuthorizeAsync(HttpListenerContext context, ApiTokenScope requiredScope, CancellationToken cancellationToken)
    {
        var authorizationHeader = context.Request.Headers["Authorization"];
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            await WriteJsonAsync(context.Response, HttpStatusCode.Unauthorized, new { error = "Bearer token is required." }, cancellationToken);
            return null;
        }

        var rawToken = authorizationHeader["Bearer ".Length..].Trim();
        await using var scope = _scopeFactory.CreateAsyncScope();
        var token = await scope.ServiceProvider.GetRequiredService<IApiTokenService>().ValidateTokenAsync(rawToken, cancellationToken);
        if (token is null)
        {
            context.Response.Headers["WWW-Authenticate"] = "Bearer error=\"invalid_token\"";
            await WriteJsonAsync(context.Response, HttpStatusCode.Unauthorized, new { error = "API token is invalid, expired, or revoked." }, cancellationToken);
            return null;
        }

        if (!token.Scopes.Contains(requiredScope))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.Forbidden, new { error = $"API token does not include the required scope: {requiredScope}." }, cancellationToken);
            return null;
        }

        return token;
    }

    private static async Task<Project?> ResolveProjectAsync(IServiceProvider services, int userId, string projectKey, CancellationToken cancellationToken)
    {
        var projects = await services.GetRequiredService<IProjectRepository>().GetAccessibleProjectsAsync(userId, cancellationToken);
        return projects.FirstOrDefault(project => project.Key.Equals(projectKey, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode statusCode, object payload, CancellationToken cancellationToken)
    {
        if (!response.OutputStream.CanWrite)
        {
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _listener.Close();
        _cts.Dispose();
    }

    private sealed class LocalApiCreateIssueRequest
    {
        public string ProjectKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? DescriptionText { get; set; }
        public IssueType? Type { get; set; }
        public IssuePriority? Priority { get; set; }
        public DateOnly? DueDate { get; set; }
        public int? StoryPoints { get; set; }
    }
}
