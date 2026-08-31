using Brp.Core.Randomness;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// A test double that serves a pre-scripted sequence of die faces instead of generating them,
/// so a damage roll's exact outcome can be pinned. Mirrors
/// <c>Brp.Core.Tests.Dice.FixedEntropySource</c>.
/// </summary>
internal sealed class FixedEntropySource : IEntropySource
{
    private readonly Queue<int> _values;

    public FixedEntropySource(params int[] values) => _values = new Queue<int>(values);

    public long DrawCount { get; private set; }

    public int NextDie(int sides)
    {
        if (_values.Count == 0)
        {
            throw new InvalidOperationException("FixedEntropySource has no more scripted values.");
        }

        DrawCount++;
        var value = _values.Dequeue();
        if (value < 1 || value > sides)
        {
            throw new InvalidOperationException($"Scripted value {value} is out of range for a d{sides}.");
        }

        return value;
    }

    public int NextD100() => NextDie(100);

    public EntropyState Capture() => new(0, 0, 0, 0, DrawCount);

    public void Restore(EntropyState state) => DrawCount = state.DrawCount;
}
