using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Resolution;
using Brp.Core.Tests.Dice;

namespace Brp.Core.Tests.Modifiers;

/// <summary>
/// Covers the typed modifier pipeline against ADR 0007: Gate -&gt; Override -&gt;
/// PermanentAdditive -&gt; Multiplicative -&gt; SituationalAdditive -&gt; Clamp, difficulty as a
/// non-stacking state whose multiplier values live on <see cref="ModifierPolicy"/> rather than
/// on the modifier or a factory, and independent rational multipliers composing alongside it.
/// Ch 5: System, "Modifying Action Rolls".
/// </summary>
public class ModifierPipelineTests
{
    // ---- Each modifier kind in isolation --------------------------------------------------

    [Theory]
    [InlineData(GateKind.Automatic)]
    [InlineData(GateKind.Impossible)]
    public void A_gate_short_circuits_with_no_effective_chance_and_no_contributions(GateKind kind)
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(50), [new GateModifier("no possible failure", kind)]);

        Assert.True(chain.IsGated);
        Assert.Equal(kind, chain.Gate);
        Assert.Equal(["no possible failure"], chain.GateSources);
        Assert.Null(chain.EffectiveChance);
        Assert.Empty(chain.Contributions);
    }

    [Fact]
    public void An_override_replaces_the_base_chance_outright()
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(30), [new OverrideModifier("shield parry", Percent.Of(60))]);

        Assert.Equal(Percent.Of(60), chain.EffectiveChance);
        Assert.Single(chain.Contributions);
    }

    [Theory]
    [InlineData(50, -20, 30)]
    [InlineData(50, 10, 60)]
    [InlineData(10, -50, 0)] // floors at zero rather than going negative
    public void A_situational_additive_modifier_applies_a_signed_delta(int baseChance, int delta, int expected)
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(baseChance), [new AdditiveModifier("penalty or bonus", delta)]);

        Assert.Equal(Percent.Of(expected), chain.EffectiveChance);
    }

    [Theory]
    [InlineData(50, -20, 30)]
    [InlineData(50, 10, 60)]
    public void A_permanent_additive_modifier_applies_a_signed_delta(int baseChance, int delta, int expected)
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(baseChance),
            [new AdditiveModifier("integral trait", delta, AdditiveKind.Permanent)]);

        Assert.Equal(Percent.Of(expected), chain.EffectiveChance);
    }

    [Theory]
    [InlineData(2, 1, 100, 200)]  // x2
    [InlineData(1, 2, 100, 50)]   // half
    [InlineData(1, 4, 100, 25)]   // quarter
    [InlineData(1, 4, 45, 12)]    // quarter, with rounding: ceil(45/4) = 12
    public void A_multiplicative_modifier_applies_a_rational_factor(
        int numerator, int denominator, int baseChance, int expected)
    {
        // The numbers here are illustrative only, chosen to exercise the arithmetic -- this is
        // not a book rule. MultiplicativeModifier is a generic capability for an arbitrary
        // independent rational multiplier; what specific multipliers exist (weapon range,
        // lighting, etc.) is a later Issue's concern.
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(baseChance), [new MultiplicativeModifier("illustrative multiplier", numerator, denominator)]);

        Assert.Equal(Percent.Of(expected), chain.EffectiveChance);
    }

    // ---- Ordering is a named policy, not implicit in call order -------------------------

    [Fact]
    public void Standard_order_matches_the_ADR_worked_example()
    {
        // Ch 5, "Modifying Action Rolls" / ADR 0007: a 65% rating with a -20% *situational*
        // penalty (firing into combat) and a halving in near-darkness resolves 65 / 2 = 33
        // (round up from 32.5), then 33 - 20 = 13%. The situational modifier is applied AFTER
        // the difficulty grade so its stated -20% is not itself halved to -10%.
        Modifier[] modifiers =
        [
            new AdditiveModifier("firing into combat", -20),
            DifficultyModifier.Difficult("darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(65), modifiers);

        Assert.Equal(Percent.Of(13), chain.EffectiveChance);
    }

    [Fact]
    public void Collapsing_additive_before_multiplicative_gives_the_rejected_answer()
    {
        // Proves ordering is read from the policy, not hardcoded in call order: moving
        // SituationalAdditive ahead of Multiplicative reproduces the ordering the book (and
        // ADR 0007) explicitly rejects -- the situational penalty gets halved along with the
        // base rating instead of applying to what the halved rating already is.
        var collapsedOrder = ModifierPolicy.Standard with
        {
            Stages =
            [
                ModifierStage.Gate,
                ModifierStage.Override,
                ModifierStage.PermanentAdditive,
                ModifierStage.SituationalAdditive,
                ModifierStage.Multiplicative,
                ModifierStage.Clamp,
            ],
        };

        Modifier[] modifiers =
        [
            new AdditiveModifier("firing into combat", -20),
            DifficultyModifier.Difficult("darkness"),
        ];

        var standard = ModifierPipeline.Evaluate(Percent.Of(65), modifiers);
        var collapsed = ModifierPipeline.Evaluate(Percent.Of(65), modifiers, collapsedOrder);

        Assert.Equal(Percent.Of(13), standard.EffectiveChance);
        Assert.Equal(Percent.Of(23), collapsed.EffectiveChance);
        Assert.NotEqual(standard.EffectiveChance, collapsed.EffectiveChance);
    }

    [Fact]
    public void A_permanent_additive_modifier_applies_before_the_difficulty_multiplier()
    {
        // Ch 5: a modifier integral to the rating is figured in before Difficult/Easy scales
        // it. 65 + 10 = 75, then 75 / 2 = 38 (round up from 37.5).
        Modifier[] modifiers =
        [
            new AdditiveModifier("marksmanship training", 10, AdditiveKind.Permanent),
            DifficultyModifier.Difficult("darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(65), modifiers);

        Assert.Equal(Percent.Of(38), chain.EffectiveChance);
    }

    [Fact]
    public void Permanent_and_situational_additive_modifiers_interleave_around_multiplicative()
    {
        // Exercises all three middle stages in one chain: permanent first (65 + 10 = 75),
        // then multiplicative (75 / 2 = 38, round up from 37.5), then situational last
        // (38 - 20 = 18).
        Modifier[] modifiers =
        [
            new AdditiveModifier("marksmanship training", 10, AdditiveKind.Permanent),
            new AdditiveModifier("firing into combat", -20),
            DifficultyModifier.Difficult("darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(65), modifiers);

        Assert.Equal(Percent.Of(18), chain.EffectiveChance);
        Assert.Equal(3, chain.Contributions.Count);
        Assert.Equal(Percent.Of(75), chain.Contributions[0].ResultingChance); // permanent first
        Assert.Equal(Percent.Of(38), chain.Contributions[1].ResultingChance); // then multiplicative
        Assert.Equal(Percent.Of(18), chain.Contributions[2].ResultingChance); // situational last
    }

    // ---- Difficulty is a state, not a stack ----------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Any_number_of_Difficult_sources_produce_exactly_one_halving(int sourceCount)
    {
        var modifiers = Enumerable.Range(1, sourceCount)
            .Select(i => DifficultyModifier.Difficult($"condition {i}"))
            .Cast<Modifier>()
            .ToArray();

        var chain = ModifierPipeline.Evaluate(Percent.Of(100), modifiers);

        Assert.Equal(Percent.Of(50), chain.EffectiveChance);
        // One collapsed step for the whole difficulty state, not one per source.
        Assert.Single(chain.Contributions);
    }

    [Fact]
    public void Easy_and_Difficult_cancel_pairwise_to_normal()
    {
        Modifier[] modifiers =
        [
            DifficultyModifier.Easy("streetwise contact"),
            DifficultyModifier.Difficult("near-darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(80), modifiers);

        Assert.Equal(Percent.Of(80), chain.EffectiveChance);
    }

    [Fact]
    public void Two_easy_and_one_difficult_net_to_a_single_easy_step_not_a_double_bonus()
    {
        // Net direction is the sign of the sum, not the sum itself: 2 Easy - 1 Difficult
        // nets +1, i.e. one doubling, never two.
        Modifier[] modifiers =
        [
            DifficultyModifier.Easy("home turf"),
            DifficultyModifier.Easy("plenty of time"),
            DifficultyModifier.Difficult("hostile crowd"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(30), modifiers);

        Assert.Equal(Percent.Of(60), chain.EffectiveChance);
    }

    // ---- Difficulty multiplier values live in policy data, not on the modifier ------------

    [Fact]
    public void Difficulty_multiplier_values_come_from_policy_not_from_the_modifier()
    {
        // DifficultyModifier carries no numerator/denominator of its own -- the multiplier a
        // net Easy or Difficult grade applies is declared on ModifierPolicy and read at
        // evaluation time, the same way ResolutionPolicy's threshold constants are read at
        // resolve time rather than baked into the roll. A custom policy proves the values
        // really are data: this is not the book's own grade, just a check that the plumbing
        // reads from the policy that was actually passed in.
        var customPolicy = ModifierPolicy.Standard with { DifficultNumerator = 1, DifficultDenominator = 5 };

        var chain = ModifierPipeline.Evaluate(
            Percent.Of(100), [DifficultyModifier.Difficult("severe hazard")], customPolicy);

        Assert.Equal(Percent.Of(20), chain.EffectiveChance); // 100 * 1/5, not the Standard 1/2
    }

    // ---- Gates short-circuit and consume no entropy ----------------------------------------

    [Fact]
    public void Impossible_takes_precedence_when_both_gate_kinds_are_asserted()
    {
        Modifier[] modifiers =
        [
            new GateModifier("no possible failure", GateKind.Automatic),
            new GateModifier("impassable obstacle", GateKind.Impossible),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(70), modifiers);

        Assert.Equal(GateKind.Impossible, chain.Gate);
        Assert.Equal(["impassable obstacle"], chain.GateSources);
    }

    [Fact]
    public void A_gated_chain_draws_no_roll_and_consumes_no_entropy()
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(70), [new GateModifier("impassable obstacle", GateKind.Impossible)]);
        var entropy = new FixedEntropySource(50);

        var outcome = chain.Resolve(Percent.Of(70), entropy);

        Assert.Null(outcome);
        Assert.Equal(0, entropy.DrawCount);
    }

    [Fact]
    public void An_ungated_chain_draws_exactly_one_roll()
    {
        var chain = ModifierPipeline.Evaluate(Percent.Of(50), []);
        var entropy = new FixedEntropySource(20);

        var outcome = chain.Resolve(Percent.Of(50), entropy);

        Assert.NotNull(outcome);
        Assert.Equal(20, outcome!.Roll);
        Assert.Equal(SuccessLevel.Success, outcome.Level);
        Assert.Equal(1, entropy.DrawCount);
    }

    // ---- The 5% floor keys on the passed printed base, not the chain's rating (#27) --------

    // The worked example from #27: Science (Forensics) at 40%, rolled Difficult with no lab
    // access (a -18 situational penalty). 40 halved is 20, less 18 is an effective 2%. Its
    // printed base is 01%, below the floor, so a roll of 01-05 must NOT be rescued. Resolving
    // the same chain against a printed base of 5% or higher WOULD be rescued on the same roll.
    // The two assertions together prove the floor reads Resolve's printedBaseChance argument,
    // not the chain's starting rating -- the exact conflation this method used to make.
    [Fact]
    public void Resolving_a_sub_5_percent_printed_base_is_not_rescued_on_01_to_05()
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(40),
            [DifficultyModifier.Difficult("no lab access"), new AdditiveModifier("no lab access", -18)]);
        Assert.Equal(Percent.Of(2), chain.EffectiveChance);

        // Printed base 01% (Science is printed at 01%): a roll of 03, inside 01-05, still fails.
        var unrescued = chain.Resolve(Percent.Of(1), new FixedEntropySource(3));
        Assert.Equal(SuccessLevel.Failure, unrescued!.Level);
    }

    [Fact]
    public void Resolving_a_5_percent_or_higher_printed_base_is_rescued_on_01_to_05()
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(40),
            [DifficultyModifier.Difficult("no lab access"), new AdditiveModifier("no lab access", -18)]);
        Assert.Equal(Percent.Of(2), chain.EffectiveChance);

        // Same chain, same roll, but a printed base of 25% (>= the 5% floor): 03 is rescued to
        // a Success by the floor, even though the effective chance is only 2%.
        var rescued = chain.Resolve(Percent.Of(25), new FixedEntropySource(3));
        Assert.Equal(SuccessLevel.Success, rescued!.Level);
    }

    // ---- Clamp floors at zero, but does not cap at 100 -------------------------------------

    [Fact]
    public void Clamp_floors_the_final_chance_at_zero()
    {
        var chain = ModifierPipeline.Evaluate(Percent.Of(10), [new AdditiveModifier("severe penalty", -50)]);

        Assert.Equal(Percent.Zero, chain.EffectiveChance);
    }

    [Fact]
    public void Clamp_does_not_cap_the_final_chance_at_one_hundred()
    {
        var chain = ModifierPipeline.Evaluate(Percent.Of(80), [DifficultyModifier.Easy("perfect conditions")]);

        Assert.Equal(Percent.Of(160), chain.EffectiveChance);
    }

    // ---- The 5% floor lives in the resolver, applied after this pipeline ------------------

    [Fact]
    public void The_five_percent_floor_still_rescues_a_roll_after_the_pipeline_crushes_the_chance()
    {
        // 10 -> (halved by darkness) 5 -> (situational -8, applied after the halving) floors
        // at 0. The resolver's 5%-floor rule still rescues a roll of 1-5 because it keys on
        // the unmodified base chance (10 >= 5), not on this crushed effective chance.
        Modifier[] modifiers =
        [
            new AdditiveModifier("severe penalty", -8),
            DifficultyModifier.Difficult("near-darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(10), modifiers);
        Assert.Equal(Percent.Zero, chain.EffectiveChance);

        var outcome = chain.Resolve(Percent.Of(10), new FixedEntropySource(5));

        Assert.NotNull(outcome);
        Assert.Equal(SuccessLevel.Success, outcome!.Level);
    }

    // ---- The chain renders its own derivation ----------------------------------------------

    [Fact]
    public void Render_matches_the_ADR_worked_example_exactly()
    {
        Modifier[] modifiers =
        [
            new AdditiveModifier("firing into combat", -20),
            DifficultyModifier.Difficult("darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(65), modifiers);

        Assert.Equal("65% → 33% (darkness ÷2) → 13% (firing into combat -20% [situational])", chain.Render());
    }

    [Fact]
    public void Render_distinguishes_permanent_from_situational_additive_steps()
    {
        Modifier[] modifiers =
        [
            new AdditiveModifier("marksmanship training", 10, AdditiveKind.Permanent),
            new AdditiveModifier("firing into combat", -20),
            DifficultyModifier.Difficult("darkness"),
        ];

        var chain = ModifierPipeline.Evaluate(Percent.Of(65), modifiers);

        Assert.Equal(
            "65% → 75% (marksmanship training +10% [permanent]) → 38% (darkness ÷2) "
            + "→ 18% (firing into combat -20% [situational])",
            chain.Render());
    }

    [Fact]
    public void Render_describes_a_gate_without_an_effective_chance()
    {
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(10), [new GateModifier("impassable obstacle", GateKind.Impossible)]);

        Assert.Equal("10% → Impossible (impassable obstacle)", chain.Render());
    }

    // ---- Guardrails -------------------------------------------------------------------------

    [Fact]
    public void A_non_positive_multiplier_denominator_throws_rather_than_dividing_by_zero()
    {
        var modifiers = new Modifier[] { new MultiplicativeModifier("broken", 1, 0) };

        Assert.Throws<ArgumentOutOfRangeException>(() => ModifierPipeline.Evaluate(Percent.Of(50), modifiers));
    }
}
