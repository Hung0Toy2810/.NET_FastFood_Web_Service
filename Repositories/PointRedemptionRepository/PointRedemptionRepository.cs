namespace LapTrinhWindows.Repositories.PointRedemptionRepository
{
    public interface IPointRedemptionRepository
    {
        Task<PointRedemption?> GetByIdAsync(int id);
        Task<List<PointRedemption>> GetAllAsync(bool includeInactive = false);
        Task<PointRedemption> CreateAsync(PointRedemption pointRedemption);
        Task<PointRedemption> UpdateAsync(PointRedemption pointRedemption);
        Task DeleteAsync(int id);
        Task<bool> ExistsBySKUAsync(string sku);
        Task<Dictionary<int, PointRedemption>> GetPointRedemptionsByIdsAsync(IEnumerable<int> redemptionIds);
        
    }
    public class PointRedemptionRepository : IPointRedemptionRepository
    {
        private readonly ApplicationDbContext _context;

        public PointRedemptionRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PointRedemption?> GetByIdAsync(int id)
        {
            return await _context.PointRedemptions
                .Include(pr => pr.Variant)
                .FirstOrDefaultAsync(pr => pr.PointRedemptionID == id);
        }

        public async Task<List<PointRedemption>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.PointRedemptions
                .Include(pr => pr.Variant)
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(pr => pr.Status == PointRedemptionStatus.Active
                    && pr.StartDate <= DateTime.UtcNow
                    && pr.EndDate >= DateTime.UtcNow);
            }

            return await query.OrderBy(pr => pr.StartDate).ToListAsync();
        }

        public async Task<PointRedemption> CreateAsync(PointRedemption pointRedemption)
        {
            await _context.PointRedemptions.AddAsync(pointRedemption);
            await _context.SaveChangesAsync();
            return pointRedemption;
        }

        public async Task<PointRedemption> UpdateAsync(PointRedemption pointRedemption)
        {
            _context.PointRedemptions.Update(pointRedemption);
            await _context.SaveChangesAsync();
            return pointRedemption;
        }

        public async Task DeleteAsync(int id)
        {
            var pointRedemption = await _context.PointRedemptions
                .FirstOrDefaultAsync(pr => pr.PointRedemptionID == id);
            if (pointRedemption != null)
            {
                _context.PointRedemptions.Remove(pointRedemption);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsBySKUAsync(string sku)
        {
            return await _context.PointRedemptions.AnyAsync(pr => pr.SKU == sku);
        }
        public async Task<Dictionary<int, PointRedemption>> GetPointRedemptionsByIdsAsync(IEnumerable<int> redemptionIds)
        {
            return await _context.PointRedemptions
                .Include(pr => pr.Batch)
                .Include(pr => pr.Variant)
                .Where(pr => redemptionIds.Contains(pr.PointRedemptionID))
                .ToDictionaryAsync(pr => pr.PointRedemptionID, pr => pr);
        }
    }
}