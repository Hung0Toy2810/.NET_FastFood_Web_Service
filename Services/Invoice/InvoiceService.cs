using LapTrinhWindows.DTO;
using LapTrinhWindows.Repositories.PointRedemptionRepository;
using LapTrinhWindows.Repositories.InvoiceDetailRepository;
using LapTrinhWindows.Repositories.InvoiceRepository;
using LapTrinhWindows.Repositories.VariantRepository;
using LapTrinhWindows.Repositories.BatchRepository;
using LapTrinhWindows.Repositories.InvoiceStatusHistoryRepository;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LapTrinhWindows.Repositories.EmployeeRepository;

namespace LapTrinhWindows.Services
{
    public interface IInvoiceService
    {
        Task<Invoice> CreateOnlineInvoiceAsync(CreateOnlineInvoiceDTO dto, Guid? customerId);
        Task<Invoice> CreateOfflineInvoiceAsync(CreateOfflineInvoiceDTO dto, Guid cashierStaff);
        Task<InvoiceResponseDTO?> GetInvoiceByIdAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task<List<InvoiceResponseDTO>> GetInvoicesByCustomerIdAsync(Guid? customerId);
        Task<List<InvoiceResponseDTO>> GetInvoicesByFilterAsync(InvoiceFilterDTO filter, Guid? userId, bool isStaffOrManager);
        Task<InvoiceResponseDTO> UpdateInvoiceAsync(int invoiceId, UpdateInvoiceDTO dto, Guid? userId, bool isStaffOrManager);
        Task<InvoiceResponseDTO> MarkInvoiceAsCancelledAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task<InvoiceResponseDTO> ProvideFeedbackAsync(int invoiceId, FeedbackDTO feedbackDto, Guid? userId);
        Task<List<InvoiceResponseDTO>> GetPendingInvoicesAsync(Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusPendingAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusInTransitAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusNotDeliveredAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusDeliveredAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task<InvoiceResponseDTO> ChangeDeliveryAddressAsync(int invoiceId, string deliveryAddress, Guid? userId);
        
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IInvoiceDetailRepository _invoiceDetailRepository;
        private readonly IVariantRepository _variantRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IPointRedemptionRepository _pointRedemptionRepository;
        private readonly IInvoiceStatusHistoryRepository _statusHistoryRepository;
        private readonly IPointService _pointService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IEmployeeRepository _employeeRepository;

        public InvoiceService(
            IInvoiceRepository invoiceRepository,
            IInvoiceDetailRepository invoiceDetailRepository,
            IVariantRepository variantRepository,
            IBatchRepository batchRepository,
            IPointRedemptionRepository pointRedemptionRepository,
            IInvoiceStatusHistoryRepository statusHistoryRepository,
            IPointService pointService,
            ApplicationDbContext context,
            IEmployeeRepository employeeRepository,
            ILogger<InvoiceService> logger)
        {
            _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
            _invoiceDetailRepository = invoiceDetailRepository ?? throw new ArgumentNullException(nameof(invoiceDetailRepository));
            _variantRepository = variantRepository ?? throw new ArgumentNullException(nameof(variantRepository));
            _batchRepository = batchRepository ?? throw new ArgumentNullException(nameof(batchRepository));
            _pointRedemptionRepository = pointRedemptionRepository ?? throw new ArgumentNullException(nameof(pointRedemptionRepository));
            _statusHistoryRepository = statusHistoryRepository ?? throw new ArgumentNullException(nameof(statusHistoryRepository));	  
            _pointService = pointService ?? throw new ArgumentNullException(nameof(pointService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        }

        private async Task<Dictionary<string, Variant>> PreloadVariantsAsync(List<CreateInvoiceDetailDTO> details)
        {
            var skus = details.Select(d => d.SKU).Distinct().ToList();
            return await _variantRepository.GetVariantsBySkusAsync(skus);
        }

        // Preload point redemptions with Batch and Variant
        private async Task<Dictionary<int, PointRedemption>> PreloadPointRedemptionsAsync(List<CreateInvoiceDetailDTO> details)
        {
            var redemptionIds = details.Where(d => d.IsPointRedemption && d.PointRedemptionID.HasValue)
                                    .Select(d => d.PointRedemptionID!.Value)
                                    .Distinct()
                                    .ToList();
            return await _pointRedemptionRepository.GetPointRedemptionsByIdsAsync(redemptionIds);
        }

        // Preload batches
        private async Task<Dictionary<int, Batch>> PreloadBatchesAsync(List<CreateInvoiceDetailDTO> details)
        {
            var batchIds = details.Select(d => d.BatchID).Distinct().ToList();
            return await _batchRepository.GetBatchesByIdsAsync(batchIds);
        }

        // Validate stock and batch
        private async Task ValidateStockAndBatchAsync(List<CreateInvoiceDetailDTO> details)
        {
            // Preload variants, point redemptions, and batches
            var variants = await PreloadVariantsAsync(details);
            var redemptions = await PreloadPointRedemptionsAsync(details);
            var batches = await PreloadBatchesAsync(details);

            foreach (var detail in details)
            {
                // Validate variant
                var variant = variants.GetValueOrDefault(detail.SKU);
                if (variant == null)
                {
                    throw new KeyNotFoundException($"Variant with SKU {detail.SKU} not found.");
                }
                if (variant.Product.ProductID != detail.ProductId)
                {
                    throw new ArgumentException($"SKU {detail.SKU} does not belong to ProductId {detail.ProductId}.");
                }

                // Validate batch
                var batch = batches.GetValueOrDefault(detail.BatchID);
                if (batch == null)
                {
                    throw new KeyNotFoundException($"Batch with BatchID {detail.BatchID} not found.");
                }
                if (batch.SKU != detail.SKU)
                {
                    throw new ArgumentException($"BatchID {detail.BatchID} does not belong to SKU {detail.SKU}.");
                }
                if (batch.ExpirationDate.HasValue && batch.ExpirationDate < DateTime.UtcNow)
                {
                    throw new InvalidOperationException($"Batch {detail.BatchID} for SKU {detail.SKU} has expired.");
                }
                if (batch.AvailableQuantity < detail.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient quantity for SKU {detail.SKU} in BatchID {detail.BatchID}. Requested: {detail.Quantity}, Available: {batch.AvailableQuantity}.");
                }

                // Validate point redemption
                if (detail.IsPointRedemption)
                {
                    if (!detail.PointRedemptionID.HasValue)
                    {
                        throw new ArgumentException($"PointRedemptionID is required for point redemption detail with SKU {detail.SKU}.");
                    }
                    var redemption = redemptions.GetValueOrDefault(detail.PointRedemptionID!.Value);
                    if (redemption == null)
                    {
                        throw new KeyNotFoundException($"PointRedemption with ID {detail.PointRedemptionID} not found.");
                    }
                    if (redemption.SKU != detail.SKU)
                    {
                        throw new ArgumentException($"PointRedemption ID {detail.PointRedemptionID} does not belong to SKU {detail.SKU}.");
                    }
                    if (redemption.BatchID != detail.BatchID)
                    {
                        throw new ArgumentException($"BatchID {detail.BatchID} does not match BatchID {redemption.BatchID} of PointRedemption ID {detail.PointRedemptionID}.");
                    }
                    if (redemption.AvailableQuantity < detail.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient available quantity for PointRedemption ID {detail.PointRedemptionID}. Requested: {detail.Quantity}, Available: {redemption.AvailableQuantity}.");
                    }
                    if (redemption.Status != PointRedemptionStatus.Active)
                    {
                        throw new InvalidOperationException($"PointRedemption ID {detail.PointRedemptionID} is not active.");
                    }
                    if (redemption.StartDate > DateTime.UtcNow || redemption.EndDate < DateTime.UtcNow)
                    {
                        throw new InvalidOperationException($"PointRedemption ID {detail.PointRedemptionID} is not valid at the current time.");
                    }
                }
            }
        }

        

        private async Task<Invoice> CreateInvoiceInternalAsync(
            string deliveryAddress,
            List<CreateInvoiceDetailDTO> details,
            Guid? customerId,
            Guid? cashierStaff,
            OrderType orderType,
            DeliveryStatus defaultDeliveryStatus,
            PaymentMethods paymentMethod)
        {
            // Validate stock, batch, and point redemption before processing
            await ValidateStockAndBatchAsync(details);

            // Preload variants, point redemptions, and batches
            var variants = await PreloadVariantsAsync(details);
            var redemptions = await PreloadPointRedemptionsAsync(details);
            var batches = await PreloadBatchesAsync(details);

            // Validate and calculate points used for redemption
            int totalPointsUsed = 0;
            if (details.Any(d => d.IsPointRedemption) && customerId.HasValue)
            {
                foreach (var detail in details.Where(d => d.IsPointRedemption))
                {
                    var redemption = redemptions[detail.PointRedemptionID!.Value];
                    totalPointsUsed += redemption.PointsRequired * detail.Quantity;
                }
                if (!await _pointService.ValidateCustomerPointsAsync(customerId.Value, totalPointsUsed))
                {
                    _logger.LogWarning("Customer {CustomerId} has insufficient points: Required {PointsRequired}", customerId, totalPointsUsed);
                    throw new InvalidOperationException("Customer does not have enough points to redeem.");
                }
            }

            // Start transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create invoice
                var invoice = new Invoice
                {
                    CashierStaff = cashierStaff,
                    CustomerID = customerId,
                    CreateAt = DateTime.UtcNow,
                    Discount = 0,
                    PaymentMethod = paymentMethod,
                    Status = InvoiceStatus.Paid,
                    Total = 0,
                    DeliveryAddress = deliveryAddress,
                    DeliveryStatus = DeliveryStatus.Pending,
                    OrderType = orderType,
                    Feedback = string.Empty,
                    Star = 0,
                    IsAnonymous = !customerId.HasValue
                };

                var createdInvoice = await _invoiceRepository.CreateInvoiceAsync(invoice);

                // Process invoice details
                double invoiceTotal = 0;
                foreach (var detailDto in details)
                {
                    var variant = variants[detailDto.SKU];
                    var batch = batches[detailDto.BatchID];

                    // Calculate total
                    double detailTotal = detailDto.IsPointRedemption
                        ? 0 // No cost for point redemption
                        : detailDto.Quantity * (double)(variant.Price * (decimal)(1 - variant.Product.Discount));
                    invoiceTotal += detailTotal;

                    // Create invoice detail
                    var detail = new InvoiceDetail
                    {
                        InvoiceID = createdInvoice.InvoiceID,
                        SKU = detailDto.SKU,
                        BatchID = detailDto.BatchID,
                        Quantity = detailDto.Quantity,
                        Total = detailTotal,
                        IsPointRedemption = detailDto.IsPointRedemption,
                        PointRedemptionID = detailDto.PointRedemptionID
                    };

                    await _invoiceDetailRepository.CreateInvoiceDetailAsync(detail);

                    // Deduct batch quantity
                    batch.AvailableQuantity -= detailDto.Quantity;
                    if (batch.AvailableQuantity < 0)
                    {
                        throw new InvalidOperationException($"Available quantity of batch {batch.BatchID} cannot be negative.");
                    }
                    await _batchRepository.UpdateBatchAsync(batch);

                    // Update variant stock
                    var batchList = await _batchRepository.GetBatchesBySkuAsync(detailDto.SKU);
                    variant.Stock = batchList.Sum(b => b.AvailableQuantity);
                    await _variantRepository.UpdateVariantAsync(variant);

                    // Deduct point redemption quantity if applicable
                    if (detailDto.IsPointRedemption)
                    {
                        var redemption = redemptions[detailDto.PointRedemptionID!.Value];
                        redemption.AvailableQuantity -= detailDto.Quantity;
                        if (redemption.AvailableQuantity < 0)
                        {
                            throw new InvalidOperationException($"Available quantity of PointRedemption ID {detailDto.PointRedemptionID} cannot be negative.");
                        }
                        await _pointRedemptionRepository.UpdateAsync(redemption);
                    }
                }

                // Update invoice total
                createdInvoice.Total = invoiceTotal;
                await _invoiceRepository.UpdateInvoiceAsync(createdInvoice);

                // Update customer points
                if (customerId.HasValue)
                {
                    if (totalPointsUsed > 0)
                    {
                        await _pointService.UpdateCustomerPointsAsync(customerId.Value, -totalPointsUsed);
                    }
                    else
                    {
                        int pointsEarned = 0;
                        foreach (var detail in details.Where(d => !d.IsPointRedemption))
                        {
                            var variant = variants[detail.SKU];
                            pointsEarned += await _pointService.CalculatePointsAsync(
                                (decimal)(detail.Quantity * (double)(variant.Price * (decimal)(1 - variant.Product.Discount))),
                                variant.Product.ProductID);
                        }
                        await _pointService.UpdateCustomerPointsAsync(customerId.Value, pointsEarned);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Invoice {InvoiceID} created successfully for customer {CustomerID}", createdInvoice.InvoiceID, customerId);
                return createdInvoice;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating invoice for customer {CustomerID}: {ErrorMessage}", customerId, ex.Message);
                throw new InvalidOperationException($"Error creating invoice: {ex.Message}", ex);
            }
        }

        public async Task<Invoice> CreateOnlineInvoiceAsync(CreateOnlineInvoiceDTO dto, Guid? customerId)
        {
            if (string.IsNullOrWhiteSpace(dto.DeliveryAddress))
            {
                throw new ArgumentException("Delivery address is required for online orders.", nameof(dto.DeliveryAddress));
            }
            if (dto.Details == null || !dto.Details.Any())
            {
                throw new ArgumentException("Invoice must have at least one detail.", nameof(dto.Details));
            }
            if (customerId.HasValue && !await _context.Customers.AnyAsync(c => c.CustomerID == customerId))
            {
                throw new ArgumentException("Invalid customer ID.", nameof(customerId));
            }
            if (!customerId.HasValue && dto.Details.Any(d => d.IsPointRedemption))
            {
                throw new ArgumentException("Anonymous customers cannot redeem points.", nameof(dto.Details));
            }

            return await CreateInvoiceInternalAsync(
                dto.DeliveryAddress,
                dto.Details,
                customerId,
                null,
                OrderType.Online,
                DeliveryStatus.Pending,
                dto.PaymentMethod);
        }

        public async Task<Invoice> CreateOfflineInvoiceAsync(CreateOfflineInvoiceDTO dto, Guid cashierStaff)
        {
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                throw new ArgumentException("Phone number is required for offline orders.", nameof(dto.PhoneNumber));
            }
            if (dto.Details == null || !dto.Details.Any())
            {
                throw new ArgumentException("Invoice must have at least one detail.", nameof(dto.Details));
            }
            if (!await _context.Employees.AnyAsync(e => e.EmployeeID == cashierStaff))
            {
                throw new ArgumentException("Invalid cashier staff ID.", nameof(cashierStaff));
            }

            Guid? customerId = null;
            var customers = await _context.Customers
                .Where(c => c.PhoneNumber == dto.PhoneNumber)
                .ToListAsync();
            if (customers.Count > 1)
            {
                throw new ArgumentException($"Multiple customers found with phone number {dto.PhoneNumber}.", nameof(dto.PhoneNumber));
            }
            if (customers.Any())
            {
                customerId = customers.First().CustomerID;
            }
            else
            {
                _logger.LogInformation("No customer found with phone number {PhoneNumber}. Creating anonymous invoice.", dto.PhoneNumber);
            }

            if (!customerId.HasValue && dto.Details.Any(d => d.IsPointRedemption))
            {
                throw new ArgumentException("Anonymous customers cannot redeem points.", nameof(dto.Details));
            }

            var invoice = await CreateInvoiceInternalAsync(
                string.Empty,
                dto.Details,
                customerId,
                cashierStaff,
                OrderType.Offline,
                DeliveryStatus.Pending,
                dto.PaymentMethod);
            if (!customerId.HasValue)
            {
                invoice.IsAnonymous = true;
                _logger.LogInformation("Created anonymous invoice {InvoiceID} with phone number {PhoneNumber}", invoice.InvoiceID, dto.PhoneNumber);
            }

            return invoice;
        }

        private static class DeliveryStateMachine
        {
            public static bool IsValidTransition(DeliveryStatus from, DeliveryStatus to)
            {
                return (from, to) switch
                {
                    (DeliveryStatus.Pending, DeliveryStatus.InTransit) => true,
                    (DeliveryStatus.InTransit, DeliveryStatus.Delivered) => true,
                    (DeliveryStatus.InTransit, DeliveryStatus.NotDelivered) => true,
                    _ => false
                };
            }

            public static bool CanChangeStatus(DeliveryStatus status, InvoiceStatus invoiceStatus)
            {
                return invoiceStatus != InvoiceStatus.Cancelled &&
                       status != DeliveryStatus.Delivered &&
                       status != DeliveryStatus.NotDelivered;
            }

            public static bool CanChangeAddress(DeliveryStatus status, InvoiceStatus invoiceStatus)
            {
                return invoiceStatus != InvoiceStatus.Cancelled &&
                       status == DeliveryStatus.Pending;
            }

            public static bool CanCancel(DeliveryStatus status, InvoiceStatus invoiceStatus)
            {
                return invoiceStatus == InvoiceStatus.Paid &&
                       (status == DeliveryStatus.Pending || status == DeliveryStatus.NotDelivered);
            }

            public static bool CanProvideFeedback(DeliveryStatus status, InvoiceStatus invoiceStatus)
            {
                return status == DeliveryStatus.Delivered || status == DeliveryStatus.NotDelivered;
            }
        }

        public async Task<InvoiceResponseDTO?> GetInvoiceByIdAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await _invoiceRepository.GetInvoiceByIdAsync(invoiceId);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice with ID {InvoiceId} not found", invoiceId);
                return null;
            }

            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);
            return MapToInvoiceResponseDTO(invoice);
        }

