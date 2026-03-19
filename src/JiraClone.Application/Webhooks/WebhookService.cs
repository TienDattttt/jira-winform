using System.ComponentModel.DataAnnotations;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Webhooks;

public class WebhookService : IWebhookService
{
    private readonly IWebhookEndpointRepository _endpoints;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IProjectRepository _projects;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWebhookDispatcher _dispatcher;
    private readonly IWebhookSecretProtector _secretProtector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IWebhookEndpointRepository endpoints,
        IWebhookDeliveryRepository deliveries,
        IProjectRepository projects,
        IPermissionService permissionService,
        ICurrentUserContext currentUserContext,
        IWebhookDispatcher dispatcher,
        IWebhookSecretProtector secretProtector,
        IUnitOfWork unitOfWork,
        ILogger<WebhookService>? logger = null)
    {
        _endpoints = endpoints;
        _deliveries = deliveries;
        _projects = projects;
        _permissionService = permissionService;
        _currentUserContext = currentUserContext;
        _dispatcher = dispatcher;
        _secretProtector = secretProtector;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<WebhookService>.Instance;
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(projectId, Permission.ManageProject, cancellationToken);

        var endpoints = await _endpoints.GetByProjectIdAsync(projectId, cancellationToken);
        var result = new List<WebhookEndpoint>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            var latestDelivery = await _deliveries.GetLatestByEndpointIdAsync(endpoint.Id, cancellationToken);
            result.Add(CloneEndpointForView(endpoint, latestDelivery));
        }

        return result;
    }

    public async Task<WebhookEndpoint> CreateAsync(int projectId, string name, string url, string secret, bool isActive, IReadOnlyCollection<WebhookEventType> subscribedEvents, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(projectId, Permission.ManageProject, cancellationToken);
        await RequireProjectAsync(projectId, cancellationToken);

        var endpoint = new WebhookEndpoint
        {
            ProjectId = projectId,
            Name = NormalizeName(name),
            Url = NormalizeUrl(url),
            Secret = _secretProtector.Protect(NormalizeSecret(secret)),
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        ApplySubscriptions(endpoint, subscribedEvents);
        await _endpoints.AddAsync(endpoint, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created webhook endpoint {WebhookEndpointId} for project {ProjectId}.", endpoint.Id, projectId);
        return CloneEndpointForView(endpoint, null);
    }

    public async Task<WebhookEndpoint?> UpdateAsync(int endpointId, string name, string url, string secret, bool isActive, IReadOnlyCollection<WebhookEventType> subscribedEvents, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(endpointId, cancellationToken);
        if (endpoint is null)
        {
            return null;
        }

        await EnsurePermissionAsync(endpoint.ProjectId, Permission.ManageProject, cancellationToken);
        endpoint.Name = NormalizeName(name);
        endpoint.Url = NormalizeUrl(url);
        endpoint.Secret = _secretProtector.Protect(NormalizeSecret(secret));
        endpoint.IsActive = isActive;
        endpoint.UpdatedAtUtc = DateTime.UtcNow;
        ApplySubscriptions(endpoint, subscribedEvents);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated webhook endpoint {WebhookEndpointId} for project {ProjectId}.", endpoint.Id, endpoint.ProjectId);
        return CloneEndpointForView(endpoint, null);
    }

    public async Task<bool> DeleteAsync(int endpointId, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(endpointId, cancellationToken);
        if (endpoint is null)
        {
            return false;
        }

        await EnsurePermissionAsync(endpoint.ProjectId, Permission.ManageProject, cancellationToken);
        await _endpoints.RemoveAsync(endpoint, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted webhook endpoint {WebhookEndpointId} from project {ProjectId}.", endpoint.Id, endpoint.ProjectId);
        return true;
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetDeliveryHistoryAsync(int endpointId, int take = 50, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(endpointId, cancellationToken)
            ?? throw new ValidationException($"Webhook endpoint {endpointId} was not found.");
        await EnsurePermissionAsync(endpoint.ProjectId, Permission.ManageProject, cancellationToken);
        return await _deliveries.GetByEndpointIdAsync(endpointId, take, cancellationToken);
    }

    public async Task<WebhookDelivery?> SendTestAsync(int endpointId, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(endpointId, cancellationToken)
            ?? throw new ValidationException($"Webhook endpoint {endpointId} was not found.");
        await EnsurePermissionAsync(endpoint.ProjectId, Permission.ManageProject, cancellationToken);

        return await _dispatcher.SendTestAsync(
            endpointId,
            new
            {
                IsTest = true,
                ProjectId = endpoint.ProjectId,
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                Message = "Jira Desktop webhook test",
                TriggeredAtUtc = DateTime.UtcNow,
            },
            cancellationToken);
    }

    private async Task EnsurePermissionAsync(int projectId, Permission permission, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.RequireUserId();
        if (!await _permissionService.HasPermissionAsync(currentUserId, projectId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to manage project webhooks.");
        }
    }

    private async Task<Project> RequireProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        return await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new ValidationException($"Project {projectId} was not found.");
    }

    private WebhookEndpoint CloneEndpointForView(WebhookEndpoint endpoint, WebhookDelivery? latestDelivery)
    {
        return new WebhookEndpoint
        {
            Id = endpoint.Id,
            ProjectId = endpoint.ProjectId,
            Name = endpoint.Name,
            Url = endpoint.Url,
            Secret = _secretProtector.Unprotect(endpoint.Secret),
            IsActive = endpoint.IsActive,
            CreatedAtUtc = endpoint.CreatedAtUtc,
            UpdatedAtUtc = endpoint.UpdatedAtUtc,
            Subscriptions = endpoint.Subscriptions
                .OrderBy(x => x.EventType)
                .Select(x => new WebhookEndpointSubscription
                {
                    WebhookEndpointId = endpoint.Id,
                    EventType = x.EventType,
                })
                .ToList(),
            Deliveries = latestDelivery is null ? [] : [CloneDelivery(latestDelivery)]
        };
    }

    private static WebhookDelivery CloneDelivery(WebhookDelivery delivery)
    {
        return new WebhookDelivery
        {
            Id = delivery.Id,
            WebhookEndpointId = delivery.WebhookEndpointId,
            EventType = delivery.EventType,
            Payload = delivery.Payload,
            ResponseCode = delivery.ResponseCode,
            Success = delivery.Success,
            AttemptedAtUtc = delivery.AttemptedAtUtc,
            RetryCount = delivery.RetryCount,
            ErrorMessage = delivery.ErrorMessage,
            CreatedAtUtc = delivery.CreatedAtUtc,
            UpdatedAtUtc = delivery.UpdatedAtUtc,
        };
    }

    private static void ApplySubscriptions(WebhookEndpoint endpoint, IReadOnlyCollection<WebhookEventType> subscribedEvents)
    {
        var normalizedEvents = (subscribedEvents ?? Array.Empty<WebhookEventType>())
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        if (normalizedEvents.Count == 0)
        {
            throw new ValidationException("Select at least one webhook event.");
        }

        endpoint.Subscriptions.Clear();
        foreach (var eventType in normalizedEvents)
        {
            endpoint.Subscriptions.Add(new WebhookEndpointSubscription
            {
                WebhookEndpoint = endpoint,
                WebhookEndpointId = endpoint.Id,
                EventType = eventType,
            });
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Webhook name is required.");
        }

        return name.Trim();
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ValidationException("Webhook URL is required.");
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("Webhook URL must be a valid http or https address.");
        }

        return uri.ToString();
    }

    private static string NormalizeSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ValidationException("Webhook secret is required.");
        }

        return secret.Trim();
    }
}
