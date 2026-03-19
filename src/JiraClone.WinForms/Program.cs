using JiraClone.Application.ActivityLog;
using JiraClone.Application.ApiTokens;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Attachments;
using JiraClone.Application.Auth;
using JiraClone.Infrastructure.Auth;
using JiraClone.Infrastructure.Api;
using JiraClone.Application.Boards;
using JiraClone.Application.Comments;
using JiraClone.Application.Components;
using JiraClone.Application.Dashboard;
using JiraClone.Application.Integrations;
using JiraClone.Application.Issues;
using JiraClone.Application.Jql;
using JiraClone.Application.Labels;
using JiraClone.Application.Notifications;
using JiraClone.Application.Permissions;
using JiraClone.Application.Projects;
using JiraClone.Application.Roadmap;
using JiraClone.Application.Roles;
using JiraClone.Application.Sprints;
using JiraClone.Application.Users;
using JiraClone.Application.Versions;
using JiraClone.Application.Watchers;
using JiraClone.Application.Webhooks;
using JiraClone.Application.Workflows;
using JiraClone.Infrastructure.Email;
using JiraClone.Infrastructure.Integrations;
using JiraClone.Infrastructure.Security;
using JiraClone.Infrastructure.Session;
using JiraClone.Infrastructure.Storage;
using JiraClone.Infrastructure.Webhooks;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace JiraClone.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("JIRACLONE_ENVIRONMENT") ??
            "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("JIRACLONE_")
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        try
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

            var storagePathSetting = configuration["App:StoragePath"];
            var attachmentRootPath = string.IsNullOrWhiteSpace(storagePathSetting)
                ? Path.Combine(AppContext.BaseDirectory, "attachments")
                : Path.IsPathRooted(storagePathSetting)
                    ? storagePathSetting
                    : Path.Combine(AppContext.BaseDirectory, storagePathSetting);

            var maxAttachmentSizeMb = int.TryParse(configuration["App:MaxAttachmentSizeMb"], out var parsedMaxAttachmentSizeMb)
                ? Math.Max(1, parsedMaxAttachmentSizeMb)
                : 25;
            var maxAttachmentSizeBytes = maxAttachmentSizeMb * 1024L * 1024L;

            var sessionFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JiraDesktop",
                "session.dat");

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });
            services.AddDbContextFactory<JiraCloneDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
#if DEBUG
                options.EnableSensitiveDataLogging();
