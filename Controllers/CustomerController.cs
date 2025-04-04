using LapTrinhWindows.Services;
using LapTrinhWindows.Models.dto.CustomerDTO;

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
}
