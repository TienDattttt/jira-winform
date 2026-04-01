using Microsoft.Extensions.Configuration;

namespace JiraClone.E2ETests.Infrastructure;

public sealed class TestConfig
{
    private TestConfig(
        string appPath,
        int startupTimeoutSeconds,
        int actionTimeoutSeconds,
        TestUserCredentials admin,
        TestUserCredentials developer,
        TestUserCredentials projectManager,
        string defaultProjectName)
    {
        AppPath = appPath;
        StartupTimeoutSeconds = startupTimeoutSeconds;
        ActionTimeoutSeconds = actionTimeoutSeconds;
        Admin = admin;
        Developer = developer;
        ProjectManager = projectManager;
        DefaultProjectName = defaultProjectName;
    }

    public string AppPath { get; }
    public int StartupTimeoutSeconds { get; }
    public int ActionTimeoutSeconds { get; }
    public int StartupTimeoutMs => StartupTimeoutSeconds * 1000;
    public int ActionTimeoutMs => ActionTimeoutSeconds * 1000;
    public TestUserCredentials Admin { get; }
    public TestUserCredentials Developer { get; }
    public TestUserCredentials ProjectManager { get; }
    public string DefaultProjectName { get; }

    public static TestConfig Load()
    {
        var basePath = AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.E2E.json", optional: false, reloadOnChange: false)
            .Build();

        var appPathSetting = configuration["E2E:AppPath"]
            ?? throw new InvalidOperationException("E2E:AppPath is required.");

        var appPath = Path.IsPathRooted(appPathSetting)
            ? appPathSetting
            : Path.GetFullPath(Path.Combine(basePath, appPathSetting));

        return new TestConfig(
            appPath,
            ReadInt(configuration, "E2E:StartupTimeoutSeconds", 15),
            ReadInt(configuration, "E2E:ActionTimeoutSeconds", 5),
            ReadUser(configuration, "TestUsers:Admin"),
            ReadUser(configuration, "TestUsers:Developer"),
            ReadUser(configuration, "TestUsers:ProjectManager"),
            configuration["TestData:DefaultProjectName"] ?? "Jira Clone Migration");
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) ? value : fallback;

    private static TestUserCredentials ReadUser(IConfiguration configuration, string sectionPath)
    {
        var section = configuration.GetSection(sectionPath);
        return new TestUserCredentials(
            section["Username"] ?? throw new InvalidOperationException($"{sectionPath}:Username is required."),
            section["Password"] ?? throw new InvalidOperationException($"{sectionPath}:Password is required."));
    }
}

public sealed record TestUserCredentials(string Username, string Password);
