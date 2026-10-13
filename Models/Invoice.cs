using System;
using System.Collections.Generic;

namespace InvoiceGen.Models
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Draft";

        public int? UserId { get; set; }

        // Foreign Key (nullable – invoice may exist without a client)
        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        // Line Items
        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

        // Complete persistent JSON state (bank accounts, upi, terms, notes, etc.)
        public string? FullDataJson { get; set; }
    }
}
