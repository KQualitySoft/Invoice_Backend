using System;

namespace InvoiceGen.Models
{
    public class PurchasedItem
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

        // Foreign Key
        public int ClientId { get; set; }
        public Client? Client { get; set; }
    }
}
