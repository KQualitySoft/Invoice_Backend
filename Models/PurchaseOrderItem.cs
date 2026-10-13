using System;
using System.Text.Json.Serialization;

namespace InvoiceGen.Models
{
    public class PurchaseOrderItem
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 0;
        public decimal TaxRate { get; set; } = 0;
        public decimal TaxAmount { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public decimal TotalAmount { get; set; } = 0;

        // Foreign Key
        public int PurchaseOrderId { get; set; }
        [JsonIgnore]
        public PurchaseOrder? PurchaseOrder { get; set; }
    }
}
