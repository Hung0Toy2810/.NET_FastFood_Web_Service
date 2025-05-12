namespace LapTrinhWindows.Repositories.VariantRepository
{
    public interface IVariantRepository
    {
        Task AddVariantAsync(Variant variant);
        Task<bool> VariantExistsForProductAsync(int productId, List<int> attributeValueIds);
        Task<bool> SkuExistsAsync(string sku);
        Task UpdateVariantPriceBySkuAsync(string sku, decimal price);
        Task<Variant?> GetVariantBySkuAsync(string sku);
        // get variant by id
        Task<Variant?> GetVariantByIdAsync(int variantId);
        Task<Dictionary<string, Variant>> GetVariantsBySkusAsync(IEnumerable<string> skus);
        Task UpdateVariantAsync(Variant variant);
        
    }
    public class VariantRepository : IVariantRepository
    {
        private readonly ApplicationDbContext _context;

        public VariantRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddVariantAsync(Variant variant)
        {
            await _context.Variants.AddAsync(variant);
        }

        public async Task<bool> VariantExistsForProductAsync(int productId, List<int> attributeValueIds)
        {
            return await _context.Variants
                .Where(v => v.ProductID == productId)
                .AnyAsync(v => v.VariantAttributes.Select(va => va.AttributeValueID)
                    .OrderBy(id => id)
                    .SequenceEqual(attributeValueIds.OrderBy(id => id)));
        }
        public async Task<bool> SkuExistsAsync(string sku) 
        {
            return await _context.Variants.AnyAsync(v => v.SKU == sku);
        }
        public async Task UpdateVariantPriceBySkuAsync(string sku, decimal price)
        {
            var variant = await _context.Variants
                .FirstOrDefaultAsync(v => v.SKU == sku);

            if (variant == null)
            {
                throw new KeyNotFoundException($"Variant with SKU '{sku}' not found.");
            }

            variant.Price = price;
            _context.Variants.Update(variant);
            await _context.SaveChangesAsync();
        }
        public async Task<Variant?> GetVariantBySkuAsync(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                throw new ArgumentException("SKU cannot be empty.", nameof(sku));
            }

            var variant = await _context.Variants
                .Include(v => v.VariantAttributes)
                .ThenInclude(va => va.AttributeValue)
                .FirstOrDefaultAsync(v => v.SKU == sku);

            return variant;
        }
        public async Task<Variant?> GetVariantByIdAsync(int variantId)
        {
            return await _context.Variants
                .Include(v => v.VariantAttributes)
                .ThenInclude(va => va.AttributeValue)
                .FirstOrDefaultAsync(v => v.VariantID == variantId);
        }
        public async Task<Dictionary<string, Variant>> GetVariantsBySkusAsync(IEnumerable<string> skus)
        {
            return await _context.Variants
                .Include(v => v.Product)
                .Where(v => skus.Contains(v.SKU))
                .ToDictionaryAsync(v => v.SKU, v => v);
        }
        public async Task UpdateVariantAsync(Variant variant)
        {
            _context.Variants.Update(variant);
            await _context.SaveChangesAsync();
        }
    }
}