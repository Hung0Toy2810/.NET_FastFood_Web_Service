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
        Task<string> GenerateTokenAsync(string id, string username, string role);
        Task RevokeTokenAsync(string token);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;
        private readonly IConnectionMultiplexer _redis;

        public JwtTokenService(IConfiguration config, IConnectionMultiplexer redis)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        }

        public Task<string> GenerateTokenAsync(string id, string username, string role)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role))
            {
                throw new ArgumentException("Id, username và role không được để trống");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }

        public async Task RevokeTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("Token cannot be null or empty.", nameof(token));

            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(token))
                throw new ArgumentException("Invalid token format.", nameof(token));

            var jwtToken = tokenHandler.ReadJwtToken(token);
            var expiresAt = jwtToken.ValidTo;

            if (expiresAt < DateTime.UtcNow)
                throw new InvalidOperationException("Token is already expired.");

            var db = _redis.GetDatabase();
            var ttl = expiresAt - DateTime.UtcNow;
            await db.StringSetAsync($"revoked:{token}", "true", ttl);
        }
    }
}