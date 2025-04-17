namespace LapTrinhWindows.Repositories.InvoiceDetailRepository
{
    public interface IInvoiceDetailRepository
    {
        Task<List<InvoiceDetail>> GetAllInvoiceDetailsAsync();
        // get all invoice detail by invoice id
        Task<List<InvoiceDetail>> GetAllInvoiceDetailsByInvoiceIdAsync(int invoiceId);
        Task<InvoiceDetail?> GetInvoiceDetailByIdAsync(int id);
        Task<InvoiceDetail> CreateInvoiceDetailAsync(InvoiceDetail invoiceDetail);
        Task<InvoiceDetail> UpdateInvoiceDetailAsync(InvoiceDetail invoiceDetail);
        Task<bool> DeleteInvoiceDetailAsync(int id);
    }

    public class InvoiceDetailRepository : IInvoiceDetailRepository
    {
        private readonly ApplicationDbContext _context;

        public InvoiceDetailRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<InvoiceDetail>> GetAllInvoiceDetailsAsync()
        {
            return await _context.InvoiceDetails.ToListAsync();
        }

        public async Task<InvoiceDetail?> GetInvoiceDetailByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            return await _context.InvoiceDetails.FindAsync(id);
        }

        public async Task<InvoiceDetail> CreateInvoiceDetailAsync(InvoiceDetail invoiceDetail)
        {
            if (invoiceDetail == null) throw new ArgumentNullException(nameof(invoiceDetail));

            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.InvoiceDetails.AddAsync(invoiceDetail);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return invoiceDetail;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
        public async Task<List<InvoiceDetail>> GetAllInvoiceDetailsByInvoiceIdAsync(int invoiceID)
        {
            if (invoiceID <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(invoiceID));
            return await _context.InvoiceDetails
                .Where(x => x.InvoiceID == invoiceID)
                .ToListAsync();
        }

        public async Task<InvoiceDetail> UpdateInvoiceDetailAsync(InvoiceDetail invoiceDetail)
        {
            if (invoiceDetail == null) throw new ArgumentNullException(nameof(invoiceDetail));

            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.InvoiceDetails.Update(invoiceDetail);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return invoiceDetail;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<bool> DeleteInvoiceDetailAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));

            //begin transaction
            using (var transaction = await _context.Database
                .BeginTransactionAsync())
            {
                try
                {
                    var invoiceDetail = await GetInvoiceDetailByIdAsync(id);
                    if (invoiceDetail == null) throw new InvalidOperationException("InvoiceDetail not found.");

                    _context.InvoiceDetails.Remove(invoiceDetail);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}