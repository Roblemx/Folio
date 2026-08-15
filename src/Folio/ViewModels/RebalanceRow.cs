using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Folio.ViewModels;

/// <summary>A rebalancing row with an editable target weight; derived columns update in place.</summary>
public sealed partial class RebalanceRow : ObservableObject
{
    public required string CoinId { get; init; }
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required string Initial { get; init; }
    public required Brush Accent { get; init; }
    public string? ImageUrl { get; init; }

    /// <summary>Invoked when the user edits the target (wired by the view model).</summary>
    public Action? TargetChanged { get; set; }

    [ObservableProperty] private string _currentText = string.Empty;
    [ObservableProperty] private string _targetText = string.Empty;
    [ObservableProperty] private string _driftText = string.Empty;
    [ObservableProperty] private bool _underweight;
    [ObservableProperty] private string _actionText = string.Empty;
    [ObservableProperty] private bool _isBuy;
    [ObservableProperty] private bool _isOnTarget;

    partial void OnTargetTextChanged(string value) => TargetChanged?.Invoke();
}
