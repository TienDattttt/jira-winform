using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms;
using JiraClone.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JiraClone.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=JiraCloneWinForms;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var attachmentRootPath = Path.Combine(AppContext.BaseDirectory, "attachments");

        var services = new ServiceCollection();
        services.AddDbContextFactory<JiraCloneDbContext>(options =>
            options.UseSqlServer(connectionString).EnableSensitiveDataLogging());

        using var serviceProvider = services.BuildServiceProvider();
        using var session = new AppSession(serviceProvider.GetRequiredService<IDbContextFactory<JiraCloneDbContext>>(), attachmentRootPath);
        System.Windows.Forms.Application.Run(new LoginForm(session));
    }
}
