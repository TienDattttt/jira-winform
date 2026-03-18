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
        WebhookEndpoint? addedEndpoint = null;

        projects.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(new Project { Id = 7, Key = "JIRA", Name = "Jira Clone" });
        endpoints.Setup(x => x.AddAsync(It.IsAny<WebhookEndpoint>(), default))
            .Callback<WebhookEndpoint, CancellationToken>((endpoint, _) =>
            {
                addedEndpoint = endpoint;
                endpoint.Id = 12;
            })
            .Returns(Task.CompletedTask);

        var service = CreateService(endpoints: endpoints, projects: projects, unitOfWork: unitOfWork);

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
        Assert.Equal("secret-key", addedEndpoint.Secret);
        Assert.True(addedEndpoint.IsActive);
        Assert.Equal([WebhookEventType.IssueCreated, WebhookEventType.IssueUpdated], addedEndpoint.Subscriptions.Select(x => x.EventType).OrderBy(x => x).ToArray());
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
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

    private static WebhookService CreateService(
        Mock<IWebhookEndpointRepository>? endpoints = null,
        Mock<IWebhookDeliveryRepository>? deliveries = null,
        Mock<IProjectRepository>? projects = null,
        Mock<IPermissionService>? permissionService = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IWebhookDispatcher>? dispatcher = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        endpoints ??= new Mock<IWebhookEndpointRepository>();
        deliveries ??= new Mock<IWebhookDeliveryRepository>();
        projects ??= new Mock<IProjectRepository>();
        permissionService ??= new Mock<IPermissionService>();
        currentUserContext ??= new Mock<ICurrentUserContext>();
        dispatcher ??= new Mock<IWebhookDispatcher>();
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
            unitOfWork.Object);
    }
}
