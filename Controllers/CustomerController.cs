using LapTrinhWindows.Services;
using LapTrinhWindows.Models.dto.CustomerDTO;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICustomerLoginService _customerLoginService;

    public CustomersController(
        ICustomerService customerService,
        ICustomerLoginService customerLoginService)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        _customerLoginService = customerLoginService ?? throw new ArgumentNullException(nameof(customerLoginService));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] CreateCustomerDTO dto)
    {
        await _customerService.AddCustomerAsync(dto);
        return Ok("Customer registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        try
        {
            // Lấy IP từ HttpContext (bắt buộc)
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(clientIp))
                return BadRequest("Cannot determine client IP.");

            // Gọi LoginAsync với IP bắt buộc
            var result = await _customerLoginService.LoginAsync(dto.PhoneNumber, dto.Password, clientIp);
            if (result == null)
                return NotFound("Customer not found");

            // Trả về cả Access Token, Refresh Token và ExpiresIn
            return Ok(new
            {
                token = result.AccessToken,        // Giữ key "token" để tương thích cũ
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

    [Authorize(Roles = "Customer")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
        Console.WriteLine("User claims in endpoint: " + string.Join(", ", claims));

        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (customerId == null)
        {
            return Unauthorized("User not authenticated");
        }

        if (!Guid.TryParse(customerId, out var customerGuid))
        {
            return BadRequest("Invalid customer ID format");
        }

        var profile = await _customerService.GetCustomerProfileByIdAsync(customerGuid);
        if (profile == null)
        {
            return NotFound("Customer not found");
        }

        return Ok(profile);
    }
    [Authorize(Policy = "CustomerOnly")]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
    {
        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (customerId == null)
        {
            return Unauthorized("User not authenticated");
        }

        if (!Guid.TryParse(customerId, out var customerGuid))
        {
            return BadRequest("Invalid customer ID format");
        }

        await _customerService.ChangePasswordAsync(customerGuid, dto);
        return Ok("Password changed successfully");
    }
    [Authorize(Policy = "CustomerOnly")]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerProfileDTO dto)
    {
        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (customerId == null)
        {
            return Unauthorized("User not authenticated");
        }

        if (!Guid.TryParse(customerId, out var customerGuid))
        {
            return BadRequest("Invalid customer ID format");
        }

        await _customerService.UpdateCustomerInformationAsync(customerGuid, dto);
        return Ok("Profile updated successfully");
    }
    [Authorize(Policy = "CustomerOnly")]
    [HttpPut("profile/avt")]
    public async Task<IActionResult> UpdateCustomerAvt([FromForm] IFormFile avtFile)
    {
        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (customerId == null)
        {
            return Unauthorized("User not authenticated");
        }

        if (!Guid.TryParse(customerId, out var customerGuid))
        {
            return BadRequest("Invalid customer ID format");
        }

        await _customerService.UpdateCustomerAvtAsync(customerGuid, avtFile);
        return Ok("Profile updated successfully");
    }
    [Authorize(Policy = "CustomerOnly")]
    [HttpDelete("account/status")]
    public async Task<IActionResult> DeleteAccount()
    {
        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (customerId == null)
        {
            return Unauthorized("User not authenticated");
        }

        if (!Guid.TryParse(customerId, out var customerGuid))
        {
            return BadRequest("Invalid customer ID format");
        }

        await _customerService.DeleteCustomerAsync(customerGuid);
        return Ok("Account deleted successfully");
    }
    //activate account
    [Authorize(Policy = "CustomerOnly")]
    [HttpPut("account/status")]
    public async Task<IActionResult> ActivateAccount()
    {
        var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (customerId == null)
        {
            return Unauthorized("User not authenticated");
        }

        if (!Guid.TryParse(customerId, out var customerGuid))
        {
            return BadRequest("Invalid customer ID format");
        }

        await _customerService.ActivateCustomerAsync(customerGuid);
        return Ok("Account status updated successfully");
    }
}