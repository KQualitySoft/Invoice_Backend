using System;
using System.Collections.Generic;

namespace InvoiceGen.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Gstin { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Postal { get; set; }
        public string? Country { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? UserId { get; set; }

        // Navigation Properties for related documents
        public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public ICollection<DeliveryChallan> DeliveryChallans { get; set; } = new List<DeliveryChallan>();
        public ICollection<ProformaInvoice> ProformaInvoices { get; set; } = new List<ProformaInvoice>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public ICollection<PaymentVoucher> PaymentVouchers { get; set; } = new List<PaymentVoucher>();
        public ICollection<PurchasedItem> PurchasedItems { get; set; } = new List<PurchasedItem>();
    }
}
