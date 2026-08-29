namespace Brp.Core.Randomness;

/// <summary>
/// The only way randomness enters the engine. Implementations must be deterministic:
/// the same seed followed by the same call sequence must produce identical results.
/// <c>System.Random</c> is banned project-wide, both because it hides state and because
/// its algorithm is not contractually stable across runtimes -- an upgrade could silently
/// change every roll in every save file.
/// </summary>
public interface IEntropySource
{
    /// <summary>Number of raw draws served since seeding. Useful for logging and replay.</summary>
    long DrawCount { get; }

    /// <summary>Rolls a single die, returning a value in <c>[1, sides]</c> inclusive.</summary>
    int NextDie(int sides);

    /// <summary>
    /// Rolls percentile dice, returning a value in <c>[1, 100]</c>. The book reads a
    /// percentile result of 00 as 100, so this never returns zero.
    /// </summary>
    int NextD100();

    /// <summary>Captures the current state for later restoration.</summary>
    EntropyState Capture();

    /// <summary>Restores a previously captured state, resuming the identical sequence.</summary>
    void Restore(EntropyState state);
}
