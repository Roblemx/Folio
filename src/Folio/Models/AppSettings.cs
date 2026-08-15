namespace Folio.Models;

/// <summary>User preferences (stored in plaintext, separate from portfolio data).</summary>
public sealed class AppSettings
{
    public string Currency { get; set; } = "USD";

    /// <summary>"Dark", "Light" or "System".</summary>
    public string Theme { get; set; } = "Dark";

    public int RefreshSeconds { get; set; } = 90;

    /// <summary>Whether the portfolio data file is encrypted.</summary>
    public bool Encrypted { get; set; }
}
