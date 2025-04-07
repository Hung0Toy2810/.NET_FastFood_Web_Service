namespace LapTrinhWindows.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<EmployeeRole> EmployeeRoles { get; set; } = null!;
        public DbSet<GiftPromotion> GiftPromotions { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<InvoiceDetail> InvoiceDetails { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Employeerole 1-n Employee
            modelBuilder.Entity<Employee>().HasOne(e => e.EmployeeRole).WithMany(r => r.Employees).HasForeignKey(e => e.RoleID).OnDelete(DeleteBehavior.Restrict);
            // Employee 1-n Invoice
            modelBuilder.Entity<Invoice>().HasOne(i => i.Employee).WithMany(e => e.Invoices).HasForeignKey(i => i.EmployeeID).OnDelete(DeleteBehavior.Restrict);
            // Customer 1-n Invoice
            modelBuilder.Entity<Invoice>().HasOne(i => i.Customer).WithMany(c => c.Invoices).HasForeignKey(i => i.CustomerID).OnDelete(DeleteBehavior.Restrict);
            // Invoice 1-n InvoiceDetail
            modelBuilder.Entity<InvoiceDetail>().HasOne(d => d.Invoice).WithMany(i => i.InvoiceDetails).HasForeignKey(d => d.InvoiceID).OnDelete(DeleteBehavior.Cascade);
            // InvoiceDetail 1-n Product
            modelBuilder.Entity<InvoiceDetail>().HasOne(d => d.Product).WithMany(p => p.InvoiceDetails).HasForeignKey(d => d.ProductID).OnDelete(DeleteBehavior.Restrict);
            // GiftPromotion 1-n Customer
            modelBuilder.Entity<GiftPromotion>().HasOne(g => g.Customer).WithMany(c => c.GiftPromotions).HasForeignKey(g => g.CustomerID).OnDelete(DeleteBehavior.Cascade);
            //GiftPromotion 1-n Product
            modelBuilder.Entity<GiftPromotion>().HasOne(g => g.Product).WithMany(p => p.GiftPromotions).HasForeignKey(g => g.ProductID).OnDelete(DeleteBehavior.Restrict);
            //category 1-n product
            modelBuilder.Entity<Product>().HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryID).OnDelete(DeleteBehavior.Restrict);

            // index for all foreign keys
            modelBuilder.Entity<Employee>().HasIndex(e => e.RoleID);
            modelBuilder.Entity<Invoice>().HasIndex(i => i.EmployeeID);
            modelBuilder.Entity<Invoice>().HasIndex(i => i.CustomerID);
            modelBuilder.Entity<InvoiceDetail>().HasIndex(d => d.InvoiceID);
            modelBuilder.Entity<InvoiceDetail>().HasIndex(d => d.ProductID);
            modelBuilder.Entity<GiftPromotion>().HasIndex(g => g.CustomerID);
            modelBuilder.Entity<GiftPromotion>().HasIndex(g => g.ProductID);
            modelBuilder.Entity<Product>().HasIndex(p => p.CategoryID);
            // Unique index customer phonenumber
            modelBuilder.Entity<Customer>().HasIndex(c => c.PhoneNumber).IsUnique();
            // Unique index emlpoyee phonenumber
            modelBuilder.Entity<Employee>().HasIndex(e => e.PhoneNumber).IsUnique();
            // Unique index product productname
            modelBuilder.Entity<Product>().HasIndex(p => p.ProductName).IsUnique();
            // Unique index category categoryname
            modelBuilder.Entity<Category>().HasIndex(c => c.CategoryName).IsUnique();

            modelBuilder.Entity<Employee>()
            .Property(e => e.EmployeeID)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Customer>()
            .Property(c => c.CustomerID)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

            base.OnModelCreating(modelBuilder);
        }
    }
}