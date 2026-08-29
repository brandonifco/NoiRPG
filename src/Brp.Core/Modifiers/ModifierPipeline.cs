using Brp.Core.Primitives;

namespace Brp.Core.Modifiers;

/// <summary>
/// Turns a base chance and a set of <see cref="Modifier"/>s into a <see cref="ModifierChain"/>,
/// per ADR 0007: Gate -&gt; Override -&gt; PermanentAdditive -&gt; Multiplicative -&gt;
/// SituationalAdditive -&gt; Clamp. Additive modifiers split around Multiplicative because Ch 5,
/// "Modifying Action Rolls" figures a modifier integral to the skill into the rating before a
/// Difficult/Easy grade doubles or halves it, but applies a modifier describing the moment
/// afterward, specifically so a stated penalty or bonus is not itself doubled or halved.
/// Difficulty collapses into a single non-stacking state, with the multiplier values it applies
/// declared on <see cref="ModifierPolicy"/> rather than hardcoded here; independent rational
/// multipliers (<see cref="MultiplicativeModifier"/>) compose alongside it. Rules source: Ch 5,
/// System -- "Modifying Action Rolls".
/// </summary>
public static class ModifierPipeline
{
    /// <summary>
    /// Evaluates <paramref name="modifiers"/> against <paramref name="baseChance"/>.
    /// </summary>
    /// <param name="baseChance">The skill's unmodified base chance.</param>
    /// <param name="modifiers">
    /// The modifiers in effect. Relative order among modifiers of the same kind is preserved
    /// for rendering (e.g. which additive penalty is listed first), but per ADR 0007 does not
    /// change the arithmetic result within that kind -- additive deltas of the same
    /// <see cref="AdditiveKind"/> sum regardless of order, and difficulty collapses to a net
    /// direction regardless of order.
    /// </param>
    /// <param name="policy">
    /// The stage order and difficulty-multiplier values to apply. Defaults to
    /// <see cref="ModifierPolicy.Standard"/>, the only policy used in production; other policies
    /// exist so a test can demonstrate that order, or the multiplier values, change the result.
    /// </param>
    /// <param name="roundingMode">
    /// The rounding mode used by every multiplicative step. Defaults to
    /// <see cref="RoundingMode.Up"/>, matching ADR 0007's worked example (32.5 rounds to 33,
    /// not 32) and this codebase's convention of rounding a penalty in the player's favor.
    /// </param>
    public static ModifierChain Evaluate(
        Percent baseChance,
        IEnumerable<Modifier> modifiers,
        ModifierPolicy? policy = null,
        RoundingMode roundingMode = RoundingMode.Up)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        policy ??= ModifierPolicy.Standard;

        var modifierList = modifiers as IReadOnlyCollection<Modifier> ?? modifiers.ToList();
        var running = baseChance;
        var contributions = new List<ModifierContribution>();

        foreach (var stage in policy.Stages)
        {
            if (stage == ModifierStage.Gate)
            {
                // Ch 5, "Modifying Action Rolls" / ADR 0007: a gate short-circuits everything
                // else and must not consume entropy, so it is resolved before any roll-shaped
                // work happens rather than folded into ApplyStage.
                var gates = modifierList.OfType<GateModifier>().ToList();
                if (gates.Count == 0)
                {
                    continue;
                }

                // Impossible takes precedence over Automatic when both are asserted at once:
                // refusing an action is the safer default than silently auto-succeeding it.
                var kind = gates.Any(g => g.Kind == GateKind.Impossible)
                    ? GateKind.Impossible
                    : GateKind.Automatic;
                var sources = gates.Where(g => g.Kind == kind).Select(g => g.Source).ToList();

                return new ModifierChain
                {
                    BaseChance = baseChance,
                    Gate = kind,
                    GateSources = sources,
                    Contributions = contributions,
                    EffectiveChance = null,
                };
            }

            running = ApplyStage(stage, modifierList, running, policy, roundingMode, contributions);
        }

