using System;

namespace InvoiceGen.Models
{
    public class ApprovalHistory
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty; // "Invoice", "Quotation", "PurchaseOrder", "DeliveryChallan", "ProformaInvoice", "PaymentVoucher"
        public string DocumentNumber { get; set; } = string.Empty;
        public int? DocumentId { get; set; }
        public string Action { get; set; } = string.Empty; // "Created", "Submitted", "Approved", "Rejected", "Resubmitted"
        public string UserEmail { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
