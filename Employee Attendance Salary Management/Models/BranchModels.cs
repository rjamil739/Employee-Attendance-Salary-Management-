using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed class BranchListItem
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public byte[]? Logo { get; init; }
    public bool IsHeadOffice { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? CreatedByName { get; init; }
    public string? UpdatedByName { get; init; }

    public string? LogoDataUrl => ImageData.ToDataUrl(Logo);
}

public sealed class BranchFormModel
{
    public Guid? Id { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Branch name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsHeadOffice { get; set; }
    public bool IsActive { get; set; } = true;
}

internal static class ImageData
{
    public static string? ToDataUrl(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return null;

        var contentType = "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            contentType = "image/jpeg";
        else if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
            contentType = "image/webp";
        else
        {
            var prefix = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 256)).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                contentType = "image/svg+xml";
        }

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }
}
