using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JiraClone.Persistence;

public class JiraCloneDbContextFactory : IDesignTimeDbContextFactory<JiraCloneDbContext>
{
    public JiraCloneDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=JiraCloneWinForms;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new JiraCloneDbContext(options);
    }
}
