using IdentityService.Application.Services;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Mocks;

// Mock Event Publisher (no RabbitMQ needed)
public class MockEventPublisher : IEventPublisher
{
    private readonly ILogger<MockEventPublisher> _logger;

    public MockEventPublisher(ILogger<MockEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(string exchange, T @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Event published to {Exchange}: {@Event}", exchange, @event);
        return Task.CompletedTask;
    }
}

// Mock Cache Service (no Redis needed)
public class MockCacheService : ICacheService
{
    private readonly ILogger<MockCacheService> _logger;

    public MockCacheService(ILogger<MockCacheService> logger)
    {
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Mock: Cache GET {Key} - always returns null", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Mock: Cache SET {Key} with expiration {Expiration}", key, expiration);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Mock: Cache REMOVE {Key}", key);
        return Task.CompletedTask;
    }
}
