namespace Brp.Rules.Gear;

/// <summary>
/// A stable identifier for a <see cref="BreakableItemDefinition"/>. Mirrors the identifier
/// pattern used for <see cref="WeaponId"/> and <see cref="ArmorId"/>: a thin wrapper over a
/// ruleset-supplied string.
/// </summary>
public readonly record struct BreakableItemId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public BreakableItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Breakable item id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
