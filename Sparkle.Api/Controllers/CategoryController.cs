using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Models;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Controllers;

[Route("category")]
public class CategoryController : Controller
{
    private readonly ApplicationDbContext _db;

    public CategoryController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Index(string slug, int page = 1, string? sort = null)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug);
        if (category == null)
        {
            return NotFound();
        }

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => p.CategoryId == category.Id && p.IsActive);

        // Sorting
        query = sort switch
        {
            "price-asc" => query.OrderBy(p => p.BasePrice),
            "price-desc" => query.OrderByDescending(p => p.BasePrice),
            "name" => query.OrderBy(p => p.Title),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        const int pageSize = 20;
        var totalCount = await query.CountAsync();
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var viewModel = new CategoryViewModel
        {
            Category = category,
            Products = products,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            CurrentSort = sort,
            Highlight = CategoryHighlightProvider.GetBySlug(category.Slug)
        };

        return View(viewModel);
    }

    public class CategoryViewModel
    {
        public Category Category { get; set; } = default!;
        public List<Product> Products { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? CurrentSort { get; set; }
        public CategoryHighlight? Highlight { get; set; }
    }
}
