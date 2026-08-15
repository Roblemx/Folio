using System.IO;
using System.Text.Json;
using FluentAssertions;
using Folio.Models;
using Folio.Services.Persistence;
using Xunit;

namespace Folio.Tests;

public class PersistenceTests
{
    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var dir = TestHelpers.TempDir();
        var ws = TestHelpers.SampleWorkspace();
        new PortfolioStore(dir).Save(ws);

        var loaded = new PortfolioStore(dir).Load();

        TestHelpers.Json(loaded).Should().Be(TestHelpers.Json(ws));
    }

    [Fact]
    public void Load_NoFile_ReturnsEmptyWorkspace()
    {
        new PortfolioStore(TestHelpers.TempDir()).Load().Portfolios.Should().BeEmpty();
    }

    [Fact]
    public void CorruptPrimary_RecoversFromBackup()
    {
        var dir = TestHelpers.TempDir();
        var store = new PortfolioStore(dir);
        var ws = TestHelpers.SampleWorkspace();
        store.Save(ws);   // no .bak yet
        store.Save(ws);   // creates .bak (valid previous content)

        File.WriteAllText(Path.Combine(dir, "portfolio.json"), "{ this is : not valid json ");

        var loaded = new PortfolioStore(dir).Load();
        TestHelpers.Json(loaded).Should().Be(TestHelpers.Json(ws));
    }

    [Fact]
    public void Migration_BumpsOldSchemaVersion()
    {
        var dir = TestHelpers.TempDir();
        var state = StorageMapper.ToStored(TestHelpers.SampleWorkspace());
        state.SchemaVersion = 0;   // simulate an older file
        File.WriteAllBytes(Path.Combine(dir, "portfolio.json"), JsonSerializer.SerializeToUtf8Bytes(state));

        var loaded = new PortfolioStore(dir).Load();

        loaded.Portfolios.Should().HaveCount(1);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var dir = TestHelpers.TempDir();
        new SettingsStore(dir).Save(new AppSettings { Currency = "EUR", Theme = "Light", RefreshSeconds = 30, Encrypted = true });

        var loaded = new SettingsStore(dir).Load();

        loaded.Currency.Should().Be("EUR");
        loaded.Theme.Should().Be("Light");
        loaded.RefreshSeconds.Should().Be(30);
        loaded.Encrypted.Should().BeTrue();
    }
}
