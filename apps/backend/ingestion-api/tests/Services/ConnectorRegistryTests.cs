using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Services;
using Xunit;

namespace IngestionApi.Tests.Services;

public sealed class ConnectorRegistryTests
{
    [Fact]
    public void Resolve_ReturnsRegisteredConnector_IgnoringCase()
    {
        var registry = new ConnectorRegistry([new StubConnector("tiktok_ads")]);

        registry.Resolve("TikTok_Ads").SourceKey.Should().Be("tiktok_ads");
    }

    [Fact]
    public void Resolve_WithUnknownKey_Throws()
    {
        var registry = new ConnectorRegistry([new StubConnector("tiktok_ads")]);

        var act = () => registry.Resolve("meta_ads");

        act.Should().Throw<InvalidOperationException>().WithMessage("*meta_ads*");
    }

    [Fact]
    public void Constructor_WithDuplicateKeys_ThrowsAtStartup()
    {
        var act = () => new ConnectorRegistry([new StubConnector("tiktok_ads"), new StubConnector("tiktok_ads")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate source connector key*");
    }

    [Fact]
    public void TryResolve_WithBlankKey_ReturnsFalse()
    {
        var registry = new ConnectorRegistry([new StubConnector("tiktok_ads")]);

        registry.TryResolve("  ", out var connector).Should().BeFalse();
        connector.Should().BeNull();
    }

    private sealed class StubConnector : ISourceConnector
    {
        public StubConnector(string sourceKey)
        {
            SourceKey = sourceKey;
        }

        public string SourceKey { get; }

        public SourceCapabilities Capabilities { get; } = new()
        {
            NativeFormat = PayloadFormat.Json,
            SupportsIncremental = true,
            SupportsBreakdowns = true,
            SupportsAttributionWindows = true,
            MaxRestatementDays = 28,
        };

        public async IAsyncEnumerable<SourceRecord> ExtractAsync(
            ExtractionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
