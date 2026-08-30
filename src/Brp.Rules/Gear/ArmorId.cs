namespace Brp.Rules.Gear;

/// <summary>
/// A stable identifier for an <see cref="ArmorDefinition"/>. Mirrors <see cref="WeaponId"/>.
/// </summary>
public readonly record struct ArmorId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public ArmorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Armor id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
