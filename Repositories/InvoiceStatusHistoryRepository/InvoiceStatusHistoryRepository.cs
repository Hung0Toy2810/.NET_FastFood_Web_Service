using LapTrinhWindows.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace LapTrinhWindows.Repositories.InvoiceStatusHistoryRepository
{
    public interface IInvoiceStatusHistoryRepository
    {
        Task CreateStatusHistoryAsync(InvoiceStatusHistory history);
    }

    public class InvoiceStatusHistoryRepository : IInvoiceStatusHistoryRepository
    {
        private readonly ApplicationDbContext _context;

        public InvoiceStatusHistoryRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task CreateStatusHistoryAsync(InvoiceStatusHistory history)
        {
            await _context.InvoiceStatusHistories.AddAsync(history);
            await _context.SaveChangesAsync();
        }
    }
}