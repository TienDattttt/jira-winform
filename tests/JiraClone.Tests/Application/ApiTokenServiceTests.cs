using JiraClone.Application.Abstractions;
using JiraClone.Application.ApiTokens;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class ApiTokenServiceTests
{
    [Fact]
    public async Task CreateTokenAsync_ReturnsRawTokenAndStoresOnlyHash()
    {
        var user = CreateUser();
        var tokens = new Mock<IApiTokenRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        ApiToken? createdToken = null;

        users.Setup(x => x.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        tokens.Setup(x => x.AddAsync(It.IsAny<ApiToken>(), default))
            .Callback<ApiToken, CancellationToken>((token, _) =>
            {
                token.Id = 12;
                createdToken = token;
            })
            .Returns(Task.CompletedTask);

        var service = new ApiTokenService(tokens.Object, users.Object, unitOfWork.Object);

        var result = await service.CreateTokenAsync(user.Id, "Claude Desktop", DateTime.UtcNow.AddDays(30), [ApiTokenScope.ReadIssues, ApiTokenScope.ReadProjects]);

        Assert.Equal(12, result.TokenId);
        Assert.StartsWith("jdt_", result.RawToken);
        Assert.NotNull(createdToken);
        Assert.NotEqual(result.RawToken, createdToken!.TokenHash);
        Assert.Equal(64, createdToken.TokenHash.Length);
        Assert.Equal(2, createdToken.ScopeGrants.Count);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_UpdatesLastUsedAt()
    {
        var user = CreateUser();
        var tokens = new Mock<IApiTokenRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        ApiToken? storedToken = null;

        users.Setup(x => x.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        tokens.Setup(x => x.AddAsync(It.IsAny<ApiToken>(), default))
            .Callback<ApiToken, CancellationToken>((token, _) =>
            {
                token.Id = 7;
                storedToken = token;
            })
            .Returns(Task.CompletedTask);
        tokens.Setup(x => x.GetByHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync(() => storedToken);

        var service = new ApiTokenService(tokens.Object, users.Object, unitOfWork.Object);
        var generated = await service.CreateTokenAsync(user.Id, "CLI", null, [ApiTokenScope.ReadIssues]);
        unitOfWork.Invocations.Clear();

        var validated = await service.ValidateTokenAsync(generated.RawToken);

        Assert.NotNull(validated);
        Assert.NotNull(storedToken!.LastUsedAtUtc);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_RevokedOrExpiredToken_ReturnsNull()
    {
        var token = new ApiToken
        {
            Id = 5,
            UserId = 1,
            User = CreateUser(),
            Name = "Old token",
            Label = "Old token",
            TokenHash = new string('A', 64),
            IsRevoked = true,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            ScopeGrants = [new ApiTokenScopeGrant { ApiTokenId = 5, Scope = ApiTokenScope.ReadIssues }]
        };
        var tokens = new Mock<IApiTokenRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        tokens.Setup(x => x.GetByHashAsync(It.IsAny<string>(), default)).ReturnsAsync(token);

        var service = new ApiTokenService(tokens.Object, users.Object, unitOfWork.Object);

        var validated = await service.ValidateTokenAsync("jdt_dummy_token_value_1234567890");

        Assert.Null(validated);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_NonOwnerNonAdmin_Throws()
    {
        var owner = CreateUser(id: 1, userName: "owner", email: "owner@example.com");
        var requestor = CreateUser(id: 2, userName: "other", email: "other@example.com");
        var token = new ApiToken
        {
            Id = 9,
            UserId = owner.Id,
            User = owner,
            Name = "Desktop",
            Label = "Desktop",
            TokenHash = new string('B', 64),
            ScopeGrants = [new ApiTokenScopeGrant { ApiTokenId = 9, Scope = ApiTokenScope.ReadIssues }]
        };
        var tokens = new Mock<IApiTokenRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        tokens.Setup(x => x.GetByIdAsync(token.Id, default)).ReturnsAsync(token);
        users.Setup(x => x.GetByIdAsync(requestor.Id, default)).ReturnsAsync(requestor);

        var service = new ApiTokenService(tokens.Object, users.Object, unitOfWork.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RevokeTokenAsync(token.Id, requestor.Id));
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    private static User CreateUser(int id = 1, string userName = "admin", string email = "admin@example.com") =>
        new()
        {
            Id = id,
            UserName = userName,
            DisplayName = userName,
            Email = email,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
        };
}
