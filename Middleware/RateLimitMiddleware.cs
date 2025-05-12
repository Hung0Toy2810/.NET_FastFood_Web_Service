using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace LapTrinhWindows.Middleware
{
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RateLimitMiddleware> _logger;
        private readonly IConfiguration _config;

        public RateLimitMiddleware(RequestDelegate next, IConnectionMultiplexer redis, ILogger<RateLimitMiddleware> logger, IConfiguration config)
        {
            _next = next;
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(clientIp))
            {
                throw new InvalidOperationException("Client IP address is not available.");
            }

            var db = _redis.GetDatabase();
            var key = $"ratelimit:{clientIp}";
            var count = await db.StringIncrementAsync(key);

            if (count == 1)
            {
                await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
            }

            int requestLimit = _config.GetValue<int>("RateLimit:RequestsPerMinute", 100);

            if (count > requestLimit)
            {
                _logger.LogWarning("IP {ClientIp} exceeded rate limit: {Count} requests", clientIp, count);

                context.Response.StatusCode = 403;  
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Rate limit exceeded. You have been blocked.");
                return;
            }

            await _next(context);
        }
    }
}
