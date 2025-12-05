using FluentAssertions;
using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

#pragma warning disable CS8620

namespace IdentityService.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly IConfiguration _configuration;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _cacheServiceMock = new Mock<ICacheService>();
        
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Secret", "test-secret-key-minimum-32-characters-long"},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _authService = new AuthService(
            _userRepositoryMock.Object,
            _eventPublisherMock.Object,
            _cacheServiceMock.Object,
            _configuration);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WhenEmailIsUnique()
    {
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe"
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(request.Email, default))
            .ReturnsAsync((User?)null);

        var result = await _authService.RegisterAsync(request);

        result.Should().NotBeNull();
        result.Token.Should().NotBeEmpty();
        result.Email.Should().Be(request.Email);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync("domain.identity.UserCreated", It.IsAny<object>(), default), Times.Once);
    }
}
