namespace Brp.Core.Modifiers;

/// <summary>
/// The stage order and difficulty-multiplier values a <see cref="ModifierPipeline"/> uses, held
/// as declared data rather than as constants scattered through the pipeline -- the same
/// precedent as <see cref="Resolution.ResolutionPolicy"/> for the resolver's grading constants.
/// <para>
/// Per ADR 0007 and Ch 5: System, "Modifying Action Rolls", ordering changes the result: a 65%
/// rating with a -20% situational penalty and a halving in near-darkness resolves 65 / 2 = 33
/// (round up from 32.5), then 33 - 20 = 13%, under Gate -&gt; Override -&gt; PermanentAdditive
/// -&gt; Multiplicative -&gt; SituationalAdditive -&gt; Clamp -- the book applies the
/// situational modifier after the difficulty grade specifically so a stated -20% is not itself
/// halved to -10%. Collapsing both additive stages ahead of Multiplicative instead gives the
/// rejected 23%.
/// </para>
/// </summary>
/// <param name="Stages">The stages to apply, in order. Repeats and omissions are permitted.</param>
/// <param name="EasyNumerator">Numerator of the multiplier a net Easy grade applies.</param>
/// <param name="EasyDenominator">Denominator of the multiplier a net Easy grade applies.</param>
/// <param name="DifficultNumerator">Numerator of the multiplier a net Difficult grade applies.</param>
/// <param name="DifficultDenominator">Denominator of the multiplier a net Difficult grade applies.</param>
public sealed record ModifierPolicy(
    IReadOnlyList<ModifierStage> Stages,
    int EasyNumerator,
    int EasyDenominator,
    int DifficultNumerator,
    int DifficultDenominator)
{
    /// <summary>The order and values fixed by ADR 0007 and Ch 5. The only policy used by production rulesets.</summary>
    public static ModifierPolicy Standard { get; } = new(
        Stages:
        [
            ModifierStage.Gate,
            ModifierStage.Override,
            ModifierStage.PermanentAdditive,
            ModifierStage.Multiplicative,
            ModifierStage.SituationalAdditive,
            ModifierStage.Clamp,
        ],
        EasyNumerator: 2,
        EasyDenominator: 1,
        DifficultNumerator: 1,
        DifficultDenominator: 2);
}
