using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms;
using JiraClone.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JiraClone.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("JIRACLONE_")
            .Build();

        var connectionString =
            configuration.GetConnectionString("JiraClone") ??
            "Server=(localdb)\\MSSQLLocalDB;Database=JiraCloneWinForms;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var attachmentRootPath =
            configuration["Attachments:RootPath"] is { Length: > 0 } configPath
                ? configPath
                : Path.Combine(AppContext.BaseDirectory, "attachments");

        try
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<JiraCloneDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
#if DEBUG
                options.EnableSensitiveDataLogging();
#endif
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var session = new AppSession(
                serviceProvider.GetRequiredService<IDbContextFactory<JiraCloneDbContext>>(),
                attachmentRootPath);

            System.Windows.Forms.Application.Run(new LoginForm(session));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Unable to start Jira Clone.\n\n{exception.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

