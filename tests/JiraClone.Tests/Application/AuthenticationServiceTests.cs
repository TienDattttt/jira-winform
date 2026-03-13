using JiraClone.Application.Abstractions;
using JiraClone.Application.Auth;
using JiraClone.Domain.Entities;
using Moq;

namespace JiraClone.Tests.Application;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsSuccessfulResult()
    {
        // Arrange
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var context = new Mock<ICurrentUserContext>();
        users.Setup(x => x.GetByUserNameAsync("admin", default)).ReturnsAsync(user);
        hasher.Setup(x => x.Verify("secret", user.PasswordHash, user.PasswordSalt)).Returns(true);
        var service = new AuthenticationService(users.Object, hasher.Object, context.Object);

        // Act
        var result = await service.LoginAsync("admin", "secret");

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        context.Verify(x => x.Set(user), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsFailedResult()
    {
        // Arrange
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        users.Setup(x => x.GetByUserNameAsync("admin", default)).ReturnsAsync(user);
        hasher.Setup(x => x.Verify("wrong", user.PasswordHash, user.PasswordSalt)).Returns(false);
        var service = new AuthenticationService(users.Object, hasher.Object, new Mock<ICurrentUserContext>().Object);

        // Act
        var result = await service.LoginAsync("admin", "wrong");

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsFailedResult()
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUserNameAsync("missing", default)).ReturnsAsync((User?)null);
        var service = new AuthenticationService(users.Object, new Mock<IPasswordHasher>().Object, new Mock<ICurrentUserContext>().Object);

        // Act
        var result = await service.LoginAsync("missing", "secret");

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsFailedResult()
    {
        // Arrange
        var user = CreateUser();
        user.IsActive = false;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUserNameAsync("admin", default)).ReturnsAsync(user);
        var service = new AuthenticationService(users.Object, new Mock<IPasswordHasher>().Object, new Mock<ICurrentUserContext>().Object);

        // Act
        var result = await service.LoginAsync("admin", "secret");

        // Assert
        Assert.False(result.Succeeded);
    }

    private static User CreateUser() =>
        new() { Id = 1, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com", PasswordHash = "hash", PasswordSalt = "salt", IsActive = true };
}
