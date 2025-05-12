using LapTrinhWindows.Models.dto;
using LapTrinhWindows.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LapTrinhWindows.Controllers
{
    [Route("api/PointRedemption")]
    [ApiController]
    public class PointRedemptionController : ControllerBase
    {
        private readonly IPointRedemptionService _pointRedemptionService;
        private readonly ILogger<PointRedemptionController> _logger;

        public PointRedemptionController(
            IPointRedemptionService pointRedemptionService,
            ILogger<PointRedemptionController> logger)
        {
            _pointRedemptionService = pointRedemptionService;
            _logger = logger;
        }
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<List<PointRedemptionDTO>>> GetAll([FromQuery] bool includeInactive = false)
        {
            var pointRedemptions = await _pointRedemptionService.GetAllAsync(includeInactive);
            return Ok(pointRedemptions);
        }
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<PointRedemptionDTO>> GetById(int id)
        {
            var pointRedemption = await _pointRedemptionService.GetByIdAsync(id);
            if (pointRedemption == null)
            {
                return NotFound($"Point redemption with ID {id} not found.");
            }
            return Ok(pointRedemption);
        }
        [Authorize(Roles = "Manager,Staff")]
        [HttpPost]
        public async Task<ActionResult<PointRedemptionDTO>> Create([FromBody] PointRedemptionDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var created = await _pointRedemptionService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.PointRedemptionID }, created);
        }
        [Authorize(Roles = "Manager,Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<PointRedemptionDTO>> Update(int id, [FromBody] PointRedemptionDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != dto.PointRedemptionID)
            {
                return BadRequest("PointRedemptionID does not match ID in URL.");
            }

            var updated = await _pointRedemptionService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        [Authorize(Roles = "Manager,Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _pointRedemptionService.DeleteAsync(id);
            return NoContent();
        }
    }
}