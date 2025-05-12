using LapTrinhWindows.Models.DTO;
using Microsoft.EntityFrameworkCore.Storage;

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
        Task EnsureNoRelatedInvoicesAsync(int productId);
        Task EnsureNoRelatedBatchesAsync(int productId);
        Task<List<Product>> SearchProduct(string searchString);
        

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
            IDbContextTransaction? transaction = default;
            bool ownsTransaction = false;

            try
            {
                // Kiểm tra giao dịch hiện tại
                if (_context.Database.CurrentTransaction == null)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    ownsTransaction = true;
                }

                var product = await _context.Products
                    .Include(p => p.Variants)
                    .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeValue)
                    .ThenInclude(av => av.Attribute)
                    .Include(p => p.Category)
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
                product.ImageUrl = product.ImageUrl;

                // Xóa các Variant không còn trong DTO
                var existingSkus = product.Variants.Select(v => v.SKU).ToHashSet();
                var newSkus = dto.Variants.Select(v => v.SKU).ToHashSet();
                var variantsToRemove = product.Variants
                    .Where(v => !newSkus.Contains(v.SKU))
                    .ToList();

                foreach (var variant in variantsToRemove)
                {
                    _context.VariantAttributes.RemoveRange(variant.VariantAttributes);
                    _context.Variants.Remove(variant);
                }

                var newAttributes = new List<LapTrinhWindows.Models.Attribute>();
                var newAttributeValues = new List<AttributeValue>();
                var newVariantAttributes = new List<VariantAttribute>();

                foreach (var variantDto in dto.Variants)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.SKU == variantDto.SKU);
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

                    foreach (var kvp in variantDto.AttributeValues)
                    {
                        // Tìm hoặc tạo Attribute
                        var attr = newAttributes.FirstOrDefault(a => a.AttributeName == kvp.Key)
                            ?? await _context.Attributes.FirstOrDefaultAsync(a => a.AttributeName == kvp.Key)
                            ?? new LapTrinhWindows.Models.Attribute { AttributeName = kvp.Key };

                        if (attr.AttributeID == 0)
                        {
                            newAttributes.Add(attr);
                            _context.Attributes.Add(attr);
                        }

                        // Tìm hoặc tạo AttributeValue
                        var attrValue = newAttributeValues.FirstOrDefault(av => av.AttributeID == attr.AttributeID && av.Value == kvp.Value)
                            ?? await _context.AttributeValues.FirstOrDefaultAsync(av => av.AttributeID == attr.AttributeID && av.Value == kvp.Value)
                            ?? new AttributeValue { AttributeID = attr.AttributeID, Value = kvp.Value };

                        if (attrValue.AttributeValueID == 0)
                        {
                            newAttributeValues.Add(attrValue);
                            _context.AttributeValues.Add(attrValue);
                        }

                        // Tạo VariantAttribute
                        newVariantAttributes.Add(new VariantAttribute
                        {
                            Variant = variant, 
                            AttributeValue = attrValue, 
                            Attribute = attr 
                        });
                    }
                }

                // Thêm VariantAttributes vào Variants
                foreach (var va in newVariantAttributes)
                {
                    va.Variant.VariantAttributes.Add(va);
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
                        .AnyAsync(va => va.AttributeValueID == av.AttributeValueID);
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
                    var isUsedElsewhere = await _context.VariantAttributes
                        .AnyAsync(va => va.AttributeID == attr.AttributeID) ||
                        await _context.AttributeValues
                        .AnyAsync(av => av.AttributeID == attr.AttributeID);

                    if (!isUsedElsewhere)
                    {
                        _context.Attributes.Remove(attr);
                    }
                }

                // Lưu tất cả thay đổi một lần
                await _context.SaveChangesAsync();

                // Cam kết giao dịch nếu sở hữu
                if (ownsTransaction)
                {
                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }
                }
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
        public async Task DeleteProductAsync(int id)
        {
            Console.WriteLine($"Starting repository deletion process for product ID: {id}");

            // Tải sản phẩm với các quan hệ, sử dụng split query để tối ưu hiệu suất
            var product = await _context.Products
                .AsSplitQuery()
                .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                .Include(p => p.AdditionalImages)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                Console.WriteLine($"Product with ID {id} not found in repository");
                throw new KeyNotFoundException($"Product with ID '{id}' not found.");
            }

            Console.WriteLine($"Product found: {product.ProductName} (ID: {id}) with {product.Variants.Count} variants and {product.AdditionalImages.Count} additional images");

            // Xóa VariantAttributes và Variants
            foreach (var variant in product.Variants.ToList())
            {
                Console.WriteLine($"Removing {variant.VariantAttributes.Count} VariantAttributes for variant ID: {variant.VariantID}");
                _context.VariantAttributes.RemoveRange(variant.VariantAttributes);
                Console.WriteLine($"Removing variant ID: {variant.VariantID}");
                _context.Variants.Remove(variant);
            }

            // Xóa ProductImages
            Console.WriteLine($"Removing {product.AdditionalImages.Count} additional images");
            _context.ProductImages.RemoveRange(product.AdditionalImages);

            // Xóa sản phẩm
            Console.WriteLine($"Removing product ID: {id}");
            _context.Products.Remove(product);

            // Lưu thay đổi để xóa các bản ghi trên
            Console.WriteLine("Saving initial changes to database");
            await _context.SaveChangesAsync();

            // Xóa tất cả AttributeValues liên quan
            var usedAttributeValueIds = product.Variants
                .SelectMany(v => v.VariantAttributes)
                .Select(va => va.AttributeValueID)
                .Distinct()
                .ToHashSet();
            Console.WriteLine($"Collected {usedAttributeValueIds.Count} unique AttributeValueIDs to delete");

            if (usedAttributeValueIds.Any())
            {
                var attributeValuesToDelete = await _context.AttributeValues
                    .Where(av => usedAttributeValueIds.Contains(av.AttributeValueID))
                    .ToListAsync();
                Console.WriteLine($"Removing {attributeValuesToDelete.Count} AttributeValues");
                _context.AttributeValues.RemoveRange(attributeValuesToDelete);
            }

            // Xóa tất cả Attributes (vì chỉ có một sản phẩm, tất cả Attributes đều không còn cần thiết)
            Console.WriteLine("Removing all Attributes");
            var attributesToDelete = await _context.Attributes.ToListAsync();
            Console.WriteLine($"Removing {attributesToDelete.Count} Attributes");
            _context.Attributes.RemoveRange(attributesToDelete);

            // Lưu thay đổi cuối cùng
            Console.WriteLine("Saving final changes to database");
            await _context.SaveChangesAsync();

            // Kiểm tra sau khi xóa để đảm bảo database sạch
            var remainingProducts = await _context.Products.AnyAsync();
            var remainingVariants = await _context.Variants.AnyAsync();
            var remainingVariantAttributes = await _context.VariantAttributes.AnyAsync();
            var remainingAttributeValues = await _context.AttributeValues.AnyAsync();
            var remainingAttributes = await _context.Attributes.AnyAsync();
            var remainingProductImages = await _context.ProductImages.AnyAsync();

            Console.WriteLine($"Post-deletion check: " +
                $"Products: {remainingProducts}, " +
                $"Variants: {remainingVariants}, " +
                $"VariantAttributes: {remainingVariantAttributes}, " +
                $"AttributeValues: {remainingAttributeValues}, " +
                $"Attributes: {remainingAttributes}, " +
                $"ProductImages: {remainingProductImages}");

            if (remainingProducts || remainingVariants || remainingVariantAttributes ||
                remainingAttributeValues || remainingAttributes || remainingProductImages)
            {
                Console.WriteLine("Warning: Some data remains in the database after deletion!");
            }
            else
            {
                Console.WriteLine("Database is clean: All related data successfully deleted");
            }

            Console.WriteLine($"Successfully deleted product ID {id} and all related data from repository");
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
        public async Task EnsureNoRelatedInvoicesAsync(int productId)
        {
            var variantSkus = await _context.Variants
                .Where(v => v.ProductID == productId)
                .Select(v => v.SKU)
                .ToListAsync();

            var hasInvoices = await _context.InvoiceDetails
                .AnyAsync(id => variantSkus.Contains(id.SKU));

            if (hasInvoices)
            {
                Console.WriteLine($"Cannot delete product ID {productId} because it is used in invoices.");
                throw new InvalidOperationException($"Cannot delete product ID {productId} because it is used in invoices.");
            }
        }
        public async Task EnsureNoRelatedBatchesAsync(int productId)
        {
            var variantSkus = await _context.Variants
                .Where(v => v.ProductID == productId)
                .Select(v => v.SKU)
                .ToListAsync();

            var hasBatches = await _context.Batches
                .AnyAsync(b => variantSkus.Contains(b.SKU));

            if (hasBatches)
            {
                Console.WriteLine("Cannot delete product ID {ProductId} because it has related batches.", productId);
                throw new InvalidOperationException($"Cannot delete product ID {productId} because it has related batches.");
            }
        }
        public async Task<List<Product>> SearchProduct(string searchString)
        {
            // Chuẩn hóa chuỗi tìm kiếm
            searchString = searchString?.Trim().ToLower() ?? string.Empty;

            // Tạo query cơ bản (không cần Include vì sử dụng lazy loading)
            var query = _context.Products.AsQueryable();

            // Nếu chuỗi tìm kiếm không rỗng, áp dụng điều kiện tìm kiếm
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                // Phân tách từ khóa
                var keywords = searchString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Xây dựng điều kiện tìm kiếm
                foreach (var keyword in keywords)
                {
                    string currentKeyword = keyword;
                    query = query.Where(p =>
                        p.ProductName.ToLower().Contains(currentKeyword) ||
                        p.Category.CategoryName.ToLower().Contains(currentKeyword) ||
                        p.ProductTags.Any(pt => pt.Tag.TagName.ToLower().Contains(currentKeyword))
                    );
                }

                // Sắp xếp theo mức độ liên quan
                query = query.OrderByDescending(p =>
                    (p.ProductName.ToLower().Contains(searchString) ? 3 : 0) + // Ưu tiên khớp chính xác ProductName
                    (p.Category.CategoryName.ToLower().Contains(searchString) ? 2 : 0) + // Ưu tiên CategoryName
                    p.ProductTags.Count(pt => pt.Tag.TagName.ToLower().Contains(searchString)) // Số lượng Tag khớp
                );
            }
            else
            {
                // Nếu không có từ khóa, sắp xếp theo ProductID (hoặc tiêu chí khác)
                query = query.OrderBy(p => p.ProductID);
            }

            // Chỉ lấy 10 sản phẩm đầu tiên
            query = query.Take(10);

            // Thực thi query và trả về kết quả
            return await query.ToListAsync();
        }
    }
}