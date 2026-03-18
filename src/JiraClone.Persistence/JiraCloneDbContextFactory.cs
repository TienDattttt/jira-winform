using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace JiraClone.Persistence;

public class JiraCloneDbContextFactory : IDesignTimeDbContextFactory<JiraCloneDbContext>
{
    public JiraCloneDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JiraClone.WinForms");
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("JIRACLONE_ENVIRONMENT") ??
            "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("JIRACLONE_")
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=JiraCloneWinForms;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new JiraCloneDbContext(options);
    }
}
