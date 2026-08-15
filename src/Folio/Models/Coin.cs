namespace Folio.Models;

/// <summary>Catalog entry for a coin/token (sourced from the market-data provider).</summary>
public sealed record Coin(string Id, string Symbol, string Name, string? ImageUrl = null);
