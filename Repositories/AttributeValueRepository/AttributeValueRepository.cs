namespace LapTrinhWindows.Repositories.VariantRepository
{
    public interface IAttributeValueRepository
    {
        Task<AttributeValue?> GetAttributeValueByIdAsync(int attributeValueId);
        Task<bool> AttributeValueExistsAsync(int attributeValueId);
        Task<AttributeValue?> GetAttributeValueByNameAsync(int attributeId, string value);
        Task AddAttributeValueAsync(AttributeValue attributeValue);
    }
    public class AttributeValueRepository : IAttributeValueRepository
    {
        private readonly ApplicationDbContext _context;

        public AttributeValueRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<AttributeValue?> GetAttributeValueByIdAsync(int attributeValueId)
        {
            return await _context.AttributeValues.FindAsync(attributeValueId);
        }

        public async Task<bool> AttributeValueExistsAsync(int attributeValueId)
        {
            return await _context.AttributeValues.AnyAsync(av => av.AttributeValueID == attributeValueId);
        }

        public async Task<AttributeValue?> GetAttributeValueByNameAsync(int attributeId, string value)
        {
            return await _context.AttributeValues
                .FirstOrDefaultAsync(av => av.AttributeID == attributeId && av.Value == value);
        }

        public async Task AddAttributeValueAsync(AttributeValue attributeValue)
        {
            await _context.AttributeValues.AddAsync(attributeValue);
        }
    }
}