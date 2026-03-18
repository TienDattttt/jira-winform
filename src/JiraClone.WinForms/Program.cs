using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms;
using JiraClone.Persistence;
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

            using var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var startupLogger = loggerFactory.CreateLogger("Startup");
            startupLogger.LogInformation("Starting Jira Clone in {EnvironmentName} mode.", environmentName);

            using var session = new AppSession(
                serviceProvider.GetRequiredService<IDbContextFactory<JiraCloneDbContext>>(),
                loggerFactory,
                attachmentRootPath,
                maxAttachmentSizeBytes);

            System.Windows.Forms.Application.Run(new LoginForm(session));
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
}
