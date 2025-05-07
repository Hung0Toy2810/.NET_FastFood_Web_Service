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
using System.Text.RegularExpressions;
using LapTrinhWindows.Repositories.EmployeeRepository;
using LapTrinhWindows.Repositories.RoleRepository;
using LapTrinhWindows.Repositories.ProductRepository;
using LapTrinhWindows.Repositories.CategoryRepository;
using LapTrinhWindows.Repositories.VariantRepository;
using LapTrinhWindows.Repositories.VariantAttributeRepository;
using LapTrinhWindows.Services.Minio;
using LapTrinhWindows.Repositories.ProductImageRepository;
using LapTrinhWindows.Repositories.ProductTagRepository;



namespace LapTrinhWindows.Services
{
    public interface IProductService
    {
        Task AddProductAsync(CreateProductDTO dto);
        Task<ProductDetailDTO> GetProductDetailAsync(int id);
        Task UpdateVariantPriceBySkuAsync(UpdateVariantPriceDTO dto);
        Task UpdateProductAsync(int id, UpdateProductDTO dto);
        Task DeleteProductAsync(int id);
        Task<List<Models.ProductImage>> UpsertProductImagesAsync(UpsertProductImagesDTO dto);
        Task<List<ProductTag>> UpsertProductTagsAsync(UpsertProductTagsDTO dto);
        
        
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
        private readonly IProductImageRepository _productImageRepository;
        private const string ProductImageBucketName = "public-product-images";
        private const int MaxImagesPerProduct = 5;
        private const long MaxFileSize = 5 * 1024 * 1024; 
        private readonly IProductTagRepository _productTagRepository;

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IVariantRepository variantRepository,
            IAttributeRepository attributeRepository,
            IAttributeValueRepository attributeValueRepository,
            ApplicationDbContext context,
            IFileService fileService,
            IProductImageRepository productImageRepository,
            ILogger<ProductService> logger,
            IProductTagRepository productTagRepository)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _variantRepository = variantRepository ?? throw new ArgumentNullException(nameof(variantRepository));
            _attributeRepository = attributeRepository ?? throw new ArgumentNullException(nameof(attributeRepository));
            _attributeValueRepository = attributeValueRepository ?? throw new ArgumentNullException(nameof(attributeValueRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _productImageRepository = productImageRepository ?? throw new ArgumentNullException(nameof(productImageRepository));
            _productTagRepository = productTagRepository ?? throw new ArgumentNullException(nameof(productTagRepository));
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
                throw new ArgumentException("At least one attribute is required.", nameof(attributes));

            if (variants == null || !variants.Any())
                throw new ArgumentException("At least one variant is required.", nameof(variants));

            var attributeNames = new HashSet<string>();
            var attributeValuesMap = new Dictionary<string, HashSet<string>>();

            foreach (var attr in attributes)
            {
                string attrName = attr is CreateAttributeDTO createAttr ? createAttr.AttributeName : (attr as AttributeDTO)?.AttributeName ?? string.Empty;
                List<string> values = attr is CreateAttributeDTO createAttr2 ? createAttr2.Values ?? new List<string>() : (attr as AttributeDTO)?.Values ?? new List<string>();

                if (string.IsNullOrWhiteSpace(attrName))
                    throw new ArgumentException("Attribute name cannot be empty.", nameof(attrName));

                if (values == null || !values.Any())
                    throw new ArgumentException($"Attribute '{attrName}' must have at least one value.", nameof(values));

                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        throw new ArgumentException($"Value for attribute '{attrName}' cannot be empty.", nameof(values));
                }

                attributeNames.Add(attrName);
                attributeValuesMap[attrName] = new HashSet<string>(values);
            }

            var variantCombinations = new HashSet<string>();

            foreach (var variant in variants)
            {
                string sku = variant is CreateVariantDTO createVar ? createVar.SKU : (variant as VariantDTO)?.SKU ?? string.Empty;
                decimal price = variant is CreateVariantDTO createVar2 ? createVar2.Price : (variant as VariantDTO)?.Price ?? 0;
                int stock = variant is CreateVariantDTO createVar3 ? createVar3.Stock : (variant as VariantDTO)?.Stock ?? -1;
                Dictionary<string, string> attrValues = variant is CreateVariantDTO createVar4 ? createVar4.AttributeValues ?? new Dictionary<string, string>() : (variant as VariantDTO)?.AttributeValues ?? new Dictionary<string, string>();

                if (string.IsNullOrWhiteSpace(sku))
                    throw new ArgumentException("SKU cannot be empty.", nameof(sku));

                if (price <= 0)
                    throw new ArgumentException($"Price must be greater than 0 for SKU '{sku}'.", nameof(price));

                if (stock < 0)
                    throw new ArgumentException($"Stock cannot be negative for SKU '{sku}'.", nameof(stock));

                if (attrValues == null || attrValues.Count != attributeNames.Count)
                    throw new ArgumentException($"Each variant must have exactly {attributeNames.Count} attribute values, but received {attrValues?.Count ?? 0} for SKU '{sku}'.", nameof(attrValues));

                foreach (var kvp in attrValues)
                {
                    var attrName = kvp.Key;
                    var value = kvp.Value;

                    if (!attributeNames.Contains(attrName))
                        throw new ArgumentException($"Attribute '{attrName}' in variant SKU '{sku}' is not defined in the product attributes.", nameof(attrValues));

                    if (!attributeValuesMap[attrName].Contains(value))
                        throw new ArgumentException($"Value '{value}' for attribute '{attrName}' in variant SKU '{sku}' is not defined in the attribute's values.", nameof(attrValues));
                }

                // Kiểm tra định dạng SKU
                if (!Regex.IsMatch(sku, @"^[a-zA-Z0-9\-]+$"))
                    throw new ArgumentException($"SKU '{sku}' contains invalid characters.", nameof(sku));

                var combination = string.Join("|", attrValues
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                if (!variantCombinations.Add(combination))
                    throw new ArgumentException($"Duplicate variant combination found for SKU '{sku}': {combination}.");
            }
        }
        public async Task AddProductAsync(CreateProductDTO dto)
        {
            ValidateInput(dto);
            await ValidateProductUniquenessAndCategory(dto);
            var (imageKey, imageUrl) = await ProcessImageAsync(dto);
            var product = CreateProduct(dto, imageKey, imageUrl);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _productRepository.AddProductAsync(product);
                var (attributeMap, attributeValueMap) = await ProcessAttributesAsync(dto.Attributes);
                await ProcessVariantsAsync(dto.Variants, product.ProductID, attributeMap, attributeValueMap);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully added product with ID '{ProductID}' and {VariantCount} variants", 
                    product.ProductID, dto.Variants.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await CleanupImageOnFailureAsync(dto, imageKey);
                _logger.LogError(ex, "Failed to add product with name '{ProductName}'", dto.ProductName);
                throw new InvalidOperationException($"Failed to add product: {ex.Message}", ex);
            }
        }

