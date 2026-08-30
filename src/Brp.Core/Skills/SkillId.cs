namespace Brp.Core.Skills;

/// <summary>
/// A data-defined skill identifier, rather than a hardcoded enum. Mirrors
/// <see cref="Abilities.CharacteristicId"/>: the stable key is the framework's canonical
/// name (see the naming rule in <c>orc-scope-filter.md</c>, "Skill naming"), not the book's.
/// </summary>
public readonly record struct SkillId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public SkillId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Skill id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
