using System;
using System.IO;
using System.Text.Json;
using Folio.Models;

namespace Folio.Services.Persistence;

/// <summary>Thrown when an encrypted data file is loaded before being unlocked.</summary>
public sealed class PortfolioLockedException : Exception
{
    public PortfolioLockedException() : base("The data file is encrypted and locked. Unlock with a password first.") { }
}

public interface IPortfolioStore
{
    bool FileExists { get; }
    bool IsEncrypted { get; }

    /// <summary>Verifies a password against the file and remembers it for load/save. Returns false if wrong.</summary>
    bool Unlock(string password);

    /// <summary>Sets the current password used for subsequent saves (null = plaintext).</summary>
    void SetPassword(string? password);

    /// <summary>Loads the workspace (plaintext, or encrypted-and-unlocked). Recovers from <c>.bak</c> if the primary is corrupt.</summary>
    Workspace Load();

    void Save(Workspace workspace);

    /// <summary>Sets a password and re-saves the workspace encrypted.</summary>
    void EnableEncryption(string password, Workspace workspace);

    /// <summary>Clears the password and re-saves the workspace as plaintext.</summary>
    void DisableEncryption(Workspace workspace);

    /// <summary>Changes the password and re-saves the workspace encrypted.</summary>
    void ChangePassword(string newPassword, Workspace workspace);
}

/// <summary>
/// Loads/saves the workspace to <c>portfolio.json</c> under the data directory, optionally
/// AES-GCM encrypted. Writes are atomic with a single backup; a corrupt primary falls back
/// to the backup on load.
/// </summary>
public sealed class PortfolioStore : IPortfolioStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _file;
    private string? _password;

    public PortfolioStore(string dataDirectory)
    {
        _file = Path.Combine(dataDirectory, "portfolio.json");
    }

    public bool FileExists => File.Exists(_file);

    public bool IsEncrypted
    {
        get
        {
            var raw = AtomicFile.TryRead(_file) ?? AtomicFile.TryRead(_file + ".bak");
            return raw != null && FileCrypto.IsEncrypted(raw);
        }
    }

    public bool Unlock(string password)
    {
        var raw = AtomicFile.TryRead(_file) ?? AtomicFile.TryRead(_file + ".bak");
        if (raw == null || !FileCrypto.IsEncrypted(raw))
        {
            _password = password; // nothing to verify (plaintext or no file)
            return true;
        }

        try
        {
            FileCrypto.Decrypt(raw, password);
            _password = password;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SetPassword(string? password) => _password = password;

    public Workspace Load()
    {
        foreach (var path in new[] { _file, _file + ".bak" })
        {
            var raw = AtomicFile.TryRead(path);
            if (raw is null || raw.Length == 0)
            {
                continue;
            }

            try
            {
                byte[] json;
                if (FileCrypto.IsEncrypted(raw))
                {
                    if (_password is null)
                    {
                        throw new PortfolioLockedException();
                    }

                    json = FileCrypto.Decrypt(raw, _password);
                }
                else
                {
                    json = raw;
                }

                var stored = JsonSerializer.Deserialize<StoredState>(json, Options);
                if (stored != null)
                {
                    return StorageMapper.ToWorkspace(StorageMigrator.Migrate(stored));
                }
            }
            catch (PortfolioLockedException)
            {
                throw;
            }
            catch
            {
                // Corrupt or undecryptable primary — fall through to the backup.
            }
        }

        return new Workspace();
    }

    public void Save(Workspace workspace)
    {
        var stored = StorageMapper.ToStored(workspace);
        var json = JsonSerializer.SerializeToUtf8Bytes(stored, Options);
        var bytes = _password is null ? json : FileCrypto.Encrypt(json, _password);
        AtomicFile.Write(_file, bytes);
    }

    // ----- Encryption controls (orchestrated by Settings in Phase 9) -----

    public void EnableEncryption(string password, Workspace workspace)
    {
        SetPassword(password);
        Save(workspace);
    }

    public void DisableEncryption(Workspace workspace)
    {
        SetPassword(null);
        Save(workspace);
    }

    public void ChangePassword(string newPassword, Workspace workspace)
    {
        SetPassword(newPassword);
        Save(workspace);
    }
}
