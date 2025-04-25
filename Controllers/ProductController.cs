using Microsoft.AspNetCore.Mvc;
using LapTrinhWindows.Models.DTO;
using LapTrinhWindows.Services;

namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/product")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        }
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductDetail(int id)
        {
            var product = await _productService.GetProductDetailAsync(id);
            return Ok(product);
            
        }
        [Authorize(Roles = "Manager,Staff")]
        [HttpPost]
        public async Task<IActionResult> AddProduct([FromForm] CreateProductDTO dto)
        {
            
            var product = await _productService.AddProductAsync(dto);
            return CreatedAtAction(nameof(GetProductDetail), new { id = product.ProductID }, product);
            
        }
        [Authorize(Roles = "Manager,Staff")]
        [HttpPut("variant/price")]
        public async Task<IActionResult> UpdateVariantPrice([FromBody] UpdateVariantPriceDTO dto)
        {
            
            await _productService.UpdateVariantPriceBySkuAsync(dto);
            return Ok(new { Message = $"Price for SKU '{dto.SKU}' updated to {dto.Price}" });
            
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDTO dto)
        {
            await _productService.UpdateProductAsync(id, dto);
            return Ok(new { Message = $"Product with ID '{id}' updated successfully" });
            
        }
    }
}