        private void ValidateInput(CreateProductDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ArgumentException("ProductName cannot be empty", nameof(dto.ProductName));
            if (dto.CategoryID <= 0) throw new ArgumentException("CategoryID must be a_positive integer", nameof(dto.CategoryID));
            ValidateProductDTO(dto);
        }

        private async Task ValidateProductUniquenessAndCategory(CreateProductDTO dto)
        {
            if (await _productRepository.GetProductByNameAsync(dto.ProductName) != null)
                throw new InvalidOperationException($"A product with name '{dto.ProductName}' already exists.");
            if (!await _categoryRepository.CategoryExistsAsync(dto.CategoryID))
                throw new InvalidOperationException($"Category with ID '{dto.CategoryID}' does not exist.");
        }

        private async Task<(string ImageKey, string ImageUrl)> ProcessImageAsync(CreateProductDTO dto)
        {
            string publicBucketName = "public-product-images"; // Bucket riêng cho file công khai
            string imageKey = dto.ImageKey ?? "default.jpg";
            string imageUrl = await _fileService.GetStaticPublicFileUrl(publicBucketName, imageKey);

            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                if (!await IsImageAsync(dto.ImageFile))
                    throw new ArgumentException("Image file must be an image (JPEG, PNG, GIF, etc.).", nameof(dto.ImageFile));

                const long maxSize = 5 * 1024 * 1024;
                using var stream = dto.ImageFile.OpenReadStream();
                imageKey = await _fileService.ConvertAndUploadPublicFileAsJpgAsync(stream, publicBucketName, $"{Guid.NewGuid()}.jpg", maxSize);
                imageUrl = await _fileService.GetStaticPublicFileUrl(publicBucketName, imageKey);
            }

