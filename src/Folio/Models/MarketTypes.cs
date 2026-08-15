using System;

namespace Folio.Models;

/// <summary>Current market price input to the engine (base currency).</summary>
public sealed record PricePoint(decimal Price, decimal Change24h);

/// <summary>A single historical price sample.</summary>
public sealed record HistoryPoint(DateTimeOffset Date, decimal Price);

/// <summary>The Crypto Fear &amp; Greed index (0 = extreme fear, 100 = extreme greed).</summary>
public sealed record FearGreed(int Value, string Label, DateTimeOffset At);
