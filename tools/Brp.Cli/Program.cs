using System.Text;

namespace Brp.Cli;

/// <summary>
/// Entry point for <c>brp</c>, the gamemaster-facing command line over the rules kernel.
/// <para>
/// Deliberately thin: it owns the console and the exit code and nothing else. Everything a
/// test needs to assert on is produced by <see cref="RollCommand"/> writing into a
/// <see cref="TextWriter"/>, so the acceptance criterion "the same seed produces identical
/// output" is checked against the real rendering path rather than a reimplementation of it.
/// </para>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // The modifier chain renders with ÷ and × (Brp.Core's ModifierChain.Render uses the
        // same glyphs), so a console left on a legacy code page would mangle the one thing
        // this tool exists to show. Failure to set it is not worth aborting over.
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Redirected to a handle that will not take an encoding change. Carry on.
        }

        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Dispatches one invocation. Returns <see cref="ExitCode.Ok"/> when a command ran to
    /// completion -- including when the roll failed or fumbled, which is a result, not an error --
    /// and <see cref="ExitCode.UsageError"/> when the command line could not be understood.
    /// </summary>
    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0)
        {
            error.Write(Usage);
            return ExitCode.UsageError;
        }

        var command = args[0];
        var rest = args.Skip(1).ToList();

        if (IsHelpFlag(command))
        {
            output.Write(Usage);
            return ExitCode.Ok;
        }

        switch (command)
        {
            case "roll":
                return RollCommand.Run(rest, output, error);

            default:
                error.Write($"brp: unknown command '{command}'.\n\n");
                error.Write(Usage);
                return ExitCode.UsageError;
        }
    }

    internal static bool IsHelpFlag(string argument) =>
        argument is "--help" or "-h" or "help";

    internal const string Usage =
        """
        brp — Basic Roleplaying rules engine, from the command line.

        usage:
          brp roll --skill <n> --seed <n> [options]
          brp --help

        commands:
          roll    Resolve one skill or action roll and show how the result was
                  produced: the base rating, every modifier with its source, the
                  effective chance, the outcome bands, the roll, and the grade.

        Run `brp roll --help` for the options of the roll command.

        """;
}

/// <summary>Process exit codes, named so a caller is not reading bare integers.</summary>
internal static class ExitCode
{
    /// <summary>The command ran. A failed or fumbled roll still exits here -- it is a result.</summary>
    public const int Ok = 0;

    /// <summary>The command line could not be understood. Nothing was rolled.</summary>
    public const int UsageError = 2;
}
