namespace Brp.Core.Randomness;

/// <summary>
/// A snapshot of an <see cref="IEntropySource"/>'s internal state, plus how many draws
/// it has served. Capturing and restoring this is what makes a save file replay the same
/// rolls, which is the mechanism behind the pre-seeded roll policy.
/// </summary>
/// <param name="S0">Generator word 0.</param>
/// <param name="S1">Generator word 1.</param>
/// <param name="S2">Generator word 2.</param>
/// <param name="S3">Generator word 3.</param>
/// <param name="DrawCount">Number of raw draws served since seeding.</param>
public readonly record struct EntropyState(ulong S0, ulong S1, ulong S2, ulong S3, long DrawCount);
