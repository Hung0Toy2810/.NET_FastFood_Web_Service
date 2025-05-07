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
        Task MarkInvoiceAsCancelledAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task<List<InvoiceResponseDTO>> GetPendingInvoicesAsync(Guid? userId, bool isStaffOrManager);
        Task<InvoiceResponseDTO> ProvideFeedbackAsync(int invoiceId, FeedbackDTO feedbackDto, Guid? userId);
        Task SetInvoiceStatusPendingAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetInvoiceStatusPaidAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusPendingAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusInTransitAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusNotDeliveredAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
        Task SetDeliveryStatusDeliveredAsync(int invoiceId, Guid? userId, bool isStaffOrManager);
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

        public InvoiceService(
            IInvoiceRepository invoiceRepository,
            IInvoiceDetailRepository invoiceDetailRepository,
            IVariantRepository variantRepository,
            IBatchRepository batchRepository,
            IPointRedemptionRepository pointRedemptionRepository,
            IInvoiceStatusHistoryRepository statusHistoryRepository,
            IPointService pointService,
            ApplicationDbContext context,
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
        }

        // Check user access
        private void EnsureUserHasAccess(Invoice invoice, Guid? userId, bool isStaffOrManager)
        {
            if (!isStaffOrManager && invoice.CustomerID != userId)
            {
                _logger.LogWarning("User {UserId} does not have access to invoice {InvoiceId}", userId, invoice.InvoiceID);
                throw new UnauthorizedAccessException("You can only edit your own invoices.");
            }
        }

        // Check InTransit status
        private void EnsureNotInTransit(Invoice invoice)
        {
            if (invoice.DeliveryStatus == DeliveryStatus.InTransit)
            {
                throw new InvalidOperationException("Cannot update invoice information while in transit. Only delivery status can be updated to NotDelivered or Delivered.");
            }
        }

        // Validate stock and batch
        private async Task ValidateStockAndBatchAsync(List<CreateInvoiceDetailDTO> details)
        {
            foreach (var detail in details)
            {
                var variant = await _variantRepository.GetVariantBySkuAsync(detail.SKU);
                if (variant == null)
                {
                    throw new KeyNotFoundException($"Variant with SKU {detail.SKU} not found.");
                }
                if (variant.Product.ProductID != detail.ProductId)
                {
                    throw new ArgumentException($"SKU {detail.SKU} does not belong to ProductId {detail.ProductId}.");
                }

                var batch = await _batchRepository.GetBatchByIdAsync(detail.BatchID);
                if (batch == null)
                {
                    throw new KeyNotFoundException($"Batch with BatchID {detail.BatchID} not found.");
                }
                if (batch.SKU != detail.SKU)
                {
                    throw new ArgumentException($"BatchID {detail.BatchID} does not belong to SKU {detail.SKU}.");
                }
                if (batch.AvailableQuantity < detail.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient quantity for SKU {detail.SKU} in BatchID {detail.BatchID}. Requested: {detail.Quantity}, Available: {batch.AvailableQuantity}.");
                }

                if (detail.IsPointRedemption && !detail.PointRedemptionID.HasValue)
                {
                    throw new ArgumentException($"PointRedemptionID is required for point redemption detail with SKU {detail.SKU}.");
                }
            }
        }

        // Preload variants
        private async Task<Dictionary<string, Variant>> PreloadVariantsAsync(List<CreateInvoiceDetailDTO> details)
        {
            var skus = details.Select(d => d.SKU).Distinct().ToList();
            var variants = await _context.Variants
                .Where(v => skus.Contains(v.SKU))
                .Include(v => v.Product)
                .ToDictionaryAsync(v => v.SKU, v => v);
            return variants;
        }

        // Create invoice (common for online and offline)
        private async Task<Invoice> CreateInvoiceInternalAsync(
            string deliveryAddress,
            List<CreateInvoiceDetailDTO> details,
            Guid? customerId,
            Guid? cashierStaff,
            OrderType orderType,
            DeliveryStatus defaultDeliveryStatus,
            PaymentMethods paymentMethod)
        {
            // Validate stock and batch before processing
            await ValidateStockAndBatchAsync(details);

            // Preload variants to reduce database queries
            var variants = await PreloadVariantsAsync(details);

            // Validate and calculate points used for redemption
            int totalPointsUsed = 0;
            if (details.Any(d => d.IsPointRedemption) && customerId.HasValue)
            {
                foreach (var detail in details.Where(d => d.IsPointRedemption))
                {
                    var redemption = await _pointRedemptionRepository.GetByIdAsync(detail.PointRedemptionID!.Value);
                    if (redemption == null)
                    {
                        throw new ArgumentException($"PointRedemption with ID {detail.PointRedemptionID} not found.");
                    }
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
                    Status = InvoiceStatus.Paid, // Default to Paid
                    Total = 0,
                    DeliveryAddress = deliveryAddress,
                    DeliveryStatus = defaultDeliveryStatus,
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
                    var variant = variants.GetValueOrDefault(detailDto.SKU);
                    if (variant == null)
                    {
                        throw new KeyNotFoundException($"Variant with SKU {detailDto.SKU} not found.");
                    }

                    var batch = await _batchRepository.GetBatchByIdAsync(detailDto.BatchID);
                    if (batch == null)
                    {
                        throw new KeyNotFoundException($"Batch with BatchID {detailDto.BatchID} not found.");
                    }

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
                    _context.Batches.Update(batch);

                    // Update variant stock
                    var batches = await _batchRepository.GetBatchesBySkuAsync(detailDto.SKU);
                    variant.Stock = batches.Sum(b => b.AvailableQuantity);
                    _context.Variants.Update(variant);

                    // Deduct point redemption quantity if applicable
                    if (detailDto.IsPointRedemption)
                    {
                        var redemption = await _pointRedemptionRepository.GetByIdAsync(detailDto.PointRedemptionID!.Value);
                        if (redemption == null || redemption.AvailableQuantity < detailDto.Quantity)
                        {
                            throw new InvalidOperationException($"Insufficient available quantity for PointRedemption ID {detailDto.PointRedemptionID}.");
                        }
                        redemption.AvailableQuantity -= detailDto.Quantity;
                        _context.PointRedemptions.Update(redemption);
                    }
                }

                // Update invoice total
                createdInvoice.Total = invoiceTotal;
                _context.Invoices.Update(createdInvoice);

                // Update customer points
                if (customerId.HasValue)
                {
                    if (totalPointsUsed > 0)
                    {
                        await _pointService.UpdateCustomerPointsAsync(customerId.Value, -totalPointsUsed);
                    }
                    else
                    {
                        // Calculate points for all products
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

        public async Task<InvoiceResponseDTO?> GetInvoiceByIdAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await _invoiceRepository.GetInvoiceByIdAsync(invoiceId);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice with ID {InvoiceId} not found", invoiceId);
                return null;
            }

            EnsureUserHasAccess(invoice, userId, isStaffOrManager);
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
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update a cancelled invoice.");
            }

            if (dto.DeliveryAddress != null && invoice.DeliveryStatus == DeliveryStatus.Delivered)
            {
                throw new InvalidOperationException("Cannot update delivery address for a delivered invoice.");
            }

            ValidateUpdateInvoiceDTO(dto);

            if (invoice.DeliveryStatus == DeliveryStatus.InTransit && HasNonDeliveryChanges(dto))
            {
                throw new InvalidOperationException("When invoice is in transit, only delivery status can be updated to NotDelivered or Delivered.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldStatus = invoice.Status;
                UpdateInvoiceFields(invoice, dto, isStaffOrManager);

                if (dto.Status.HasValue && oldStatus != dto.Status.Value)
                {
                    var history = new InvoiceStatusHistory
                    {
                        InvoiceID = invoiceId,
                        OldStatus = oldStatus,
                        NewStatus = dto.Status.Value,
                        ChangedAt = DateTime.UtcNow,
                        ChangedBy = userId
                    };
                    await _statusHistoryRepository.CreateStatusHistoryAsync(history);
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

        public async Task MarkInvoiceAsCancelledAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Invoice is already cancelled.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.Delivered)
            {
                throw new InvalidOperationException("Cannot cancel a delivered invoice.");
            }
            EnsureNotInTransit(invoice);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldStatus = invoice.Status;
                invoice.Status = InvoiceStatus.Cancelled;

                var history = new InvoiceStatusHistory
                {
                    InvoiceID = invoiceId,
                    OldStatus = oldStatus,
                    NewStatus = InvoiceStatus.Cancelled,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = userId
                };
                await _statusHistoryRepository.CreateStatusHistoryAsync(history);

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();

                _logger.LogInformation("Invoice {InvoiceId} marked as cancelled.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error cancelling invoice: {ex.Message}", ex);
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

        public async Task<InvoiceResponseDTO> ProvideFeedbackAsync(int invoiceId, FeedbackDTO feedbackDto, Guid? userId)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            EnsureUserHasAccess(invoice, userId, false);
            EnsureNotInTransit(invoice);

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

        public async Task SetInvoiceStatusPendingAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change invoice status.");
            }
            if (invoice.Status == InvoiceStatus.Pending)
            {
                throw new InvalidOperationException("Invoice is already in Pending status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot change status from Cancelled to Pending.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldStatus = invoice.Status;
                invoice.Status = InvoiceStatus.Pending;

                var history = new InvoiceStatusHistory
                {
                    InvoiceID = invoiceId,
                    OldStatus = oldStatus,
                    NewStatus = InvoiceStatus.Pending,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = userId
                };
                await _statusHistoryRepository.CreateStatusHistoryAsync(history);

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to Pending status.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating Pending status for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating Pending status: {ex.Message}", ex);
            }
        }

        public async Task SetInvoiceStatusPaidAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change invoice status.");
            }
            if (invoice.Status == InvoiceStatus.Paid)
            {
                throw new InvalidOperationException("Invoice is already in Paid status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot change status from Cancelled to Paid.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldStatus = invoice.Status;
                invoice.Status = InvoiceStatus.Paid;

                var history = new InvoiceStatusHistory
                {
                    InvoiceID = invoiceId,
                    OldStatus = oldStatus,
                    NewStatus = InvoiceStatus.Paid,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = userId
                };
                await _statusHistoryRepository.CreateStatusHistoryAsync(history);

                await _invoiceRepository.UpdateInvoiceAsync(invoice);
                await transaction.CommitAsync();
                _logger.LogInformation("Invoice {InvoiceId} updated to Paid status.", invoiceId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating Paid status for invoice {InvoiceId}: {ErrorMessage}", invoiceId, ex.Message);
                throw new InvalidOperationException($"Error updating Paid status: {ex.Message}", ex);
            }
        }

        public async Task SetDeliveryStatusPendingAsync(int invoiceId, Guid? userId, bool isStaffOrManager)
        {
            var invoice = await GetInvoiceOrThrowAsync(invoiceId);
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

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
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.Status != InvoiceStatus.Paid)
            {
                throw new InvalidOperationException("Can only change to InTransit when invoice is paid.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.InTransit)
            {
                throw new InvalidOperationException("Invoice is already in InTransit status.");
            }
            if (invoice.DeliveryStatus != DeliveryStatus.Pending)
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
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.Status != InvoiceStatus.Paid)
            {
                throw new InvalidOperationException("Can only change to NotDelivered when invoice is paid.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.NotDelivered)
            {
                throw new InvalidOperationException("Invoice is already in NotDelivered status.");
            }
            if (invoice.DeliveryStatus != DeliveryStatus.InTransit)
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
            EnsureUserHasAccess(invoice, userId, isStaffOrManager);

            if (!isStaffOrManager)
            {
                throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot update delivery status for a cancelled invoice.");
            }
            if (invoice.Status != InvoiceStatus.Paid)
            {
                throw new InvalidOperationException("Can only change to Delivered when invoice is paid.");
            }
            if (invoice.DeliveryStatus == DeliveryStatus.Delivered)
            {
                throw new InvalidOperationException("Invoice is already in Delivered status.");
            }
            if (invoice.DeliveryStatus != DeliveryStatus.InTransit)
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

        private void ValidateUpdateInvoiceDTO(UpdateInvoiceDTO dto)
        {
            if (dto.Star.HasValue && (dto.Star.Value < 1 || dto.Star.Value > 5))
            {
                throw new ArgumentException("Rating joined must be between 1 and 5.");
            }
            if (dto.DeliveryAddress?.Trim() == "")
            {
                throw new ArgumentException("Delivery address cannot be empty.");
            }
        }

        private bool HasNonDeliveryChanges(UpdateInvoiceDTO dto)
        {
            return dto.DeliveryAddress != null
                || dto.Status.HasValue
                || dto.Feedback != null
                || dto.Star.HasValue
                || dto.Discount.HasValue;
        }

        private void UpdateInvoiceFields(Invoice invoice, UpdateInvoiceDTO dto, bool isStaffOrManager)
        {
            if (dto.DeliveryAddress != null)
            {
                invoice.DeliveryAddress = dto.DeliveryAddress;
            }
            if (dto.Status.HasValue)
            {
                if (!isStaffOrManager)
                {
                    throw new UnauthorizedAccessException("Only staff or managers can change invoice status.");
                }
                invoice.Status = dto.Status.Value;
            }
            if (dto.DeliveryStatus.HasValue)
            {
                if (!isStaffOrManager)
                {
                    throw new UnauthorizedAccessException("Only staff or managers can change delivery status.");
                }
                invoice.DeliveryStatus = dto.DeliveryStatus.Value;
            }
            if (dto.Feedback != null)
            {
                invoice.Feedback = dto.Feedback;
            }
            if (dto.Star.HasValue)
            {
                invoice.Star = dto.Star.Value;
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
    }
}