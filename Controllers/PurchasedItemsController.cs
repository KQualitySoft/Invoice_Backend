using InvoiceGen.Data;
using InvoiceGen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchasedItemsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PurchasedItemsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PurchasedItem>>> GetPurchasedItems()
        {
            return await _context.PurchasedItems.Include(p => p.Client).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<PurchasedItem>> PostPurchasedItem(PurchasedItem purchasedItem)
        {
            _context.PurchasedItems.Add(purchasedItem);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPurchasedItem), new { id = purchasedItem.Id }, purchasedItem);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PurchasedItem>> GetPurchasedItem(int id)
        {
            var purchasedItem = await _context.PurchasedItems.Include(p => p.Client).FirstOrDefaultAsync(p => p.Id == id);
            if (purchasedItem == null) return NotFound();
            return purchasedItem;
        }

        [HttpGet("client/{clientId}")]
        public async Task<ActionResult<IEnumerable<PurchasedItem>>> GetPurchasedItemsByClient(int clientId)
        {
            return await _context.PurchasedItems.Where(p => p.ClientId == clientId).ToListAsync();
        }
    }
}
