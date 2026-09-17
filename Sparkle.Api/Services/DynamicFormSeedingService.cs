using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sparkle.Domain.DynamicForms;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

public class DynamicFormSeedingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DynamicFormSeedingService> _logger;

    public DynamicFormSeedingService(ApplicationDbContext db, ILogger<DynamicFormSeedingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedFormsAsync()
    {
        try 
        {
            if (await _db.DynamicForms.AnyAsync(f => f.Code == "SELLER_REG_V1"))
            {
                _logger.LogInformation("Dynamic forms already seeded.");
                return;
            }

            _logger.LogInformation("Seeding Dynamic Forms...");

            // Stage 1: Create and save the Form first
            var sellerForm = new DynamicFormDefinition
            {
                Name = "Seller Registration Additional Info",
                Code = "SELLER_REG_V1",
                Description = "Additional information required for seller registration approval",
                TargetModule = "Sellers",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.DynamicForms.Add(sellerForm);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Form created with ID: {FormId}", sellerForm.Id);

            // Stage 2: Create and save Sections with explicit FormId
            var section1 = new DynamicFormSection
            {
                FormId = sellerForm.Id,
                Title = "Business Details",
                Order = 1,
                IsCollapsible = false,
                CreatedAt = DateTime.UtcNow
            };
            var section2 = new DynamicFormSection
            {
                FormId = sellerForm.Id,
                Title = "Documents",
                Order = 2,
                IsCollapsible = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Set<DynamicFormSection>().AddRange(section1, section2);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Sections created with IDs: {S1}, {S2}", section1.Id, section2.Id);

            // Stage 3: Create and save Fields with explicit FormId and SectionId
            var fields = new List<DynamicFormField>
            {
                // Section 1 Fields
                new DynamicFormField
                {
                    FormId = sellerForm.Id,
                    SectionId = section1.Id,
                    Label = "Business Type",
                    Name = "bus_type",
                    FieldType = FieldType.Dropdown,
                    IsRequired = true,
                    Order = 1,
                    WidthPercentage = 50,
                    CreatedAt = DateTime.UtcNow
                },
                new DynamicFormField
                {
                    FormId = sellerForm.Id,
                    SectionId = section1.Id,
                    Label = "Trade License Number",
                    Name = "trade_license",
                    FieldType = FieldType.TextBox,
                    IsRequired = true,
                    Placeholder = "Enter your trade license number",
                    Order = 2,
                    WidthPercentage = 50,
                    CreatedAt = DateTime.UtcNow
                },
                new DynamicFormField
                {
                    FormId = sellerForm.Id,
                    SectionId = section1.Id,
                    Label = "Establishment Date",
                    Name = "est_date",
                    FieldType = FieldType.Date,
                    IsRequired = false,
                    Order = 3,
                    WidthPercentage = 50,
                    CreatedAt = DateTime.UtcNow
                },
                new DynamicFormField
                {
                    FormId = sellerForm.Id,
                    SectionId = section1.Id,
                    Label = "Business Description",
                    Name = "bus_desc",
                    FieldType = FieldType.RichText,
                    IsRequired = false,
                    Order = 4,
                    WidthPercentage = 100,
                    CreatedAt = DateTime.UtcNow
                },
                // Section 2 Fields
                new DynamicFormField
                {
                    FormId = sellerForm.Id,
                    SectionId = section2.Id,
                    Label = "Upload Trade License",
                    Name = "doc_trade_license",
                    FieldType = FieldType.File,
                    IsRequired = true,
                    Order = 1,
                    CreatedAt = DateTime.UtcNow
                },
                new DynamicFormField
                {
                    FormId = sellerForm.Id,
                    SectionId = section2.Id,
                    Label = "TIN Certificate",
                    Name = "doc_tin",
                    FieldType = FieldType.File,
                    IsRequired = true,
                    Order = 2,
                    CreatedAt = DateTime.UtcNow
                }
            };
            _db.Set<DynamicFormField>().AddRange(fields);
            await _db.SaveChangesAsync();

            // Stage 4: Add Options for Dropdown field
            var busTypeField = fields.First(f => f.Name == "bus_type");
            var options = new List<DynamicFieldOption>
            {
                new DynamicFieldOption { FieldId = busTypeField.Id, Label = "Sole Proprietorship", Value = "sole", Order = 1, CreatedAt = DateTime.UtcNow },
                new DynamicFieldOption { FieldId = busTypeField.Id, Label = "Partnership", Value = "partner", Order = 2, CreatedAt = DateTime.UtcNow },
                new DynamicFieldOption { FieldId = busTypeField.Id, Label = "Limited Company", Value = "ltd", Order = 3, CreatedAt = DateTime.UtcNow }
            };
            _db.Set<DynamicFieldOption>().AddRange(options);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Dynamic Forms seeded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding dynamic forms");
            // Don't rethrow - allow app to start even if seeding fails
        }
    }
}