#endif
            });
            services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<JiraCloneDbContext>>().CreateDbContext());

            services.AddSingleton<CurrentUserContext>();
            services.AddSingleton<ICurrentUserContext>(provider => provider.GetRequiredService<CurrentUserContext>());
            services.AddSingleton<AuthorizationService>();
            services.AddSingleton<IAuthorizationService>(provider => provider.GetRequiredService<AuthorizationService>());
            services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
            services.AddSingleton<FileShareAttachmentService>(provider =>
                new FileShareAttachmentService(
                    attachmentRootPath,
                    maxAttachmentSizeBytes,
                    provider.GetRequiredService<ILogger<FileShareAttachmentService>>()));
            services.AddSingleton<IAttachmentService>(provider => provider.GetRequiredService<FileShareAttachmentService>());

            var emailOptions = new EmailOptions
            {
                SmtpHost = configuration["Email:SmtpHost"] ?? string.Empty,
                SmtpPort = int.TryParse(configuration["Email:SmtpPort"], out var parsedSmtpPort) ? parsedSmtpPort : 587,
                UseSsl = !bool.TryParse(configuration["Email:UseSsl"], out var parsedUseSsl) || parsedUseSsl,
                FromAddress = configuration["Email:FromAddress"] ?? string.Empty,
                FromName = configuration["Email:FromName"] ?? "Jira Desktop",
                UserName = configuration["Email:UserName"],
                Password = configuration["Email:Password"]
            };
            services.AddSingleton(emailOptions);
            services.AddSingleton<IEmailService, MailKitEmailService>();
            services.AddSingleton<INotificationEmailTemplateRenderer, NotificationEmailTemplateRenderer>();
            services.AddSingleton<IIntegrationConfigProtector, DpapiIntegrationConfigProtector>();
            services.AddSingleton<IWebhookSecretProtector, DpapiWebhookSecretProtector>();
            services.AddSingleton(new WebhookDispatcherOptions());

            var oauthOptions = new OAuthOptions
            {
                Enabled = bool.TryParse(configuration["OAuth:Enabled"], out var oauthEnabled) && oauthEnabled,
                ProviderName = configuration["OAuth:ProviderName"] ?? "SSO",
                AuthorizationEndpoint = configuration["OAuth:AuthorizationEndpoint"] ?? string.Empty,
                TokenEndpoint = configuration["OAuth:TokenEndpoint"] ?? string.Empty,
                Issuer = configuration["OAuth:Issuer"] ?? string.Empty,
                JwksUri = configuration["OAuth:JwksUri"] ?? string.Empty,
                ClientId = configuration["OAuth:ClientId"] ?? string.Empty,
                RedirectUri = configuration["OAuth:RedirectUri"] ?? "http://localhost:8765/callback",
                Scopes = configuration.GetSection("OAuth:Scopes").GetChildren().Select(child => child.Value).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
            };
            if (oauthOptions.Scopes.Length == 0)
            {
                oauthOptions.Scopes = ["openid", "profile", "email"];
            }
            services.AddSingleton(oauthOptions);
            services.AddSingleton(new SessionPersistenceOptions { SessionFilePath = sessionFilePath });
            services.AddSingleton<ISessionPersistenceService, DpapiSessionPersistenceService>();
            services.AddSingleton(new HttpClient());

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IAttachmentRepository, AttachmentRepository>();
            services.AddScoped<IApiTokenRepository, ApiTokenRepository>();
            services.AddScoped<ICommentRepository, CommentRepository>();
            services.AddScoped<IComponentRepository, ComponentRepository>();
            services.AddScoped<IIssueRepository, IssueRepository>();
            services.AddScoped<ILabelRepository, LabelRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IProjectIntegrationConfigRepository, ProjectIntegrationConfigRepository>();
            services.AddScoped<IProjectVersionRepository, ProjectVersionRepository>();
            services.AddScoped<ISavedFilterRepository, SavedFilterRepository>();
            services.AddScoped<ISprintRepository, SprintRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IWatcherRepository, WatcherRepository>();
            services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
            services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
            services.AddScoped<IWorkflowRepository, WorkflowRepository>();

            services.AddScoped<AuthenticationService>();
            services.AddScoped<IApiTokenService, ApiTokenService>();
            services.AddScoped<IOAuthService, OAuthService>();
            services.AddScoped<ProjectQueryService>();
            services.AddScoped<IProjectCommandService, ProjectCommandService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IBoardQueryService, BoardQueryService>();
            services.AddScoped<UserQueryService>();
            services.AddScoped<UserCommandService>();
            services.AddScoped<IWorkflowService, WorkflowService>();
            services.AddScoped<IJqlService, JqlService>();
            services.AddScoped<IntegrationConfigStore>();
            services.AddScoped<IIntegrationCatalogService, IntegrationCatalogService>();
            services.AddScoped<IGitHubIntegrationService, GitHubIntegrationService>();
            services.AddScoped<IConfluenceIntegrationService, ConfluenceIntegrationService>();
            services.AddScoped<ISavedFilterService, SavedFilterService>();
            services.AddScoped<IssueService>();
            services.AddScoped<IWatcherService, WatcherService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddSingleton<IWebhookDispatcher, WebhookDispatcher>();
            services.AddScoped<IWebhookService, WebhookService>();
            services.AddScoped<ILabelService, LabelService>();
            services.AddScoped<IComponentService, ComponentService>();
            services.AddScoped<IVersionService, VersionService>();
            services.AddScoped<CommentService>();
            services.AddScoped<ISprintService, SprintService>();
            services.AddScoped<ActivityLogService>();
            services.AddScoped<AttachmentFacade>();
            services.AddScoped<IIssueQueryService, IssueQueryService>();
            services.AddScoped<DashboardQueryService>();
            services.AddScoped<IRoadmapService, RoadmapService>();

            RegisterIntegrationPlugins(services);
            services.AddSingleton<GitHubIntegrationSyncWorker>();
            services.AddSingleton<LocalApiServer>();
            services.AddSingleton<AppSession>();

            using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var startupLogger = loggerFactory.CreateLogger("Startup");
            startupLogger.LogInformation("Starting Jira Clone in {EnvironmentName} mode.", environmentName);
            _ = serviceProvider.GetRequiredService<GitHubIntegrationSyncWorker>();
            var apiServer = serviceProvider.GetRequiredService<LocalApiServer>();
            apiServer.StartAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.ApplicationExit += HandleApplicationExit;

            void HandleApplicationExit(object? sender, EventArgs e)
            {
                try
                {
                    apiServer.StopAsync().GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    startupLogger.LogWarning(exception, "Unable to stop local API server cleanly during application exit.");
                }
            }

            var startupForm = CreateStartupForm(serviceProvider, startupLogger);
            System.Windows.Forms.Application.Run(startupForm);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Unable to start Jira Clone.");
            MessageBox.Show(
                $"Unable to start Jira Clone.\n\n{exception.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void RegisterIntegrationPlugins(ServiceCollection services)
    {
        var pluginAssemblies = new[]
        {
            typeof(IIntegrationPlugin).Assembly,
            typeof(IntegrationPluginRegistrationMarker).Assembly
        };

        var pluginTypes = pluginAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IIntegrationPlugin).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .Distinct();

        foreach (var pluginType in pluginTypes)
        {
            services.AddScoped(typeof(IIntegrationPlugin), pluginType);
        }
    }

    private static Form CreateStartupForm(IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger startupLogger)
    {
        var session = serviceProvider.GetRequiredService<AppSession>();
        var sessionPersistence = serviceProvider.GetRequiredService<ISessionPersistenceService>();

        if (TryRestoreSession(session, sessionPersistence, startupLogger))
        {
            var displayName = session.CurrentUserContext.CurrentUser?.DisplayName
                ?? session.CurrentUserContext.CurrentUser?.UserName
                ?? "User";
            return new MainForm(session, displayName, sessionPersistence);
        }

        return new LoginForm(session, sessionPersistence);
    }

    private static bool TryRestoreSession(AppSession session, ISessionPersistenceService sessionPersistence, Microsoft.Extensions.Logging.ILogger startupLogger)
    {
        var persistedSession = sessionPersistence.LoadAsync().GetAwaiter().GetResult();
        if (persistedSession is null)
        {
            return false;
        }

        try
        {
            if (persistedSession.ExpiresAtUtc <= DateTime.UtcNow)
            {
                startupLogger.LogInformation("Persisted session expired for user {UserId} at {ExpiresAtUtc}.", persistedSession.UserId, persistedSession.ExpiresAtUtc);
                session.Authentication.ClearPersistentSessionAsync(persistedSession.UserId).GetAwaiter().GetResult();
                sessionPersistence.ClearAsync().GetAwaiter().GetResult();
                return false;
            }

            var restored = session.Authentication.ValidateRefreshTokenAsync(persistedSession.UserId, persistedSession.RefreshToken).GetAwaiter().GetResult();
            if (!restored)
            {
                startupLogger.LogWarning("Persisted session validation failed for user {UserId}.", persistedSession.UserId);
                sessionPersistence.ClearAsync().GetAwaiter().GetResult();
                return false;
            }

            session.InitializeActiveProjectAsync().GetAwaiter().GetResult();
            startupLogger.LogInformation("Persisted session restored for user {UserId}.", persistedSession.UserId);
            return true;
        }
        catch (Exception exception)
        {
            startupLogger.LogWarning(exception, "Unable to restore persisted session for user {UserId}.", persistedSession.UserId);
            try
            {
                session.Authentication.ClearPersistentSessionAsync(persistedSession.UserId).GetAwaiter().GetResult();
            }
            catch (Exception clearException)
            {
                startupLogger.LogWarning(clearException, "Unable to clear server-side persisted session state for user {UserId} after restore failure.", persistedSession.UserId);
            }

            session.CurrentUserContext.Clear();
            sessionPersistence.ClearAsync().GetAwaiter().GetResult();
            return false;
        }
    }
}