            return (imageKey, imageUrl);
        }

        private Product CreateProduct(CreateProductDTO dto, string imageKey, string imageUrl)
        {
            return new Product
            {
                ProductName = dto.ProductName,
                CategoryID = dto.CategoryID,
                Discount = dto.Discount,
                ImageKey = imageKey,
                ImageUrl = imageUrl
            };
        }

        private async Task<(Dictionary<string, int>, Dictionary<(int, string), int>)> ProcessAttributesAsync(List<CreateAttributeDTO> attributes)
        {
            var attributeMap = new Dictionary<string, int>();
            var attributeValueMap = new Dictionary<(int, string), int>();

            var attributeNames = attributes.Select(a => a.AttributeName).Distinct().ToList();
            var existingAttributes = new List<LapTrinhWindows.Models.Attribute>();
            foreach (var attributeName in attributeNames)
            {
                var attribute = await _attributeRepository.GetAttributeByNameAsync(attributeName);
                if (attribute != null)
                {
                    existingAttributes.Add(attribute);
                }
            }

            // Giai đoạn 1: Xử lý và lưu Attributes trước
            var newAttributes = new Dictionary<string, LapTrinhWindows.Models.Attribute>();
            foreach (var attrDto in attributes)
            {
                var attribute = existingAttributes.FirstOrDefault(a => a.AttributeName == attrDto.AttributeName);
                if (attribute == null)
                {
                    attribute = new LapTrinhWindows.Models.Attribute { AttributeName = attrDto.AttributeName };
                    await _attributeRepository.AddAttributeAsync(attribute);
                    _logger.LogInformation("Added new attribute: {AttributeName}, AttributeID: {AttributeID}", 
                        attribute.AttributeName, attribute.AttributeID);
                    newAttributes[attrDto.AttributeName] = attribute;
                }
                else
                {
                    attributeMap[attrDto.AttributeName] = attribute.AttributeID;
                }
            }

            // Lưu Attributes để đảm bảo AttributeID được tạo
            await _context.SaveChangesAsync();

            // Gán AttributeID vào attributeMap sau khi lưu
            foreach (var attrDto in attributes)
            {
                if (newAttributes.ContainsKey(attrDto.AttributeName))
                {
                    var attribute = newAttributes[attrDto.AttributeName];
                    attributeMap[attrDto.AttributeName] = attribute.AttributeID;
                    _logger.LogInformation("Updated attributeMap for {AttributeName} with AttributeID: {AttributeID}", 
                        attrDto.AttributeName, attribute.AttributeID);
                }
            }

            // Giai đoạn 2: Xử lý AttributeValues sau khi Attributes đã được lưu
            foreach (var attrDto in attributes)
            {
                var attribute = existingAttributes.FirstOrDefault(a => a.AttributeName == attrDto.AttributeName) 
                            ?? await _context.Attributes.FirstAsync(a => a.AttributeName == attrDto.AttributeName);

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
                        await _context.SaveChangesAsync(); // Lưu ngay để lấy AttributeValueID
                        if (attributeValue.AttributeValueID == 0)
                        {
                            throw new InvalidOperationException($"Failed to generate AttributeValueID for AttributeID: {attribute.AttributeID}, Value: {value}");
                        }
                        _logger.LogInformation("Added new attribute value: AttributeID: {AttributeID}, Value: {Value}, AttributeValueID: {AttributeValueID}", 
                            attributeValue.AttributeID, attributeValue.Value, attributeValue.AttributeValueID);
                    }
                    attributeValueMap[(attribute.AttributeID, value)] = attributeValue.AttributeValueID;
                }
            }

            // Không cần gọi SaveChangesAsync ở đây nữa vì đã lưu trong vòng lặp

            // Kiểm tra AttributeID và AttributeValueID
            foreach (var attrId in attributeMap.Values)
            {
                if (attrId == 0 || !await _context.Attributes.AnyAsync(a => a.AttributeID == attrId))
                    throw new InvalidOperationException($"AttributeID '{attrId}' is invalid or does not exist in the database.");
            }

            foreach (var attrValueId in attributeValueMap.Values)
            {
                if (attrValueId == 0 || !await _context.AttributeValues.AnyAsync(av => av.AttributeValueID == attrValueId))
                    throw new InvalidOperationException($"AttributeValueID '{attrValueId}' is invalid or does not exist in the database.");
            }

            return (attributeMap, attributeValueMap);
        }

        private async Task ProcessVariantsAsync(List<CreateVariantDTO> variants, int productId, Dictionary<string, int> attributeMap, Dictionary<(int, string), int> attributeValueMap)
        {
            // Thêm log để kiểm tra attributeMap và attributeValueMap
            _logger.LogInformation("Processing variants with AttributeMap: {@AttributeMap}, AttributeValueMap: {@AttributeValueMap}", 
                attributeMap, attributeValueMap);

            foreach (var variantDto in variants)
            {
                _logger.LogInformation("Processing variant: {@VariantDTO}", variantDto);

                if (await _variantRepository.SkuExistsAsync(variantDto.SKU))
                    throw new InvalidOperationException($"SKU '{variantDto.SKU}' already exists.");

                var attributeValueIds = new List<(int AttributeValueID, int AttributeID)>();
                foreach (var kvp in variantDto.AttributeValues)
                {
                    var attrName = kvp.Key;
                    var value = kvp.Value;
                    var attrId = attributeMap[attrName];
                    var attributeValueId = attributeValueMap[(attrId, value)];
                    attributeValueIds.Add((attributeValueId, attrId));
                    _logger.LogInformation("Mapping attribute '{AttrName}' with value '{Value}' to AttributeID: {AttrId}, AttributeValueID: {AttributeValueId}", 
                        attrName, value, attrId, attributeValueId);
                }

                var variant = new Variant
                {
                    ProductID = productId,
                    SKU = variantDto.SKU,
                    Price = variantDto.Price,
                    Stock = variantDto.Stock,
                    VariantAttributes = attributeValueIds
                        .Select(tuple => new VariantAttribute
                        {
                            AttributeValueID = tuple.AttributeValueID,
                            AttributeID = tuple.AttributeID
                        })
                        .ToList()
                };

                await _variantRepository.AddVariantAsync(variant);
            }
        }

        private async Task CleanupImageOnFailureAsync(CreateProductDTO dto, string imageKey)
        {
            if (dto.ImageFile != null && !string.Equals(imageKey, dto.ImageKey))
            {
                await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                _logger.LogInformation("Deleted uploaded image '{ImageKey}' due to rollback", imageKey);
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
            Console.WriteLine($"Starting deletion process for product ID: {id}");
            await _productRepository.EnsureNoRelatedBatchesAsync(id);
            await _productRepository.EnsureNoRelatedInvoicesAsync(id);
            
            // Kiểm tra sản phẩm tồn tại
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Attempted to delete non-existent product with ID '{ProductID}'", id);
                Console.WriteLine($"Product with ID {id} not found");
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }

            Console.WriteLine($"Product found: {product.ProductName} (ID: {id}) with ImageKey: {product.ImageKey ?? "null"}");

            // Lấy danh sách ảnh phụ
            var additionalImages = await _productRepository.GetProductImagesByProductIdAsync(id);
            Console.WriteLine($"Found {additionalImages.Count} additional images for product ID: {id}");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            Console.WriteLine("Database transaction started");

            try
            {
                // Xóa sản phẩm và dữ liệu liên quan trong database
                await _productRepository.DeleteProductAsync(id);
                Console.WriteLine($"Product ID {id} deleted from database");

                // Thu thập danh sách ImageKey cần xóa
                var imageKeysToDelete = new List<string>();
                Console.WriteLine("Collecting image keys to delete");

                // Kiểm tra và thêm ảnh chính
                if (!string.IsNullOrEmpty(product.ImageKey) && product.ImageKey != "default.jpg")
                {
                    imageKeysToDelete.Add(product.ImageKey);
                    Console.WriteLine($"Added main image key to delete: {product.ImageKey}");
                }
                else
                {
                    Console.WriteLine($"Main image key is invalid or default (ImageKey: {product.ImageKey ?? "null"})");
                }

                // Kiểm tra và thêm ảnh phụ
                foreach (var image in additionalImages)
                {
                    if (!string.IsNullOrEmpty(image.ImageKey) && image.ImageKey != "default.jpg")
                    {
                        imageKeysToDelete.Add(image.ImageKey);
                        Console.WriteLine($"Added additional image key to delete: {image.ImageKey}");
                    }
                    else
                    {
                        Console.WriteLine($"Additional image key is invalid or default (ImageKey: {image.ImageKey ?? "null"})");
                    }
                }

                Console.WriteLine($"Total {imageKeysToDelete.Count} images queued for deletion");

                // Xóa các tệp ảnh
                foreach (var imageKey in imageKeysToDelete)
                {
                    try
                    {
                        Console.WriteLine($"Attempting to delete image from MinIO: {imageKey}");
                        await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                        _logger.LogInformation("Deleted image '{ImageKey}' for product ID '{ProductID}'", imageKey, id);
                        Console.WriteLine($"Successfully deleted image: {imageKey}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete image '{ImageKey}' for product ID '{ProductID}'", imageKey, id);
                        Console.WriteLine($"Failed to delete image: {imageKey}. Error: {ex.Message}");
                        throw new InvalidOperationException($"Failed to delete image '{imageKey}': {ex.Message}", ex);
                    }
                }

                // Commit transaction nếu tất cả thành công
                await transaction.CommitAsync();
                _logger.LogInformation("Successfully deleted product with ID '{ProductID}' and related data", id);
                Console.WriteLine($"Transaction committed. Product ID {id} and related data successfully deleted");
            }
            catch (Exception ex)
            {
                // Rollback transaction nếu có lỗi
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete product with ID '{ProductID}'", id);
                Console.WriteLine($"Transaction rolled back. Failed to delete product ID {id}. Error: {ex.Message}");
                throw new InvalidOperationException($"Failed to delete product: {ex.Message}", ex);
            }
        }
        public async Task<List<Models.ProductImage>> UpsertProductImagesAsync(UpsertProductImagesDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            var productImages = new List<Models.ProductImage>();
            var uploadedImageKeys = new List<(string ImageKey, IFormFile ImageFile)>();

            try
            {
                // Get and delete all existing images
                var existingImages = await _context.ProductImages
                    .Where(pi => pi.ProductID == dto.ProductID)
                    .ToListAsync();

                if (existingImages.Any())
                {
                    // Remove from database
                    _context.ProductImages.RemoveRange(existingImages);
                    await _context.SaveChangesAsync();

                    // Delete from MinIO
                    var imageKeysToDelete = existingImages
                        .Where(pi => pi.ImageKey != "default.jpg")
                        .Select(pi => pi.ImageKey)
                        .ToList();

                    foreach (var imageKey in imageKeysToDelete)
                    {
                        try
                        {
                            Console.WriteLine($"Attempting to delete image from MinIO: {imageKey}");
                            await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                            _logger.LogInformation("Deleted image '{ImageKey}' for product ID '{ProductID}'", imageKey, dto.ProductID);
                            Console.WriteLine($"Successfully deleted image: {imageKey}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete image '{ImageKey}' for product ID '{ProductID}'", imageKey, dto.ProductID);
                            Console.WriteLine($"Failed to delete image: {imageKey}. Error: {ex.Message}");
                            throw new InvalidOperationException($"Failed to delete image '{imageKey}': {ex.Message}", ex);
                        }
                    }
                }

                // If no images provided, commit and return empty list
                if (dto.Images == null || !dto.Images.Any())
                {
                    await transaction.CommitAsync();
                    return new List<Models.ProductImage>();
                }

                // Validate image count
                var newImageCount = dto.Images.Count;
                if (newImageCount > MaxImagesPerProduct)
                {
                    throw new InvalidOperationException($"Product cannot have more than {MaxImagesPerProduct} additional images.");
                }

                // Validate ordinal numbers
                var providedOrdinalNumbers = dto.Images.Select(i => i.OrdinalNumbers).ToList();
                if (providedOrdinalNumbers.Any(n => n < 1 || n > 5))
                {
                    throw new ArgumentException("Ordinal numbers must be between 1 and 5.", nameof(dto.Images));
                }

                if (providedOrdinalNumbers.Distinct().Count() != providedOrdinalNumbers.Count)
                {
                    throw new ArgumentException("Ordinal numbers must be unique.", nameof(dto.Images));
                }

                // Process new images
                foreach (var imageDto in dto.Images)
                {
                    if (imageDto.ImageFile == null || imageDto.ImageFile.Length == 0)
                    {
                        throw new ArgumentException("Image file cannot be null or empty.", nameof(imageDto.ImageFile));
                    }

                    var (imageKey, imageUrl) = await ProcessProductImageAsync(imageDto.ImageFile);
                    uploadedImageKeys.Add((imageKey, imageDto.ImageFile));

                    var productImage = new Models.ProductImage
                    {
                        ProductID = dto.ProductID,
                        ImageKey = imageKey,
                        ImageUrl = imageUrl,
                        OrdinalNumbers = imageDto.OrdinalNumbers
                    };

                    productImages.Add(productImage);
                }

                // Save new images
                await _context.ProductImages.AddRangeAsync(productImages);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();
                return productImages;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // Cleanup newly uploaded images
                foreach (var (imageKey, _) in uploadedImageKeys)
                {
                    if (!string.IsNullOrEmpty(imageKey) && imageKey != "default.jpg")
                    {
                        try
                        {
                            Console.WriteLine($"Attempting to clean up image from MinIO: {imageKey}");
                            await _fileService.DeleteFileAsync(ProductImageBucketName, imageKey);
                            _logger.LogInformation("Cleaned up image '{ImageKey}' due to failure", imageKey);
                            Console.WriteLine($"Successfully cleaned up image: {imageKey}");
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.LogError(cleanupEx, "Failed to clean up image '{ImageKey}'", imageKey);
                            Console.WriteLine($"Failed to clean up image: {imageKey}. Error: {cleanupEx.Message}");
                        }
                    }
                }
                _logger.LogError(ex, "Failed to upsert product images for ProductID {ProductID}", dto.ProductID);
                throw;
            }
        }
        private async Task<(string ImageKey, string ImageUrl)> ProcessProductImageAsync(IFormFile imageFile)
        {
            string publicBucketName = "public-product-images";
            const long maxSize = 5 * 1024 * 1024; // 5MB

            if (imageFile == null || imageFile.Length == 0)
            {
                throw new ArgumentException("Image file cannot be null or empty.", nameof(imageFile));
            }

            if (!await IsImageAsync(imageFile))
            {
                throw new ArgumentException("File must be an image (JPEG, PNG, GIF, etc.).", nameof(imageFile));
            }

            string imageKey;
            string imageUrl;

            try
            {
                using var stream = imageFile.OpenReadStream();
                imageKey = await _fileService.ConvertAndUploadPublicFileAsJpgAsync(
                    stream, 
                    publicBucketName, 
                    $"{Guid.NewGuid()}.jpg", 
                    maxSize
                );

                if (string.IsNullOrEmpty(imageKey))
                {
                    throw new InvalidOperationException("Failed to generate image key during upload.");
                }

                imageUrl = await _fileService.GetStaticPublicFileUrl(publicBucketName, imageKey);
                if (string.IsNullOrEmpty(imageUrl))
                {
                    throw new InvalidOperationException("Failed to generate image URL.");
                }

                _logger.LogInformation("Successfully processed image with key '{ImageKey}'", imageKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image for upload to bucket '{BucketName}'", publicBucketName);
                throw;
            }

            return (imageKey, imageUrl);
        }
        public async Task<List<ProductTag>> UpsertProductTagsAsync(UpsertProductTagsDTO dto)
        {
            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            var productTags = new List<ProductTag>();

            try
            {
                // Get or create tags
                var tagNames = dto.TagNames.Select(n => n.Trim()).Distinct().ToList();
                var existingTags = await _productTagRepository.GetTagsByNamesAsync(tagNames);
                var existingTagNames = existingTags.Select(t => t.TagName).ToList();
                var newTagNames = tagNames.Except(existingTagNames).ToList();

                var tags = existingTags.ToList();
                foreach (var newTagName in newTagNames)
                {
                    if (!string.IsNullOrWhiteSpace(newTagName))
                    {
                        var newTag = await _productTagRepository.CreateTagAsync(newTagName);
                        tags.Add(newTag);
                    }
                }

                // Delete existing product tags
                await _productTagRepository.DeleteProductTagsByProductIdAsync(dto.ProductID);

                // Create new product tags
                foreach (var tag in tags)
                {
                    var productTag = new ProductTag
                    {
                        ProductID = dto.ProductID,
                        TagID = tag.TagID
                    };
                    productTags.Add(productTag);
                }

                // Save new product tags
                await _productTagRepository.AddProductTagsAsync(productTags);

                // Delete unused tags
                var unusedTags = await _productTagRepository.GetUnusedTagsAsync();
                if (unusedTags.Any())
                {
                    var unusedTagIds = unusedTags.Select(t => t.TagID).ToList();
                    await _productTagRepository.DeleteTagsAsync(unusedTagIds);
                    _logger.LogInformation("Deleted {Count} unused tags", unusedTags.Count);
                }

                // Commit transaction
                await transaction.CommitAsync();
                return productTags;
            }
            catch (Exception ex)
            {
                // Rollback transaction
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to upsert product tags for ProductID {ProductID}", dto.ProductID);
                throw;
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