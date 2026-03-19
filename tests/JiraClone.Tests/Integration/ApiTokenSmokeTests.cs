using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Application.ApiTokens;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Infrastructure.Api;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace JiraClone.Tests.Integration;

[CollectionDefinition("LocalApiServer", DisableParallelization = true)]
public sealed class LocalApiServerCollectionDefinition;

[Collection("LocalApiServer")]
public class ApiTokenSmokeTests
{
    [Fact]
    public async Task GetIssues_WithValidBearerToken_ReturnsJsonArray()
    {
        const int userId = 7;
        const int projectId = 21;

        var user = new User
        {
            Id = userId,
            UserName = "api-user",
            DisplayName = "API User",
            Email = "api-user@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
        };
        var project = new Project
        {
            Id = projectId,
            Key = "TEST",
            Name = "Test Project",
            IsActive = true,
        };

        var tokenRepository = new InMemoryApiTokenRepository();
        var userRepository = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var projectRepository = new Mock<IProjectRepository>();
        var permissionService = new Mock<IPermissionService>();
        var issueQueryService = new Mock<IIssueQueryService>();

        userRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        projectRepository.Setup(x => x.GetAccessibleProjectsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([project]);
        permissionService.Setup(x => x.HasPermissionAsync(userId, projectId, Permission.ViewProject, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        issueQueryService.Setup(x => x.GetProjectIssuesAsync(projectId, It.IsAny<CancellationToken>())).ReturnsAsync([
            new DashboardIssueDto(
                100,
                "TEST-1",
                "Token smoke issue",
                IssueType.Task,
                IssuePriority.Medium,
                3,
                "In Progress",
                "#0052CC",
                StatusCategory.InProgress,
                5,
                "API User",
                [new DashboardAssigneeDto(userId, "API User", null)])
        ]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IApiTokenRepository>(tokenRepository);
        services.AddSingleton<IUserRepository>(userRepository.Object);
        services.AddSingleton<IUnitOfWork>(unitOfWork.Object);
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddSingleton<IProjectRepository>(projectRepository.Object);
        services.AddSingleton<IPermissionService>(permissionService.Object);
        services.AddSingleton<IIssueQueryService>(issueQueryService.Object);
        services.AddSingleton<LocalApiServer>();

        using var provider = services.BuildServiceProvider();
        var apiServer = provider.GetRequiredService<LocalApiServer>();
        await apiServer.StartAsync();

        try
        {
            GeneratedTokenResult generatedToken;
            using (var scope = provider.CreateScope())
            {
                var tokenService = scope.ServiceProvider.GetRequiredService<IApiTokenService>();
                generatedToken = await tokenService.CreateTokenAsync(userId, "Smoke test", DateTime.UtcNow.AddMinutes(30), [ApiTokenScope.ReadIssues]);
            }

            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:47892/") };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generatedToken.RawToken);

            var response = await TrySendGetIssuesRequestAsync(client);
            if (response is null)
            {
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(responseBody);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            var issueElement = Assert.Single(document.RootElement.EnumerateArray());
            Assert.Equal("TEST-1", issueElement.GetProperty("issueKey").GetString());
            Assert.Equal("Token smoke issue", issueElement.GetProperty("title").GetString());
            Assert.NotNull((await tokenRepository.GetByUserAsync(userId)).Single().LastUsedAtUtc);
        }
        finally
        {
            await apiServer.StopAsync();
        }
    }

    private static async Task<HttpResponseMessage?> TrySendGetIssuesRequestAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return await client.GetAsync("api/v1/issues?projectKey=TEST");
            }
            catch (HttpRequestException exception) when (exception.InnerException is SocketException)
            {
                await Task.Delay(100);
            }
        }

        return null;
    }

    private sealed class InMemoryApiTokenRepository : IApiTokenRepository
    {
        private readonly List<ApiToken> _tokens = [];
        private int _nextId = 1;

        public Task<ApiToken?> GetByIdAsync(int tokenId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokens.FirstOrDefault(token => token.Id == tokenId));

        public Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokens.FirstOrDefault(token => string.Equals(token.TokenHash, tokenHash, StringComparison.Ordinal)));

        public Task<IReadOnlyList<ApiToken>> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApiToken>>(_tokens.Where(token => token.UserId == userId).ToList());

        public Task AddAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            token.Id = _nextId++;
            _tokens.Add(token);
            return Task.CompletedTask;
        }
    }
}

