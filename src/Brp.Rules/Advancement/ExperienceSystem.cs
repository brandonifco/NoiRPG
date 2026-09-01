using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;
using Brp.Core.Skills;
using Brp.Rules.Characters;

namespace Brp.Rules.Advancement;

/// <summary>
/// Tick-on-use experience: records a skill's use during a case, and resolves the improvement
/// roll at case close. Sourced to Ch 5: System, "Skill Improvement", "Making an Experience
/// Roll", "Increasing Skills by Experience", "Exceeding 100% in a Skill", and "Skill Training
/// and Research" (pp.138-140), except where noted as the tick-on-use house rule (see
/// <see cref="ExperiencePolicy.TickOnUse"/> and `noir-rpg-framework.md` v0.2). All randomness
/// is drawn from the injected <see cref="IEntropySource"/> (AGENTS.md invariant 5) -- nothing
/// here calls <c>System.Random</c> or reads the clock.
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
    /// and raises the rating by a further die roll if the roll beats the success threshold.
    /// The experience check is cleared either way. Returns the points gained (0 if none).
    /// <para>
    /// Ch 5 p.139, "Exceeding 100% in a Skill": once a skill is at or above 100%, an
    /// unmodified d100 can never again roll "higher than your character's current skill
    /// rating" -- the printed rating has outrun the die. The book replaces the ordinary
    /// comparison with a fixed threshold at that point: "you must roll over 100 on D100 ...
    /// which means the experience modifier is necessary [to get there] ... [but] no matter
    /// how much over 100% the skill has risen, any roll of 100 or over earns a skill
    /// improvement." A skill this high is reachable under tick-on-use over a campaign (the
    /// book does not cut skills at 100%, and NoiRPG does not either), so the threshold is
    /// capped at 100 rather than at the (possibly much higher) current rating.
    /// </para>
    /// <para>
    /// <paramref name="experienceBonus"/> defaults to 0 at this low level, because a bare
    /// <see cref="CharacterSkill"/> carries no <see cref="Core.Abilities.AbilitySet"/> to
    /// derive it from. Callers that hold a <see cref="Character"/> should not rely on that
    /// default -- Ch 5 p.138 says the bonus is *always* added ("Your character's experience
    /// bonus ... is added to the die roll"), so <see cref="CloseCase"/>, the path play
    /// actually uses, always supplies the character's
    /// <see cref="Core.Abilities.AbilitySet.ExperienceBonus"/> rather than leaving it at 0.
    /// </para>
    /// </summary>
    public static int ImprovementRoll(
        CharacterSkill skill,
        IEntropySource entropy,
        int gainDieSides = 6,
        int experienceBonus = 0,
        bool useDefaultGain = false)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(entropy);

        if (!skill.HasExperienceCheck)
        {
            return 0;
        }

        var roll = entropy.NextD100() + experienceBonus;
        skill.ClearExperienceCheck();

        // Below 100%, the ordinary rule applies: strictly higher than the current rating.
        // At or above 100%, the threshold is pinned at 100 -- a raw d100 (max 100, since a
        // printed 00 reads as 100) can still just reach it, matching "any roll of 100 or
        // over earns a skill improvement."
        var cappedThreshold = Math.Min(skill.CurrentRating, 100);
        var succeeded = cappedThreshold >= 100 ? roll >= 100 : roll > cappedThreshold;
        if (!succeeded)
        {
            return 0;
        }

        // Ch 5 p.138: "If you do not feel lucky rolling for a skill increase, you can
        // choose to add a default of +3 to the skill rating instead of rolling. This must
        // be announced before rolling." `useDefaultGain` is that announcement -- the
        // caller decides before the roll above is even made, matching the book's
        // ordering, even though the branch below only needs to know it after success is
        // determined. No entropy draw happens for the gain when the default is taken, so a
        // scripted <see cref="IEntropySource"/> with only the percentile roll queued still
        // works (see the deterministic tests for this option).
        var gain = useDefaultGain ? DefaultGain(gainDieSides) : entropy.NextDie(gainDieSides);
        skill.Improve(gain);
        return gain;
    }

    /// <summary>
    /// Ch 5 p.138: "If the die type for the skill increase is higher than 1D6, increase it
    /// to half the dice maximum -- for 1D8 it's +4, and for 1D10 it's +5." The book's own
    /// examples are exactly half of the die's maximum (1D6 to +3 is the un-quoted base
    /// case implied by the same sentence), so this reads as a formula over
    /// <paramref name="gainDieSides"/> rather than a lookup table over a fixed set of dice
    /// -- the campaign-level dice already vary via <see cref="ImprovementRoll"/>'s own
    /// <c>gainDieSides</c> parameter (p.138, "epic" 1D8 / "superhuman" 1D10), and this
    /// default tracks whichever one a table is using. Only defined for the book's own even
    /// gain dice (1D6/1D8/1D10) -- the book names no odd gain die, so an odd
    /// <paramref name="gainDieSides"/> is not a case this formula needs to round for.
    /// </summary>
    public static int DefaultGain(int gainDieSides) => gainDieSides / 2;

    /// <summary>
    /// Resolves the improvement roll for every skill on <paramref name="character"/> that
    /// carries an experience check, in a stable order keyed by <see cref="SkillId"/> so the
    /// draw sequence -- and therefore the resulting roll log -- is reproducible for a given
    /// seed. Returns each ticked skill's gain (0 for a failed improvement roll).
    /// <para>
    /// Ch 5 p.138, "Making an Experience Roll": the character's experience bonus (½ INT,
    /// rounded up) is *always* added to the roll -- it is not optional. This is the default
    /// improvement-roll path used at case close, so it always applies
    /// <see cref="Core.Abilities.AbilitySet.ExperienceBonus"/> unless a caller explicitly
    /// opts out via <paramref name="includeExperienceBonus"/> (for a test double or a house
    /// rule that wants the un-modified roll -- the book itself has no such option).
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<SkillId, int> CloseCase(
        Character character,
        IEntropySource entropy,
        int gainDieSides = 6,
        bool includeExperienceBonus = true,
        bool useDefaultGain = false)
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

            results[id] = ImprovementRoll(skill, entropy, gainDieSides, bonus, useDefaultGain);
        }

        return results;
    }

    /// <summary>
    /// Ch 5 p.138-139, "Skill Training and Research" / "Skill Training": a teacher attempts
    /// an ordinary skill roll against <paramref name="teacherTeachChance"/>, graded through
    /// the same five-grade resolution every other skill roll uses (Ch 5: System, "Evaluating
    /// Success or Failure", pp.127-128). A success (Success, Special, or Critical -- the book
    /// does not distinguish between them for teaching) raises the student's skill by a die
    /// roll, capped so training alone can never carry it above
    /// <paramref name="ruleset"/>'s <see cref="ExperienceRuleset.TrainingCapPercent"/>
    /// (75% per Ch 5 p.139 -- see that property for why this is a ruleset field and not the
    /// same number as <c>Creation.CharacterCreationRuleset.StartingSkillCapPercent</c>). A
    /// plain failure grants nothing. A fumble is counterproductive: "the teacher caus[es]
    /// self-doubt and contradict[s] your character's prior learnings, reducing the skill by
    /// -1D3."
    /// <para>
    /// Returns the signed change actually applied to the student's rating: positive for a
    /// successful lesson (0 if the skill was already at or above the training cap), negative
    /// for a fumble, 0 for a plain failure.
    /// </para>
    /// </summary>
    public static int Teach(
        CharacterSkill studentSkill,
        Percent teacherTeachChance,
        IEntropySource entropy,
        ExperienceRuleset ruleset,
        int gainDieSides = 6,
        int fumbleDieSides = 3)
    {
        ArgumentNullException.ThrowIfNull(studentSkill);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentNullException.ThrowIfNull(ruleset);

        var roll = entropy.NextD100();
        var outcome = SkillResolver.Resolve(teacherTeachChance, teacherTeachChance, roll);

        if (outcome.Level == SuccessLevel.Fumble)
        {
            var penalty = entropy.NextDie(fumbleDieSides);
            var before = studentSkill.CurrentRating;
            studentSkill.Degrade(penalty);
            return studentSkill.CurrentRating - before;
        }

        if (!outcome.Succeeded)
        {
            return 0;
        }

        var die = entropy.NextDie(gainDieSides);
        var gain = Math.Clamp(ruleset.TrainingCapPercent - studentSkill.CurrentRating, 0, die);
        if (gain > 0)
        {
            studentSkill.Improve(gain);
        }

        return gain;
    }

    /// <summary>
    /// Ch 5 pp.138-139, "Skill Training and Research" / "Researching": self-directed
    /// study -- "self-help or self-tutoring: delving into ancient tomes, scouring
    /// databases; disciplined exercise; holographic instructors" -- costs the same time as
    /// <see cref="Teach"/> (p.139: "Dedicated research takes as much time as training but
    /// does not incur the same cost"), which this engine does not model (no downtime clock
    /// here); what differs mechanically is the roll. Research uses an ordinary experience
    /// roll, not a teacher's skill roll: "After the required time is spent, make an
    /// experience roll as normal" (p.139), i.e. the same d100-plus-experience-bonus test
    /// against the current rating (or the at-or-above-100% threshold) as
    /// <see cref="ImprovementRoll"/>, with no teacher and therefore no fumble branch.
    /// <para>
    /// On success, "increase the skill by 1D6-2 points, or choose to add 2 to the current
    /// skill rating" (p.139) -- <paramref name="useDefaultGain"/> is that choice,
    /// announced before rolling exactly as the general default-gain option is (see
    /// <see cref="DefaultGain"/>), except research's flat alternative is a book-printed 2,
    /// not half of a die maximum, and unlike <see cref="ImprovementRoll"/>'s gain die,
    /// research's die is not scaled for epic/superhuman campaigns -- the book prints it as
    /// a fixed "1D6-2" with no such clause. Both numbers therefore come from
    /// <paramref name="ruleset"/> (<see cref="ExperienceRuleset.ResearchGainDieSides"/>,
    /// <see cref="ExperienceRuleset.ResearchGainOffset"/>,
    /// <see cref="ExperienceRuleset.ResearchDefaultGain"/> -- AGENTS.md invariant 7: rules
    /// values are data, not caller-tunable parameters) rather than method parameters the
    /// way <see cref="Teach"/>'s gain/fumble dice are: a caller must not be able to pass,
    /// say, <c>defaultGain: 3</c> and get a result the book does not allow. Unlike
    /// <see cref="Teach"/>, research has no training-cap ceiling: "Unlike training,
    /// researching allows your character to improve more than 75% in a skill" (p.139), so
    /// it never reads <see cref="ExperienceRuleset.TrainingCapPercent"/>.
    /// </para>
    /// <para>
    /// House rule (owner-approved; the book is silent on this case): a negative roll
    /// (1D6-2 on a natural 1) is passed through to <see cref="CharacterSkill.Improve(int)"/>
    /// unchanged, which floors any negative amount to no change rather than lowering the
    /// skill. The book prints "<em>increase</em> the skill by 1D6-2 points" -- an increase
    /// cannot itself be negative -- and elsewhere, when it does mean a decrease, it says so
    /// explicitly: a teaching fumble "reduc[es] the skill by -1D3" (p.139). The absence of
    /// that "reducing" language for research is read here as deliberate: research can fail
    /// to help (gain 0), but unlike a bad teacher, self-study is never worse than doing
    /// nothing.
    /// </para>
    /// <para>
    /// Returns the change actually applied to the skill's rating (0 on a failed experience
    /// roll or a negative 1D6-2 draw).
    /// </para>
    /// </summary>
    public static int Research(
        CharacterSkill skill,
        IEntropySource entropy,
        ExperienceRuleset ruleset,
        int experienceBonus = 0,
        bool useDefaultGain = false)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentNullException.ThrowIfNull(ruleset);

        var roll = entropy.NextD100() + experienceBonus;

        // Same threshold rule as ImprovementRoll (Ch 5, "Making an Experience Roll" (p.138)
        // and "Exceeding 100% in a Skill" (p.139)) -- research says only "make an
        // experience roll as normal," it does not restate the rule.
        var cappedThreshold = Math.Min(skill.CurrentRating, 100);
        var succeeded = cappedThreshold >= 100 ? roll >= 100 : roll > cappedThreshold;
        if (!succeeded)
        {
            return 0;
        }

        var before = skill.CurrentRating;
        var gain = useDefaultGain
            ? ruleset.ResearchDefaultGain
            : entropy.NextDie(ruleset.ResearchGainDieSides) + ruleset.ResearchGainOffset;
        skill.Improve(gain);
        return skill.CurrentRating - before;
    }
}
