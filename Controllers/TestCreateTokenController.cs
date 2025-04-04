using LapTrinhWindows.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LapTrinhWindows.Models.dto;
using LapTrinhWindows.Services;
namespace LapTrinhWindows.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IJwtTokenService _jwtTokenService;

        public TokenController(IJwtTokenService jwtTokenService)
        {
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> CreateToken([FromBody] TokenRequest request)
        {
            var token = await _jwtTokenService.GenerateTokenAsync(
                request.Id,
                request.Username,
                request.Role
            );
            return Ok(new { token });
        }
    }
}