namespace Brp.Core.Abilities;

/// <summary>A data-defined characteristic identifier, rather than a hardcoded enum.</summary>
public readonly record struct CharacteristicId
{
    /// <summary>Creates an identifier from its ruleset id.</summary>
    public CharacteristicId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Characteristic id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The stable ruleset identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
