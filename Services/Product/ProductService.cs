using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Context;
using LapTrinhWindows.Models;
using LapTrinhWindows.Models.DTO;
using LapTrinhWindows.Repositories;
using Microsoft.Extensions.Logging;
using LapTrinhWindows.Repositories.EmployeeRepository;
using LapTrinhWindows.Repositories.RoleRepository;
using LapTrinhWindows.Repositories.ProductRepository;
using LapTrinhWindows.Repositories.CategoryRepository;
using LapTrinhWindows.Repositories.VariantRepository;
using LapTrinhWindows.Repositories.VariantAttributeRepository;
using LapTrinhWindows.Services.Minio;


namespace LapTrinhWindows.Services
{
    public interface IProductService
    {
        Task<Product> AddProductAsync(CreateProductDTO dto);
        Task<ProductDetailDTO> GetProductDetailAsync(int id);
        Task UpdateVariantPriceBySkuAsync(UpdateVariantPriceDTO dto);
        Task UpdateProductAsync(int id, UpdateProductDTO dto);
        
    }
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IVariantRepository _variantRepository;
        private readonly IAttributeRepository _attributeRepository;
        private readonly IAttributeValueRepository _attributeValueRepository;
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ILogger<ProductService> _logger;
        private const string ProductImageBucketName = "product-images";

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IVariantRepository variantRepository,
            IAttributeRepository attributeRepository,
            IAttributeValueRepository attributeValueRepository,
            ApplicationDbContext context,
            IFileService fileService,
            ILogger<ProductService> logger)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _variantRepository = variantRepository ?? throw new ArgumentNullException(nameof(variantRepository));
            _attributeRepository = attributeRepository ?? throw new ArgumentNullException(nameof(attributeRepository));
            _attributeValueRepository = attributeValueRepository ?? throw new ArgumentNullException(nameof(attributeValueRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Product> AddProductAsync(CreateProductDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Product DTO is null");
            if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ArgumentException("ProductName cannot be empty", nameof(dto.ProductName));
            if (dto.CategoryID <= 0) throw new ArgumentException("CategoryID must be a positive integer", nameof(dto.CategoryID));
            if (dto.Attributes == null || !dto.Attributes.Any()) throw new ArgumentException("At least one attribute is required.", nameof(dto.Attributes));
            if (dto.Variants == null || !dto.Variants.Any()) throw new ArgumentException("At least one variant is required.", nameof(dto.Variants));

            var existingProduct = await _productRepository.GetProductByNameAsync(dto.ProductName);
            if (existingProduct != null)
            {
                throw new InvalidOperationException($"A product with name '{dto.ProductName}' already exists.");
            }

            if (!await _categoryRepository.CategoryExistsAsync(dto.CategoryID))
            {
                throw new InvalidOperationException($"Category with ID '{dto.CategoryID}' does not exist.");
            }

            string imageKey = dto.ImageKey ?? "default.jpg";
            string imageUrl = _fileService.GetPublicImageUrl(ProductImageBucketName, imageKey);
            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                if (!await IsImageAsync(dto.ImageFile))
                {
                    throw new ArgumentException("Image file must be an image (JPEG, PNG, GIF, etc.).", nameof(dto.ImageFile));
                }

                const long maxSize = 5 * 1024 * 1024;
                using var stream = dto.ImageFile.OpenReadStream();
                imageKey = await _fileService.ConvertAndUploadAsJpgAsync(
                    stream,
                    ProductImageBucketName,
                    $"{Guid.NewGuid()}.jpg",
                    maxSize
                );
                imageUrl = _fileService.GetPublicImageUrl(ProductImageBucketName, imageKey);
            }

            var product = new Product
            {
                ProductName = dto.ProductName,
                CategoryID = dto.CategoryID,
                Discount = dto.Discount,
                ImageKey = imageKey,
                ImageUrl = imageUrl
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _productRepository.AddProductAsync(product);
                await _context.SaveChangesAsync();

                var attributeMap = new Dictionary<string, int>();
                var attributeValueMap = new Dictionary<(int, string), int>();

                foreach (var attrDto in dto.Attributes)
                {
                    if (string.IsNullOrWhiteSpace(attrDto.AttributeName) || attrDto.Values == null || !attrDto.Values.Any())
                    {
                        throw new ArgumentException($"Attribute '{attrDto.AttributeName}' must have at least one value.", nameof(attrDto.Values));
                    }

                    var attribute = await _attributeRepository.GetAttributeByNameAsync(attrDto.AttributeName);
                    if (attribute == null)
                    {
                        attribute = new LapTrinhWindows.Models.Attribute { AttributeName = attrDto.AttributeName };
                        await _attributeRepository.AddAttributeAsync(attribute);
                        await _context.SaveChangesAsync();
                    }
                    attributeMap[attrDto.AttributeName] = attribute.AttributeID;

                    foreach (var value in attrDto.Values)
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            throw new ArgumentException($"Attribute value for '{attrDto.AttributeName}' cannot be empty.", nameof(attrDto.Values));
                        }

                        var attributeValue = await _attributeValueRepository.GetAttributeValueByNameAsync(attribute.AttributeID, value);
                        if (attributeValue == null)
                        {
                            attributeValue = new AttributeValue
                            {
                                AttributeID = attribute.AttributeID,
                                Value = value
                            };
                            await _attributeValueRepository.AddAttributeValueAsync(attributeValue);
                            await _context.SaveChangesAsync();
                        }
                        attributeValueMap[(attribute.AttributeID, value)] = attributeValue.AttributeValueID;
                    }
                }

                int expectedVariantCount = dto.Attributes.Aggregate(1, (acc, attr) => acc * attr.Values.Count);
                if (dto.Variants.Count != expectedVariantCount)
                {
                    throw new InvalidOperationException($"Expected {expectedVariantCount} variants for the given attribute combinations, but received {dto.Variants.Count}.");
                }

                var usedSkus = new HashSet<string>();
                var usedCombinations = new HashSet<string>();

                foreach (var variantDto in dto.Variants)
                {
                    if (string.IsNullOrWhiteSpace(variantDto.SKU))
                    {
                        throw new ArgumentException("SKU cannot be empty.", nameof(variantDto.SKU));
                    }
                    if (usedSkus.Contains(variantDto.SKU) || await _variantRepository.SkuExistsAsync(variantDto.SKU))
                    {
                        throw new InvalidOperationException($"SKU '{variantDto.SKU}' already exists.");
                    }
                    usedSkus.Add(variantDto.SKU);

                    if (variantDto.AttributeValues == null || variantDto.AttributeValues.Count != dto.Attributes.Count)
                    {
                        throw new ArgumentException($"Each variant must have exactly {dto.Attributes.Count} attribute values.", nameof(variantDto.AttributeValues));
                    }

                    var attributeValueIds = new List<int>();
                    var combinationKey = new List<string>();

                    foreach (var kvp in variantDto.AttributeValues)
                    {
                        var attrName = kvp.Key;
                        var value = kvp.Value;

                        if (!attributeMap.ContainsKey(attrName))
                        {
                            throw new ArgumentException($"Attribute '{attrName}' is not defined in the product attributes.", nameof(variantDto.AttributeValues));
                        }

                        var attrId = attributeMap[attrName];
                        if (!attributeValueMap.ContainsKey((attrId, value)))
                        {
                            throw new ArgumentException($"Value '{value}' is not defined for attribute '{attrName}'.", nameof(variantDto.AttributeValues));
                        }

                        attributeValueIds.Add(attributeValueMap[(attrId, value)]);
                        combinationKey.Add($"{attrName}:{value}");
                    }

                    var combination = string.Join("|", combinationKey.OrderBy(k => k));
                    if (usedCombinations.Contains(combination))
                    {
                        throw new InvalidOperationException($"Duplicate variant combination: {combination}.");
                    }
                    usedCombinations.Add(combination);

                    var variant = new Variant
                    {
                        ProductID = product.ProductID,
                        SKU = variantDto.SKU,
                        Price = variantDto.Price,
                        Stock = variantDto.Stock,
                        VariantAttributes = attributeValueIds
                            .Select(attributeValueId => new VariantAttribute
                            {
                                AttributeValueID = attributeValueId
                            })
                            .ToList()
                    };

                    await _variantRepository.AddVariantAsync(variant);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Successfully added product with ID '{ProductID}' and {VariantCount} variants", product.ProductID, dto.Variants.Count);
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
                            await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                            _logger.LogInformation("Deleted uploaded image '{ImageKey}' due to rollback", imageKey);
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
        public async Task<ProductDetailDTO> GetProductDetailAsync(int id)
        {
            var productDetail = await _productRepository.GetProductDetailAsync(id);
            if (productDetail == null)
            {
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }
            return productDetail;
        }
        public async Task UpdateVariantPriceBySkuAsync(UpdateVariantPriceDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "UpdateVariantPriceDTO is null");
            if (string.IsNullOrWhiteSpace(dto.SKU)) throw new ArgumentException("SKU cannot be empty", nameof(dto.SKU));
            if (dto.Price <= 0) throw new ArgumentException("Price must be greater than 0", nameof(dto.Price));

            await _variantRepository.UpdateVariantPriceBySkuAsync(dto.SKU, dto.Price);
            _logger.LogInformation("Updated price for variant with SKU '{SKU}' to {Price}", dto.SKU, dto.Price);
        }
        public async Task UpdateProductAsync(int id, UpdateProductDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "UpdateProductDTO is null");
            if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ArgumentException("ProductName cannot be empty", nameof(dto.ProductName));
            if (dto.CategoryID <= 0) throw new ArgumentException("CategoryID must be a positive integer", nameof(dto.CategoryID));
            if (dto.Attributes == null || !dto.Attributes.Any()) throw new ArgumentException("At least one attribute is required.", nameof(dto.Attributes));
            if (dto.Variants == null || !dto.Variants.Any()) throw new ArgumentException("At least one variant is required.", nameof(dto.Variants));

            var existingProduct = await _productRepository.GetProductByNameAsync(dto.ProductName);
            if (existingProduct != null && existingProduct.ProductID != id)
            {
                throw new InvalidOperationException($"A product with name '{dto.ProductName}' already exists.");
            }

            if (!await _categoryRepository.CategoryExistsAsync(dto.CategoryID))
            {
                throw new InvalidOperationException($"Category with ID '{dto.CategoryID}' does not exist.");
            }

            int expectedVariantCount = dto.Attributes.Aggregate(1, (acc, attr) => acc * attr.Values.Count);
            if (dto.Variants.Count != expectedVariantCount)
            {
                throw new InvalidOperationException($"Expected {expectedVariantCount} variants for the given attribute combinations, but received {dto.Variants.Count}.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _productRepository.UpdateProductAsync(id, dto);
                await transaction.CommitAsync();
                _logger.LogInformation("Successfully updated product with ID '{ProductID}'", id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update product with ID '{ProductID}'", id);
                throw new InvalidOperationException($"Failed to update product: {ex.Message}", ex);
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