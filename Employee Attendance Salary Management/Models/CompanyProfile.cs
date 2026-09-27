namespace Employee_Attendance_Salary_Management.Models;

public sealed class CompanyProfile
{
    public string CompanyName { get; init; } = string.Empty;
    public byte[]? Logo { get; init; }

    public string? LogoDataUrl => Logo is { Length: > 0 }
        ? $"data:{DetectContentType(Logo)};base64,{Convert.ToBase64String(Logo)}"
        : null;

    private static string DetectContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
            return "image/webp";

        var prefix = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 256)).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            ? "image/svg+xml"
            : "image/png";
    }
}
