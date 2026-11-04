namespace Erp.Seeder;

internal enum SeedSize
{
    Small,
    Full,
}

/// <summary>How much mock data one company gets.</summary>
internal sealed record SeedProfile(int BusinessPartners, int Articles)
{
    public static SeedProfile For(SeedSize size) => size switch
    {
        SeedSize.Small => new SeedProfile(BusinessPartners: 25, Articles: 40),
        SeedSize.Full => new SeedProfile(BusinessPartners: 500, Articles: 2000),
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null),
    };
}

/// <summary>
/// Command line. There is no default password: every seeded user signs in with the one given here,
/// so no shared credentials live in the repository.
/// </summary>
internal sealed record SeedOptions(SeedSize Size, string Password, bool Reset)
{
    public const string PasswordVariable = "ERP_SEED_PASSWORD";

    public const string Usage =
        "Usage: Erp.Seeder [--size small|full] --password <password> [--reset]\n" +
        $"  --password  Password for every seeded user (or set {PasswordVariable}).\n" +
        "  --reset     Drop and re-create the core and identity schemas first. Deletes all data in them.";

    public SeedProfile Profile => SeedProfile.For(Size);

    public static (SeedOptions? Options, string? Error) Parse(IReadOnlyList<string> args, string? passwordFromEnvironment)
    {
        ArgumentNullException.ThrowIfNull(args);

        var size = SeedSize.Small;
        var password = passwordFromEnvironment;
        var reset = false;
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--size" when index + 1 < args.Count:
                    switch (args[++index])
                    {
                        case "small":
                            size = SeedSize.Small;
                            break;
                        case "full":
                            size = SeedSize.Full;
                            break;
                        default:
                            return (null, $"Unknown size '{args[index]}'.");
                    }

                    break;
                case "--password" when index + 1 < args.Count:
                    password = args[++index];
                    break;
                case "--reset":
                    reset = true;
                    break;
                default:
                    return (null, $"Unknown or incomplete argument '{args[index]}'.");
            }
        }

        return string.IsNullOrEmpty(password)
            ? (null, $"A password is required: pass --password or set {PasswordVariable}.")
            : (new SeedOptions(size, password, reset), null);
    }
}
