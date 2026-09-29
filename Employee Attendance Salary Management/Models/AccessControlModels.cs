using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed class RoleItem
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ScopeType { get; init; } = "branch";
    public bool IsSystem { get; init; }
    public int UserCount { get; init; }
}

public sealed class RoleFormModel
{
    public Guid? Id { get; set; }
    [StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [Required] public string ScopeType { get; set; } = "branch";
}

public sealed class UserAccessAssignment
{
    public Guid UserId { get; set; }
    public HashSet<Guid> RoleIds { get; set; } = [];
    public HashSet<Guid> BranchIds { get; set; } = [];
    public Guid? DefaultBranchId { get; set; }
    public bool HasAllBranchAccess { get; set; }
}
