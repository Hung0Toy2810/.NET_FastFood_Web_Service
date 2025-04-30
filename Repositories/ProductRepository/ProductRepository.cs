using LapTrinhWindows.Models.DTO;
namespace LapTrinhWindows.Repositories.ProductRepository
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product?> GetProductByNameAsync(string productName);
        Task AddProductAsync(Product product);
        Task<ProductDetailDTO> GetProductDetailAsync(int id);
        Task UpdateProductAsync(int id, UpdateProductDTO dto);
        Task DeleteProductAsync(int id);
        Task<List<ProductImage>> GetProductImagesByProductIdAsync(int productId);
        Task<bool> IsImageKeyUsedByOtherImagesAsync(string imageKey, int excludeProductId);

    }

    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            return await _context.Products.FindAsync(id);
        }

        public async Task AddProductAsync(Product product)
        {
            await _context.Products.AddAsync(product);
        }
        public async Task<Product?> GetProductByNameAsync(string productName)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.ProductName == productName);
        }
        public async Task<ProductDetailDTO> GetProductDetailAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                .ThenInclude(va => va.AttributeValue)
                .ThenInclude(av => av.Attribute)
                .Include(p => p.AdditionalImages) // Thêm nạp AdditionalImages
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID {id} not found.");
            }

            var attributes = product.Variants
                .SelectMany(v => v.VariantAttributes)
                .GroupBy(va => va.AttributeValue.Attribute)
                .Select(g => new AttributeDTO
                {
                    AttributeName = g.Key.AttributeName,
                    Values = g.Select(va => va.AttributeValue.Value).Distinct().ToList()
                })
                .ToList();

            var variants = product.Variants
                .Select(v => new VariantDTO
                {
                    VariantID = v.VariantID,
                    SKU = v.SKU,
                    Price = v.Price,
                    Stock = v.Stock,
                    AttributeValues = v.VariantAttributes
                        .ToDictionary(
                            va => va.AttributeValue.Attribute.AttributeName,
                            va => va.AttributeValue.Value
                        )
                })
                .ToList();

            return new ProductDetailDTO
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                CategoryID = product.CategoryID,
                Discount = product.Discount,
                ImageUrl = product.ImageUrl,
                AdditionalImageUrls = product.AdditionalImages.Select(pi => pi.ImageUrl).ToList(), // Thêm danh sách URL ảnh phụ
                Attributes = attributes,
                Variants = variants
            };
        }
        public async Task UpdateProductAsync(int id, UpdateProductDTO dto)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }

            // Cập nhật thông tin cơ bản
            product.ProductName = dto.ProductName;
            product.CategoryID = dto.CategoryID;
            product.Discount = dto.Discount;
            product.ImageKey = dto.ImageKey;
            product.ImageUrl = product.ImageUrl; // Giữ nguyên ImageUrl

            // Lấy danh sách SKU hiện tại
            var existingSkus = product.Variants.Select(v => v.SKU).ToHashSet();
            var newSkus = dto.Variants.Select(v => v.SKU).ToHashSet();

            // Xóa các Variant không còn trong DTO
            var variantsToRemove = product.Variants
                .Where(v => !newSkus.Contains(v.SKU))
                .ToList();

            foreach (var variant in variantsToRemove)
            {
                _context.VariantAttributes.RemoveRange(variant.VariantAttributes);
                _context.Variants.Remove(variant);
            }

            // Thêm/cập nhật Variants
            foreach (var variantDto in dto.Variants)
            {
                var variant = product.Variants
                    .FirstOrDefault(v => v.SKU == variantDto.SKU);

                if (variant == null)
                {
                    variant = new Variant
                    {
                        ProductID = product.ProductID,
                        SKU = variantDto.SKU,
                        Price = variantDto.Price,
                        Stock = variantDto.Stock
                    };
                    product.Variants.Add(variant);
                }
                else
                {
                    variant.Price = variantDto.Price;
                    variant.Stock = variantDto.Stock;
                    _context.VariantAttributes.RemoveRange(variant.VariantAttributes);
                }

                // Thêm VariantAttributes
                foreach (var kvp in variantDto.AttributeValues)
                {
                    var attr = await _context.Attributes
                        .FirstOrDefaultAsync(a => a.AttributeName == kvp.Key)
                        ?? new LapTrinhWindows.Models.Attribute { AttributeName = kvp.Key };

                    if (attr.AttributeID == 0)
                    {
                        _context.Attributes.Add(attr);
                        await _context.SaveChangesAsync();
                    }

                    var attrValue = await _context.AttributeValues
                        .FirstOrDefaultAsync(av => av.AttributeID == attr.AttributeID && av.Value == kvp.Value)
                        ?? new AttributeValue { AttributeID = attr.AttributeID, Value = kvp.Value };

                    if (attrValue.AttributeValueID == 0)
                    {
                        _context.AttributeValues.Add(attrValue);
                        await _context.SaveChangesAsync();
                    }

                    variant.VariantAttributes.Add(new VariantAttribute
                    {
                        VariantID = variant.VariantID,
                        AttributeValueID = attrValue.AttributeValueID
                    });
                }
            }

            // Xóa AttributeValues không còn được sử dụng
            var usedAttributeValueIds = product.Variants
                .SelectMany(v => v.VariantAttributes)
                .Select(va => va.AttributeValueID)
                .Distinct()
                .ToHashSet();

            var unusedAttributeValues = await _context.AttributeValues
                .Where(av => !usedAttributeValueIds.Contains(av.AttributeValueID))
                .ToListAsync();

            foreach (var av in unusedAttributeValues)
            {
                var isUsedElsewhere = await _context.VariantAttributes
                    .AnyAsync(va => va.AttributeValueID == av.AttributeValueID && va.Variant.ProductID != id);

                if (!isUsedElsewhere)
                {
                    _context.AttributeValues.Remove(av);
                }
            }

            // Xóa Attributes không còn được sử dụng
            var usedAttributeIds = (await _context.AttributeValues
                .Where(av => usedAttributeValueIds.Contains(av.AttributeValueID))
                .Select(av => av.AttributeID)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var unusedAttributes = await _context.Attributes
                .Where(a => !usedAttributeIds.Contains(a.AttributeID))
                .ToListAsync();

            foreach (var attr in unusedAttributes)
            {
                var isUsedElsewhere = await _context.AttributeValues
                    .AnyAsync(av => av.AttributeID == attr.AttributeID && usedAttributeValueIds.Contains(av.AttributeValueID));

                if (!isUsedElsewhere)
                {
                    _context.Attributes.Remove(attr);
                }
            }

            await _context.SaveChangesAsync();
        }
        public async Task DeleteProductAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                .Include(p => p.AdditionalImages)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }

            // Xóa VariantAttributes và Variants
            foreach (var variant in product.Variants)
            {
                _context.VariantAttributes.RemoveRange(variant.VariantAttributes);
                _context.Variants.Remove(variant);
            }

            // Xóa ProductImages
            _context.ProductImages.RemoveRange(product.AdditionalImages);

            // Xóa sản phẩm
            _context.Products.Remove(product);

            // Xóa AttributeValues không còn được sử dụng
            var usedAttributeValueIds = product.Variants
                .SelectMany(v => v.VariantAttributes)
                .Select(va => va.AttributeValueID)
                .Distinct()
                .ToHashSet();

            var unusedAttributeValues = await _context.AttributeValues
                .Where(av => usedAttributeValueIds.Contains(av.AttributeValueID))
                .ToListAsync();

            foreach (var av in unusedAttributeValues)
            {
                var isUsedElsewhere = await _context.VariantAttributes
                    .AnyAsync(va => va.AttributeValueID == av.AttributeValueID && va.Variant.ProductID != id);

                if (!isUsedElsewhere)
                {
                    _context.AttributeValues.Remove(av);
                }
            }

            // Xóa Attributes không còn được sử dụng
            var usedAttributeIds = await _context.AttributeValues
                .Where(av => usedAttributeValueIds.Contains(av.AttributeValueID))
                .Select(av => av.AttributeID)
                .Distinct()
                .ToHashSetAsync();

            var unusedAttributes = await _context.Attributes
                .Where(a => !usedAttributeIds.Contains(a.AttributeID))
                .ToListAsync();

            foreach (var attr in unusedAttributes)
            {
                var isUsedElsewhere = await _context.AttributeValues
                    .AnyAsync(av => av.AttributeID == attr.AttributeID);

                if (!isUsedElsewhere)
                {
                    _context.Attributes.Remove(attr);
                }
            }

            await _context.SaveChangesAsync();
        }
        public async Task<List<ProductImage>> GetProductImagesByProductIdAsync(int productId)
        {
            return await _context.ProductImages
                .Where(pi => pi.ProductID == productId)
                .ToListAsync();
        }

        public async Task<bool> IsImageKeyUsedByOtherImagesAsync(string imageKey, int excludeProductId)
        {
            return await _context.Products
                .AnyAsync(p => p.ImageKey == imageKey && p.ProductID != excludeProductId)
                || await _context.ProductImages
                    .AnyAsync(pi => pi.ImageKey == imageKey && pi.ProductID != excludeProductId);
        }
    }
}