using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Context;
using LapTrinhWindows.Models;
using LapTrinhWindows.Models.DTO;
using Microsoft.Extensions.Logging;
using LapTrinhWindows.Repositories.ProductRepository;
using LapTrinhWindows.Services.Minio;

namespace LapTrinhWindows.Services
{
    public interface IProductService
    {
        Task<Product> AddProductAsync(CreateProductDTO dto);
        // add additional product image
    }
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ILogger<ProductService> _logger;
        private const string ProductImageBucketName = "product-images";

        public ProductService(
            IProductRepository productRepository,
            ApplicationDbContext context,
            IFileService fileService,
            ILogger<ProductService> logger)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Product> AddProductAsync(CreateProductDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Product DTO is null");
            if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ArgumentException("ProductName cannot be empty", nameof(dto.ProductName));
            if (dto.CategoryID <= 0) throw new ArgumentException("CategoryID must be a positive integer", nameof(dto.CategoryID));

            // Kiểm tra tên sản phẩm trùng lặp
            var existingProduct = await _productRepository.GetProductByNameAsync(dto.ProductName);
            if (existingProduct != null)
            {
                throw new InvalidOperationException($"A product with name '{dto.ProductName}' already exists.");
            }

            // Kiểm tra danh mục tồn tại
            var category = await _context.Categories.FindAsync(dto.CategoryID);
            if (category == null)
            {
                throw new InvalidOperationException($"Category with ID '{dto.CategoryID}' does not exist.");
            }

            // Xử lý upload ảnh chính
            string imageKey = dto.ImageKey;
            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                if (!await IsImageAsync(dto.ImageFile))
                {
                    throw new ArgumentException("Image file must be an image (JPEG, PNG, GIF, etc.).", nameof(dto.ImageFile));
                }

                const long maxSize = 5 * 1024 * 1024; // 5MB
                using var stream = dto.ImageFile.OpenReadStream();
                imageKey = await _fileService.ConvertAndUploadAsJpgAsync(
                    stream,
                    ProductImageBucketName,
                    $"{Guid.NewGuid()}.jpg",
                    maxSize
                );
            }

            // Tạo entity Product
            var product = new Product
            {
                ProductName = dto.ProductName,
                CategoryID = dto.CategoryID,
                Discount = dto.Discount,
                ImageKey = imageKey
            };

            // Sử dụng giao dịch để đảm bảo tính toàn vẹn
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _productRepository.AddProductAsync(product);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Successfully added product with name '{ProductName}' and ID '{ProductID}'", product.ProductName, product.ProductID);
                return product;
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        if (dto.ImageFile != null && !string.Equals(imageKey, dto.ImageKey))
                        {
                            _logger.LogInformation("Deleting uploaded image '{ImageKey}' due to rollback", imageKey);
                            await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Rollback failed for product '{ProductName}'", dto.ProductName);
                        throw new InvalidOperationException("Rollback failed after an error occurred.", rollbackEx);
                    }
                }
                _logger.LogError(ex, "Failed to add product with name '{ProductName}'", dto.ProductName);
                throw new InvalidOperationException($"Failed to add product: {ex.Message}", ex);
            }
        }

        private async Task<bool> IsImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;

            var validImageTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp" };
            if (!validImageTypes.Contains(file.ContentType.ToLower()))
            {
                return false;
            }

            using var stream = file.OpenReadStream();
            byte[] buffer = new byte[4];
            try
            {
                await stream.ReadExactlyAsync(buffer, 0, 4);
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            if (buffer[0] == 0xFF && buffer[1] == 0xD8) return true; // JPEG
            if (buffer[0] == 0x89 && buffer[1] == 0x50) return true; // PNG
            if (buffer[0] == 0x47 && buffer[1] == 0x49) return true; // GIF
            if (buffer[0] == 0x42 && buffer[1] == 0x4D) return true; // BMP

            return false;
        }
    
    }
}