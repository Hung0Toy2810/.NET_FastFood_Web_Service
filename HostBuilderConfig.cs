using LapTrinhWindows.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Proxies;
using LapTrinhWindows.Services;
using LapTrinhWindows.Repositories.CustomerRepository;
using LapTrinhWindows.Services.DbContextFactory;
using Minio;
using LapTrinhWindows.Repositories.Minio;
using LapTrinhWindows.Services.Minio;

namespace LapTrinhWindows
{
    public static class HostBuilderConfig
    {
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var connectionString = hostContext.Configuration.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(connectionString));
                    // use lazyloading
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseLazyLoadingProxies()
                               .UseSqlServer(connectionString));

                    services.AddControllers()
                        .AddApplicationPart(typeof(Program).Assembly);

                    services.AddLogging();
                    services.AddScoped<IDbContextFactory, DbContextFactory>();
                    services.AddScoped<IJwtTokenService, JwtTokenService>();
                    services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
                    services.AddScoped<ICustomerRepository, CustomerRepository>();
                    services.AddScoped<ICustomerService, CustomerService>();
                    services.AddScoped<ICustomerLoginService, CustomerLoginService>();

                    // Thêm MinIO configuration
                    services.AddSingleton<IMinioClient>(sp =>
                    {
                        var configuration = hostContext.Configuration;
                        var minioConfig = configuration.GetSection("Minio");
                        
                        return new MinioClient()
                            .WithEndpoint(minioConfig["Endpoint"])
                            .WithCredentials(
                                minioConfig["AccessKey"],
                                minioConfig["SecretKey"])
                            .WithSSL(minioConfig.GetValue<bool>("Secure"))
                            .Build();
                    });

                    
                    services.AddScoped<IFileRepository, MinioFileRepository>();
                    services.AddScoped<IFileService, FileService>();

                    services.AddJwtAuthentication(hostContext.Configuration);
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