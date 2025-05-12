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
            
                await _stockService.AddBatchAndUpdateStockAsync(batchDto);
                return Ok(new { Message = "Thêm lô hàng thành công." });
            
        }
    }
}