using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using StackExchange.Redis;

namespace LapTrinhWindows.Services
{
    public interface IJwtTokenService
    {
        Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(string id, string username, string role, string clientIp); // Xóa '?'
        Task RevokeTokenAsync(string token);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;
        private readonly IConnectionMultiplexer _redis;
        private readonly SymmetricSecurityKey _key;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(IConfiguration config, IConnectionMultiplexer redis, ILogger<JwtTokenService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        }

        public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(string id, string username, string role, string clientIp)
        {
            if (string.IsNullOrEmpty(clientIp))
                throw new ArgumentException("Client IP cannot be empty", nameof(clientIp));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("client_ip", clientIp) 
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshToken = Guid.NewGuid().ToString();
            var db = _redis.GetDatabase();

            // Lưu thông tin phiên: chỉ cho phép một phiên hoạt động
            var sessionKey = $"session:{id}";
            var existingSession = await db.StringGetAsync(sessionKey);
            if (!string.IsNullOrEmpty(existingSession))
            {
                var oldJti = existingSession.ToString().Split(':')[0];
                await db.StringSetAsync($"revoked:{oldJti}", "true", TimeSpan.FromMinutes(15));
            }

            // Lưu phiên mới: jti:refreshToken:clientIp
            await db.KeyDeleteAsync(sessionKey);
            await db.StringSetAsync(sessionKey, $"{token.Id}:{refreshToken}:{clientIp}", TimeSpan.FromDays(7));
            await db.StringSetAsync($"refresh:{refreshToken}", id, TimeSpan.FromDays(7));

            _logger.LogInformation("Generated tokens for user {Username} with IP {ClientIp}", username, clientIp);
            return (accessToken, refreshToken);
        }

        public async Task RevokeTokenAsync(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var jti = jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            var db = _redis.GetDatabase();
            var ttl = jwtToken.ValidTo - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero) throw new InvalidOperationException("Token is already expired.");

            await db.StringSetAsync($"revoked:{jti}", "true", ttl);
            _logger.LogInformation("Revoked token with JTI {Jti}", jti);
        }
    }
}