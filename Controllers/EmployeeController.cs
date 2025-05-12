using LapTrinhWindows.Models.dto.EmployeeDTO;
using LapTrinhWindows.Services;
using LapTrinhWindows.Models.dto.CustomerDTO;
using LapTrinhWindows.Models.dto;
namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/employee")]
    public class EmployeeController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly IEmployeeLoginService _employeeLoginService;
        private readonly IJwtTokenService _jwtTokenService;
        public EmployeeController(IEmployeeService employeeService, IEmployeeLoginService employeeLoginService, IJwtTokenService jwtTokenService)
        {
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
            _employeeLoginService = employeeLoginService ?? throw new ArgumentNullException(nameof(employeeLoginService));
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] CreateEmployeeDTO dto)
        {
            if (dto == null)
                return BadRequest("Invalid employee data.");

            await _employeeService.AddEmployeeAsync(dto);
            return Ok("Employee registered successfully.");
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] EmployeeLoginDTO dto)
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(clientIp))
                    return BadRequest("Cannot determine client IP.");

                var result = await _employeeLoginService.LoginAsync(dto.Email, dto.Password, clientIp);
                if (result == null)
                    return NotFound("Employee not found");

                return Ok(new
                {
                    token = result.AccessToken,
                    refreshToken = result.RefreshToken,
                    expiresIn = result.ExpiresIn
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [Authorize (Roles = "Staff,Manager")]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var employeeId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(employeeId))
                return BadRequest("Invalid token");

            if (!Guid.TryParse(employeeId, out var employeeGuid))
                return BadRequest("Invalid employee ID format.");

            var profile = await _employeeService.GetEmployeeProfileByIdAsync(employeeGuid);
            if (profile == null)
                return NotFound("Employee not found");

            return Ok(profile);
        }
        [AllowAnonymous]
        [HttpPost("newToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest("Refresh token is required");

            var newTokens = await _jwtTokenService.RefreshTokenAsync(request.RefreshToken);
            if (newTokens.AccessToken == null || newTokens.RefreshToken == null)
                return Unauthorized("Invalid refresh token");

            return Ok(new
            {
                token = newTokens.AccessToken,
                refreshToken = newTokens.RefreshToken
            });
        }
    } 
}