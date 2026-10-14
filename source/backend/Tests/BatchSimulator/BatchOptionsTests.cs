using PolskiStreamerSimulator.BatchSimulator;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.BatchSimulator;

public class BatchOptionsTests
{
    [Fact]
    public void Parse_defaults()
    {
        var o = BatchOptions.Parse([]).Value;
        Assert.Equal(1000, o.Careers);
        Assert.Equal(42UL, o.Seed);
        Assert.Equal(["Balanced", "Grinder", "RestHeavy", "Specialist", "Rotator"], o.Policies);
        // The nearest content/m1 may be the repository's or the identical copy WebApp places next to the test binaries.
        Assert.Equal(new JsonContentSource(TestPaths.ContentM1).Load().Digest, new JsonContentSource(o.ContentDirectory).Load().Digest);
        Assert.Null(o.OutputDirectory);
        Assert.Null(o.MaxDegreeOfParallelism);
    }

    [Fact]
    public void Parse_reads_every_flag()
    {
        var o = BatchOptions.Parse(["--careers", "12", "--seed", "18446744073709551615", "--policies", "Grinder,Rotator", "--content", "x/y", "--out", "reports", "--parallel", "2"]).Value;
        Assert.Equal(12, o.Careers);
        Assert.Equal(ulong.MaxValue, o.Seed);
        Assert.Equal(["Grinder", "Rotator"], o.Policies);
        Assert.Equal("x/y", o.ContentDirectory);
        Assert.Equal("reports", o.OutputDirectory);
        Assert.Equal(2, o.MaxDegreeOfParallelism);
    }

    [Fact]
    public void Parse_accepts_all_as_every_policy()
        => Assert.Equal(5, BatchOptions.Parse(["--policies", "all"]).Value.Policies.Count);

    [Theory]
    [InlineData("--nope")]
    [InlineData("--careers")]
    [InlineData("--careers", "many")]
    [InlineData("--careers", "0")]
    [InlineData("--seed", "-1")]
    [InlineData("--parallel", "0")]
    [InlineData("--policies", "Balanced,Gambler")]
    public void Parse_rejects_invalid_arguments(params string[] args)
    {
        var result = BatchOptions.Parse(args);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Error.Detail);
    }

    [Fact]
    public void Parse_rejects_unknown_flag() => Assert.False(BatchOptions.Parse(["--nope"]).IsSuccess);
}
