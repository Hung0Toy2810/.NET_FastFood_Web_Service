using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LapTrinhWindows.Middleware
{
    public static class AuthenticationSetup
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        {
            var jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key không được để trống.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
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
                        RoleClaimType = ClaimTypes.Role
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var authHeader = context.Request.Headers["Authorization"].ToString();
                            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                            {
                                _logger.LogWarning("No valid Bearer token found in request. Header: {AuthHeader}", authHeader);
                            }
                            else
                            {
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                                _logger.LogInformation("Token received: {Token}", context.Token);
                            }
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            _logger.LogError("Authentication failed: {Message}", context.Exception.Message);
                            throw new SecurityTokenException($"Authentication failed: {context.Exception.Message}");
                        },
                        OnTokenValidated = async context =>
                        {
                            var token = context.SecurityToken as JwtSecurityToken;
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
                                    context.Fail("Invalid token format.");
                                    return;
                                }
                            }

                            var jti = token?.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                            var userId = token?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                            var clientIp = token?.Claims?.FirstOrDefault(c => c.Type == "client_ip")?.Value;

                            if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(clientIp))
                            {
                                _logger.LogWarning("Missing required claims. JTI: {Jti}, UserId: {UserId}, ClientIp: {ClientIp}", jti, userId, clientIp);
                                context.Fail("Token is missing required claims.");
                                return;
                            }

                            // check role
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
                                    context.Fail($"User does not have the required roles. Required: {string.Join(", ", requiredRoles)}");
                                    return;
                                }
                            }

                            var redis = context.HttpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                            var db = redis.GetDatabase();

                            var isRevoked = await db.StringGetAsync($"revoked:{jti}");
                            if (!string.IsNullOrEmpty(isRevoked) && isRevoked == "true")
                            {
                                _logger.LogWarning("Token with JTI {Jti} has been revoked", jti);
                                context.Fail("Token has been revoked.");
                                return;
                            }

                            var sessionKey = $"session:{userId}";
                            var sessionData = await db.StringGetAsync(sessionKey);
                            if (string.IsNullOrEmpty(sessionData))
                            {
                                _logger.LogWarning("No session found for user {UserId}", userId);
                                context.Fail("Session not found.");
                                return;
                            }

                            var sessionParts = sessionData.ToString().Split('|');
                            if (sessionParts.Length != 7 || sessionParts[0] != jti || sessionParts[2] != clientIp || sessionParts[3] != userId)
                            {
                                _logger.LogWarning("Session mismatch for user {UserId}. Expected JTI: {Jti}, IP: {ClientIp}, UserId: {UserId}. Found: {SessionData}", 
                                    userId, jti, clientIp, userId, sessionData);
                                context.Fail("Session data mismatch.");
                                return;
                            }

                            if (!long.TryParse(sessionParts[6], out var loginTimestamp))
                            {
                                _logger.LogWarning("Invalid login timestamp for user {UserId}: {Timestamp}", userId, sessionParts[6]);
                                context.Fail("Invalid session timestamp.");
                                return;
                            }

                            var loginTime = DateTimeOffset.FromUnixTimeSeconds(loginTimestamp).UtcDateTime;
                            var sessionDuration = DateTime.UtcNow - loginTime;
                            if (sessionDuration.TotalDays > 7)
                            {
                                _logger.LogWarning("Session expired for user {UserId}. Login required.", userId);
                                await db.KeyDeleteAsync(sessionKey);
                                await db.KeyDeleteAsync($"refresh:{sessionParts[1]}");
                                context.Fail("Session expired. Please log in again.");
                                return;
                            }

                            var claims = context.Principal?.Claims?.Select(c => $"{c.Type}: {c.Value}") ?? Enumerable.Empty<string>();
                            _logger.LogInformation("Token validated for user {UserId}. Claims: {Claims}", userId, string.Join(", ", claims));
                        },
                        OnChallenge = context =>
                        {
                            _logger.LogWarning("Access denied for request. Reason: {ErrorDescription}", context.ErrorDescription);
                            context.HandleResponse();
                            throw new UnauthorizedAccessException("Access denied. You do not have the required permissions.");
                        }
                    };
                });

            return services;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("JwtAuthentication");
    }
}