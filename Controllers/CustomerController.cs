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
        var token = await _customerLoginService.LoginAsync(dto.PhoneNumber, dto.Password);
        return Ok(new { token });
    }

    [Authorize(Policy = "CustomerOnly")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
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