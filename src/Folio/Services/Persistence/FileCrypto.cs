using System;
using System.IO;
using System.Security.Cryptography;

namespace Folio.Services.Persistence;

/// <summary>
/// Password-based authenticated encryption for the data file. Key = PBKDF2(SHA-256,
/// 200k iterations) over a random salt; payload = AES-256-GCM with a random nonce and an
/// auth tag. A wrong password or any tampering fails decryption (throws), so loading an
/// encrypted file is fail-closed.
///
/// File layout: [ "FLO1" (4) | version (1) | salt (16) | nonce (12) | tag (16) | ciphertext ].
/// </summary>
public static class FileCrypto
{
    private static readonly byte[] Magic = { (byte)'F', (byte)'L', (byte)'O', (byte)'1' };
    private const byte Version = 1;
    private const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int TagLen = 16;
    private const int KeyLen = 32;
    private const int Iterations = 200_000;

    public static bool IsEncrypted(byte[] data) =>
        data.Length >= Magic.Length &&
        data[0] == Magic[0] && data[1] == Magic[1] && data[2] == Magic[2] && data[3] == Magic[3];

    public static byte[] Encrypt(byte[] plaintext, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var key = DeriveKey(password, salt);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLen];
        using (var gcm = new AesGcm(key, TagLen))
        {
            gcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using var ms = new MemoryStream();
        ms.Write(Magic, 0, Magic.Length);
        ms.WriteByte(Version);
        ms.Write(salt, 0, salt.Length);
        ms.Write(nonce, 0, nonce.Length);
        ms.Write(tag, 0, tag.Length);
        ms.Write(ciphertext, 0, ciphertext.Length);
        return ms.ToArray();
    }

    /// <summary>Decrypts a file produced by <see cref="Encrypt"/>. Throws on wrong password or tampering.</summary>
    public static byte[] Decrypt(byte[] data, string password)
    {
        if (!IsEncrypted(data))
        {
            throw new InvalidDataException("Not an encrypted Folio file.");
        }

        var offset = Magic.Length;
        offset += 1; // version

        var salt = data[offset..(offset + SaltLen)];
        offset += SaltLen;
        var nonce = data[offset..(offset + NonceLen)];
        offset += NonceLen;
        var tag = data[offset..(offset + TagLen)];
        offset += TagLen;
        var ciphertext = data[offset..];

        var key = DeriveKey(password, salt);
        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, TagLen);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext); // throws CryptographicException on auth failure
        return plaintext;
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(KeyLen);
    }
}
