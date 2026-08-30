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
    /// Ch 5 p.138, "Exceeding 100% in a Skill": once a skill is at or above 100%, an
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
    /// <paramref name="experienceBonus"/> defaults to 0, which is the form
    /// <c>tools/advancement_sim.py</c> simulates (updated alongside this fix to share the
    /// same 100%-cap rule, so the two stay reconciled). Passing a character's
    /// <see cref="Core.Abilities.AbilitySet.ExperienceBonus"/> reproduces Ch 5's printed rule
    /// exactly, including the 100%-and-above case.
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
}
