using System;
using System.Collections.Generic;

namespace InvoiceGen.Models
{
    public class ProformaInvoice
    {
        public int Id { get; set; }
        public string ProformaNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Draft";

        public int? UserId { get; set; }

        // Foreign Key (nullable)
        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        // Line Items
        public ICollection<ProformaInvoiceItem> Items { get; set; } = new List<ProformaInvoiceItem>();

        // Complete persistent JSON state
        public string? FullDataJson { get; set; }
    }
}
