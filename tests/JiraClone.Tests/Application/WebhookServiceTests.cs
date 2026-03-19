using JiraClone.Application.Abstractions;
using JiraClone.Application.Webhooks;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class WebhookServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidInput_AddsEndpointWithSubscriptions()
    {
        var endpoints = new Mock<IWebhookEndpointRepository>();
        var projects = new Mock<IProjectRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var secretProtector = CreateSecretProtector();
        WebhookEndpoint? addedEndpoint = null;

        projects.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(new Project { Id = 7, Key = "JIRA", Name = "Jira Clone" });
        endpoints.Setup(x => x.AddAsync(It.IsAny<WebhookEndpoint>(), default))
            .Callback<WebhookEndpoint, CancellationToken>((endpoint, _) =>
            {
                addedEndpoint = endpoint;
                endpoint.Id = 12;
            })
            .Returns(Task.CompletedTask);

        var service = CreateService(endpoints: endpoints, projects: projects, unitOfWork: unitOfWork, secretProtector: secretProtector);

        var endpoint = await service.CreateAsync(
            7,
            "  Build Hook  ",
            "https://example.com/webhook",
            " secret-key ",
            true,
            [WebhookEventType.IssueCreated, WebhookEventType.IssueUpdated]);

        Assert.Equal(12, endpoint.Id);
        Assert.NotNull(addedEndpoint);
        Assert.Equal("Build Hook", addedEndpoint!.Name);
        Assert.Equal("https://example.com/webhook", addedEndpoint.Url);
        Assert.Equal("dpapi:secret-key", addedEndpoint.Secret);
        Assert.Equal("secret-key", endpoint.Secret);
        Assert.True(addedEndpoint.IsActive);
        Assert.Equal([WebhookEventType.IssueCreated, WebhookEventType.IssueUpdated], addedEndpoint.Subscriptions.Select(x => x.EventType).OrderBy(x => x).ToArray());
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsDecryptedSecretsForEditing()
    {
        var endpoints = new Mock<IWebhookEndpointRepository>();
        var deliveries = new Mock<IWebhookDeliveryRepository>();
        var protectedEndpoint = new WebhookEndpoint
        {
            Id = 4,
            ProjectId = 7,
            Name = "Automation Hook",
            Url = "https://example.com/hook",
            Secret = "dpapi:secret",
            IsActive = true,
            Subscriptions = [new WebhookEndpointSubscription { WebhookEndpointId = 4, EventType = WebhookEventType.IssueCreated }]
        };

        endpoints.Setup(x => x.GetByProjectIdAsync(7, default)).ReturnsAsync([protectedEndpoint]);
        deliveries.Setup(x => x.GetLatestByEndpointIdAsync(4, default)).ReturnsAsync((WebhookDelivery?)null);

        var service = CreateService(endpoints: endpoints, deliveries: deliveries, secretProtector: CreateSecretProtector());

        var result = await service.GetByProjectAsync(7);

        Assert.Single(result);
        Assert.Equal("secret", result[0].Secret);
        Assert.NotSame(protectedEndpoint, result[0]);
    }

    [Fact]
    public async Task SendTestAsync_ExistingEndpoint_DelegatesToDispatcher()
    {
        var endpoints = new Mock<IWebhookEndpointRepository>();
        var dispatcher = new Mock<IWebhookDispatcher>();
        var endpoint = new WebhookEndpoint
        {
            Id = 4,
            ProjectId = 7,
            Name = "Automation Hook",
            Url = "https://example.com/hook",
            Secret = "secret",
            IsActive = true,
            Subscriptions = [new WebhookEndpointSubscription { WebhookEndpointId = 4, EventType = WebhookEventType.IssueCreated }]
        };
        var expectedDelivery = new WebhookDelivery
        {
            Id = 9,
            WebhookEndpointId = 4,
            EventType = WebhookEventType.IssueCreated,
            Success = true,
            ResponseCode = 200,
            Payload = "{}"
        };

        endpoints.Setup(x => x.GetByIdAsync(4, default)).ReturnsAsync(endpoint);
        dispatcher.Setup(x => x.SendTestAsync(4, It.IsAny<object>(), default)).ReturnsAsync(expectedDelivery);

        var service = CreateService(endpoints: endpoints, dispatcher: dispatcher);

        var result = await service.SendTestAsync(4);

        Assert.Same(expectedDelivery, result);
        dispatcher.Verify(x => x.SendTestAsync(4, It.IsAny<object>(), default), Times.Once);
    }

    private static Mock<IWebhookSecretProtector> CreateSecretProtector()
    {
        var secretProtector = new Mock<IWebhookSecretProtector>();
        secretProtector.Setup(x => x.Protect("secret-key")).Returns("dpapi:secret-key");
        secretProtector.Setup(x => x.Unprotect("dpapi:secret-key")).Returns("secret-key");
        secretProtector.Setup(x => x.Unprotect("dpapi:secret")).Returns("secret");
        secretProtector.Setup(x => x.Unprotect(It.Is<string>(value => !value.StartsWith("dpapi:", StringComparison.Ordinal)))).Returns<string>(value => value);
        return secretProtector;
    }

    private static WebhookService CreateService(
        Mock<IWebhookEndpointRepository>? endpoints = null,
        Mock<IWebhookDeliveryRepository>? deliveries = null,
        Mock<IProjectRepository>? projects = null,
        Mock<IPermissionService>? permissionService = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IWebhookDispatcher>? dispatcher = null,
        Mock<IWebhookSecretProtector>? secretProtector = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        endpoints ??= new Mock<IWebhookEndpointRepository>();
        deliveries ??= new Mock<IWebhookDeliveryRepository>();
        projects ??= new Mock<IProjectRepository>();
        permissionService ??= new Mock<IPermissionService>();
        currentUserContext ??= new Mock<ICurrentUserContext>();
        dispatcher ??= new Mock<IWebhookDispatcher>();
        secretProtector ??= CreateSecretProtector();
        unitOfWork ??= new Mock<IUnitOfWork>();

        currentUserContext.Setup(x => x.RequireUserId()).Returns(99);
        permissionService.Setup(x => x.HasPermissionAsync(99, It.IsAny<int>(), Permission.ManageProject, default)).ReturnsAsync(true);

        return new WebhookService(
            endpoints.Object,
            deliveries.Object,
            projects.Object,
            permissionService.Object,
            currentUserContext.Object,
            dispatcher.Object,
            secretProtector.Object,
            unitOfWork.Object);
    }
}
