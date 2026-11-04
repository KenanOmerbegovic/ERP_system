using Erp.Seeder;

namespace Erp.Seeder.Tests;

public sealed class SeedOptionsTests
{
    [Fact]
    public void Defaults_to_small_without_reset()
    {
        var (options, error) = SeedOptions.Parse(["--password", "a long password"], null);

        Assert.Null(error);
        Assert.Equal(new SeedOptions(SeedSize.Small, "a long password", Reset: false), options);
    }

    [Fact]
    public void Reads_size_and_reset()
    {
        var (options, _) = SeedOptions.Parse(["--size", "full", "--reset", "--password", "a long password"], null);

        Assert.Equal(new SeedOptions(SeedSize.Full, "a long password", Reset: true), options);
    }

    [Fact]
    public void Takes_the_password_from_the_environment()
    {
        var (options, _) = SeedOptions.Parse(["--size", "small"], "from the environment");

        Assert.Equal("from the environment", options?.Password);
    }

    [Theory]
    [InlineData]
    [InlineData("--size", "small")]
    [InlineData("--password")]
    public void A_password_is_required(params string[] args)
    {
        var (options, error) = SeedOptions.Parse(args, null);

        Assert.Null(options);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("--size", "huge", "--password", "a long password")]
    [InlineData("--sizes", "small", "--password", "a long password")]
    public void Unknown_arguments_are_rejected(params string[] args)
    {
        Assert.Null(SeedOptions.Parse(args, null).Options);
    }
}
