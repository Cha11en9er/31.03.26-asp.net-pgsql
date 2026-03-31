using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;

namespace MyApp.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var products = await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.PriceRub
            })
            .ToListAsync();

        return Ok(products);
    }
}

