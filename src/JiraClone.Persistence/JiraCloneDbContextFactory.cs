using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace JiraClone.Persistence;

public class JiraCloneDbContextFactory : IDesignTimeDbContextFactory<JiraCloneDbContext>
{
    public JiraCloneDbContext CreateDbContext(string[] args)
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("JIRACLONE_ENVIRONMENT") ??
            "Production";

        var startupProjectPath = ResolveStartupProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(startupProjectPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("JIRACLONE_")
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required for design-time DbContext creation.");

        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new JiraCloneDbContext(options);
    }

    private static string ResolveStartupProjectPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "JiraClone.WinForms"),
            Path.Combine(Directory.GetCurrentDirectory(), "JiraClone.WinForms"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JiraClone.WinForms")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "JiraClone.WinForms")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "JiraClone.WinForms"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate JiraClone.WinForms appsettings.json for design-time configuration.");
    }
}
