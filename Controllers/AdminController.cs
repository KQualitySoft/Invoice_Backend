using InvoiceGen.Data;
using InvoiceGen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Admin/cleanup-dummy-data
        [HttpPost("cleanup-dummy-data")]
        public async Task<IActionResult> CleanupDummyData()
        {
            // 1. Identify and verify Main Administrator
            var adminUser = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email == "admin@qualitysalespro.com" || u.Username == "admin");

            if (adminUser == null)
            {
                // Ensure Admin exists if somehow missing
                adminUser = new User
                {
                    Email = "admin@qualitysalespro.com",
                    Username = "admin",
                    FullName = "Main Administrator",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = "MainAdmin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();
            }

            // 2. Count and remove any non-admin users
            var nonAdminUsers = await _context.Users.Where(u => u.Id != adminUser.Id).ToListAsync();
            int deletedUsersCount = nonAdminUsers.Count;
            if (deletedUsersCount > 0)
            {
                _context.Users.RemoveRange(nonAdminUsers);
            }

            // 3. Count and remove approval histories
            var approvalHistories = await _context.ApprovalHistories.ToListAsync();
            int deletedApprovalsCount = approvalHistories.Count;
            if (deletedApprovalsCount > 0)
            {
                _context.ApprovalHistories.RemoveRange(approvalHistories);
            }

            // 4. Count and remove PurchasedItems
            var purchasedItems = await _context.PurchasedItems.ToListAsync();
            int deletedPurchasedItemsCount = purchasedItems.Count;
            if (deletedPurchasedItemsCount > 0)
            {
                _context.PurchasedItems.RemoveRange(purchasedItems);
            }

            // 5. Invoices and InvoiceItems
            var invoiceItems = await _context.InvoiceItems.ToListAsync();
            int deletedInvoiceItemsCount = invoiceItems.Count;
            if (deletedInvoiceItemsCount > 0)
            {
                _context.InvoiceItems.RemoveRange(invoiceItems);
            }

            var invoices = await _context.Invoices.ToListAsync();
            int deletedInvoicesCount = invoices.Count;
            if (deletedInvoicesCount > 0)
            {
                _context.Invoices.RemoveRange(invoices);
            }

            // 6. Quotations and QuotationItems
            var quotationItems = await _context.QuotationItems.ToListAsync();
            int deletedQuotationItemsCount = quotationItems.Count;
            if (deletedQuotationItemsCount > 0)
            {
                _context.QuotationItems.RemoveRange(quotationItems);
            }

            var quotations = await _context.Quotations.ToListAsync();
            int deletedQuotationsCount = quotations.Count;
            if (deletedQuotationsCount > 0)
            {
                _context.Quotations.RemoveRange(quotations);
            }

            // 7. Purchase Orders and PurchaseOrderItems
            var poItems = await _context.PurchaseOrderItems.ToListAsync();
            int deletedPoItemsCount = poItems.Count;
            if (deletedPoItemsCount > 0)
            {
                _context.PurchaseOrderItems.RemoveRange(poItems);
            }

            var pos = await _context.PurchaseOrders.ToListAsync();
            int deletedPosCount = pos.Count;
            if (deletedPosCount > 0)
            {
                _context.PurchaseOrders.RemoveRange(pos);
            }

            // 8. Delivery Challans and DeliveryChallanItems
            var dcItems = await _context.DeliveryChallanItems.ToListAsync();
            int deletedDcItemsCount = dcItems.Count;
            if (deletedDcItemsCount > 0)
            {
                _context.DeliveryChallanItems.RemoveRange(dcItems);
            }

            var dcs = await _context.DeliveryChallans.ToListAsync();
            int deletedDcsCount = dcs.Count;
            if (deletedDcsCount > 0)
            {
                _context.DeliveryChallans.RemoveRange(dcs);
            }

            // 9. Proforma Invoices and ProformaInvoiceItems
            var piItems = await _context.ProformaInvoiceItems.ToListAsync();
            int deletedPiItemsCount = piItems.Count;
            if (deletedPiItemsCount > 0)
            {
                _context.ProformaInvoiceItems.RemoveRange(piItems);
            }

            var pis = await _context.ProformaInvoices.ToListAsync();
            int deletedPisCount = pis.Count;
            if (deletedPisCount > 0)
            {
                _context.ProformaInvoices.RemoveRange(pis);
            }

            // 10. Payment Vouchers
            var pvs = await _context.PaymentVouchers.ToListAsync();
            int deletedPvsCount = pvs.Count;
            if (deletedPvsCount > 0)
            {
                _context.PaymentVouchers.RemoveRange(pvs);
            }

            // 11. Clients
            var clients = await _context.Clients.ToListAsync();
            int deletedClientsCount = clients.Count;
            if (deletedClientsCount > 0)
            {
                _context.Clients.RemoveRange(clients);
            }

            await _context.SaveChangesAsync();

            int totalDeleted = deletedUsersCount + deletedApprovalsCount + deletedPurchasedItemsCount +
                               deletedInvoiceItemsCount + deletedInvoicesCount +
                               deletedQuotationItemsCount + deletedQuotationsCount +
                               deletedPoItemsCount + deletedPosCount +
                               deletedDcItemsCount + deletedDcsCount +
                               deletedPiItemsCount + deletedPisCount +
                               deletedPvsCount + deletedClientsCount;

            return Ok(new
            {
                success = true,
                message = "All dummy/test records purged successfully while preserving the Administrator account.",
                adminPreserved = new
                {
                    id = adminUser.Id,
                    email = adminUser.Email,
                    username = adminUser.Username,
                    fullName = adminUser.FullName,
                    role = adminUser.Role
                },
                deletedCounts = new
                {
                    users = deletedUsersCount,
                    clients = deletedClientsCount,
                    invoices = deletedInvoicesCount,
                    invoiceItems = deletedInvoiceItemsCount,
                    quotations = deletedQuotationsCount,
                    quotationItems = deletedQuotationItemsCount,
                    purchaseOrders = deletedPosCount,
                    purchaseOrderItems = deletedPoItemsCount,
                    deliveryChallans = deletedDcsCount,
                    deliveryChallanItems = deletedDcItemsCount,
                    proformaInvoices = deletedPisCount,
                    proformaInvoiceItems = deletedPiItemsCount,
                    paymentVouchers = deletedPvsCount,
                    approvalHistories = deletedApprovalsCount,
                    purchasedItems = deletedPurchasedItemsCount,
                    totalRecordsDeleted = totalDeleted
                }
            });
        }
    }
}
