using System.Numerics;

namespace Brp.Core.Randomness;

/// <summary>
/// xoshiro256** generator, seeded through SplitMix64.
/// <para>
/// Chosen because the algorithm is fixed and published, so a given seed produces the same
/// stream forever regardless of runtime version. That is the property save-file replay
/// depends on, and the property <c>System.Random</c> does not offer.
/// </para>
/// </summary>
public sealed class Xoshiro256StarStar : IEntropySource
{
    private ulong _s0, _s1, _s2, _s3;

    /// <summary>Creates a generator from a 64-bit seed.</summary>
    public Xoshiro256StarStar(ulong seed)
    {
        // SplitMix64 expands one seed word into four, avoiding the all-zero state and
        // the poor early output xoshiro shows when seeded with mostly-zero words.
        var z = seed;
        _s0 = SplitMix64(ref z);
        _s1 = SplitMix64(ref z);
        _s2 = SplitMix64(ref z);
        _s3 = SplitMix64(ref z);
    }

    /// <inheritdoc />
    public long DrawCount { get; private set; }

    private static ulong SplitMix64(ref ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        var result = z;
        result = (result ^ (result >> 30)) * 0xBF58476D1CE4E5B9UL;
        result = (result ^ (result >> 27)) * 0x94D049BB133111EBUL;
        return result ^ (result >> 31);
    }

    private ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;

        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        DrawCount++;
        return result;
    }

    /// <inheritdoc />
    public int NextDie(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        if (sides == 1)
        {
            return 1;
        }

        // Rejection sampling. The naive `value % sides` is biased toward low faces
        // because 2^64 is not a multiple of most die sizes; on a d6 that skew is small
        // but it is real, and a rules engine should not ship a loaded die.
        var n = (ulong)sides;
        var threshold = unchecked((ulong)-(long)n) % n; // 2^64 mod n
        ulong draw;
        do
        {
            draw = NextUInt64();
        }
        while (draw < threshold);

        return (int)(draw % n) + 1;
    }

    /// <inheritdoc />
    public int NextD100() => NextDie(100);

    /// <inheritdoc />
    public EntropyState Capture() => new(_s0, _s1, _s2, _s3, DrawCount);

    /// <inheritdoc />
    public void Restore(EntropyState state)
    {
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
        DrawCount = state.DrawCount;
    }
}
