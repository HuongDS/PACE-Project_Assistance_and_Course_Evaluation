using Microsoft.Extensions.Configuration;
using PACE.Application.Common.Interfaces;
using StackExchange.Redis;

namespace PACE.Infrastructure.Services;

public class UpstashRedisOtpCacheService : IOtpCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private const string KeyPrefix = "pace:otp:";

    public UpstashRedisOtpCacheService(IConfiguration config)
    {
        var connectionString = config["Redis:ConnectionString"] 
            ?? "trusty-ladybug-170669.upstash.io:6379,password=gQAAAAAAApqtAAIgcDEzZDE1MjBiYmM4OWI0MjNhYjQ4NGNlNjFkZDlhMjk2Ng==,ssl=True,abortConnect=False";

        // StackExchange.Redis ConfigurationOptions setup
        var options = ConfigurationOptions.Parse(connectionString);
        options.Ssl = true;
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 10000;
        options.SyncTimeout = 10000;

        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
    }

    public async Task SetOtpAsync(string email, string otpCode, TimeSpan expiry, CancellationToken ct = default)
    {
        var key = $"{KeyPrefix}{email.Trim().ToLowerInvariant()}";
        await _db.StringSetAsync(key, otpCode, expiry);
    }

    public async Task<string?> GetOtpAsync(string email, CancellationToken ct = default)
    {
        var key = $"{KeyPrefix}{email.Trim().ToLowerInvariant()}";
        var value = await _db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task RemoveOtpAsync(string email, CancellationToken ct = default)
    {
        var key = $"{KeyPrefix}{email.Trim().ToLowerInvariant()}";
        await _db.KeyDeleteAsync(key);
    }
}

