using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Ingestion.Keys;
using FluentAssertions;
using Xunit;

namespace IngestionApi.Tests.Ingestion;

public sealed class PayloadHasherTests
{
    [Fact]
    public void ComputeHash_IsStableAcrossPropertyOrdering()
    {
        var first = JsonNode.Parse("""{"spend":"41.5","ad_id":"1","impressions":900}""");
        var second = JsonNode.Parse("""{"impressions":900,"ad_id":"1","spend":"41.5"}""");

        PayloadHasher.ComputeHash(second).Should().Be(PayloadHasher.ComputeHash(first));
    }

    [Fact]
    public void ComputeHash_IsStableForNestedObjects()
    {
        var first = JsonNode.Parse("""{"a":{"y":1,"x":2},"b":[1,2,3]}""");
        var second = JsonNode.Parse("""{"b":[1,2,3],"a":{"x":2,"y":1}}""");

        PayloadHasher.ComputeHash(second).Should().Be(PayloadHasher.ComputeHash(first));
    }

    [Fact]
    public void ComputeHash_RespectsArrayOrdering()
    {
        var first = JsonNode.Parse("""{"actions":[1,2]}""");
        var second = JsonNode.Parse("""{"actions":[2,1]}""");

        PayloadHasher.ComputeHash(second).Should().NotBe(PayloadHasher.ComputeHash(first));
    }

    [Fact]
    public void ComputeHash_ChangesWhenAValueChanges()
    {
        var first = JsonNode.Parse("""{"spend":"41.5"}""");
        var second = JsonNode.Parse("""{"spend":"41.6"}""");

        PayloadHasher.ComputeHash(second).Should().NotBe(PayloadHasher.ComputeHash(first));
    }

    [Fact]
    public void ComputeHash_IsPrefixedForSelfDescription()
    {
        PayloadHasher.ComputeHash(JsonNode.Parse("""{"a":1}""")).Should().StartWith("sha256:");
    }
}
