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
        // Lấy danh sách lô hàng theo SKU
        Task<List<Batch>> GetBatchesBySkuAsync(string sku);
        // Lấy lô hàng theo ID
        Task<Batch?> GetBatchByIdAsync(int batchId);
        // Thêm lô hàng mới
        Task AddBatchAsync(Batch batch);
    }

    public class BatchRepository : IBatchRepository
    {
        private readonly ApplicationDbContext _context;

        public BatchRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // Lấy tất cả lô hàng liên quan đến SKU
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

        // Lấy lô hàng theo BatchID
        public async Task<Batch?> GetBatchByIdAsync(int batchId)
        {
            return await _context.Batches
                .FirstOrDefaultAsync(b => b.BatchID == batchId);
        }

        // Thêm lô hàng mới vào cơ sở dữ liệu
        public async Task AddBatchAsync(Batch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch), "Lô hàng không được null.");
            }

            await _context.Batches.AddAsync(batch);
            await _context.SaveChangesAsync();
        }
    }
}