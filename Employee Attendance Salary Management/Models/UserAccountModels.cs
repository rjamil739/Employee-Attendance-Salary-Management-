using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed class UserAccountListItem
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public bool HasAllBranchAccess { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<Guid> BranchIds { get; init; } = [];
    public Guid? DefaultBranchId { get; init; }
}

public sealed class UserAccountFormModel
{
    public Guid? Id { get; set; }

    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Display name must be between 2 and 150 characters.")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(320)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Select a role.")]
    public string RoleCode { get; set; } = "HR_MANAGER";

    public bool HasAllBranchAccess { get; set; }
    [Required(ErrorMessage = "Select an access scope.")]
    public string AccessScope { get; set; } = "branch";
    public HashSet<Guid> BranchIds { get; set; } = [];
    public Guid? DefaultBranchId { get; set; }
    public bool IsActive { get; set; } = true;

    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must contain at least 8 characters.")]
    public string? Password { get; set; }
}

public sealed class PasswordChangeFormModel
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must contain at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm the password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record UserRoleOption(string Code, string Name);
