using Microsoft.EntityFrameworkCore;
using LapTrinhWindows.Models;

namespace LapTrinhWindows.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies();
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeRole> EmployeeRoles { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceDetail> InvoiceDetails { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Variant> Variants { get; set; }
        public DbSet<LapTrinhWindows.Models.Attribute> Attributes { get; set; }
        public DbSet<AttributeValue> AttributeValues { get; set; }
        public DbSet<VariantAttribute> VariantAttributes { get; set; }
        public DbSet<PointRedemption> PointRedemptions { get; set; }
        public DbSet<ProductTag> ProductTags { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<InvoiceStatusHistory> InvoiceStatusHistories { get; set; }
        //ProductImages
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Batch> Batches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvoiceStatusHistory>()
                .HasOne(ish => ish.Invoice)
                .WithMany(i => i.InvoiceStatusHistories)
                .HasForeignKey(ish => ish.InvoiceID)
                .OnDelete(DeleteBehavior.Cascade);
            // EmployeeRole 1-n Employee
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.EmployeeRole)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleID)
                .OnDelete(DeleteBehavior.Restrict);

            // Employee 1-n Invoice
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Employee)
                .WithMany(e => e.Invoices)
                .HasForeignKey(i => i.CashierStaff)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer 1-n Invoice
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoice 1-n InvoiceDetail
            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(d => d.Invoice)
                .WithMany(i => i.InvoiceDetails)
                .HasForeignKey(d => d.InvoiceID)
                .OnDelete(DeleteBehavior.Cascade);

            // InvoiceDetail 1-n Variant
            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(d => d.Variant)
                .WithMany(v => v.InvoiceDetails)
                .HasForeignKey(d => d.SKU)
                .OnDelete(DeleteBehavior.Restrict);

            // Category 1-n Product
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            // Product 1-n Variant
            modelBuilder.Entity<Variant>()
                .HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductID)
                .OnDelete(DeleteBehavior.Cascade);


            // Attribute 1-n AttributeValue
            modelBuilder.Entity<AttributeValue>()
                .HasOne(av => av.Attribute)
                .WithMany(a => a.AttributeValues)
                .HasForeignKey(av => av.AttributeID)
                .OnDelete(DeleteBehavior.Cascade);

            // Variant 1-n VariantAttribute
            modelBuilder.Entity<VariantAttribute>()
                .HasOne(va => va.Variant)
                .WithMany(v => v.VariantAttributes)
                .HasForeignKey(va => va.VariantID)
                .OnDelete(DeleteBehavior.Cascade);

            // Attribute 1-n VariantAttribute
            modelBuilder.Entity<VariantAttribute>()
                .HasOne(va => va.Attribute)
                .WithMany(a => a.VariantAttributes)
                .HasForeignKey(va => va.AttributeID)
                .OnDelete(DeleteBehavior.Restrict);

            // AttributeValue 1-n VariantAttribute
            modelBuilder.Entity<VariantAttribute>()
                .HasOne(va => va.AttributeValue)
                .WithMany(av => av.VariantAttributes)
                .HasForeignKey(va => va.AttributeValueID)
                .OnDelete(DeleteBehavior.Restrict);

            // Product 1-n ProductTag
            modelBuilder.Entity<ProductTag>()
                .HasOne(pt => pt.Product)
                .WithMany(p => p.ProductTags)
                .HasForeignKey(pt => pt.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            // Tag 1-n ProductTag
            modelBuilder.Entity<ProductTag>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.ProductTags)
                .HasForeignKey(pt => pt.TagID)
                .OnDelete(DeleteBehavior.Restrict);
            // PointRedemption 1-n Variant
            modelBuilder.Entity<PointRedemption>()
                .HasOne(pr => pr.Variant)
                .WithMany(v => v.PointRedemptions)  
                .HasForeignKey(pr => pr.SKU)
                .HasPrincipalKey(v => v.SKU)         
                .OnDelete(DeleteBehavior.Restrict);

            // InvoiceDetail 1-n PointRedemption
            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(id => id.PointRedemption)
                .WithMany(p => p.InvoiceDetails) 
                .HasForeignKey(id => id.PointRedemptionID)
                .OnDelete(DeleteBehavior.SetNull);
            // Product 1-n ProductImage
            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.AdditionalImages)
                .HasForeignKey(pi => pi.ProductID)
                .OnDelete(DeleteBehavior.NoAction);
            // Batch 1-n Variant
            modelBuilder.Entity<Batch>()
                .HasOne(b => b.Variant)
                .WithMany(v => v.Batches)
                .HasForeignKey(b => b.SKU)
                .HasPrincipalKey(v => v.SKU)
                .OnDelete(DeleteBehavior.Restrict);
            // PointRedemption 1-n Batch
            modelBuilder.Entity<PointRedemption>()
                .HasOne(pr => pr.Batch)
                .WithMany(b => b.PointRedemptions)
                .HasForeignKey(pr => pr.BatchID)
                .OnDelete(DeleteBehavior.Restrict);
            // Batch 1-n InvoiceDetail
            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(id => id.Batch)
                .WithMany(b => b.InvoiceDetails)
                .HasForeignKey(id => id.BatchID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InvoiceDetail>()
                .HasOne(id => id.Variant)
                .WithMany(v => v.InvoiceDetails)
                .HasForeignKey(id => id.SKU)
                .HasPrincipalKey(v => v.SKU)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InvoiceStatusHistory>()
                .HasOne(ish => ish.Invoice)
                .WithMany(i => i.InvoiceStatusHistories)
                .HasForeignKey(ish => ish.InvoiceID)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InvoiceStatusHistory>()
                .HasOne(ish => ish.Employee)
                .WithMany(e => e.InvoiceStatusHistories)
                .HasForeignKey(ish => ish.EmployeeID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InvoiceStatusHistory>()
                .HasOne(ish => ish.Customer)
                .WithMany(c => c.InvoiceStatusHistories)
                .HasForeignKey(ish => ish.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);
            
                
            // Indexes for foreign keys and unique constraints
            modelBuilder.Entity<Employee>().HasIndex(e => e.RoleID);
            modelBuilder.Entity<Invoice>().HasIndex(i => i.CashierStaff);
            modelBuilder.Entity<Invoice>().HasIndex(i => i.CustomerID);
            modelBuilder.Entity<InvoiceDetail>().HasIndex(d => d.InvoiceID);
            modelBuilder.Entity<InvoiceDetail>().HasIndex(d => d.SKU);
            modelBuilder.Entity<Product>().HasIndex(p => p.CategoryID);
            modelBuilder.Entity<Customer>().HasIndex(c => c.PhoneNumber).IsUnique();
            modelBuilder.Entity<Employee>().HasIndex(e => e.PhoneNumber).IsUnique(); 
            modelBuilder.Entity<Employee>().HasIndex(e => e.Email).IsUnique(); 
            modelBuilder.Entity<Product>().HasIndex(p => p.ProductName).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.CategoryName).IsUnique();
            modelBuilder.Entity<Tag>().HasIndex(t => t.TagName).IsUnique();
            modelBuilder.Entity<Variant>().HasIndex(v => v.ProductID);
            modelBuilder.Entity<AttributeValue>().HasIndex(av => av.AttributeID);
            modelBuilder.Entity<VariantAttribute>().HasIndex(va => va.VariantID);
            modelBuilder.Entity<VariantAttribute>().HasIndex(va => va.AttributeID);
            modelBuilder.Entity<VariantAttribute>().HasIndex(va => va.AttributeValueID);
            modelBuilder.Entity<ProductTag>().HasIndex(pt => pt.TagID);
            modelBuilder.Entity<ProductTag>().HasIndex(pt => pt.ProductID);
            modelBuilder.Entity<InvoiceDetail>().HasIndex(id => id.PointRedemptionID);
            modelBuilder.Entity<PointRedemption>().HasIndex(pr => pr.SKU);
            modelBuilder.Entity<PointRedemption>().HasIndex(pr => new { pr.StartDate, pr.EndDate });
            modelBuilder.Entity<Customer>().HasIndex(c => c.Points);
            modelBuilder.Entity<EmployeeRole>().HasIndex(r => r.RoleName).IsUnique();
            modelBuilder.Entity<Invoice>().HasIndex(i => i.CreateAt);
            modelBuilder.Entity<ProductImage>().HasIndex(pi => pi.ProductID);
            modelBuilder.Entity<PointRedemption>().HasIndex(pr => pr.Status);
            modelBuilder.Entity<Variant>().HasIndex(v => v.SKU).IsUnique();
            modelBuilder.Entity<VariantAttribute>().HasKey(va => new { va.VariantID, va.AttributeValueID });
            modelBuilder.Entity<VariantAttribute>().HasIndex(va => new { va.VariantID, va.AttributeValueID });
            modelBuilder.Entity<Variant>()
                .HasAlternateKey(v => v.SKU)
                .HasName("AK_Variant_SKU");
            modelBuilder.Entity<InvoiceStatusHistory>().HasIndex(ish => ish.InvoiceID);
            // Default values for GUIDs
            modelBuilder.Entity<Employee>().Property(e => e.EmployeeID).HasDefaultValueSql("NEWSEQUENTIALID()");
            modelBuilder.Entity<Customer>().Property(c => c.CustomerID).HasDefaultValueSql("NEWSEQUENTIALID()");

            base.OnModelCreating(modelBuilder);
        }
    }
}