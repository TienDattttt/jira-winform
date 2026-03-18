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
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var context = new Mock<ICurrentUserContext>();
        users.Setup(x => x.GetByUserNameAsync("admin", default)).ReturnsAsync(user);
        hasher.Setup(x => x.Verify("secret", user.PasswordHash, user.PasswordSalt)).Returns(true);
        var service = CreateService(users: users, passwordHasher: hasher, currentUserContext: context);

        var result = await service.LoginAsync("admin", "secret");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        context.Verify(x => x.Set(user), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsFailedResult()
    {
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        users.Setup(x => x.GetByUserNameAsync("admin", default)).ReturnsAsync(user);
        hasher.Setup(x => x.Verify("wrong", user.PasswordHash, user.PasswordSalt)).Returns(false);
        var service = CreateService(users: users, passwordHasher: hasher);

        var result = await service.LoginAsync("admin", "wrong");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsFailedResult()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUserNameAsync("missing", default)).ReturnsAsync((User?)null);
        var service = CreateService(users: users);

        var result = await service.LoginAsync("missing", "secret");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsFailedResult()
    {
        var user = CreateUser();
        user.IsActive = false;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUserNameAsync("admin", default)).ReturnsAsync(user);
        var service = CreateService(users: users);

        var result = await service.LoginAsync("admin", "secret");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreatePersistentSessionAsync_StoresHashedTokenAndExpiry()
    {
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        users.Setup(x => x.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        var service = CreateService(users: users, unitOfWork: unitOfWork);

        var session = await service.CreatePersistentSessionAsync(user.Id);

        Assert.Equal(user.Id, session.UserId);
        Assert.Equal(user.UserName, session.Username);
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.NotNull(user.LastRefreshToken);
        Assert.NotEqual(session.RefreshToken, user.LastRefreshToken);
        Assert.True(user.SessionExpiresAtUtc > DateTime.UtcNow.AddDays(29));
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ValidToken_ReturnsTrueAndSetsCurrentUser()
    {
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var context = new Mock<ICurrentUserContext>();
        var unitOfWork = new Mock<IUnitOfWork>();
        users.Setup(x => x.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        var service = CreateService(users: users, currentUserContext: context, unitOfWork: unitOfWork);
        var session = await service.CreatePersistentSessionAsync(user.Id);
        unitOfWork.Invocations.Clear();

        var isValid = await service.ValidateRefreshTokenAsync(user.Id, session.RefreshToken);

        Assert.True(isValid);
        context.Verify(x => x.Set(user), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_InvalidToken_ClearsStoredSession()
    {
        var user = CreateUser();
        var users = new Mock<IUserRepository>();
        var context = new Mock<ICurrentUserContext>();
        var unitOfWork = new Mock<IUnitOfWork>();
        users.Setup(x => x.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        var service = CreateService(users: users, currentUserContext: context, unitOfWork: unitOfWork);
        _ = await service.CreatePersistentSessionAsync(user.Id);
        unitOfWork.Invocations.Clear();

        var isValid = await service.ValidateRefreshTokenAsync(user.Id, "invalid-token");

        Assert.False(isValid);
        Assert.Null(user.LastRefreshToken);
        Assert.Null(user.SessionExpiresAtUtc);
        context.Verify(x => x.Clear(), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ClearPersistentSessionAsync_RemovesStoredTokenWithoutClearingCurrentUserContext()
    {
        var user = CreateUser();
        user.LastRefreshToken = "ABC123";
        user.SessionExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        var users = new Mock<IUserRepository>();
        var context = new Mock<ICurrentUserContext>();
        var unitOfWork = new Mock<IUnitOfWork>();
        context.Setup(x => x.CurrentUser).Returns(user);
        users.Setup(x => x.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        var service = CreateService(users: users, currentUserContext: context, unitOfWork: unitOfWork);

        await service.ClearPersistentSessionAsync(user.Id);

        Assert.Null(user.LastRefreshToken);
        Assert.Null(user.SessionExpiresAtUtc);
        context.Verify(x => x.Clear(), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }
    [Fact]
    public void GetPasswordValidationError_WeakPassword_ReturnsHelpfulMessage()
    {
        var error = AuthenticationService.GetPasswordValidationError("weakpass");

        Assert.Equal("Password must be at least 8 characters and include at least 1 uppercase letter and 1 number.", error);
    }

    [Fact]
    public void GetPasswordValidationError_ValidPassword_ReturnsNull()
    {
        var error = AuthenticationService.GetPasswordValidationError("Strong123");

        Assert.Null(error);
    }

    private static AuthenticationService CreateService(
        Mock<IUserRepository>? users = null,
        Mock<IPasswordHasher>? passwordHasher = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        return new AuthenticationService(
            (users ?? new Mock<IUserRepository>()).Object,
            (passwordHasher ?? new Mock<IPasswordHasher>()).Object,
            (currentUserContext ?? new Mock<ICurrentUserContext>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static User CreateUser() =>
        new()
        {
            Id = 1,
            UserName = "admin",
            DisplayName = "Admin User",
            Email = "admin@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true
        };
}

