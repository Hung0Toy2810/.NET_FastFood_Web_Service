using Microsoft.Extensions.DependencyInjection;
using LapTrinhWindows.Models;

namespace LapTrinhWindows.Services.DbContextFactory
{
    public interface IDbContextFactory
    {
        ApplicationDbContext CreateContext();
    }

    public class DbContextFactory : IDbContextFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DbContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public ApplicationDbContext CreateContext()
        {
            // Tạo một DbContext mới từ DI container
            return _serviceProvider.GetRequiredService<ApplicationDbContext>();
        }
    }
}