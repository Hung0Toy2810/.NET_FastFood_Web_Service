namespace LapTrinhWindows.Repositories.VariantAttributeRepository
{
    public interface IVariantAttributeRepository
    {
        Task AddVariantAttributeAsync(VariantAttribute variantAttribute);
    }
    public class VariantAttributeRepository : IVariantAttributeRepository
    {
        private readonly ApplicationDbContext _context;

        public VariantAttributeRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddVariantAttributeAsync(VariantAttribute variantAttribute)
        {
            await _context.VariantAttributes.AddAsync(variantAttribute);
        }
    }
}