        public async Task<List<InvoiceResponseDTO>> GetInvoicesByCustomerIdAsync(Guid? customerId)
        {
            var invoices = await _invoiceRepository.GetInvoicesByCustomerIdAsync(customerId);
            return invoices.Select(MapToInvoiceResponseDTO).ToList();
        }

        public async Task<List<InvoiceResponseDTO>> GetInvoicesByFilterAsync(InvoiceFilterDTO filter, Guid? userId, bool isStaffOrManager)
        {
            var invoices = await _invoiceRepository.GetInvoicesByFilterAsync(
                filter.Status,
                filter.OrderType,
                filter.StartDate,
                filter.EndDate);

            if (!isStaffOrManager)
            {
                invoices = invoices.Where(i => i.CustomerID == userId).ToList();
            }

            return invoices.Select(MapToInvoiceResponseDTO).ToList();
        }

        public async Task<InvoiceResponseDTO> UpdateInvoiceAsync(int invoiceId, UpdateInvoiceDTO dto, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (dto.DeliveryAddress != null && !DeliveryStateMachine.CanChangeAddress(invoice.DeliveryStatus, invoice.Status))
            {
                throw new InvalidOperationException("Cannot update delivery address for this invoice status.");
            }

            if (dto.DeliveryStatus.HasValue && !DeliveryStateMachine.CanChangeStatus(invoice.DeliveryStatus, invoice.Status))
            {
                throw new InvalidOperationException("Cannot update delivery status for this invoice status.");
            }

            if (dto.Feedback != null || dto.Star.HasValue)
            {
                throw new InvalidOperationException("Feedback and rating must be updated via ProvideFeedbackAsync.");
            }

            ValidateUpdateInvoiceDTO(dto, invoice);

            if (invoice.DeliveryStatus == DeliveryStatus.InTransit && HasNonDeliveryChanges(dto))
            {
                throw new InvalidOperationException("When invoice is in transit, only delivery status can be updated to NotDelivered or Delivered.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldDeliveryStatus = invoice.DeliveryStatus;
                UpdateInvoiceFields(invoice, dto, isStaffOrManager);

                if (dto.DeliveryStatus.HasValue && oldDeliveryStatus != dto.DeliveryStatus.Value)
                {
                    if (!DeliveryStateMachine.IsValidTransition(oldDeliveryStatus, dto.DeliveryStatus.Value))
                    {
                        throw new InvalidOperationException($"Invalid delivery status transition from {oldDeliveryStatus} to {dto.DeliveryStatus.Value}.");
                    }
                }

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();

                _logger.LogInformation("Invoice {InvoiceId} updated successfully", invoiceId);
                return MapToInvoiceResponseDTO(invoice);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating invoice: {ex.Message}", ex);
            }
        }

        public async Task<InvoiceResponseDTO> MarkInvoiceAsCancelledAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Invoice is already cancelled.");
            }

