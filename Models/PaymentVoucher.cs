using System;

namespace InvoiceGen.Models
{
    public class PaymentVoucher
    {
        public int Id { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";

        public int? UserId { get; set; }

        // Foreign Key (nullable)
        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        // Complete persistent JSON state
        public string? FullDataJson { get; set; }
    }
}
