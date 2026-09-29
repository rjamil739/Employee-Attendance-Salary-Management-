using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed class EmployeeListItem
{
    public Guid Id { get; init; }
    public string EmployeeNo { get; init; } = string.Empty;
    public string? PayrollNo { get; init; }
    public string FullName { get; init; } = string.Empty;
    public byte[]? Photo { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
    public string EmploymentStatus { get; init; } = string.Empty;
    public string EmploymentType { get; init; } = string.Empty;
    public DateOnly HireDate { get; init; }
    public Guid? BranchId { get; init; }
    public string? BranchName { get; init; }
    public string? DepartmentName { get; init; }
    public string? DesignationName { get; init; }
    public decimal? BaseSalary { get; init; }
    public string? PhotoDataUrl => ImageData.ToDataUrl(Photo);
}

public sealed class EmployeeWorkspaceData
{
    public EmployeeEditorModel Employee { get; init; } = new();
    public IReadOnlyList<EmployeeAssignmentItem> Assignments { get; init; } = [];
    public IReadOnlyList<EmployeeCompensationItem> Compensations { get; init; } = [];
    public IReadOnlyList<EmployeeBankItem> BankAccounts { get; init; } = [];
    public IReadOnlyList<EmployeeDocumentItem> Documents { get; init; } = [];
    public IReadOnlyList<EmployeeFingerprintItem> Fingerprints { get; init; } = [];
}

public sealed class EmployeeOnboardingModel
{
    public EmployeeEditorModel Profile { get; set; } = new();
    public EmployeeAssignmentForm Assignment { get; set; } = new();
    public EmployeeCompensationForm Compensation { get; set; } = new();
    public EmployeeBankForm Bank { get; set; } = new();
    public bool AddCompensation { get; set; } = true;
    public bool AddBankAccount { get; set; }
}

public sealed class EmployeeEditorModel
{
    public Guid? Id { get; set; }
    public Guid? UserAccountId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string? PayrollNo { get; set; }
    [Required, StringLength(100, MinimumLength = 2)] public string FirstName { get; set; } = string.Empty;
    [StringLength(100)] public string? LastName { get; set; }
    [StringLength(150)] public string? FatherName { get; set; }
    [StringLength(30)] public string? NationalId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    [EmailAddress, StringLength(320)] public string? Email { get; set; }
    [StringLength(30)] public string? MobilePhone { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? District { get; set; }
    [StringLength(100)] public string? Province { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [Required] public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    [Required] public string EmploymentStatus { get; set; } = "active";
    [Required] public string EmploymentType { get; set; } = "permanent";
    [Required] public string PreferredPaymentChannel { get; set; } = "cash";
    public string? PreferredBankPaymentType { get; set; }
    public byte[]? ExistingPhoto { get; set; }
}

public sealed class EmployeeAssignmentItem
{
    public Guid Id { get; init; }
    public Guid BranchId { get; init; }
    public Guid DepartmentId { get; init; }
    public Guid DesignationId { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public string DesignationName { get; init; } = string.Empty;
    public string? ManagerName { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}

public sealed class EmployeeAssignmentForm
{
    public Guid? Id { get; set; }
    [Required] public Guid? BranchId { get; set; }
    [Required] public Guid? DepartmentId { get; set; }
    [Required] public Guid? DesignationId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    [Required] public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? EffectiveTo { get; set; }
}

public sealed class EmployeeCompensationItem
{
    public Guid Id { get; init; }
    public decimal BaseSalary { get; init; }
    public string PayFrequency { get; init; } = string.Empty;
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Reason { get; init; }
}

public sealed class EmployeeCompensationForm
{
    public Guid? Id { get; set; }
    [Range(0, 9999999999999999d)] public decimal BaseSalary { get; set; }
    [Required] public string PayFrequency { get; set; } = "monthly";
    [Required] public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? EffectiveTo { get; set; }
    [StringLength(300)] public string? Reason { get; set; }
}

public sealed class EmployeeBankItem
{
    public Guid Id { get; init; }
    public string BankName { get; init; } = string.Empty;
    public string AccountTitle { get; init; } = string.Empty;
    public string? Iban { get; init; }
    public string? AccountNumber { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class EmployeeBankForm
{
    public Guid? Id { get; set; }
    [Required, StringLength(150)] public string BankName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string AccountTitle { get; set; } = string.Empty;
    [StringLength(50)] public string? Iban { get; set; }
    [StringLength(50)] public string? AccountNumber { get; set; }
    public bool IsPrimary { get; set; } = true;
}

public sealed class EmployeeDocumentItem
{
    public Guid Id { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string ObjectKey { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public DateOnly? ExpiresOn { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class EmployeeFingerprintItem
{
    public Guid Id { get; init; }
    public string FingerType { get; init; } = string.Empty;
    public string? TemplateFormat { get; init; }
    public byte[]? FingerprintImage { get; init; }
    public bool IsActive { get; init; }
    public string? ImageDataUrl => ImageData.ToDataUrl(FingerprintImage);
}

public sealed record EmployeeOption(Guid Id, string Name, string EmployeeNo);