            if (!DeliveryStateMachine.CanCancel(invoice.DeliveryStatus, invoice.Status))
            {
                throw new InvalidOperationException("Cannot cancel invoice in this status.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldStatus = invoice.Status;
                invoice.Status = InvoiceStatus.Cancelled;

                await _invoiceRepository.UpdateInvoiceAsync(invoice);

                if (oldStatus == InvoiceStatus.Paid)
                {
                    await RefundAsync(invoiceId, invoice.CustomerID);
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} marked as cancelled.", invoiceId);
                return MapToInvoiceResponseDTO(invoice);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error cancelling invoice: {ex.Message}", ex);
            }
        }

        public async Task<InvoiceResponseDTO> ProvideFeedbackAsync(int invoiceId, FeedbackDTO feedbackDto, Guid? userId)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, false);

            if (!DeliveryStateMachine.CanProvideFeedback(invoice.DeliveryStatus, invoice.Status))
            {
                throw new InvalidOperationException("Cannot provide feedback for this invoice status.");
            }

            if (!string.IsNullOrEmpty(invoice.Feedback))
            {
                throw new InvalidOperationException("Feedback has already been provided for this invoice.");
            }

            if (string.IsNullOrWhiteSpace(feedbackDto.Feedback))
            {
                throw new ArgumentException("Feedback cannot be empty.");
            }
            if (feedbackDto.Star < 1 || feedbackDto.Star > 5)
            {
                throw new ArgumentException("Rating must be between 1 and 5.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice.Feedback = feedbackDto.Feedback;
                invoice.Star = feedbackDto.Star;

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();

                _logger.LogInformation("Feedback updated for invoice {InvoiceId}", invoiceId);
                return MapToInvoiceResponseDTO(invoice);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating feedback for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating feedback: {ex.Message}", ex);
            }
        }

