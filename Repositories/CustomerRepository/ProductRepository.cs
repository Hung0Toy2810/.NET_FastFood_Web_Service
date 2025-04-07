using LapTrinhWindows.Models;
using Microsoft.EntityFrameworkCore;

namespace LapTrinhWindows.Repositories.CustomerRepository
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllProducts();
        Task<Product> GetProductById(int id);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Product>> GetAllProducts()
        {
            var products = await _context.Products.ToListAsync();

            foreach (var product in products)
            {
                if (product.Category != null) 
                {
                    await _context.Entry(product.Category)
                        .Collection(c => c.Products)
                        .LoadAsync();
                }
            }

            return products;
        }

        public async Task<Product> GetProductById(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }
            if (product.Category != null)
            {
                await _context.Entry(product.Category)
                    .Collection(c => c.Products)
                    .LoadAsync();
            }

            return product;
        }
    }
}