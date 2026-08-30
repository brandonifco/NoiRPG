namespace Brp.Rules.Characters;

/// <summary>
/// A character's wound list. A plain container -- add, read, remove -- carrying no wound
/// mechanics of its own (see <see cref="Wound"/>). Layer 4 (#21) will decide what populates
/// and resolves this list; Layer 3 only guarantees a <see cref="Character"/> has somewhere to
/// keep it.
/// </summary>
public sealed class WoundTrack
{
    private readonly List<Wound> _wounds = [];

    /// <summary>Every wound currently recorded, in the order added.</summary>
    public IReadOnlyList<Wound> Wounds => _wounds;

    /// <summary>Records a new wound.</summary>
    public void Add(Wound wound)
    {
        ArgumentNullException.ThrowIfNull(wound);
        _wounds.Add(wound);
    }

    /// <summary>Removes a previously recorded wound (e.g. once healed).</summary>
    public bool Remove(Wound wound) => _wounds.Remove(wound);
}
