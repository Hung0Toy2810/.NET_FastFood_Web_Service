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
        Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(string id, string username, string role, string clientIp);
        Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken);
        // refreshemployeetoken
        Task RevokeTokenAsync(string token);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;
        private readonly IConnectionMultiplexer _redis;
        private readonly SymmetricSecurityKey _key;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(
            IConfiguration config,
            IConnectionMultiplexer redis,
            ILogger<JwtTokenService> logger
            )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        }

        public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(string id, string phoneNumber, string role, string clientIp)
        {
            if (string.IsNullOrEmpty(clientIp))
                throw new ArgumentException("Client IP cannot be empty", nameof(clientIp));

            string GenerateSecureRandomString(int byteLength)
            {
                byte[] randomBytes = new byte[byteLength];
                RandomNumberGenerator.Fill(randomBytes);
                return Convert.ToBase64String(randomBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');
            }

            var jti = GenerateSecureRandomString(32);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, phoneNumber),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
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
            var db = _redis.GetDatabase();

            string refreshToken;
            int maxAttempts = 3;
            int attempt = 0;
            do
            {
                if (attempt >= maxAttempts)
                {
                    _logger.LogError("Failed to generate unique refresh token for user {UserId} after {MaxAttempts} attempts", id, maxAttempts);
                    throw new InvalidOperationException("Unable to generate unique refresh token.");
                }
                refreshToken = GenerateSecureRandomString(32);
                attempt++;
            } while (await db.KeyExistsAsync($"refresh:{refreshToken}"));

            var sessionKey = $"session:{id}";
            var existingSession = await db.StringGetAsync(sessionKey);
            if (!string.IsNullOrEmpty(existingSession))
            {
                var oldJti = existingSession.ToString().Split('|')[0];
                await db.StringSetAsync($"revoked:{oldJti}", "true", TimeSpan.FromMinutes(15));
            }

            await db.KeyDeleteAsync(sessionKey);
            var loginTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sessionData = $"{jti}|{refreshToken}|{clientIp}|{id}|{phoneNumber}|{role}|{loginTimestamp}";
            Console.WriteLine("Saving session data to Redis: " + sessionData);
            await db.StringSetAsync(sessionKey, sessionData, TimeSpan.FromDays(7));
            await db.StringSetAsync($"refresh:{refreshToken}", id, TimeSpan.FromDays(7));

            _logger.LogInformation("Generated tokens for user {PhoneNumber} with IP {ClientIp}", phoneNumber, clientIp);
            return (accessToken, refreshToken);
        }

        public async Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken)
        {
            var db = _redis.GetDatabase();
            var userIdValue = await db.StringGetAsync($"refresh:{refreshToken}");

            string? userId = userIdValue.HasValue ? userIdValue.ToString() : null;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Invalid or expired refresh token: {RefreshToken}", refreshToken);
                throw new SecurityTokenException("Invalid or expired refresh token");
            }

            var sessionKey = $"session:{userId}";
            var sessionData = await db.StringGetAsync(sessionKey);
            if (!sessionData.HasValue || string.IsNullOrEmpty(sessionData.ToString()))
            {
                _logger.LogWarning("Session not found for user {UserId}", userId);
                throw new SecurityTokenException("Session not found");
            }

            var sessionParts = sessionData.ToString().Split('|');
            if (sessionParts.Length != 7 || sessionParts[1] != refreshToken)
            {
                _logger.LogWarning("Refresh token mismatch for user {UserId}", userId);
                throw new SecurityTokenException("Refresh token mismatch");
            }

            var oldJti = sessionParts[0];
            var clientIp = sessionParts[2];
            var storedUserId = sessionParts[3];
            var phoneNumber = sessionParts[4];
            var role = sessionParts[5];
            var loginTimestamp = long.Parse(sessionParts[6]);

            if (storedUserId != userId)
            {
                _logger.LogWarning("User ID mismatch for user {UserId}", userId);
                throw new SecurityTokenException("User ID mismatch");
            }

            var loginTime = DateTimeOffset.FromUnixTimeSeconds(loginTimestamp).UtcDateTime;
            var sessionDuration = DateTime.UtcNow - loginTime;
            if (sessionDuration.TotalDays > 7)
            {
                _logger.LogWarning("Session expired for user {UserId}. Login required.", userId);
                await db.KeyDeleteAsync(sessionKey);
                await db.KeyDeleteAsync($"refresh:{refreshToken}");
                throw new SecurityTokenException("Session expired. Please log in again.");
            }

            await db.StringSetAsync($"revoked:{oldJti}", "true", TimeSpan.FromMinutes(15));

            string GenerateSecureRandomString(int byteLength)
            {
                byte[] randomBytes = new byte[byteLength];
                RandomNumberGenerator.Fill(randomBytes);
                return Convert.ToBase64String(randomBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');
            }

            string newRefreshToken;
            int maxAttempts = 3;
            int attempt = 0;
            do
            {
                if (attempt >= maxAttempts)
                {
                    _logger.LogError("Failed to generate unique refresh token for user {UserId} after {MaxAttempts} attempts", userId, maxAttempts);
                    throw new InvalidOperationException("Unable to generate unique refresh token.");
                }
                newRefreshToken = GenerateSecureRandomString(32);
                attempt++;
            } while (await db.KeyExistsAsync($"refresh:{newRefreshToken}"));
            var jti = GenerateSecureRandomString(32);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, phoneNumber),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim("client_ip", clientIp)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
            );

            var newAccessToken = new JwtSecurityTokenHandler().WriteToken(token);

            // Cập nhật dữ liệu phiên mới, giữ nguyên loginTimestamp
            await db.KeyDeleteAsync(sessionKey);
            var newSessionData = $"{jti}|{newRefreshToken}|{clientIp}|{userId}|{phoneNumber}|{role}|{loginTimestamp}";
            Console.WriteLine("Saving new session data to Redis: " + newSessionData);
            await db.StringSetAsync(sessionKey, newSessionData, TimeSpan.FromDays(7 - sessionDuration.TotalDays));
            await db.StringSetAsync($"refresh:{newRefreshToken}", userId, TimeSpan.FromDays(7 - sessionDuration.TotalDays));
            await db.KeyDeleteAsync($"refresh:{refreshToken}");

            _logger.LogInformation("Refreshed token for user {UserId} with IP {ClientIp}", userId, clientIp);
            return (newAccessToken, newRefreshToken);
        }
        
        public async Task RevokeTokenAsync(string token)
        {
            // Khởi tạo handler để đọc JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("Invalid token format.");
                throw new SecurityTokenException("Invalid token format.");
            }

            // Đọc thông tin từ JWT
            var jwtToken = tokenHandler.ReadJwtToken(token);

            // Lấy JTI (JWT ID) - định danh duy nhất của token
            var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
            if (jtiClaim == null)
            {
                _logger.LogWarning("Token does not contain JTI.");
                throw new SecurityTokenException("Invalid token: JTI missing.");
            }
            var jti = jtiClaim.Value;

            // Tính thời gian sống còn lại của token
            var ttl = jwtToken.ValidTo - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero)
            {
                _logger.LogWarning("Token with JTI {Jti} is already expired.", jti);
                throw new InvalidOperationException("Token is already expired.");
            }

            // Đánh dấu token đã bị thu hồi trong Redis (key: revoked:{jti})
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"revoked:{jti}", "true", ttl);

            // Lấy userId từ claim để xóa session và refresh token liên quan
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                var userId = userIdClaim.Value;
                var sessionKey = $"session:{userId}";
                var sessionData = await db.StringGetAsync(sessionKey);

                // Nếu session tồn tại, xóa session và refresh token khỏi Redis
                if (sessionData.HasValue && !string.IsNullOrEmpty(sessionData.ToString()))
                {
                    var sessionParts = sessionData.ToString().Split('|');
                    if (sessionParts.Length == 7)
                    {
                        var refreshToken = sessionParts[1];
                        await db.KeyDeleteAsync(sessionKey);
                        await db.KeyDeleteAsync($"refresh:{refreshToken}");
                        _logger.LogInformation("Revoked session and refresh token for user {UserId}", userId);
                    }
                }
            }

            _logger.LogInformation("Revoked token with JTI {Jti}", jti);
        }
    }
}