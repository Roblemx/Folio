using System.IO;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Folio.Services.Persistence;
using Xunit;

namespace Folio.Tests;

public class EncryptionTests
{
    [Fact]
    public void FileCrypto_RoundTrips()
    {
        var data = Encoding.UTF8.GetBytes("hello folio");
        var encrypted = FileCrypto.Encrypt(data, "pw");

        FileCrypto.IsEncrypted(encrypted).Should().BeTrue();
        FileCrypto.Decrypt(encrypted, "pw").Should().Equal(data);
    }

    [Fact]
    public void FileCrypto_WrongPassword_Throws()
    {
        var encrypted = FileCrypto.Encrypt(new byte[] { 1, 2, 3 }, "pw");

        var act = () => FileCrypto.Decrypt(encrypted, "wrong");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Store_Encrypted_RoundTripsAfterUnlock()
    {
        var dir = TestHelpers.TempDir();
        var ws = TestHelpers.SampleWorkspace();
        var store = new PortfolioStore(dir);
        store.SetPassword("secret");
        store.Save(ws);

        store.IsEncrypted.Should().BeTrue();

        var fresh = new PortfolioStore(dir);
        fresh.Unlock("secret").Should().BeTrue();
        TestHelpers.Json(fresh.Load()).Should().Be(TestHelpers.Json(ws));
    }

    [Fact]
    public void Store_Encrypted_WrongPassword_Fails()
    {
        var dir = TestHelpers.TempDir();
        var store = new PortfolioStore(dir);
        store.SetPassword("secret");
        store.Save(TestHelpers.SampleWorkspace());

        new PortfolioStore(dir).Unlock("nope").Should().BeFalse();
    }

    [Fact]
    public void Store_Encrypted_LoadWithoutUnlock_Throws()
    {
        var dir = TestHelpers.TempDir();
        var store = new PortfolioStore(dir);
        store.SetPassword("secret");
        store.Save(TestHelpers.SampleWorkspace());

        var act = () => new PortfolioStore(dir).Load();

        act.Should().Throw<PortfolioLockedException>();
    }

    [Fact]
    public void Store_Encrypted_Tamper_FailsToUnlock()
    {
        var dir = TestHelpers.TempDir();
        var store = new PortfolioStore(dir);
        store.SetPassword("secret");
        store.Save(TestHelpers.SampleWorkspace());

        var path = Path.Combine(dir, "portfolio.json");
        var bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0xFF;   // flip last ciphertext byte
        File.WriteAllBytes(path, bytes);

        new PortfolioStore(dir).Unlock("secret").Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_OldFails_NewWorks()
    {
        var dir = TestHelpers.TempDir();
        var ws = TestHelpers.SampleWorkspace();
        var store = new PortfolioStore(dir);
        store.EnableEncryption("old", ws);

        store.ChangePassword("new", ws);

        new PortfolioStore(dir).Unlock("old").Should().BeFalse();
        var fresh = new PortfolioStore(dir);
        fresh.Unlock("new").Should().BeTrue();
        TestHelpers.Json(fresh.Load()).Should().Be(TestHelpers.Json(ws));
    }

    [Fact]
    public void DisableEncryption_ProducesPlaintext()
    {
        var dir = TestHelpers.TempDir();
        var ws = TestHelpers.SampleWorkspace();
        var store = new PortfolioStore(dir);
        store.EnableEncryption("secret", ws);
        store.IsEncrypted.Should().BeTrue();

        store.DisableEncryption(ws);

        store.IsEncrypted.Should().BeFalse();
        TestHelpers.Json(new PortfolioStore(dir).Load()).Should().Be(TestHelpers.Json(ws));
    }
}
