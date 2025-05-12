using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LapTrinhWindows.Models.DTO;
using LapTrinhWindows.Services;
using LapTrinhWindows.Repositories.ProductRepository;
using LapTrinhWindows.Repositories.ProductImageRepository;
using LapTrinhWindows.Repositories.ProductTagRepository;

namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IProductImageRepository _productImageRepository;
        private readonly IProductTagRepository _productTagRepository;
 
        public ProductController(IProductService productService, IProductImageRepository productImageRepository, IProductTagRepository productTagRepository)
        {
            _productService = productService 
                ?? throw new ArgumentNullException(nameof(productService));
            _productImageRepository = productImageRepository 
                ?? throw new ArgumentNullException(nameof(productImageRepository));
            _productTagRepository = productTagRepository 
                ?? throw new ArgumentNullException(nameof(productTagRepository));
        }
        
 
        // GET: api/products/{id}
        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProductDetail(int id)
        {
            var product = await _productService.GetProductDetailAsync(id);
            return Ok(product);
        }
 
        // POST: api/products
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> AddProduct([FromForm] CreateProductDTO dto)
        {
            await _productService.AddProductAsync(dto);
            return Ok("Product created successfully");
        }
 
        // PUT: api/products/variant/price
        [Authorize(Roles = "Manager,Staff")]
        [HttpPut("variant/price")]
        public async Task<IActionResult> UpdateVariantPrice([FromBody] UpdateVariantPriceDTO dto)
        {
            await _productService.UpdateVariantPriceBySkuAsync(dto);
            return Ok(new { Message = $"Price for SKU '{dto.SKU}' updated to {dto.Price}" });
        }
 
        // PUT: api/products/{id}
        [AllowAnonymous]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDTO dto)
        {
            await _productService.UpdateProductAsync(id, dto);
            return Ok(new { Message = $"Product with ID '{id}' updated successfully" });
        }
 
        // DELETE: api/products/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            await _productService.DeleteProductAsync(id);
            return Ok(new { Message = "Product deleted successfully." });
        }
 
        // POST: api/products/images
        [HttpPost("images")]
        public async Task<IActionResult> UpsertProductImages([FromForm] UpsertProductImagesDTO dto)
        {
            var productImages = await _productService.UpsertProductImagesAsync(dto);
            return Ok("Product images updated successfully");
        }
 
        // POST: api/products/tags
        [HttpPost("tags")]
        public async Task<IActionResult> UpsertProductTags([FromBody] UpsertProductTagsDTO dto)
        {
            var productTags = await _productService.UpsertProductTagsAsync(dto);
            return Ok("Product tags updated successfully");
        }
        [AllowAnonymous]
        [HttpGet("additional_images/{id:int}")]
        public async Task<IActionResult> GetAdditionalImages(int id)
        {
            var images = await _productImageRepository.GetAdditionalProductImagesByProductIdAsync(id);
            return Ok(images);
        }
        [AllowAnonymous]
        [HttpGet("tags/{id:int}")]
        public async Task<IActionResult> GetProductTags(int id)
        {
            var tags = await _productTagRepository.GetProductTagNamesByProductIdAsync(id);
            return Ok(tags);
        }
    }
}