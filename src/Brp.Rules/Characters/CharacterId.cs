namespace Brp.Rules.Characters;

/// <summary>
/// A stable identifier for a <see cref="Character"/>. Mirrors the identifier pattern used
/// for <c>CharacteristicId</c> and <c>SkillId</c> in <c>Brp.Core</c>: a thin wrapper over a
/// caller-supplied string rather than a database-generated key, since Layer 3 has no
/// persistence concept of its own.
/// </summary>
public readonly record struct CharacterId
{
    /// <summary>Creates an identifier from a caller-chosen value.</summary>
    public CharacterId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Character id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The identifier's value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
