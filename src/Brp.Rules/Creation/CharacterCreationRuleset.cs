using Brp.Core.Abilities;

namespace Brp.Rules.Creation;

/// <summary>
/// Every numeric parameter <see cref="CharacterBuilder"/>'s point-buy path reads, kept as
/// ruleset data rather than constants (AGENTS.md invariant 7). Sourced to Ch 2: Characters,
/// "Point-Based Character Creation (option)" (p.9-10) for the characteristic pool, and
/// "Step Seven: Profession and Skills" (p.8) for the skill-point figures.
/// </summary>
public sealed class CharacterCreationRuleset
{
    /// <summary>Creates a creation ruleset from data-defined values.</summary>
    public CharacterCreationRuleset(
        int characteristicPointPool,
        int startingCharacteristicValue,
        int characteristicCreationMaximum,
        IReadOnlyDictionary<CharacteristicId, int> characteristicCosts,
        int freeShiftPoints,
        int professionalSkillPoints,
        int personalSkillPointsIntMultiplier,
        int increasedPersonalSkillPointsIntMultiplier,
        int startingSkillCapPercent)
    {
        ArgumentNullException.ThrowIfNull(characteristicCosts);
        if (characteristicCosts.Count == 0)
        {
            throw new ArgumentException("At least one characteristic cost is required.", nameof(characteristicCosts));
        }

        CharacteristicPointPool = characteristicPointPool;
        StartingCharacteristicValue = startingCharacteristicValue;
        CharacteristicCreationMaximum = characteristicCreationMaximum;
        CharacteristicCosts = characteristicCosts;
        FreeShiftPoints = freeShiftPoints;
        ProfessionalSkillPoints = professionalSkillPoints;
        PersonalSkillPointsIntMultiplier = personalSkillPointsIntMultiplier;
        IncreasedPersonalSkillPointsIntMultiplier = increasedPersonalSkillPointsIntMultiplier;
        StartingSkillCapPercent = startingSkillCapPercent;
    }

    /// <summary>
    /// Ch 2 p.9: "You have 24 points to spend on characteristics. This is the equivalent of
    /// the 'normal' power level for a campaign."
    /// </summary>
    public int CharacteristicPointPool { get; }

    /// <summary>Ch 2 p.9: "All characteristics ... begin at 10."</summary>
    public int StartingCharacteristicValue { get; }

    /// <summary>
    /// Ch 2 p.9: "No initial characteristic can be raised to higher than 21." This is a
    /// creation-time ceiling distinct from <see cref="CharacteristicDefinition.Maximum"/>,
    /// which is <see langword="null"/> for INT and POW because those two have no ceiling once
    /// play begins (Ch 2 p.10, "Mental characteristics ... can usually be raised without
    /// limits") -- the 21 cap applies only at character creation.
    /// </summary>
    public int CharacteristicCreationMaximum { get; }

    /// <summary>
    /// Ch 2 p.9: "Each point of STR, CON, SIZ, or CHA costs 1 point" and "Each point of DEX,
    /// INT, and POW costs 3," symmetric for reductions ("For each point ... you reduce ...
    /// you get [the same number] back"). Keyed by characteristic id.
    /// </summary>
    public IReadOnlyDictionary<CharacteristicId, int> CharacteristicCosts { get; }

    /// <summary>
    /// <strong>House rule, not printed for this path.</strong> Ch 2 p.8's rolled-Step-One
    /// redistribution ("you may redistribute up to 3 points between your characteristics")
    /// is written for the dice-rolled path. NoiRPG extends the same small post-allocation
    /// adjustment to the point-buy path so a build can be fine-tuned without spending
    /// additional pool points; see the ADR for this issue. Zero disables the shift entirely.
    /// </summary>
    public int FreeShiftPoints { get; }

    /// <summary>
    /// Ch 2 p.8, "Step Seven": Normal power level allots 250 points to professional skills.
    /// </summary>
    public int ProfessionalSkillPoints { get; }

    /// <summary>
    /// Ch 2 p.8: "multiply your character's INT×10 to determine their personal skill point
    /// pool." The BRP RAW baseline.
    /// </summary>
    public int PersonalSkillPointsIntMultiplier { get; }

    /// <summary>
    /// Ch 2 p.8, "Increased Personal Skill Points (Option)": the book states INT×15/20/25 for
    /// heroic/epic/superhuman tiers and is silent on a Normal-power-level increase. NoiRPG
    /// runs at Normal power level (250 professional points, 75% cap) and borrows the heroic
    /// tier's INT×15 personal multiplier only, per the Noir entry's recommendation
    /// (`orc-scope-filter.md`) that the option suits "characters ... tremendously competent
    /// and skilled beyond what their current profession would indicate." See the ADR.
    /// </summary>
    public int IncreasedPersonalSkillPointsIntMultiplier { get; }

    /// <summary>
    /// Ch 2 p.8: "No skill should begin higher than 75%," the Normal power level's starting
    /// soft cap, enforced during creation only -- advancement can carry a skill past it.
    /// </summary>
    public int StartingSkillCapPercent { get; }
}
