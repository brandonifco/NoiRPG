namespace Brp.Rules.Gear;

/// <summary>
/// A stable identifier for a <see cref="VehicleDefinition"/>. Mirrors <see cref="WeaponId"/> and
/// <see cref="ArmorId"/>.
/// </summary>
public readonly record struct VehicleId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public VehicleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Vehicle id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
