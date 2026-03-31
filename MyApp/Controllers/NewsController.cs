using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;

namespace MyApp.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NewsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var list = await _context.News.AsNoTracking().OrderByDescending(n => n.Id).ToListAsync();
        return Ok(list);
    }
}
