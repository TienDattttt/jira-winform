using JiraClone.Application.Roles;

namespace JiraClone.Tests.Application;

public class RoleCatalogTests
{
    [Fact]
    public void RoleCatalog_Contains_Expected_Default_Roles()
    {
        Assert.Equal("Admin", RoleCatalog.Admin);
        Assert.Equal("ProjectManager", RoleCatalog.ProjectManager);
        Assert.Equal("Developer", RoleCatalog.Developer);
        Assert.Equal("Viewer", RoleCatalog.Viewer);
    }
}
