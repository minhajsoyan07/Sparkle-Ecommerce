using System;
using System.Collections.Generic;
using Sparkle.Domain.Common;

namespace Sparkle.Domain.DynamicForms;

// 1. FORM_MASTER
public class DynamicFormDefinition : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!; // Unique Key (e.g. "PRODUCT_CREATE")
    public string? Description { get; set; }
    public string TargetModule { get; set; } = default!; // e.g. "Catalog", "Identity"
    public bool IsActive { get; set; } = true;

    public ICollection<DynamicFormSection> Sections { get; set; } = new List<DynamicFormSection>();
    public ICollection<DynamicFormField> Fields { get; set; } = new List<DynamicFormField>();
}

// 7. FORM_SECTION_MASTER
public class DynamicFormSection : BaseEntity
{
    public int FormId { get; set; }
    public DynamicFormDefinition Form { get; set; } = default!;

    public string Title { get; set; } = default!;
    public int Order { get; set; }
    public bool IsCollapsible { get; set; }

    public ICollection<DynamicFormField> Fields { get; set; } = new List<DynamicFormField>();
}

// 2. FORM_FIELD_MASTER & 8. UI_RENDERING_RULES
public class DynamicFormField : BaseEntity
{
    public int FormId { get; set; }
    public DynamicFormDefinition Form { get; set; } = default!;

    public int? SectionId { get; set; }
    public DynamicFormSection? Section { get; set; }

    public string Label { get; set; } = default!;
    public string Name { get; set; } = default!; // Unique per form
    public FieldType FieldType { get; set; }
    
    public string? Placeholder { get; set; }
    public string? DefaultValue { get; set; }
    
    // Validation
    public bool IsRequired { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? ValidationRegex { get; set; }
    
    // Behavior
    public int Order { get; set; }
    public bool IsSearchable { get; set; }
    public FieldVisibility Visibility { get; set; } = FieldVisibility.All;

    // UI Rendering Rules
    public string? CssClass { get; set; }
    public int? WidthPercentage { get; set; }
    public string? Tooltip { get; set; }
    
    public ICollection<DynamicFieldOption> Options { get; set; } = new List<DynamicFieldOption>();
    public ICollection<DynamicFieldDependency> Dependencies { get; set; } = new List<DynamicFieldDependency>();
}

public enum FieldType
{
    TextBox,
    TextArea,
    Number,
    Dropdown,
    Checkbox,
    Radio,
    Date,
    Time,
    Email,
    File,
    Image,
    RichText
}

public enum FieldVisibility
{
    All,
    Admin,
    Seller,
    User
}

// 3. FORM_FIELD_OPTIONS
public class DynamicFieldOption : BaseEntity
{
    public int FieldId { get; set; }
    public DynamicFormField Field { get; set; } = default!;

    public string Label { get; set; } = default!;
    public string Value { get; set; } = default!;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

// 6. FIELD_DEPENDENCIES
public class DynamicFieldDependency : BaseEntity
{
    public int FieldId { get; set; } // The child/dependent field
    public DynamicFormField Field { get; set; } = default!;

    public int DependsOnFieldId { get; set; } // The parent field
    public DynamicFormField DependsOnField { get; set; } = default!;

    public string ExpectedValue { get; set; } = default!;
    public DependencyAction Action { get; set; }
}

public enum DependencyAction
{
    Show,
    Hide,
    Enable,
    Disable
}

// 4. FORM_DATA
public class DynamicFormEntry : BaseEntity
{
    public int FormId { get; set; }
    public DynamicFormDefinition Form { get; set; } = default!;

    public string ReferenceId { get; set; } = default!; // e.g., ProductId
    public string ReferenceType { get; set; } = default!; // e.g., "Product"
    
    public string? SubmittedBy { get; set; } // UserId
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DynamicFormValue> Values { get; set; } = new List<DynamicFormValue>();
}

// 5. FORM_DATA_FIELD_VALUES
public class DynamicFormValue : BaseEntity
{
    public int EntryId { get; set; }
    public DynamicFormEntry Entry { get; set; } = default!;

    public int FieldId { get; set; }
    public DynamicFormField Field { get; set; } = default!;

    public string? Value { get; set; }
}

// 10. ACTIVITY_LOG
public class DynamicFormActivityLog : BaseEntity
{
    public string? UserId { get; set; }
    public string Action { get; set; } = default!; // Create, Update, Delete
    public string TargetEntity { get; set; } = default!;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
