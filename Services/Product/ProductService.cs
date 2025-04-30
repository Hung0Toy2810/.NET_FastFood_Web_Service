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
        private void ValidateProductDTO<T>(T dto) where T : class
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "DTO cannot be null");

            List<object> attributes;
            List<object> variants;

            // Handle CreateProductDTO
            if (dto is CreateProductDTO createDto)
            {
                attributes = createDto.Attributes.Cast<object>().ToList();
                variants = createDto.Variants.Cast<object>().ToList();
            }
            // Handle UpdateProductDTO
            else if (dto is UpdateProductDTO updateDto)
            {
                attributes = updateDto.Attributes.Cast<object>().ToList();
                variants = updateDto.Variants.Cast<object>().ToList();
            }
            else
            {
                throw new ArgumentException("Unsupported DTO type.", nameof(dto));
            }

            ValidateAttributesAndVariants(attributes, variants);
        }
        private void ValidateAttributesAndVariants(List<object> attributes, List<object> variants)
        {
            if (attributes == null || !attributes.Any())
            {
                throw new ArgumentException("At least one attribute is required.", nameof(attributes));
            }

            if (variants == null || !variants.Any())
            {
                throw new ArgumentException("At least one variant is required.", nameof(variants));
            }

            // Validate attributes and build valid value mappings
            var attributeNames = new HashSet<string>();
            var attributeValuesMap = new Dictionary<string, HashSet<string>>();

            foreach (var attr in attributes)
            {
                string attrName = attr is CreateAttributeDTO createAttr ? createAttr.AttributeName : (attr as AttributeDTO)?.AttributeName ?? string.Empty;
                List<string> values = attr is CreateAttributeDTO createAttr2 ? createAttr2.Values ?? new List<string>() : (attr as AttributeDTO)?.Values ?? new List<string>();

                if (string.IsNullOrWhiteSpace(attrName))
                {
                    throw new ArgumentException("Attribute name cannot be empty.", nameof(attrName));
                }

                if (values == null || !values.Any())
                {
                    throw new ArgumentException($"Attribute '{attrName}' must have at least one value.", nameof(values));
                }

                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException($"Value for attribute '{attrName}' cannot be empty.", nameof(values));
                    }
                }

                attributeNames.Add(attrName);
                attributeValuesMap[attrName] = new HashSet<string>(values);
            }

            // Validate variants
            var variantCombinations = new HashSet<string>();

            foreach (var variant in variants)
            {
                string sku = variant is CreateVariantDTO createVar ? createVar.SKU : (variant as VariantDTO)?.SKU ?? string.Empty;
                decimal price = variant is CreateVariantDTO createVar2 ? createVar2.Price : (variant as VariantDTO)?.Price ?? 0;
                int stock = variant is CreateVariantDTO createVar3 ? createVar3.Stock : (variant as VariantDTO)?.Stock ?? -1;
                Dictionary<string, string> attrValues = variant is CreateVariantDTO createVar4 ? createVar4.AttributeValues ?? new Dictionary<string, string>() : (variant as VariantDTO)?.AttributeValues ?? new Dictionary<string, string>();

                if (string.IsNullOrWhiteSpace(sku))
                {
                    throw new ArgumentException("SKU cannot be empty.", nameof(sku));
                }

                if (price <= 0)
                {
                    throw new ArgumentException($"Price must be greater than 0 for SKU '{sku}'.", nameof(price));
                }

                if (stock < 0)
                {
                    throw new ArgumentException($"Stock cannot be negative for SKU '{sku}'.", nameof(stock));
                }

                if (attrValues == null || attrValues.Count != attributeNames.Count)
                {
                    throw new ArgumentException($"Each variant must have exactly {attributeNames.Count} attribute values, but received {attrValues?.Count ?? 0} for SKU '{sku}'.", nameof(attrValues));
                }

                // Validate attribute keys and values
                foreach (var kvp in attrValues)
                {
                    var attrName = kvp.Key;
                    var value = kvp.Value;

                    if (!attributeNames.Contains(attrName))
                    {
                        throw new ArgumentException($"Attribute '{attrName}' in variant SKU '{sku}' is not defined in the product attributes.", nameof(attrValues));
                    }

                    if (!attributeValuesMap[attrName].Contains(value))
                    {
                        throw new ArgumentException($"Value '{value}' for attribute '{attrName}' in variant SKU '{sku}' is not defined in the attribute's values.", nameof(attrValues));
                    }
                }

                // Check for unique variant combinations
                var combination = string.Join("|", attrValues
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                if (!variantCombinations.Add(combination))
                {
                    throw new ArgumentException($"Duplicate variant combination found for SKU '{sku}': {combination}.");
                }
            }
        }
        public async Task<Product> AddProductAsync(CreateProductDTO dto)
        {
            // Kiểm tra dữ liệu đầu vào
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Product DTO cannot be null");
            if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ArgumentException("ProductName cannot be empty", nameof(dto.ProductName));
            if (dto.CategoryID <= 0) throw new ArgumentException("CategoryID must be a positive integer", nameof(dto.CategoryID));

            // Kiểm tra thêm logic nghiệp vụ tùy chỉnh (nếu có)
            ValidateProductDTO(dto);

            // Kiểm tra trùng tên sản phẩm
            var existingProduct = await _productRepository.GetProductByNameAsync(dto.ProductName);
            if (existingProduct != null)
                throw new InvalidOperationException($"A product with name '{dto.ProductName}' already exists.");

            // Kiểm tra danh mục tồn tại
            if (!await _categoryRepository.CategoryExistsAsync(dto.CategoryID))
                throw new InvalidOperationException($"Category with ID '{dto.CategoryID}' does not exist.");

            // Xử lý ảnh: nếu không có ảnh thì dùng ảnh mặc định
            string imageKey = dto.ImageKey ?? "default.jpg";
            string imageUrl = _fileService.GetPublicImageUrl(ProductImageBucketName, imageKey);

            // Nếu người dùng có upload ảnh mới thì xử lý và upload lên hệ thống lưu trữ
            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                if (!await IsImageAsync(dto.ImageFile))
                    throw new ArgumentException("Image file must be an image (JPEG, PNG, GIF, etc.).", nameof(dto.ImageFile));

                const long maxSize = 5 * 1024 * 1024; // 5MB

                using var stream = dto.ImageFile.OpenReadStream();
                imageKey = await _fileService.ConvertAndUploadAsJpgAsync(
                    stream,
                    ProductImageBucketName,
                    $"{Guid.NewGuid()}.jpg",
                    maxSize
                );
                imageUrl = _fileService.GetPublicImageUrl(ProductImageBucketName, imageKey);
            }

            // Tạo đối tượng Product
            var product = new Product
            {
                ProductName = dto.ProductName,
                CategoryID = dto.CategoryID,
                Discount = dto.Discount,
                ImageKey = imageKey,
                ImageUrl = imageUrl
            };

            // Bắt đầu transaction để đảm bảo toàn vẹn dữ liệu
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Thêm product vào DB
                await _productRepository.AddProductAsync(product);
                await _context.SaveChangesAsync();

                // Tạo map tạm thời cho Attribute và AttributeValue
                var attributeMap = new Dictionary<string, int>();
                var attributeValueMap = new Dictionary<(int, string), int>();

                // Xử lý từng thuộc tính
                foreach (var attrDto in dto.Attributes)
                {
                    var attribute = await _attributeRepository.GetAttributeByNameAsync(attrDto.AttributeName);
                    if (attribute == null)
                    {
                        attribute = new LapTrinhWindows.Models.Attribute { AttributeName = attrDto.AttributeName };
                        await _attributeRepository.AddAttributeAsync(attribute);
                        await _context.SaveChangesAsync();
                    }
                    attributeMap[attrDto.AttributeName] = attribute.AttributeID;

                    // Xử lý các giá trị của thuộc tính đó
                    foreach (var value in attrDto.Values)
                    {
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

                // Xử lý từng biến thể sản phẩm
                foreach (var variantDto in dto.Variants)
                {
                    // Kiểm tra trùng SKU
                    if (await _variantRepository.SkuExistsAsync(variantDto.SKU))
                        throw new InvalidOperationException($"SKU '{variantDto.SKU}' already exists.");

                    // Tạo danh sách các AttributeValueID cho biến thể
                    var attributeValueIds = new List<int>();
                    foreach (var kvp in variantDto.AttributeValues)
                    {
                        var attrName = kvp.Key;
                        var value = kvp.Value;
                        var attrId = attributeMap[attrName];
                        attributeValueIds.Add(attributeValueMap[(attrId, value)]);
                    }

                    // Tạo đối tượng Variant
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

                // Lưu mọi thay đổi
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully added product with ID '{ProductID}' and {VariantCount} variants", product.ProductID, dto.Variants.Count);
                return product;
            }
            catch (Exception ex)
            {
                // Hoàn tác transaction nếu có lỗi
                await transaction.RollbackAsync();

                // Nếu ảnh mới được upload thì xóa đi
                if (dto.ImageFile != null && !string.Equals(imageKey, dto.ImageKey))
                {
                    await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                    _logger.LogInformation("Deleted uploaded image '{ImageKey}' due to rollback", imageKey);
                }

                _logger.LogError(ex, "Failed to add product with name '{ProductName}'", dto.ProductName);
                throw new InvalidOperationException($"Failed to add product: {ex.Message}", ex);
            }
        }
        public async Task<ProductDetailDTO> GetProductDetailAsync(int id)
        {
                try
            {
                var productDetail = await _productRepository.GetProductDetailAsync(id);
                _logger.LogInformation("Successfully retrieved product details for product ID '{ProductID}'", id);
                return productDetail;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve product details for product ID '{ProductID}'", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving product details for product ID '{ProductID}'", id);
                throw new InvalidOperationException($"Failed to retrieve product details: {ex.Message}", ex);
            }
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
            if (dto == null) throw new ArgumentNullException(nameof(dto), "UpdateProductDTO cannot be null");
            if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ArgumentException("ProductName cannot be empty", nameof(dto.ProductName));
            if (dto.CategoryID <= 0) throw new ArgumentException("CategoryID must be a positive integer", nameof(dto.CategoryID));

            // Check if product exists
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }

            ValidateProductDTO(dto);

            // Check for duplicate product name
            var existingProduct = await _productRepository.GetProductByNameAsync(dto.ProductName);
            if (existingProduct != null && existingProduct.ProductID != id)
            {
                throw new InvalidOperationException($"A product with name '{dto.ProductName}' already exists.");
            }

            // Check if category exists
            if (!await _categoryRepository.CategoryExistsAsync(dto.CategoryID))
            {
                throw new InvalidOperationException($"Category with ID '{dto.CategoryID}' does not exist.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check for duplicate SKUs in variants
                foreach (var variantDto in dto.Variants)
                {
                    var existingVariant = await _variantRepository.GetVariantBySkuAsync(variantDto.SKU);
                    if (existingVariant != null && existingVariant.ProductID != id)
                    {
                        throw new InvalidOperationException($"SKU '{variantDto.SKU}' already exists in another product.");
                    }
                }

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
        public async Task DeleteProductAsync(int id)
        {
            // Kiểm tra sản phẩm tồn tại
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Attempted to delete non-existent product with ID '{ProductID}'", id);
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }

            // Lấy danh sách ảnh phụ
            var additionalImages = await _productRepository.GetProductImagesByProductIdAsync(id);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Xóa sản phẩm và dữ liệu liên quan trong database
                await _productRepository.DeleteProductAsync(id);

                // Thu thập danh sách ImageKey cần xóa
                var imageKeysToDelete = new List<string>();

                // Kiểm tra và thêm ảnh chính
                if (!string.IsNullOrEmpty(product.ImageKey) && product.ImageKey != "default.jpg")
                {
                    var isImageUsedElsewhere = await _productRepository.IsImageKeyUsedByOtherImagesAsync(product.ImageKey, id);
                    if (!isImageUsedElsewhere)
                    {
                        imageKeysToDelete.Add(product.ImageKey);
                    }
                }

                // Kiểm tra và thêm ảnh phụ
                foreach (var image in additionalImages)
                {
                    if (!string.IsNullOrEmpty(image.ImageKey) && image.ImageKey != "default.jpg")
                    {
                        var isImageUsedElsewhere = await _productRepository.IsImageKeyUsedByOtherImagesAsync(image.ImageKey, id);
                        if (!isImageUsedElsewhere)
                        {
                            imageKeysToDelete.Add(image.ImageKey);
                        }
                    }
                }

                // Xóa các tệp ảnh
                foreach (var imageKey in imageKeysToDelete)
                {
                    try
                    {
                        await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                        _logger.LogInformation("Deleted image '{ImageKey}' for product ID '{ProductID}'", imageKey, id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete image '{ImageKey}' for product ID '{ProductID}'", imageKey, id);
                        throw new InvalidOperationException($"Failed to delete image '{imageKey}': {ex.Message}", ex);
                    }
                }

                // Commit transaction nếu tất cả thành công
                await transaction.CommitAsync();
                _logger.LogInformation("Successfully deleted product with ID '{ProductID}' and related data", id);
            }
            catch (Exception ex)
            {
                // Rollback transaction nếu có lỗi
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete product with ID '{ProductID}'", id);
                throw new InvalidOperationException($"Failed to delete product: {ex.Message}", ex);
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