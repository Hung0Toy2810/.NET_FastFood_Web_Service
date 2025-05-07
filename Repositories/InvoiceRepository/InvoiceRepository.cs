using LapTrinhWindows.Models.dto;
using LapTrinhWindows.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LapTrinhWindows.Repositories.InvoiceRepository
{
    public interface IInvoiceRepository
    {
        Task<Invoice> CreateInvoiceAsync(Invoice invoice);
        Task<Batch?> GetRandomBatchAsync(string sku, int requiredQuantity);
        Task UpdateBatchAsync(Batch batch);
        Task UpdateCustomerAsync(Customer customer);
        Task<Invoice?> GetInvoiceByIdAsync(int invoiceId);
        Task<List<Invoice>> GetInvoicesByCustomerIdAsync(Guid? customerId);
        Task<List<Invoice>> GetInvoicesByFilterAsync(
            InvoiceStatus? status = null,
            OrderType? orderType = null,
            DateTime? startDate = null,
            DateTime? endDate = null);
        Task UpdateInvoiceAsync(Invoice invoice);
        Task<bool> DeleteInvoiceAsync(int invoiceId);
        Task<bool> InvoiceExistsAsync(int invoiceId);
        Task<List<Invoice>> GetPendingInvoicesAsync();
    }

    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly Random _random;

        public InvoiceRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _random = new Random();
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            await _context.Invoices.AddAsync(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<Batch?> GetRandomBatchAsync(string sku, int requiredQuantity)
        {
            if (string.IsNullOrEmpty(sku)) throw new ArgumentException("SKU cannot be null or empty.", nameof(sku));
            if (requiredQuantity <= 0) throw new ArgumentException("Required quantity must be greater than 0.", nameof(requiredQuantity));

            var batches = await _context.Batches
                .Where(b => b.SKU == sku && b.AvailableQuantity >= requiredQuantity)
                .ToListAsync();
            if (!batches.Any())
            {
                return null;
            }
            return batches[_random.Next(batches.Count)];
        }

        public async Task UpdateBatchAsync(Batch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            _context.Batches.Update(batch);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(int invoiceId)
        {
            if (invoiceId <= 0) throw new ArgumentException("Invoice ID must be greater than 0.", nameof(invoiceId));

            return await _context.Invoices
                .Include(i => i.InvoiceDetails)
                .Include(i => i.Customer)
                .Include(i => i.Employee)
                .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
        }

        public async Task<List<Invoice>> GetInvoicesByCustomerIdAsync(Guid? customerId)
        {
            if (customerId.HasValue)
            {
                return await _context.Invoices
                    .Where(i => i.CustomerID == customerId)
                    .Include(i => i.InvoiceDetails)
                    .OrderByDescending(i => i.CreateAt)
                    .ToListAsync();
            }
            else
            {
                return await _context.Invoices
                    .Where(i => i.CustomerID == null)
                    .Include(i => i.InvoiceDetails)
                    .OrderByDescending(i => i.CreateAt)
                    .ToListAsync();
            }
        }

        public async Task<List<Invoice>> GetInvoicesByFilterAsync(
            InvoiceStatus? status = null,
            OrderType? orderType = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = _context.Invoices
                .Include(i => i.InvoiceDetails)
                .Include(i => i.Customer)
                .Include(i => i.Employee)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            if (orderType.HasValue)
            {
                query = query.Where(i => i.OrderType == orderType.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(i => i.CreateAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(i => i.CreateAt <= endDate.Value);
            }

            return await query
                .OrderByDescending(i => i.CreateAt)
                .ToListAsync();
        }

        public async Task UpdateInvoiceAsync(Invoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            _context.Invoices.Update(invoice);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteInvoiceAsync(int invoiceId)
        {
            if (invoiceId <= 0) throw new ArgumentException("Invoice ID must be greater than 0.", nameof(invoiceId));

            var invoice = await _context.Invoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                return false;
            }

            // Soft delete by setting status to Cancelled
            invoice.Status = InvoiceStatus.Cancelled;
            _context.Invoices.Update(invoice);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> InvoiceExistsAsync(int invoiceId)
        {
            if (invoiceId <= 0) throw new ArgumentException("Invoice ID must be greater than 0.", nameof(invoiceId));

            return await _context.Invoices.AnyAsync(i => i.InvoiceID == invoiceId);
        }

        public async Task<List<Invoice>> GetPendingInvoicesAsync()
        {
            return await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Pending)
                .Include(i => i.InvoiceDetails)
                .Include(i => i.Customer)
                .Include(i => i.Employee)
                .OrderBy(i => i.CreateAt)
                .ToListAsync();
        }
    }
}