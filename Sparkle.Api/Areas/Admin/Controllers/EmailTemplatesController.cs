using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Configuration;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class EmailTemplatesController : Controller
{
    private readonly ApplicationDbContext _db;

    public EmailTemplatesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var templates = await _db.EmailTemplates
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(templates);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EmailTemplateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmailTemplateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var template = new EmailTemplate
        {
            Name = model.Name,
            Subject = model.Subject,
            Body = model.Body,
            TemplateType = model.TemplateType,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.EmailTemplates.Add(template);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Email template created successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var template = await _db.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var model = new EmailTemplateViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Subject = template.Subject,
            Body = template.Body,
            TemplateType = template.TemplateType,
            IsActive = template.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmailTemplateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var template = await _db.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        template.Name = model.Name;
        template.Subject = model.Subject;
        template.Body = model.Body;
        template.TemplateType = model.TemplateType;
        template.IsActive = model.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Email template updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _db.EmailTemplates.FindAsync(id);
        if (template != null)
        {
            _db.EmailTemplates.Remove(template);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Email template deleted successfully!";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SaveTemplate(int id, string name, string subject, string body)
    {
        var template = await _db.EmailTemplates.FindAsync(id);
        if (template == null)
        {
            template = new EmailTemplate
            {
                Name = name,
                Subject = subject,
                Body = body,
                TemplateType = EmailTemplateType.Custom,
                CreatedAt = DateTime.UtcNow
            };
            _db.EmailTemplates.Add(template);
        }
        else
        {
            template.Name = name;
            template.Subject = subject;
            template.Body = body;
            template.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Template saved successfully";
        return RedirectToAction("Index");
    }
}

public class EmailTemplateViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public EmailTemplateType TemplateType { get; set; }
    public bool IsActive { get; set; } = true;
}
