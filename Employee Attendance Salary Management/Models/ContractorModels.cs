using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed class ContractorItem
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<Guid> ActiveBranchIds { get; init; } = [];
    public IReadOnlyList<string> ActiveBranchNames { get; init; } = [];
}

public sealed class ContractorFormModel
{
    public Guid? Id { get; set; }
    [StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = string.Empty;
    [StringLength(150)] public string? ContactPerson { get; set; }
    [Phone, StringLength(30)] public string? PhoneNumber { get; set; }
    [EmailAddress, StringLength(320)] public string? Email { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    public bool IsActive { get; set; } = true;
    public HashSet<Guid> BranchIds { get; set; } = [];
}
