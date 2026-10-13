using System;
using System.Collections.Generic;

namespace InvoiceGen.Models
{
    public class DeliveryChallan
    {
        public int Id { get; set; }
        public string ChallanNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Draft";
        public decimal TotalAmount { get; set; }

        public int? UserId { get; set; }

        // Foreign Key (nullable)
        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        // Line Items
        public ICollection<DeliveryChallanItem> Items { get; set; } = new List<DeliveryChallanItem>();

        // Complete persistent JSON state
        public string? FullDataJson { get; set; }
    }
}
