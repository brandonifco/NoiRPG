using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Randomness;

namespace Brp.Cli;

/// <summary>
/// The <c>roll</c> command: resolve one skill or action roll and print its whole derivation.
/// <para>
/// The command line is parsed by hand rather than by a parser library. The output of this tool
/// is its contract -- it is meant to be pasted into an Issue and diffed -- so the fewer moving
/// parts between the arguments and the rendering, the better, and a parser package would bring
/// its own error text and its own help layout into that contract.
/// </para>
/// </summary>
internal static class RollCommand
{
    private static readonly HashSet<string> KnownOptions =
        ["--skill", "--base-chance", "--seed", "--difficulty", "--modifier", "--permanent-modifier"];

    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Any(Program.IsHelpFlag))
        {
            output.Write(Usage);
            return ExitCode.Ok;
        }

        if (!TryParse(args, out var options, out var message))
        {
            error.Write($"brp roll: {message}\n\n");
            error.Write(Usage);
            return ExitCode.UsageError;
        }

        var chain = ModifierPipeline.Evaluate(options.Skill, options.Modifiers);

        // One generator, seeded from the command line and drawn from exactly once. ADR 0003
        // makes the save-scumming policy a configuration choice the game has yet to pick, so
        // this is not a claim about how a scene is seeded -- only that the same seed and the
        // same call sequence produce the same roll, which is the invariant the ADR does settle.
        var entropy = new Xoshiro256StarStar(options.Seed);

        // Resolved through ModifierChain.Resolve, passing the printed base chance explicitly:
        // the chain starts from the character's rating, while the 5% floor keys on the skill's
        // printed base chance (Ch 5: System, "Skill Rolls"), and those are two different numbers
        // -- which is what --base-chance is for (#27). A gate short-circuits the chain to null
        // without drawing; no option on this command produces a GateModifier, so a null here means
        // an impossible state, and the throw makes it fail loudly rather than render a blank report.
        var outcome = chain.Resolve(options.BaseChance, entropy)
            ?? throw new InvalidOperationException(
                "The chain was gated, but the roll command exposes no gate modifiers.");

        output.Write(RollReport.Render(options.Seed, chain, outcome));
        return ExitCode.Ok;
    }

    private static bool TryParse(
        IReadOnlyList<string> args,
        [NotNullWhen(true)] out RollOptions? options,
        [NotNullWhen(false)] out string? error)
    {
        options = null;
        error = null;

        int? skill = null;
        int? baseChance = null;
        ulong? seed = null;
        DifficultyModifier? difficulty = null;
        var seenDifficulty = false;
        var modifiers = new List<Modifier>();

        for (var i = 0; i < args.Count; i++)
        {
            var (name, inlineValue) = SplitOption(args[i]);

            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"unexpected argument '{args[i]}'.";
                return false;
            }

            if (!KnownOptions.Contains(name))
            {
                // Checked before the value is consumed, so a misspelled option reports itself
                // rather than the missing value that consuming the next argument would produce.
                error = $"unknown option '{name}'.";
                return false;
            }

            // A modifier's value legitimately begins with '-' ("-20 firing-into-combat"), so the
            // next argument is taken as the value whatever it looks like. Guessing from a
            // leading dash would reject the command line this tool exists to serve.
            string? value = inlineValue;
            if (value is null)
            {
                if (i + 1 >= args.Count)
                {
                    error = $"option '{name}' needs a value.";
                    return false;
                }

                value = args[++i];
            }

            switch (name)
            {
                case "--skill":
                    if (skill is not null)
                    {
                        error = "'--skill' was given more than once.";
                        return false;
                    }

                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
                    {
                        error = $"'--skill' expects a whole number, got '{value}'.";
                        return false;
                    }

                    if (rating < 0)
                    {
                        // Percent floors at zero. Flooring silently here would print a base
                        // rating the caller never typed, which is exactly the kind of invisible
                        // step this command exists to prevent.
                        error = $"'--skill' cannot be negative, got {rating}.";
                        return false;
                    }

                    skill = rating;
                    break;

                case "--base-chance":
                    if (baseChance is not null)
                    {
                        error = "'--base-chance' was given more than once.";
                        return false;
                    }

                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var printed))
                    {
                        error = $"'--base-chance' expects a whole number, got '{value}'.";
                        return false;
                    }

                    if (printed < 0)
                    {
                        error = $"'--base-chance' cannot be negative, got {printed}.";
                        return false;
                    }

                    baseChance = printed;
                    break;

                case "--seed":
                    if (seed is not null)
                    {
                        error = "'--seed' was given more than once.";
                        return false;
                    }

                    if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed))
                    {
                        error = $"'--seed' expects a whole number from 0 to {ulong.MaxValue}, got '{value}'.";
                        return false;
                    }

                    seed = parsedSeed;
                    break;

                case "--difficulty":
                    if (seenDifficulty)
                    {
                        error = "'--difficulty' was given more than once.";
                        return false;
                    }

                    seenDifficulty = true;
                    switch (value.ToLowerInvariant())
                    {
                        case "normal":
                            difficulty = null;
                            break;
                        case "easy":
                            difficulty = DifficultyModifier.Easy("easy");
                            break;
                        case "difficult":
                            difficulty = DifficultyModifier.Difficult("difficult");
                            break;
                        default:
                            error = $"'--difficulty' expects easy, normal, or difficult, got '{value}'.";
                            return false;
                    }

                    break;

                case "--modifier":
                case "--permanent-modifier":
                    var kind = name == "--modifier" ? AdditiveKind.Situational : AdditiveKind.Permanent;
                    if (!TryParseModifier(value, kind, out var modifier, out error))
                    {
                        return false;
                    }

                    modifiers.Add(modifier);
                    break;

                default:
                    throw new InvalidOperationException($"'{name}' is listed in {nameof(KnownOptions)} but not handled.");
            }
        }

        if (skill is null)
        {
            error = "'--skill' is required.";
            return false;
        }

        if (seed is null)
        {
            // No default seed, and no clock-derived one. Every roll in this engine is seeded
            // (AGENTS.md invariant 5); a tool that quietly invented a seed would be the one
            // place in the system where a result could not be reproduced from what was typed.
            error = "'--seed' is required — pick any number, and the same one always rolls the same result.";
            return false;
        }

        if (difficulty is not null)
        {
            modifiers.Add(difficulty);
        }

        options = new RollOptions
        {
            Skill = Percent.Of(skill.Value),

            // Defaulting the printed base chance to the character's rating is right whenever the
            // skill's printed base is 5% or higher, because the floor rule only asks which side
            // of 5% that base falls on. It is wrong for the handful of in-scope skills printed
            // at 01% -- Science, Strategy, Martial Arts -- where a trained character's rating is
            // above the floor and the skill's base chance is not. Hence the option.
            BaseChance = Percent.Of(baseChance ?? skill.Value),
            Seed = seed.Value,
            Modifiers = modifiers,
        };
        return true;
    }

    /// <summary>
    /// Splits <c>--name=value</c> into its parts. Returns a null value for the <c>--name value</c>
    /// form, which the caller then reads from the next argument.
    /// </summary>
    private static (string Name, string? Value) SplitOption(string argument)
    {
        var equals = argument.IndexOf('=', StringComparison.Ordinal);
        return equals < 0
            ? (argument, null)
            : (argument[..equals], argument[(equals + 1)..]);
    }

    private static bool TryParseModifier(
        string raw,
        AdditiveKind kind,
        [NotNullWhen(true)] out AdditiveModifier? modifier,
        [NotNullWhen(false)] out string? error)
    {
        modifier = null;
        error = null;

        var parts = raw.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "a modifier cannot be empty; expected \"<+/-n> <source label>\".";
            return false;
        }

        var deltaText = parts[0].TrimEnd('%');
        if (!int.TryParse(deltaText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var delta))
        {
            error = $"'{parts[0]}' is not a percentage adjustment; expected \"<+/-n> <source label>\".";
            return false;
        }

        var label = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        if (label.Length == 0)
        {
            // Every modifier carries a source label because that is the whole point of the
            // command: an anonymous number in the chain cannot be traced back to the table it
            // came from.
            error = $"the modifier '{raw.Trim()}' needs a source label, e.g. \"-20 firing-into-combat\".";
            return false;
        }

        modifier = new AdditiveModifier(label, delta, kind);
        return true;
    }

    internal const string Usage =
        """
        brp roll — resolve one skill or action roll, showing every step.

        usage:
          brp roll --skill <n> --seed <n> [options]

        required:
          --skill <n>            The character's rating in the skill, as a
                                 percentage.
          --seed <n>             Any whole number. The same seed always produces
                                 the same roll, so a result can be reproduced
                                 from the command line that made it.

        options:
          --base-chance <n>      The skill's printed base chance — what an
                                 untrained character rolls against. Only the
                                 5% floor reads it. Defaults to --skill, which
                                 is right for every skill printed at 5% or
                                 above; pass it for the ones printed at 01%,
                                 such as Science, Strategy, or Martial Arts.
          --difficulty <grade>   easy, normal, or difficult. Default: normal.
                                 Doubles the rating or halves it, rounding up.
          --modifier "<n> <label>"
                                 A flat adjustment describing the moment, e.g.
                                 "-20 firing-into-combat". Applied after the
                                 difficulty grade, so its stated weight is not
                                 itself doubled or halved. Repeatable.
          --permanent-modifier "<n> <label>"
                                 A flat adjustment that is part of the rating
                                 itself, e.g. "+10 specialist-training". Applied
                                 before the difficulty grade. Repeatable.
          --help                 This text.

        example:
          brp roll --skill 65 --difficulty difficult \
                   --modifier "-20 firing-into-combat" --seed 42

        """;
}

/// <summary>A parsed, validated <c>roll</c> command line.</summary>
internal sealed class RollOptions
{
    /// <summary>The character's rating in the skill. The modifier chain starts from this.</summary>
    public required Percent Skill { get; init; }

    /// <summary>
    /// The skill's printed base chance -- what an untrained character rolls against. Read by
    /// nothing except the 5% floor (Ch 5: System, "Skill Rolls"). Defaults to <see cref="Skill"/>.
    /// </summary>
    public required Percent BaseChance { get; init; }

    /// <summary>The seed for the single draw this command makes.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Every modifier named on the command line, in the order given.</summary>
    public required IReadOnlyList<Modifier> Modifiers { get; init; }
}
