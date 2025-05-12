namespace LapTrinhWindows.Repositories.ProductTagRepository
{
    public interface IProductTagRepository
    {
        Task<List<Tag>> GetTagsByNamesAsync(List<string> tagNames);
        Task<Tag> CreateTagAsync(string tagName);
        Task<List<ProductTag>> GetProductTagsByProductIdAsync(int productId);
        Task DeleteProductTagsByProductIdAsync(int productId);
        Task AddProductTagsAsync(List<ProductTag> productTags);
        Task<List<Tag>> GetUnusedTagsAsync();
        Task DeleteTagsAsync(List<int> tagIds);
        Task <List<string>> GetProductTagNamesByProductIdAsync(int productId);
    }
    public class ProductTagRepository : IProductTagRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductTagRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Tag>> GetTagsByNamesAsync(List<string> tagNames)
        {
            return await _context.Tags
                .Where(t => tagNames.Contains(t.TagName))
                .ToListAsync();
        }

        public async Task<Tag> CreateTagAsync(string tagName)
        {
            var tag = new Tag { TagName = tagName };
            await _context.Tags.AddAsync(tag);
            await _context.SaveChangesAsync();
            return tag;
        }

        public async Task<List<ProductTag>> GetProductTagsByProductIdAsync(int productId)
        {
            return await _context.ProductTags
                .Where(pt => pt.ProductID == productId)
                .ToListAsync();
        }

        public async Task DeleteProductTagsByProductIdAsync(int productId)
        {
            var productTags = await _context.ProductTags
                .Where(pt => pt.ProductID == productId)
                .ToListAsync();
            _context.ProductTags.RemoveRange(productTags);
            await _context.SaveChangesAsync();
        }

        public async Task AddProductTagsAsync(List<ProductTag> productTags)
        {
            await _context.ProductTags.AddRangeAsync(productTags);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Tag>> GetUnusedTagsAsync()
        {
            return await _context.Tags
                .Where(t => !t.ProductTags.Any())
                .ToListAsync();
        }

        public async Task DeleteTagsAsync(List<int> tagIds)
        {
            var tags = await _context.Tags
                .Where(t => tagIds.Contains(t.TagID))
                .ToListAsync();
            _context.Tags.RemoveRange(tags);
            await _context.SaveChangesAsync();
        }
        public async Task <List<string>> GetProductTagNamesByProductIdAsync(int productId)
        {
            return await _context.ProductTags
                .Where(pt => pt.ProductID == productId)
                .Include(pt => pt.Tag) 
                .Select(pt => pt.Tag.TagName)
                .OrderByDescending(tagName => tagName) 
                .ToListAsync();
        }
    }
}