        public async Task<List<InvoiceResponseDTO>> GetPendingInvoicesAsync(Guid? userId, bool isStaffOrManager)
        {
            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can view pending invoices.");
            }

            var invoices = await _invoiceRepository.GetPendingInvoicesAsync();
            return invoices.Select(MapToInvoiceResponseDTO).ToList();
        }

        public async Task SetDeliveryStatusPendingAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.Pending)
            {
                throw new InvalidOperationException("Invoice is already in Pending status.");
            }
            if (invoice.DeliveryStatus != DeliveryStatus.NotDelivered)
            {
                throw new InvalidOperationException($"Cannot change delivery status from {invoice.DeliveryStatus} to Pending.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice.DeliveryStatus = DeliveryStatus.Pending;

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to Pending delivery status.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating Pending delivery status for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating Pending delivery status: {ex.Message}", ex);
            }
        }

        public async Task SetDeliveryStatusInTransitAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.InTransit)
            {
                throw new InvalidOperationException("Invoice is already in InTransit status.");
            }
            if (!DeliveryStateMachine.IsValidTransition(invoice.DeliveryStatus, DeliveryStatus.InTransit))
            {
                throw new InvalidOperationException($"Cannot change delivery status from {invoice.DeliveryStatus} to InTransit.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice.DeliveryStatus = DeliveryStatus.InTransit;

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to InTransit delivery status.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating InTransit delivery status for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating InTransit delivery status: {ex.Message}", ex);
            }
        }

        public async Task SetDeliveryStatusNotDeliveredAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.NotDelivered)
            {
                throw new InvalidOperationException("Invoice is already in NotDelivered status.");
            }
            if (!DeliveryStateMachine.IsValidTransition(invoice.DeliveryStatus, DeliveryStatus.NotDelivered))
            {
                throw new InvalidOperationException($"Cannot change delivery status from {invoice.DeliveryStatus} to NotDelivered.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice.DeliveryStatus = DeliveryStatus.NotDelivered;

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to NotDelivered delivery status.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating NotDelivered delivery status for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating NotDelivered delivery status: {ex.Message}", ex);
            }
        }

        public async Task SetDeliveryStatusDeliveredAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            await EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.Delivered)
            {
                throw new InvalidOperationException("Invoice is already in Delivered status.");
            }
            if (!DeliveryStateMachine.IsValidTransition(invoice.DeliveryStatus, DeliveryStatus.Delivered))
            {
                throw new InvalidOperationException($"Cannot change delivery status from {invoice.DeliveryStatus} to Delivered.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice.DeliveryStatus = DeliveryStatus.Delivered;

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to Delivered delivery status.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating Delivered delivery status for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating Delivered delivery status: {ex.Message}", ex);
            }
        }

        public async Task<InvoiceResponseDTO> ChangeDeliveryAddressAsync(int invoiceId, string deliveryAddress, Guid? userId)
        {
            if (string.IsNullOrEmpty(deliveryAddress) || deliveryAddress.Length > 500)
            {
                throw new ArgumentException("Delivery address is required and must not exceed 500 characters.");
            }

            var invoice = await _invoiceRepository.GetInvoiceByIdAsync(invoiceId);
            if (invoice == null)
            {
                throw new KeyNotFoundException($"Invoice with ID {invoiceId} not found.");
            }

            if (userId == null || invoice.CustomerID != userId)
            {
                throw new UnauthorizedAccessException("You do not have permission to change this invoice's delivery address.");
            }

            if (!DeliveryStateMachine.CanChangeAddress(invoice.DeliveryStatus, invoice.Status))
            {
                throw new InvalidOperationException("Cannot change delivery address: Invoice is cancelled or delivery status is not Pending.");
            }

            invoice.DeliveryAddress = deliveryAddress;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _invoiceRepository.UpdateInvoiceAsync(invoice);

                await transaction.CommitAsync();
                _logger.LogInformation("Delivery address for invoice {InvoiceId} updated to {DeliveryAddress}", invoiceId, deliveryAddress);

                var invoiceResponse = await GetInvoiceByIdAsync(invoiceId, userId, false);
                if (invoiceResponse == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {invoiceId} could not be found.");
                }
                return invoiceResponse;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating delivery address for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating delivery address: {ex.Message}", ex);
            }
        }

        private async Task RefundAsync(int invoiceId, Guid? customerId)
        {
            _logger.LogInformation("Refund initiated for invoice {InvoiceId} to customer {CustomerId}", invoiceId, customerId);
            await Task.CompletedTask;
        }

        private async Task<Invoice> GetInvoiceOrThrowAsync(int invoiceId)
        {
            var invoice = await _invoiceRepository.GetInvoiceByIdAsync(invoiceId);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice with ID {InvoiceId} not found", invoiceId);
                throw new KeyNotFoundException($"Invoice with ID {invoiceId} not found.");
            }
            return invoice;
        }

        private void ValidateUpdateInvoiceDTO(UpdateInvoiceDTO dto, Invoice invoice)
        {
            if (dto.Star.HasValue && (dto.Star.Value < 1 || dto.Star.Value > 5))
            {
                throw new ArgumentException("Rating must be between 1 and 5.");
            }
            if (dto.DeliveryAddress?.Trim() == "")
            {
                throw new ArgumentException("Delivery address cannot be empty.");
            }
            if (dto.Discount.HasValue && (dto.Discount.Value < 0 || dto.Discount.Value > invoice.Total))
            {
                throw new ArgumentException("Discount must be between 0 and the invoice total.");
            }
            if (dto.DeliveryStatus.HasValue && !Enum.IsDefined(typeof(DeliveryStatus), dto.DeliveryStatus.Value))
            {
                throw new ArgumentException("Invalid delivery status.");
            }
        }

        private bool HasNonDeliveryChanges(UpdateInvoiceDTO dto)
        {
            return dto.DeliveryAddress != null || dto.Discount.HasValue;
        }

        private void UpdateInvoiceFields(Invoice invoice, UpdateInvoiceDTO dto, bool isStaffOrManager)
        {
            if (dto.DeliveryAddress != null)
            {
                invoice.DeliveryAddress = dto.DeliveryAddress;
            }
            if (dto.DeliveryStatus.HasValue)
            {
                if (!isStaffOrManager)
                {
                    throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
                }
                invoice.DeliveryStatus = dto.DeliveryStatus.Value;
            }
            if (dto.Discount.HasValue)
            {
                invoice.Discount = dto.Discount.Value;
            }
        }

        private InvoiceResponseDTO MapToInvoiceResponseDTO(Invoice invoice)
        {
            return new InvoiceResponseDTO
            {
                InvoiceID = invoice.InvoiceID,
                CashierStaff = invoice.CashierStaff,
                CustomerID = invoice.CustomerID,
                CreateAt = invoice.CreateAt,
                Discount = invoice.Discount,
                PaymentMethod = invoice.PaymentMethod,
                Status = invoice.Status,
                Total = invoice.Total,
                DeliveryAddress = invoice.DeliveryAddress,
                DeliveryStatus = invoice.DeliveryStatus,
                OrderType = invoice.OrderType,
                Feedback = invoice.Feedback,
                Star = invoice.Star,
                IsAnonymous = invoice.IsAnonymous,
                InvoiceDetails = invoice.InvoiceDetails.Select(d => new InvoiceDetailDTO
                {
                    InvoiceDetailID = d.InvoiceDetailID,
                    SKU = d.SKU,
                    BatchID = d.BatchID,
                    Quantity = d.Quantity,
                    Total = d.Total,
                    IsPointRedemption = d.IsPointRedemption,
                    PointRedemptionID = d.PointRedemptionID
                }).ToList()
            };
        }

        private async Task EnsureUserHasAccess(Invoice invoice, Guid? userId, bool isStaffOrManager)
        {
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized access attempt to invoice {InvoiceId}: User not authenticated", invoice.InvoiceID);
                throw new UnauthorizedAccessException("User must be authenticated.");
            }

            if (isStaffOrManager)
            {
                var employee = await _employeeRepository.GetEmployeeByIdAsync(userId.Value);
                if (employee == null)
                {
                    _logger.LogWarning("User {UserId} is not a valid employee for invoice {InvoiceId}", userId, invoice.InvoiceID);
                    throw new UnauthorizedAccessException("Invalid employee ID.");
                }
            }
            else
            {
                if (invoice.IsAnonymous)
                {
                    _logger.LogWarning("User {UserId} attempted to access anonymous invoice {InvoiceId}", userId, invoice.InvoiceID);
                    throw new UnauthorizedAccessException("Only staff or managers can access anonymous invoices.");
                }

                if (invoice.CustomerID != userId)
                {
                    _logger.LogWarning("User {UserId} does not have access to invoice {InvoiceId} (CustomerID: {CustomerID})", userId, invoice.InvoiceID, invoice.CustomerID);
                    throw new UnauthorizedAccessException("You can only edit your own invoices.");
                }
            }
        }
    }
}