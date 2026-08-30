using System.Text;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Core.Modifiers;

/// <summary>
/// The result of running a set of <see cref="Modifier"/>s through <see cref="ModifierPipeline"/>:
/// the base chance, either a short-circuiting gate or an ordered list of contributions, and the
/// final effective chance. Self-describing by design -- ADR 0007 and the transparency pillar in
/// <c>noir-rpg-framework.md</c> require that the engine can explain how it got a number, not
/// just report it.
/// </summary>
public sealed record ModifierChain
{
    /// <summary>
    /// The rating the chain started from -- the value handed to <see cref="ModifierPipeline"/>,
    /// which is the character's current rating in the skill. This is <em>not</em> the skill's
    /// printed base chance: for a trained character the two differ, and the 5% floor keys on the
    /// printed base, not on this. Resolving a roll therefore takes the printed base as a separate
    /// argument (see <see cref="Resolve"/>); the two must not be conflated (#27).
    /// </summary>
    public required Percent BaseChance { get; init; }

    /// <summary>Non-null when a gate short-circuited the chain; no roll should be attempted.</summary>
    public GateKind? Gate { get; init; }

    /// <summary>The source labels responsible for <see cref="Gate"/>. Empty when not gated.</summary>
    public IReadOnlyList<string> GateSources { get; init; } = [];

    /// <summary>Every applied step, in the order the ordering policy produced them.</summary>
    public IReadOnlyList<ModifierContribution> Contributions { get; init; } = [];

    /// <summary>The final chance to resolve a roll against. Null when <see cref="IsGated"/>.</summary>
    public Percent? EffectiveChance { get; init; }

    /// <summary>True when a gate short-circuited the chain -- no roll should be attempted.</summary>
    public bool IsGated => Gate is not null;

    /// <summary>
    /// Renders the full derivation, e.g. <c>65% &#8594; 33% (darkness &#247;2) &#8594; 13%
    /// (firing into combat -20% [situational])</c>, or <c>10% &#8594; Impossible (no way to
    /// attempt it)</c> when gated.
    /// </summary>
    public string Render()
    {
        const string arrow = " → ";

        if (IsGated)
        {
            return $"{BaseChance}{arrow}{Gate} ({string.Join(", ", GateSources)})";
        }

        var sb = new StringBuilder(BaseChance.ToString());
        foreach (var step in Contributions)
        {
            sb.Append(arrow).Append(step.ResultingChance).Append(" (").Append(step.Description).Append(')');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Draws a roll and resolves it via <see cref="SkillResolver"/>, or returns
    /// <see langword="null"/> without touching <paramref name="entropy"/> when
    /// <see cref="IsGated"/> -- per ADR 0007, a gate consumes no entropy.
    /// </summary>
    /// <param name="printedBaseChance">
    /// The skill's printed base chance -- what an untrained character rolls against. It is a
    /// required argument, not <see cref="BaseChance"/>, because the two are different numbers: the
    /// chain starts from the character's rating (<see cref="BaseChance"/>), while the 5%-base-chance
    /// floor (Ch 5: System, "Skill Rolls", p.128) keys on the printed base. Passing it explicitly is
    /// what stops a trained character in a 01%-base skill (Science, Strategy, Martial Arts) from
    /// being wrongly rescued on 01--05 (#27). Where a <c>SkillDefinition</c> is in hand,
    /// <c>SkillRoll</c> supplies this from the skill's identity instead.
    /// </param>
    /// <param name="entropy">The source the single percentile roll is drawn from, unless gated.</param>
    /// <param name="policy">The resolution policy, or <see langword="null"/> for the default.</param>
    public RollOutcome? Resolve(Percent printedBaseChance, IEntropySource entropy, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        return IsGated ? null : SkillResolver.Resolve(printedBaseChance, EffectiveChance!.Value, entropy, policy);
    }
}
