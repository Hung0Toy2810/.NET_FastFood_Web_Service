using LapTrinhWindows.Models.DTO;
using LapTrinhWindows.Models;
using LapTrinhWindows.Repositories.BatchRepository;
using LapTrinhWindows.Repositories.VariantRepository;

namespace LapTrinhWindows.Services
{
    public interface IStockService
    {
        Task UpdateVariantStockBySkuAsync(string sku);
        Task AddBatchAndUpdateStockAsync(BatchCreateDto batchDto);
    }

    public class StockService : IStockService
    {
        private readonly IBatchRepository _batchRepository;
        private readonly IVariantRepository _variantRepository;
        private readonly ApplicationDbContext _context;

        public StockService(
            IBatchRepository batchRepository,
            IVariantRepository variantRepository,
            ApplicationDbContext context)
        {
            _batchRepository = batchRepository ?? throw new ArgumentNullException(nameof(batchRepository));
            _variantRepository = variantRepository ?? throw new ArgumentNullException(nameof(variantRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task UpdateVariantStockBySkuAsync(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                throw new ArgumentException("SKU không được để trống.", nameof(sku));
            }

            var batches = await _batchRepository.GetBatchesBySkuAsync(sku);
            var totalStock = batches.Sum(b => b.AvailableQuantity);

            var variant = await _variantRepository.GetVariantBySkuAsync(sku);
            if (variant == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy Variant với SKU '{sku}'.");
            }

            variant.Stock = totalStock;
            await _context.SaveChangesAsync();
        }

        public async Task AddBatchAndUpdateStockAsync(BatchCreateDto batchDto)
        {
            if (batchDto == null)
            {
                throw new ArgumentNullException(nameof(batchDto), "Dữ liệu lô hàng không được null.");
            }

            if (string.IsNullOrWhiteSpace(batchDto.Sku))
            {
                throw new ArgumentException("SKU của lô hàng không được để trống.", nameof(batchDto.Sku));
            }
            var variant = await _variantRepository.GetVariantBySkuAsync(batchDto.Sku);
            if (variant == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy Variant với SKU '{batchDto.Sku}'.");
            }

            var batch = new Batch
            {
                SKU = batchDto.Sku,
                Variant = variant, 
                ExpirationDate = batchDto.ExpirationDate,
                ProductionDate = batchDto.ProductionDate,
                AvailableQuantity = batchDto.AvailableQuantity
            };

            // Thêm lô hàng mới
            await _batchRepository.AddBatchAsync(batch);

            // Cập nhật stock của Variant
            await UpdateVariantStockBySkuAsync(batchDto.Sku);
        }
    }
}