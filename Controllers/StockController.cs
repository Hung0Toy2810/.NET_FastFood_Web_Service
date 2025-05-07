using LapTrinhWindows.Models.DTO;
using LapTrinhWindows.Services;
using Microsoft.AspNetCore.Mvc;

namespace LapTrinhWindows.Controllers
{
    [Route("api/stock")]
    [ApiController]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;

        public StockController(IStockService stockService)
        {
            _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        }

        [HttpPost("batches")]
        public async Task<IActionResult> AddBatch([FromBody] BatchCreateDto batchDto)
        {
            try
            {
                await _stockService.AddBatchAndUpdateStockAsync(batchDto);
                return Ok(new { Message = "Thêm lô hàng thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Đã xảy ra lỗi khi thêm lô hàng.", Details = ex.Message });
            }
        }
    }
}