        return new ModifierChain
        {
            BaseChance = baseChance,
            Contributions = contributions,
            EffectiveChance = running,
        };
    }

    private static Percent ApplyStage(
        ModifierStage stage,
        IReadOnlyCollection<Modifier> modifiers,
        Percent running,
        ModifierPolicy policy,
        RoundingMode roundingMode,
        List<ModifierContribution> contributions)
    {
        switch (stage)
        {
            case ModifierStage.Override:
                foreach (var o in modifiers.OfType<OverrideModifier>())
                {
                    running = o.Value;
                    contributions.Add(
                        new ModifierContribution(o.Source, $"{o.Source} overrides to {o.Value}", running));
                }

                return running;

            case ModifierStage.PermanentAdditive:
                return ApplyAdditive(AdditiveKind.Permanent, modifiers, running, contributions);

            case ModifierStage.SituationalAdditive:
                return ApplyAdditive(AdditiveKind.Situational, modifiers, running, contributions);

            case ModifierStage.Multiplicative:
                return ApplyMultiplicative(modifiers, running, policy, roundingMode, contributions);

            case ModifierStage.Clamp:
                // Percent floors at zero on every operation already, so this never changes the
                // value in practice. Kept explicit because ADR 0007 names Clamp as its own
                // stage, and because Percent's continuous flooring is only equivalent to a
                // single end-of-chain clamp because every later stage is multiplicative
                // (scaling a floored zero can never recover a "true" negative) -- a fact worth
                // stating rather than leaving implicit.
                return Percent.Of(running.Value);

            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Gate is handled by the caller.");
        }
    }

    private static Percent ApplyAdditive(
        AdditiveKind kind,
        IReadOnlyCollection<Modifier> modifiers,
        Percent running,
        List<ModifierContribution> contributions)
    {
        // Ch 5, "Modifying Action Rolls": a permanent modifier is figured into the rating
        // before the difficulty multiplier; a situational one is applied after, so its stated
        // weight is not itself doubled or halved. Tagging the kind in the description is what
        // lets Render() answer "why did my -20 apply after the halving?" (ADR 0007).
        var label = kind == AdditiveKind.Permanent ? "permanent" : "situational";

        foreach (var a in modifiers.OfType<AdditiveModifier>().Where(a => a.Kind == kind))
        {
            running = running.Add(a.Delta);
            var sign = a.Delta >= 0 ? "+" : string.Empty;
            contributions.Add(
                new ModifierContribution(a.Source, $"{a.Source} {sign}{a.Delta}% [{label}]", running));
        }

        return running;
    }

    private static Percent ApplyMultiplicative(
        IReadOnlyCollection<Modifier> modifiers,
        Percent running,
        ModifierPolicy policy,
        RoundingMode roundingMode,
        List<ModifierContribution> contributions)
    {
        var plainMultipliers = modifiers.OfType<MultiplicativeModifier>().ToList();
        foreach (var m in plainMultipliers)
        {
            if (m.Numerator <= 0 || m.Denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modifiers),
                    $"'{m.Source}' has a non-positive multiplier ({m.Numerator}/{m.Denominator}).");
            }
        }

        // Difficulty is a state, not a stack (ADR 0007): any number of Difficult sources
        // produce one halving, any number of Easy sources produce one doubling, and the two
        // cancel pairwise. Taking the sign of the sum of each source's own direction (+1 for
        // Easy, -1 for Difficult) reproduces exactly that: magnitude beyond +/-1 never matters.
        // The actual multiplier applied comes from the policy, not from the modifier --
        // DifficultyModifier carries no numerator/denominator of its own (see its remarks).
        var difficulty = modifiers.OfType<DifficultyModifier>().ToList();
        if (difficulty.Count > 0)
        {
            var net = Math.Sign(difficulty.Sum(d => d.Direction == DifficultyDirection.Easier ? 1 : -1));
            var sources = string.Join(", ", difficulty.Select(d => d.Source));

            switch (net)
            {
                case > 0:
                    running = running.Scale(policy.EasyNumerator, policy.EasyDenominator, roundingMode);
                    contributions.Add(new ModifierContribution(
                        sources, $"{sources} {RenderRatio(policy.EasyNumerator, policy.EasyDenominator)}", running));
                    break;
                case < 0:
                    running = running.Scale(policy.DifficultNumerator, policy.DifficultDenominator, roundingMode);
                    contributions.Add(new ModifierContribution(
                        sources,
                        $"{sources} {RenderRatio(policy.DifficultNumerator, policy.DifficultDenominator)}",
                        running));
                    break;
                default:
                    contributions.Add(new ModifierContribution(sources, $"{sources} cancel out", running));
                    break;
            }
        }

        // Independent rational multipliers compose alongside the net difficulty grade rather
        // than folding into it (ADR 0007), applied in the order supplied.
        foreach (var m in plainMultipliers)
        {
            running = running.Scale(m.Numerator, m.Denominator, roundingMode);
            contributions.Add(
                new ModifierContribution(m.Source, $"{m.Source} {RenderRatio(m.Numerator, m.Denominator)}", running));
        }

        return running;
    }

    private static string RenderRatio(int numerator, int denominator)
    {
        if (denominator == 1)
        {
            return $"×{numerator}";
        }

        if (numerator == 1)
        {
            return $"÷{denominator}";
        }

        return $"×{numerator}/{denominator}";
    }
}
