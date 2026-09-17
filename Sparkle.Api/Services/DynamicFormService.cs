using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.DynamicForms;
using Sparkle.Infrastructure;
using System.Text.Json;

namespace Sparkle.Api.Services;

public interface IDynamicFormService
{
    Task<DynamicFormDefinition?> GetFormDefinitionAsync(string formCode);
    Task<DynamicFormEntry> SaveFormSubmissionAsync(string formCode, string referenceId, string referenceType, string userId, Dictionary<string, object> data);
    Task<IEnumerable<DynamicFormEntry>> GetFormEntriesAsync(string formCode, string? referenceType = null, string? referenceId = null);
}

public class DynamicFormService : IDynamicFormService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DynamicFormService> _logger;

    public DynamicFormService(ApplicationDbContext db, ILogger<DynamicFormService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DynamicFormDefinition?> GetFormDefinitionAsync(string formCode)
    {
        return await _db.DynamicForms
            .Include(f => f.Sections)
            .Include(f => f.Fields)
                .ThenInclude(field => field.Options)
            .Include(f => f.Fields)
                .ThenInclude(field => field.Dependencies)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == formCode && f.IsActive);
    }

    public async Task<DynamicFormEntry> SaveFormSubmissionAsync(string formCode, string referenceId, string referenceType, string userId, Dictionary<string, object> data)
    {
        var form = await _db.DynamicForms
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.Code == formCode);

        if (form == null)
            throw new ArgumentException($"Form definition '{formCode}' not found.");

        var entry = new DynamicFormEntry
        {
            FormId = form.Id,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            SubmittedBy = userId,
            SubmittedAt = DateTime.UtcNow
        };

        foreach (var field in form.Fields)
        {
            if (data.ContainsKey(field.Name))
            {
                var val = data[field.Name];
                string? stringValue = null;

                if (val is JsonElement jsonElement)
                {
                    stringValue = jsonElement.ToString();
                }
                else
                {
                    stringValue = val?.ToString();
                }

                entry.Values.Add(new DynamicFormValue
                {
                    FieldId = field.Id,
                    Value = stringValue
                });
            }
            else if (field.IsRequired && string.IsNullOrEmpty(field.DefaultValue))
            {
                // Simple validation - could be expanded
               _logger.LogWarning($"Required field '{field.Name}' missing for form '{formCode}'");
            }
        }

        _db.DynamicFormEntries.Add(entry);
        
        // Log activity
        _db.DynamicFormActivityLogs.Add(new DynamicFormActivityLog 
        {
            UserId = userId,
            Action = "Submit",
            TargetEntity = "DynamicFormEntry",
            Details = $"Submitted form {formCode} for Ref {referenceType}:{referenceId}"
        });

        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<IEnumerable<DynamicFormEntry>> GetFormEntriesAsync(string formCode, string? referenceType = null, string? referenceId = null)
    {
        var query = _db.DynamicFormEntries
            .Include(e => e.Values)
                .ThenInclude(v => v.Field)
            .Where(e => e.Form.Code == formCode);

        if (!string.IsNullOrEmpty(referenceType))
            query = query.Where(e => e.ReferenceType == referenceType);

        if (!string.IsNullOrEmpty(referenceId))
            query = query.Where(e => e.ReferenceId == referenceId);

        return await query.ToListAsync();
    }
}
