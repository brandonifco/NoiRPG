namespace Brp.Rules.Gear;

/// <summary>
/// A stable identifier for a <see cref="WeaponDefinition"/>. Mirrors the identifier pattern
/// used for <c>CharacterId</c> in <see cref="Characters.CharacterId"/> and <c>SkillId</c> in
/// <c>Brp.Core.Skills</c>: a thin wrapper over a ruleset-supplied string.
/// </summary>
public readonly record struct WeaponId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public WeaponId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Weapon id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
