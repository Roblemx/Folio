using System;

namespace Folio.Models;

/// <summary>A named portfolio. Holdings/transactions are stored alongside it (Phase 2).</summary>
public sealed record Portfolio(
    string Id,
    string Name,
    PortfolioMode Mode,
    CostBasisMethod CostBasis,
    DateTimeOffset CreatedAt);
