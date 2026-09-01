namespace Brp.Rules.Combat;

/// <summary>
/// A stable identifier for a <see cref="PoisonCatalogEntry"/>. Mirrors the identifier pattern
/// used for <c>WeaponId</c> and <c>ArmorId</c> in <see cref="Brp.Rules.Gear"/>: a thin wrapper
/// over a ruleset-supplied string.
/// </summary>
public readonly record struct PoisonId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public PoisonId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Poison id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
