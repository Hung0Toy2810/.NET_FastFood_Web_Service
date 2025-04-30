using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LapTrinhWindows.Middleware
{
    // Custom exception dùng để đại diện cho các lỗi xác thực
    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message) { }
        public AuthenticationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class AuthenticationSetup
    {
        // Phương thức mở rộng để cấu hình JWT authentication
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        {
            // Lấy secret key từ cấu hình và tạo khóa mã hóa
            var jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key cannot be empty.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Cấu hình tham số xác thực token
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = config["Jwt:Issuer"],
                        ValidAudience = config["Jwt:Audience"],
                        IssuerSigningKey = key,
                        ClockSkew = TimeSpan.FromMinutes(5),
                        RoleClaimType = ClaimTypes.Role // Thiết lập kiểu claim cho vai trò
                    };

                    // Cấu hình các sự kiện xử lý trong quá trình xác thực JWT
                    options.Events = new JwtBearerEvents
                    {
                        // Khi nhận được token từ request
                        OnMessageReceived = context =>
                        {
                            var authHeader = context.Request.Headers["Authorization"].ToString();
                            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                            {
                                _logger.LogWarning("No valid Bearer token found in request. Header: {AuthHeader}", authHeader);
                            }
                            else
                            {
                                // Cắt bỏ "Bearer " để lấy token thật sự
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                                _logger.LogInformation("Token received: {Token}", context.Token);
                            }
                            return Task.CompletedTask;
                        },

                        // Khi xác thực token thất bại
                        OnAuthenticationFailed = context =>
                        {
                            _logger.LogError("Authentication failed: {Message}", context.Exception.Message);
                            throw new SecurityTokenException($"Authentication failed: {context.Exception.Message}", context.Exception);
                        },

                        // Khi token đã được xác thực thành công
                        OnTokenValidated = async context =>
                        {
                            var token = context.SecurityToken as JwtSecurityToken;

                            // Trường hợp token bị lỗi format nên không parse được
                            if (token == null)
                            {
                                var authHeader = context.Request.Headers["Authorization"].ToString();
                                _logger.LogWarning("Invalid token format. Header: {AuthHeader}", authHeader);
                                try
                                {
                                    var handler = new JwtSecurityTokenHandler();
                                    var tokenToValidate = authHeader.StartsWith("Bearer ") ? authHeader.Substring("Bearer ".Length).Trim() : authHeader;
                                    var validatedToken = handler.ValidateToken(tokenToValidate, options.TokenValidationParameters, out var securityToken);
                                    context.SecurityToken = securityToken;
                                    context.Principal = validatedToken;
                                    token = securityToken as JwtSecurityToken;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("Manual token validation failed: {Message}", ex.Message);
                                    throw new AuthenticationException("Invalid token format.", ex);
                                }
                            }

                            // Trích xuất thông tin cần thiết từ claims
                            var jti = token?.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                            var userId = token?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                            var clientIp = token?.Claims?.FirstOrDefault(c => c.Type == "client_ip")?.Value;

                            // Kiểm tra các claim bắt buộc
                            if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(clientIp))
                            {
                                _logger.LogWarning("Missing required claims. JTI: {Jti}, UserId: {UserId}, ClientIp: {ClientIp}", jti, userId, clientIp);
                                throw new AuthenticationException("Token is missing required claims.");
                            }

                            // Kiểm tra vai trò người dùng nếu endpoint yêu cầu [Authorize(Roles = ...)]
                            var endpoint = context.HttpContext.GetEndpoint();
                            if (endpoint?.Metadata.GetMetadata<AuthorizeAttribute>() is AuthorizeAttribute authAttribute)
                            {
                                var requiredRoles = authAttribute.Roles?.Split(',').Select(r => r.Trim()).ToList();
                                var userRoles = context.Principal?.Claims
                                    .Where(c => c.Type == ClaimTypes.Role)
                                    .Select(c => c.Value)
                                    .ToList();

                                if (requiredRoles != null && requiredRoles.Any() &&
                                    (userRoles == null || !requiredRoles.Any(r => userRoles.Contains(r))))
                                {
                                    _logger.LogWarning("User {UserId} lacks required roles. Required: {RequiredRoles}, Found: {UserRoles}",
                                        userId, string.Join(", ", requiredRoles), string.Join(", ", userRoles ?? new List<string>()));
                                    throw new AuthenticationException($"User does not have the required roles. Required: {string.Join(", ", requiredRoles)}");
                                }
                            }

                            // Lấy Redis database
                            var redis = context.HttpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                            var db = redis.GetDatabase();

                            // Kiểm tra xem token đã bị thu hồi chưa
                            var isRevoked = await db.StringGetAsync($"revoked:{jti}");
                            if (!string.IsNullOrEmpty(isRevoked) && isRevoked == "true")
                            {
                                _logger.LogWarning("Token with JTI {Jti} has been revoked", jti);
                                throw new AuthenticationException("Token has been revoked.");
                            }

                            // Kiểm tra session trong Redis
                            var sessionKey = $"session:{userId}";
                            var sessionData = await db.StringGetAsync(sessionKey);
                            if (string.IsNullOrEmpty(sessionData))
                            {
                                _logger.LogWarning("No session found for user {UserId}", userId);
                                throw new AuthenticationException("Session not found.");
                            }

                            // Kiểm tra tính hợp lệ của session
                            var sessionParts = sessionData.ToString().Split('|');
                            if (sessionParts.Length != 7 || sessionParts[0] != jti || sessionParts[2] != clientIp || sessionParts[3] != userId)
                            {
                                _logger.LogWarning("Session mismatch for user {UserId}. Expected JTI: {Jti}, IP: {ClientIp}, UserId: {UserId}. Found: {SessionData}",
                                    userId, jti, clientIp, userId, sessionData);
                                throw new AuthenticationException("Session data mismatch.");
                            }

                            // Kiểm tra thời gian login còn hợp lệ (không quá 7 ngày)
                            if (!long.TryParse(sessionParts[6], out var loginTimestamp))
                            {
                                _logger.LogWarning("Invalid login timestamp for user {UserId}: {Timestamp}", userId, sessionParts[6]);
                                throw new AuthenticationException("Invalid session timestamp.");
                            }

                            var loginTime = DateTimeOffset.FromUnixTimeSeconds(loginTimestamp).UtcDateTime;
                            var sessionDuration = DateTime.UtcNow - loginTime;
                            if (sessionDuration.TotalDays > 7)
                            {
                                _logger.LogWarning("Session expired for user {UserId}. Login required.", userId);
                                await db.KeyDeleteAsync(sessionKey);
                                await db.KeyDeleteAsync($"refresh:{sessionParts[1]}"); // Xoá luôn refresh token nếu có
                                throw new AuthenticationException("Session expired. Please log in again.");
                            }

                            // Ghi log thông tin xác thực thành công
                            var claims = context.Principal?.Claims?.Select(c => $"{c.Type}: {c.Value}") ?? Enumerable.Empty<string>();
                            _logger.LogInformation("Token validated for user {UserId}. Claims: {Claims}", userId, string.Join(", ", claims));
                        },

                        // Khi truy cập bị từ chối (ví dụ thiếu quyền)
                        OnChallenge = context =>
                        {
                            _logger.LogWarning("Access denied for request. Reason: {ErrorDescription}", context.ErrorDescription);
                            throw new UnauthorizedAccessException("Access denied. You do not have the required permissions.");
                        }
                    };
                });

            return services;
        }

        // Logger cục bộ dùng trong lớp này
        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("JwtAuthentication");
    }
}
