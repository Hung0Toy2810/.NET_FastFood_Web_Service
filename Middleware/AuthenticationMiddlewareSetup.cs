using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LapTrinhWindows.Middleware
{
    public static class AuthenticationSetup
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        {
            // Lấy khóa bí mật từ cấu hình
            var jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key không được để trống.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            // Cấu hình xác thực JWT
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
                        IssuerSigningKey = key 
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Console.WriteLine("OnAuthenticationFailed: " + context.Exception.Message);

                            // Ném ngoại lệ SecurityTokenException để ExceptionHandlingMiddleware xử lý
                            throw new SecurityTokenException("Authentication failed: " + context.Exception.Message);
                        },
                        OnTokenValidated = async context =>
                        {
                            Console.WriteLine("OnTokenValidated: Checking policy");
                            var user = context.Principal;
                            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
                            if (user == null)
                            {
                                context.Fail("Access denied. User information is missing.");
                                return;
                            }

                            var policyResult = await authService.AuthorizeAsync(user, null, "ManagerOnly"); 

                            if (!policyResult.Succeeded)
                            {
                                Console.WriteLine("Policy failed: Throwing UnauthorizedAccessException");
                                context.Fail("Access denied. You do not have the required permissions.");
                            }
                        },
                        OnChallenge = context =>
                        {
                            Console.WriteLine("OnChallenge is running");

                            // Ngăn phản hồi mặc định
                            context.HandleResponse();

                            
                            throw new UnauthorizedAccessException("Access denied. You do not have the required permissions.");
                        }
                    };
                });

            
            services.AddAuthorization(options =>
            {
                options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager")); 
                options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer")); 
                options.AddPolicy("EmployeeOnly", policy => policy.RequireRole("Employee")); 
            });

            return services;
        }
    }
}