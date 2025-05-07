namespace LapTrinhWindows.Repositories.VariantRepository
{
    public interface IAttributeRepository
    {
        Task<LapTrinhWindows.Models.Attribute?> GetAttributeByIdAsync(int attributeId);
        Task<LapTrinhWindows.Models.Attribute?> GetAttributeByNameAsync(string attributeName);
        Task AddAttributeAsync(LapTrinhWindows.Models.Attribute attribute);
    }

    public class AttributeRepository : IAttributeRepository
    {
        private readonly ApplicationDbContext _context;

        public AttributeRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<LapTrinhWindows.Models.Attribute?> GetAttributeByIdAsync(int attributeId)
        {
            return await _context.Attributes.FindAsync(attributeId);
        }

        public async Task<LapTrinhWindows.Models.Attribute?> GetAttributeByNameAsync(string attributeName)
        {
            return await _context.Attributes
                .FirstOrDefaultAsync(a => a.AttributeName == attributeName);
        }

        public async Task AddAttributeAsync(LapTrinhWindows.Models.Attribute attribute)
        {
            await _context.Attributes.AddAsync(attribute);
            
        }
    }
}