using LapTrinhWindows.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LapTrinhWindows.Repositories.BatchRepository
{
    public interface IBatchRepository
    {
        Task<List<Batch>> GetBatchesBySkuAsync(string sku);
        
        Task<Batch?> GetBatchByIdAsync(int batchId);
        
        Task AddBatchAsync(Batch batch);
        Task<Dictionary<int, Batch>> GetBatchesByIdsAsync(IEnumerable<int> batchIds);
        Task UpdateBatchAsync(Batch batch);
    }

    public class BatchRepository : IBatchRepository
    {
        private readonly ApplicationDbContext _context;

        public BatchRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Batch>> GetBatchesBySkuAsync(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                throw new ArgumentException("SKU không được để trống.", nameof(sku));
            }

            return await _context.Batches
                .Where(b => b.SKU == sku)
                .ToListAsync();
        }

        
        public async Task<Batch?> GetBatchByIdAsync(int batchId)
        {
            return await _context.Batches
                .FirstOrDefaultAsync(b => b.BatchID == batchId);
        }

        
        public async Task AddBatchAsync(Batch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch), "Lô hàng không được null.");
            }

            await _context.Batches.AddAsync(batch);
            await _context.SaveChangesAsync();
        }
        public async Task<Dictionary<int, Batch>> GetBatchesByIdsAsync(IEnumerable<int> batchIds)
        {
            return await _context.Batches
                .Where(b => batchIds.Contains(b.BatchID))
                .ToDictionaryAsync(b => b.BatchID, b => b);
        }
        public async Task UpdateBatchAsync(Batch batch)
        {
            _context.Batches.Update(batch);
            await _context.SaveChangesAsync();
        }
    }
}