using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Skills;
using Brp.Rules.Characters;

namespace Brp.Rules.Advancement;

/// <summary>
/// Tick-on-use experience: records a skill's use during a case, and resolves the improvement
/// roll at case close. Sourced to Ch 5: System, "Skill Improvement", "Making an Experience
/// Roll", "Increasing Skills by Experience", and "Training and Study" (pp.138-140), except
/// where noted as the tick-on-use house rule (see <see cref="ExperiencePolicy.TickOnUse"/>
/// and `noir-rpg-framework.md` v0.2). All randomness is drawn from the injected
/// <see cref="IEntropySource"/> (AGENTS.md invariant 5) -- nothing here calls
/// <c>System.Random</c> or reads the clock.
/// </summary>
public static class ExperienceSystem
{
    /// <summary>
    /// Records one skill's use under <paramref name="stakes"/>, applying the mechanical gate:
    /// an Easy or no-stakes check is never eligible (Ch 5 p.138), and under
    /// <see cref="ExperiencePolicy.RawTickOnSuccess"/> an unsuccessful check is not eligible
    /// either. Enforces "once per case" via <paramref name="ledger"/>. Returns
    /// <see langword="true"/> only if this call newly recorded a tick.
    /// </summary>
    public static bool RecordUse(
        CaseExperienceLedger ledger,
        CharacterSkill skill,
        CheckStakes stakes,
        bool succeeded,
        ExperiencePolicy policy = ExperiencePolicy.TickOnUse)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(skill);

        // Ch 5 p.138: "If a skill roll was Easy, no experience check is allowed." The
        // "nothing at stake" gate is the same mechanical refusal for a check the scenario
        // layer classified as carrying no consequence -- there is no gamemaster here to
        // adjudicate either exemption by hand, so both are refused unconditionally.
        if (stakes != CheckStakes.RealStakes)
        {
            return false;
        }

        // The only difference between the two policies: RAW requires success, tick-on-use
        // does not. Nothing else about the gate or the ledger changes between them.
        if (policy == ExperiencePolicy.RawTickOnSuccess && !succeeded)
        {
            return false;
        }

        if (!ledger.TryTick(skill.Definition.Id))
        {
            return false;
        }

        skill.MarkExperienceCheck();
        return true;
    }

    /// <summary>
    /// Resolves one skill's improvement roll at case close (Ch 5 p.138, "Making an
    /// Experience Roll"): a no-op if the skill carries no experience check; otherwise draws a
    /// d100, adds <paramref name="experienceBonus"/> to the roll (never to the gain -- "The
    /// experience bonus is not added to the actual skill points gained, just to the roll"),
    /// and raises the rating by a further die roll if the result exceeds the current rating.
    /// The experience check is cleared either way. Returns the points gained (0 if none).
    /// <para>
    /// <paramref name="experienceBonus"/> defaults to 0, which is the form
    /// <c>tools/advancement_sim.py</c> simulates. Passing a character's
    /// <see cref="Core.Abilities.AbilitySet.ExperienceBonus"/> reproduces Ch 5's printed rule
    /// exactly.
    /// </para>
    /// </summary>
    public static int ImprovementRoll(
        CharacterSkill skill, IEntropySource entropy, int gainDieSides = 6, int experienceBonus = 0)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(entropy);

        if (!skill.HasExperienceCheck)
        {
            return 0;
        }

        var roll = entropy.NextD100() + experienceBonus;
        skill.ClearExperienceCheck();

        if (roll <= skill.CurrentRating)
        {
            return 0;
        }

        var gain = entropy.NextDie(gainDieSides);
        skill.Improve(gain);
        return gain;
    }

    /// <summary>
    /// Resolves the improvement roll for every skill on <paramref name="character"/> that
    /// carries an experience check, in a stable order keyed by <see cref="SkillId"/> so the
    /// draw sequence -- and therefore the resulting roll log -- is reproducible for a given
    /// seed. Returns each ticked skill's gain (0 for a failed improvement roll).
    /// </summary>
    public static IReadOnlyDictionary<SkillId, int> CloseCase(
        Character character, IEntropySource entropy, int gainDieSides = 6, bool includeExperienceBonus = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(entropy);

        var results = new Dictionary<SkillId, int>();
        var bonus = includeExperienceBonus ? character.Abilities.ExperienceBonus : 0;
        foreach (var (id, skill) in character.Skills.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
        {
            if (!skill.HasExperienceCheck)
            {
                continue;
            }

            results[id] = ImprovementRoll(skill, entropy, gainDieSides, bonus);
        }

        return results;
    }

    /// <summary>
    /// Ch 5 p.138, "Training and Study": a teacher attempts a Teach roll against
    /// <paramref name="teacherTeachChance"/>; on success the student's skill rises by a die
    /// roll. A failed roll grants no benefit. The book's fumble consequence (the teacher
    /// causing the student self-injury) is not modeled -- that is a combat/injury effect
    /// (Layer 4, #21), out of scope here.
    /// </summary>
    public static bool Teach(
        CharacterSkill studentSkill, Percent teacherTeachChance, IEntropySource entropy, int gainDieSides = 6)
    {
        ArgumentNullException.ThrowIfNull(studentSkill);
        ArgumentNullException.ThrowIfNull(entropy);

        var roll = entropy.NextD100();
        if (roll > teacherTeachChance.Value)
        {
            return false;
        }

        studentSkill.Improve(entropy.NextDie(gainDieSides));
        return true;
    }
}
