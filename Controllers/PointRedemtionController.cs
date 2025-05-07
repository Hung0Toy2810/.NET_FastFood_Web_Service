using Microsoft.AspNetCore.Mvc;
using LapTrinhWindows.Services;
using LapTrinhWindows.Models.dto;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/pointredemptions")]
    public class PointRedemtionController : ControllerBase
    {
        private readonly IPointRedemptionService _pointRedemptionService;

        public PointRedemtionController(IPointRedemptionService pointRedemptionService)
        {
            _pointRedemptionService = pointRedemptionService;
        }

        // GET: api/pointredemptions?includeInactive=false
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            List<PointRedemptionDTO> redemptions = await _pointRedemptionService.GetAllAsync(includeInactive);
            return Ok(redemptions);
        }

        // GET: api/pointredemptions/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            PointRedemptionDTO? redemption = await _pointRedemptionService.GetByIdAsync(id);
            if (redemption == null)
            {
                return NotFound();
            }
            return Ok(redemption);
        }

        // POST: api/pointredemptions
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PointRedemptionDTO dto)
        {
            PointRedemptionDTO created = await _pointRedemptionService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.PointRedemptionID }, created);
        }

        // PUT: api/pointredemptions/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PointRedemptionDTO dto)
        {
            PointRedemptionDTO updated = await _pointRedemptionService.UpdateAsync(id, dto);
            return Ok(updated);
        }

        // DELETE: api/pointredemptions/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _pointRedemptionService.DeleteAsync(id);
            return NoContent();
        }
    }
}