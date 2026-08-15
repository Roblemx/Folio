using System;
using System.IO;

namespace Folio.Services.Persistence;

/// <summary>Resolves the local data directory (%AppData%/Folio).</summary>
public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Folio");
}
