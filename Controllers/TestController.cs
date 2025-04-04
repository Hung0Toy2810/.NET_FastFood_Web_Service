using LapTrinhWindows.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        // Test CustomValidationException với danh sách lỗi
        [HttpGet("validation")]
        public IActionResult TestValidation()
        {
            var errors = new Dictionary<string, string[]>
            {
                { "Name", new[] { "Tên không được để trống" } },
                { "Age", new[] { "Tuổi phải là số dương", "Tuổi không được vượt quá 150" } }
            };
            throw new CustomValidationException(errors);
        }

        // Test NotFoundException
        [HttpGet("not-found")]
        public IActionResult TestNotFound()
        {
            throw new NotFoundException("Không tìm thấy tài nguyên bạn yêu cầu");
        }

        // Test BusinessRuleException
        [HttpGet("business-rule")]
        public IActionResult TestBusinessRule()
        {
            throw new BusinessRuleException("Bạn không thể đặt hàng vượt quá 100 sản phẩm");
        }

        // Test UnauthorizedAccessException (có thể liên quan đến JWT middleware)
        [Authorize]
        [HttpGet("unauthorized")]
        public IActionResult TestUnauthorized()
        {
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập vào tài nguyên này");
        }

        // Test lỗi mặc định
        [HttpGet("error")]
        public IActionResult TestDefaultError()
        {
            throw new Exception("Lỗi bất ngờ xảy ra trong hệ thống");
        }

        [Authorize(Policy = "ManagerOnly")]
        [HttpGet("manager-data")]
        public IActionResult GetManagerData()
        {
            return Ok("This data is only accessible by managers.");
        }

        [Authorize(Policy = "CustomerOnly")]
        [HttpGet("customer-data")]
        public IActionResult GetCustomerData()
        {
            return Ok("This data is only accessible by customers.");
        }

        [Authorize(Policy = "EmployeeOnly")]
        [HttpGet("employee-data")]
        public IActionResult GetEmployeeData()
        {
            return Ok("This data is only accessible by employees.");
        }
        // write a test var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        [HttpGet("user-id")]
        [Authorize]
        public IActionResult GetUserId()
        {
            var userId = User.FindFirst("id")?.Value;
            return Ok(userId);
        }
    }
}