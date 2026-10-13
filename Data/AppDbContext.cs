using InvoiceGen.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<DeliveryChallan> DeliveryChallans { get; set; }
        public DbSet<DeliveryChallanItem> DeliveryChallanItems { get; set; }
        public DbSet<ProformaInvoice> ProformaInvoices { get; set; }
        public DbSet<ProformaInvoiceItem> ProformaInvoiceItems { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<PaymentVoucher> PaymentVouchers { get; set; }
        public DbSet<PurchasedItem> PurchasedItems { get; set; }
        public DbSet<ApprovalHistory> ApprovalHistories { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Client relationships (nullable foreign keys)
            modelBuilder.Entity<Client>()
                .HasMany(c => c.Quotations)
                .WithOne(q => q.Client)
                .HasForeignKey(q => q.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.PurchaseOrders)
                .WithOne(p => p.Client)
                .HasForeignKey(p => p.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.DeliveryChallans)
                .WithOne(d => d.Client)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.ProformaInvoices)
                .WithOne(p => p.Client)
                .HasForeignKey(p => p.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.Invoices)
                .WithOne(i => i.Client)
                .HasForeignKey(i => i.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.PaymentVouchers)
                .WithOne(p => p.Client)
                .HasForeignKey(p => p.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.PurchasedItems)
                .WithOne(p => p.Client)
                .HasForeignKey(p => p.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Document Line Item relationships
            modelBuilder.Entity<Invoice>()
                .HasMany(i => i.Items)
                .WithOne(item => item.Invoice)
                .HasForeignKey(item => item.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quotation>()
                .HasMany(q => q.Items)
                .WithOne(item => item.Quotation)
                .HasForeignKey(item => item.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseOrder>()
                .HasMany(p => p.Items)
                .WithOne(item => item.PurchaseOrder)
                .HasForeignKey(item => item.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeliveryChallan>()
                .HasMany(d => d.Items)
                .WithOne(item => item.DeliveryChallan)
                .HasForeignKey(item => item.DeliveryChallanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProformaInvoice>()
                .HasMany(p => p.Items)
                .WithOne(item => item.ProformaInvoice)
                .HasForeignKey(item => item.ProformaInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

