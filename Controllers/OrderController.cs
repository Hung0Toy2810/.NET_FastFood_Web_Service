using LapTrinhWindows.Services;
using LapTrinhWindows.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
        }

        
        [AllowAnonymous]
        [HttpPost("online")]
        public async Task<IActionResult> CreateOnlineInvoice([FromBody] CreateOnlineInvoiceDTO dto)
        {
            Guid? customerId = null;
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(customerIdClaim) && Guid.TryParse(customerIdClaim, out var customerGuid))
            {
                customerId = customerGuid;
            }

            var invoice = await _invoiceService.CreateOnlineInvoiceAsync(dto, customerId);
            var response = await _invoiceService.GetInvoiceByIdAsync(invoice.InvoiceID, customerId, false);
            return Ok(response);
        }

        
        [AllowAnonymous]
        [HttpPost("offline")]
        public async Task<IActionResult> CreateOfflineInvoice([FromBody] CreateOfflineInvoiceDTO dto)
        {
            var cashierStaff = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cashierStaff))
            {
                return Unauthorized("User not authenticated");
            }

            if (!Guid.TryParse(cashierStaff, out var cashierGuid))
            {
                return BadRequest("Invalid cashier ID format");
            }

            var invoice = await _invoiceService.CreateOfflineInvoiceAsync(dto, cashierGuid);
            var response = await _invoiceService.GetInvoiceByIdAsync(invoice.InvoiceID, null, true);
            return Ok(response);
        }

        
        [Authorize]
        [HttpGet("{invoiceId}")]
        public async Task<IActionResult> GetInvoiceById(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId, userId, isStaffOrManager);
            if (invoice == null)
            {
                return NotFound($"Invoice with ID {invoiceId} not found.");
            }
            return Ok(invoice);
        }

        
        [Authorize]
        [HttpGet("customer")]
        public async Task<IActionResult> GetInvoicesByCustomer()
        {
            Guid? customerId = null;
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(customerIdClaim) && Guid.TryParse(customerIdClaim, out var customerGuid))
            {
                customerId = customerGuid;
            }

            var invoices = await _invoiceService.GetInvoicesByCustomerIdAsync(customerId);
            return Ok(invoices);
        }

        
        [Authorize]
        [HttpGet("filter")]
        public async Task<IActionResult> GetInvoicesByFilter([FromQuery] InvoiceFilterDTO filter)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var invoices = await _invoiceService.GetInvoicesByFilterAsync(filter, userId, isStaffOrManager);
            return Ok(invoices);
        }

        
        [Authorize]
        [HttpPut("{invoiceId}")]
        public async Task<IActionResult> UpdateInvoice(int invoiceId, [FromBody] UpdateInvoiceDTO dto)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var updatedInvoice = await _invoiceService.UpdateInvoiceAsync(invoiceId, dto, userId, isStaffOrManager);
            return Ok(updatedInvoice);
        }

        
        [Authorize]
        [HttpPost("{invoiceId}/cancel")]
        public async Task<IActionResult> CancelInvoice(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            await _invoiceService.MarkInvoiceAsCancelledAsync(invoiceId, userId, isStaffOrManager);
            return Ok("Invoice cancelled successfully");
        }

        
        [Authorize(Roles = "Staff,Manager")]
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingInvoices()
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            var invoices = await _invoiceService.GetPendingInvoicesAsync(userId, true);
            return Ok(invoices);
        }

        
        [Authorize]
        [HttpPost("{invoiceId}/feedback")]
        public async Task<IActionResult> ProvideFeedback(int invoiceId, [FromBody] FeedbackDTO feedbackDto)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            var updatedInvoice = await _invoiceService.ProvideFeedbackAsync(invoiceId, feedbackDto, userId);
            return Ok(updatedInvoice);
        }

        
        /// <returns>Success message.</returns>
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("{invoiceId}/status/pending")]
        public async Task<IActionResult> SetInvoiceStatusPending(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            await _invoiceService.SetInvoiceStatusPendingAsync(invoiceId, userId, true);
            return Ok("Invoice status updated to Pending successfully");
        }

        
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("{invoiceId}/status/paid")]
        public async Task<IActionResult> SetInvoiceStatusPaid(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            await _invoiceService.SetInvoiceStatusPaidAsync(invoiceId, userId, true);
            return Ok("Invoice status updated to Paid successfully");
        }

        
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("{invoiceId}/delivery-status/pending")]
        public async Task<IActionResult> SetDeliveryStatusPending(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            await _invoiceService.SetDeliveryStatusPendingAsync(invoiceId, userId, true);
            return Ok("Delivery status updated to Pending successfully");
        }

        
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("{invoiceId}/delivery-status/in-transit")]
        public async Task<IActionResult> SetDeliveryStatusInTransit(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            await _invoiceService.SetDeliveryStatusInTransitAsync(invoiceId, userId, true);
            return Ok("Delivery status updated to InTransit successfully");
        }

        
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("{invoiceId}/delivery-status/not-delivered")]
        public async Task<IActionResult> SetDeliveryStatusNotDelivered(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            await _invoiceService.SetDeliveryStatusNotDeliveredAsync(invoiceId, userId, true);
            return Ok("Delivery status updated to NotDelivered successfully");
        }

        
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("{invoiceId}/delivery-status/delivered")]
        public async Task<IActionResult> SetDeliveryStatusDelivered(int invoiceId)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                userId = userGuid;
            }

            await _invoiceService.SetDeliveryStatusDeliveredAsync(invoiceId, userId, true);
            return Ok("Delivery status updated to Delivered successfully");
        }
    }
}