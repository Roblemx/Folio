namespace Folio.ViewModels;

/// <summary>A transient in-app notification (e.g. a triggered price alert).</summary>
public sealed class ToastItem
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public bool IsUp { get; init; }
}
