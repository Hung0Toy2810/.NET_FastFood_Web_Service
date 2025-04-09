using LapTrinhWindows.Middleware;
using Microsoft.EntityFrameworkCore;
using LapTrinhWindows.Services;
using LapTrinhWindows.Repositories.CustomerRepository;
using LapTrinhWindows.Services.DbContextFactory;
using Minio;
using LapTrinhWindows.Repositories.Minio;
using LapTrinhWindows.Services.Minio;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace LapTrinhWindows
{
    public static class HostBuilderConfig
    {
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    var connectionString = configuration.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                    
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(connectionString)
                               .UseLazyLoadingProxies());

                    try
                    {
                        var redisConfig = ConfigurationOptions.Parse($"{configuration["Redis:Host"]}:{configuration["Redis:Port"]}");
                        redisConfig.Password = configuration["Redis:Password"];
                        redisConfig.AbortOnConnectFail = false; 
                        var redis = ConnectionMultiplexer.Connect(redisConfig);
                        services.AddSingleton<IConnectionMultiplexer>(redis);
                    }
                    catch (RedisConnectionException ex)
                    {
                        var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("Redis");
                        logger?.LogError(ex, "Failed to connect to Redis at {Host}:{Port}", configuration["Redis:Host"], configuration["Redis:Port"]);
                        throw;
                    }

                    services.AddControllers()
                        .AddApplicationPart(typeof(Program).Assembly);

                    services.AddLogging();
                    services.AddScoped<IDbContextFactory, DbContextFactory>();
                    services.AddScoped<IJwtTokenService, JwtTokenService>();
                    services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
                    services.AddScoped<ICustomerRepository, CustomerRepository>();
                    services.AddScoped<ICustomerService, CustomerService>();
                    services.AddScoped<ICustomerLoginService, CustomerLoginService>();

                    services.AddSingleton<IMinioClient>(sp =>
                    {
                        var minioConfig = configuration.GetSection("Minio");
                        return new MinioClient()
                            .WithEndpoint(minioConfig["Endpoint"])
                            .WithCredentials(minioConfig["AccessKey"], minioConfig["SecretKey"])
                            .WithSSL(minioConfig.GetValue<bool>("Secure"))
                            .Build();
                    });

                    services.AddScoped<IFileRepository, MinioFileRepository>();
                    services.AddScoped<IFileService, FileService>();

                    services.AddJwtAuthentication(configuration);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.Configure(app =>
                    {
                        app.UseExceptionHandlingMiddleware();
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });
                });
    }
}