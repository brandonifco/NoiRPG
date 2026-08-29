namespace Brp.Cli.Tests;

/// <summary>
/// Runs the command line in-process against string writers. Every test goes through
/// <see cref="Program.Run"/> -- the same entry point the executable uses -- so what is asserted
/// on is the real rendering path, not a reimplementation of it.
/// </summary>
internal static class CliHarness
{
    public static CliResult Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = Program.Run(args, output, error);
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>The three things a command line produces: what it printed, where, and its exit code.</summary>
    public sealed record CliResult(int ExitCode, string Output, string Error);
}
