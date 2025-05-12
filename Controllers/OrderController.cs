using LapTrinhWindows.Services;
using LapTrinhWindows.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LapTrinhWindows.Controllers
{
    [ApiController]
    [Route("api/invoices")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
        }

        [AllowAnonymous]
        [HttpPost("anonymous-online")]
        public async Task<IActionResult> CreateAnonymousOnlineInvoice([FromBody] CreateOnlineInvoiceDTO dto)
        {
            var invoice = await _invoiceService.CreateOnlineInvoiceAsync(dto, null);
            var response = await _invoiceService.GetInvoiceByIdAsync(invoice.InvoiceID, null, false);
            return Ok(response);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("customer-online")]
        public async Task<IActionResult> CreateCustomerOnlineInvoice([FromBody] CreateOnlineInvoiceDTO dto)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerIdClaim) || !Guid.TryParse(customerIdClaim, out var customerId))
            {
                return Unauthorized("Invalid customer ID in token.");
            }

            var invoice = await _invoiceService.CreateOnlineInvoiceAsync(dto, customerId);
            var response = await _invoiceService.GetInvoiceByIdAsync(invoice.InvoiceID, customerId, false);
            return Ok(response);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("offline")]
        public async Task<IActionResult> CreateOfflineInvoice([FromBody] CreateOfflineInvoiceDTO dto)
        {
            var cashierStaffClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cashierStaffClaim) || !Guid.TryParse(cashierStaffClaim, out var cashierStaff))
            {
                return Unauthorized("Invalid cashier staff ID in token.");
            }

            var invoice = await _invoiceService.CreateOfflineInvoiceAsync(dto, cashierStaff);
            var response = await _invoiceService.GetInvoiceByIdAsync(invoice.InvoiceID, cashierStaff, true);
            return Ok(response);
        }

        [Authorize]
        [HttpPost("get")]
        public async Task<IActionResult> GetInvoiceById([FromBody] InvoiceIdRequestDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? userId = string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var guid) ? null : guid;

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var invoice = await _invoiceService.GetInvoiceByIdAsync(request.InvoiceId, userId, isStaffOrManager);
            if (invoice == null)
            {
                return NotFound($"Invoice with ID {request.InvoiceId} not found.");
            }
            return Ok(invoice);
        }

        [Authorize]
        [HttpGet("customer")]
        public async Task<IActionResult> GetInvoicesByCustomer()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerIdClaim) || !Guid.TryParse(customerIdClaim, out var customerId))
            {
                return Unauthorized("Invalid customer ID in token.");
            }

            var invoices = await _invoiceService.GetInvoicesByCustomerIdAsync(customerId);
            return Ok(invoices);
        }

        [Authorize]
        [HttpGet("filter")]
        public async Task<IActionResult> GetInvoicesByFilter([FromQuery] InvoiceFilterDTO filter)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? userId = string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var guid) ? null : guid;

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var invoices = await _invoiceService.GetInvoicesByFilterAsync(filter, userId, isStaffOrManager);
            return Ok(invoices);
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> UpdateInvoice([FromBody] UpdateInvoiceDTO dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? userId = string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var guid) ? null : guid;

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var updatedInvoice = await _invoiceService.UpdateInvoiceAsync(dto.InvoiceId, dto, userId, isStaffOrManager);
            return Ok(updatedInvoice);
        }

        [Authorize]
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelInvoice([FromBody] InvoiceIdRequestDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? userId = string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var guid) ? null : guid;

            var isStaffOrManager = User.IsInRole("Staff") || User.IsInRole("Manager");
            var cancelledInvoice = await _invoiceService.MarkInvoiceAsCancelledAsync(request.InvoiceId, userId, isStaffOrManager);
            return Ok(cancelledInvoice);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingInvoices()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid user ID in token.");
            }

            var invoices = await _invoiceService.GetPendingInvoicesAsync(userId, true);
            return Ok(invoices);
        }

        [Authorize]
        [HttpPost("feedback")]
        public async Task<IActionResult> ProvideFeedback([FromBody] FeedbackDTO dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid customer ID in token.");
            }

            var updatedInvoice = await _invoiceService.ProvideFeedbackAsync(dto.InvoiceId, dto, userId);
            return Ok(updatedInvoice);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("delivery-status/pending")]
        public async Task<IActionResult> SetDeliveryStatusPending([FromBody] InvoiceIdRequestDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid staff ID in token.");
            }

            await _invoiceService.SetDeliveryStatusPendingAsync(request.InvoiceId, userId, true);
            var response = await _invoiceService.GetInvoiceByIdAsync(request.InvoiceId, userId, true);
            return Ok(response);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("delivery-status/in-transit")]
        public async Task<IActionResult> SetDeliveryStatusInTransit([FromBody] InvoiceIdRequestDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid staff ID in token.");
            }

            await _invoiceService.SetDeliveryStatusInTransitAsync(request.InvoiceId, userId, true);
            var response = await _invoiceService.GetInvoiceByIdAsync(request.InvoiceId, userId, true);
            return Ok(response);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("delivery-status/not-delivered")]
        public async Task<IActionResult> SetDeliveryStatusNotDelivered([FromBody] InvoiceIdRequestDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid staff ID in token.");
            }

            await _invoiceService.SetDeliveryStatusNotDeliveredAsync(request.InvoiceId, userId, true);
            var response = await _invoiceService.GetInvoiceByIdAsync(request.InvoiceId, userId, true);
            return Ok(response);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("delivery-status/delivered")]
        public async Task<IActionResult> SetDeliveryStatusDelivered([FromBody] InvoiceIdRequestDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid staff ID in token.");
            }

            await _invoiceService.SetDeliveryStatusDeliveredAsync(request.InvoiceId, userId, true);
            var response = await _invoiceService.GetInvoiceByIdAsync(request.InvoiceId, userId, true);
            return Ok(response);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("change-delivery-address")]
        public async Task<IActionResult> ChangeDeliveryAddress([FromBody] ChangeDeliveryAddressDTO dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid customer ID in token.");
            }

            var updatedInvoice = await _invoiceService.ChangeDeliveryAddressAsync(dto.InvoiceId, dto.DeliveryAddress, userId);
            return Ok(updatedInvoice);
        }
    }
}