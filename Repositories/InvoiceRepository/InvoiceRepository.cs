namespace LapTrinhWindows.Repositories.InvoiceRepository
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetInvoiceByIdAsync(int invoiceId);
        Task<List<Invoice>> GetAllInvoicesAsync(long offset = 0, long limit = 100);
        Task<bool> AddInvoiceAsync(Invoice invoice);
        Task<bool> UpdateInvoiceAsync(Invoice invoice);
        Task<bool> DeleteInvoiceAsync(int invoiceId);
        // get invoice by customer id
        Task<List<Invoice>> GetInvoicesByCustomerIdAsync(Guid customerId);
        // get invoice by employee id
        Task<List<Invoice>> GetInvoicesByEmployeeIdAsync(Guid employeeId);
        // get invoice by customerid if where InvoiceStatus = InvoiceStatus(input)
        Task<List<Invoice>> GetInvoicesByCustomerIdAndStatusAsync(Guid customerId, InvoiceStatus status);
        // get invoice by employeeid if where InvoiceStatus = InvoiceStatus(input)
        Task<List<Invoice>> GetInvoicesByEmployeeIdAndStatusAsync(Guid employeeId, InvoiceStatus status);
        // get invoice by employeeid if where InvoiceStatus = InvoiceStatus(input) and DeliveryStatus = DeliveryStatus(input)
        Task<List<Invoice>> GetInvoicesByEmployeeIdAndStatusAndDeliveryStatusAsync(Guid employeeId, InvoiceStatus status, DeliveryStatus deliveryStatus);
        // get invoice by customerid if where InvoiceStatus = InvoiceStatus(input) and DeliveryStatus = DeliveryStatus(input)
        Task<List<Invoice>> GetInvoicesByCustomerIdAndStatusAndDeliveryStatusAsync(Guid customerId, InvoiceStatus status, DeliveryStatus deliveryStatus);
    }

    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly ApplicationDbContext _context;

        public InvoiceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);

            return invoice ?? null;
        }

        public async Task<List<Invoice>> GetAllInvoicesAsync(long offset = 0, long limit = 100)
        {
            if (offset < 0) throw new ArgumentException("Offset must be greater than or equal to 0.", nameof(offset));
            if (limit <= 0) throw new ArgumentException("Limit must be greater than 0.", nameof(limit));

            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Skip((int)offset)
                .Take((int)limit)
                .ToListAsync();
        }
        public async Task<bool> AddInvoiceAsync(Invoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.Invoices.AddAsync(invoice);
                    var result = await _context.SaveChangesAsync() > 0;
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<bool> UpdateInvoiceAsync(Invoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Invoices.Update(invoice);
                    var result = await _context.SaveChangesAsync() > 0;
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<bool> DeleteInvoiceAsync(int invoiceId)
        {
            if (invoiceId <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(invoiceId));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var invoice = await GetInvoiceByIdAsync(invoiceId);
                    if (invoice == null) throw new InvalidOperationException("Invoice not found.");

                    _context.Invoices.Remove(invoice);
                    var result = await _context.SaveChangesAsync() > 0;
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
        public async Task<List<Invoice>> GetInvoicesByCustomerIdAsync(Guid customerId)
        {
            if (customerId == Guid.Empty) throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Where(i => i.CustomerID == customerId)
                .ToListAsync();
        }
        public async Task<List<Invoice>> GetInvoicesByEmployeeIdAsync(Guid employeeId)
        {
            if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));
            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Where(i => i.EmployeeID == employeeId)
                .ToListAsync();
        }
        public async Task<List<Invoice>> GetInvoicesByCustomerIdAndStatusAsync(Guid customerId, InvoiceStatus status)
        {
            if (customerId == Guid.Empty) 
                throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));

            // Convert enum to int since it's stored as number in DB
            int statusValue = (int)status;

            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Where(i => i.CustomerID == customerId && (int)i.Status == statusValue)
                .ToListAsync();
        }
        public async Task<List<Invoice>> GetInvoicesByEmployeeIdAndStatusAsync(Guid employeeId, InvoiceStatus status)
        {
            if (employeeId == Guid.Empty) 
                throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));

            // Convert enum to int since it's stored as number in DB
            int statusValue = (int)status;

            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Where(i => i.EmployeeID == employeeId && (int)i.Status == statusValue)
                .ToListAsync();
        }
        public async Task<List<Invoice>> GetInvoicesByEmployeeIdAndStatusAndDeliveryStatusAsync(Guid employeeId, InvoiceStatus status, DeliveryStatus deliveryStatus)
        {
            if (employeeId == Guid.Empty) 
                throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));

            // Convert enum to int since it's stored as number in DB
            int statusValue = (int)status;
            int deliveryStatusValue = (int)deliveryStatus;

            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Where(i => i.EmployeeID == employeeId && (int)i.Status == statusValue && (int)i.DeliveryStatus == deliveryStatusValue)
                .ToListAsync();
        }
        public async Task<List<Invoice>> GetInvoicesByCustomerIdAndStatusAndDeliveryStatusAsync(Guid customerId, InvoiceStatus status, DeliveryStatus deliveryStatus)
        {
            if (customerId == Guid.Empty) 
                throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));

            // Convert enum to int since it's stored as number in DB
            int statusValue = (int)status;
            int deliveryStatusValue = (int)deliveryStatus;

            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .ThenInclude(id => id.Product)
                .Where(i => i.CustomerID == customerId && (int)i.Status == statusValue && (int)i.DeliveryStatus == deliveryStatusValue)
                .ToListAsync();
        }
    }
}