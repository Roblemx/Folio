using System.IO;

namespace Folio.Services.Persistence;

/// <summary>
/// Crash-safe file writes: write to a temp file, back up the current file, then atomically
/// replace. A single <c>.bak</c> of the previous version is kept for recovery.
/// </summary>
public static class AtomicFile
{
    public static void Write(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);

        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
            File.Replace(tmp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    public static byte[]? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
