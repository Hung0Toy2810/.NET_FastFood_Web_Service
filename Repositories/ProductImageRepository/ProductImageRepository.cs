namespace LapTrinhWindows.Repositories.ProductImageRepository
{
    public interface IProductImageRepository
    {
        Task<int> CountImagesByProductIdAsync(int productId);
        Task AddProductImageAsync(Models.ProductImage productImage);
        Task<List<Models.ProductImage>> AddProductImagesAsync(List<Models.ProductImage> productImages);
        Task<Models.ProductImage?> GetProductImageByIdAsync(int productImageId);
        Task<List<Models.ProductImage>> GetImagesByProductIdAsync(int productId);
        Task DeleteImagesByProductIdAsync(int productId);
    }
    public class ProductImageRepository : IProductImageRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductImageRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<int> CountImagesByProductIdAsync(int productId)
        {
            return await _context.ProductImages
                .CountAsync(pi => pi.ProductID == productId);
        }

        public async Task AddProductImageAsync(Models.ProductImage productImage)
        {
            await _context.ProductImages.AddAsync(productImage);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Models.ProductImage>> AddProductImagesAsync(List<Models.ProductImage> productImages)
        {
            await _context.ProductImages.AddRangeAsync(productImages);
            await _context.SaveChangesAsync();
            return productImages;
        }

        public async Task<Models.ProductImage?> GetProductImageByIdAsync(int productImageId)
        {
            return await _context.ProductImages
                .FirstOrDefaultAsync(pi => pi.ProductImageID == productImageId);
        }

        public async Task<List<Models.ProductImage>> GetImagesByProductIdAsync(int productId)
        {
            return await _context.ProductImages
                .Where(pi => pi.ProductID == productId)
                .ToListAsync();
        }

        public async Task DeleteImagesByProductIdAsync(int productId)
        {
            var images = await _context.ProductImages
                .Where(pi => pi.ProductID == productId)
                .ToListAsync();
            _context.ProductImages.RemoveRange(images);
            await _context.SaveChangesAsync();
        }
    }
}