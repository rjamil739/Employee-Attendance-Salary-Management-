using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed record EntityOption(Guid Id, string Name, string Code);

public sealed class DepartmentItem
{
    public Guid Id { get; init; }
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public Guid? ParentDepartmentId { get; init; }
    public string? ParentDepartmentName { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class DepartmentFormModel
{
    public Guid? Id { get; set; }
    [Required(ErrorMessage = "Select a branch.")] public Guid? BranchId { get; set; }
    public Guid? ParentDepartmentId { get; set; }
    [StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class DesignationItem
{
    public Guid Id { get; init; }
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public Guid DepartmentId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Grade { get; init; }
    public bool IsActive { get; init; }
}

public sealed class DesignationFormModel
{
    public Guid? Id { get; set; }
    [Required(ErrorMessage = "Select a branch.")] public Guid? BranchId { get; set; }
    [Required(ErrorMessage = "Select a department.")] public Guid? DepartmentId { get; set; }
    [StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = string.Empty;
    [StringLength(50)] public string? Grade { get; set; }
    public bool IsActive { get; set; } = true;
}
