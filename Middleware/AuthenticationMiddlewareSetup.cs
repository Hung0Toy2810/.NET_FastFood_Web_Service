using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
            if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key phải dài ít nhất 32 ký tự.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            Console.WriteLine("JWT Key in AuthenticationSetup: " + jwtKey); // Log để kiểm tra

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
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.Name
                    };

                    options.RequireHttpsMetadata = false; // Dùng localhost
                    options.SaveToken = true; // Lưu token

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var authHeader = context.Request.Headers["Authorization"].ToString();
                            Console.WriteLine("Auth Header: " + authHeader);

                            if (string.IsNullOrEmpty(context.Token) && authHeader.StartsWith("Bearer "))
                            {
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                                Console.WriteLine("Manually set Token: " + context.Token);
                            }

                            Console.WriteLine("Extracted Token: " + context.Token);
                            if (string.IsNullOrEmpty(context.Token))
                            {
                                Console.WriteLine("No token found in request");
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            Console.WriteLine("OnTokenValidated started");
                            var token = context.SecurityToken as JwtSecurityToken;
                            if (token == null)
                            {
                                Console.WriteLine("Token is invalid");
                                var authHeader = context.Request.Headers["Authorization"].ToString();
                                Console.WriteLine("Original Token from Header: " + authHeader);
                                try
                                {
                                    var handler = new JwtSecurityTokenHandler();
                                    var tokenToValidate = authHeader.StartsWith("Bearer ") ? authHeader.Substring("Bearer ".Length).Trim() : authHeader;
                                    var validatedToken = handler.ValidateToken(tokenToValidate, options.TokenValidationParameters, out var securityToken);
                                    Console.WriteLine("Manual validation succeeded: " + securityToken);

                                    // Gán thủ công SecurityToken và Principal
                                    context.SecurityToken = securityToken;
                                    context.Principal = validatedToken;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Manual validation failed: " + ex.Message);
                                    context.Fail("Invalid token format.");
                                    return Task.CompletedTask;
                                }
                            }
                            var claims = context.Principal?.Claims?.Select(c => $"{c.Type}: {c.Value}") ?? Enumerable.Empty<string>();
                            Console.WriteLine("Claims: " + string.Join(", ", claims));
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            Console.WriteLine("OnChallenge is running");
                            context.HandleResponse();
                            throw new UnauthorizedAccessException("Access denied. You do not have the required permissions.");
                        }
                    };
                });

            return services;
        }
    }
}