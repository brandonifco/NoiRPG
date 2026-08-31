namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values for Ch 6: Combat, "Major Wounds" and "Fatal Wounds" (pp.155-156) that
/// <see cref="MajorWoundResolver"/> reads (AGENTS.md invariant 7: rules values are data, not
/// constants). Loaded from <c>major-wound-ruleset.json</c> by
/// <c>Brp.Data.NoirMajorWoundRuleset.Load()</c>. See <c>docs/decisions/0021-major-wounds.md</c>.
/// <para>
/// Deliberately does <em>not</em> carry the major-wound <em>threshold</em> (half of total hit
/// points): that figure already exists, tested, at Layer 1 as
/// <see cref="Core.Abilities.AbilitySet.MajorWoundLevel"/> (Ch 2, p.14, rounded up), which the
/// resolver reuses directly. Nor does it carry the shock collapse threshold or the death threshold:
/// those are the unconscious/dead hit-point levels on <see cref="DamageRuleset"/> (Ch 2, p.13),
/// reused so a single source defines each.
/// </para>
/// </summary>
public sealed class MajorWoundRuleset
{
    /// <summary>Creates a major wound ruleset from data-defined values.</summary>
    public MajorWoundRuleset(MajorWoundTable table, int fatalWoundRescueWindowRounds, int collapseUnconsciousHours)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegative(fatalWoundRescueWindowRounds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(collapseUnconsciousHours);

        Table = table;
        FatalWoundRescueWindowRounds = fatalWoundRescueWindowRounds;
        CollapseUnconsciousHours = collapseUnconsciousHours;
    }

    /// <summary>Ch 6, "Major Wounds Table" (pp.155-156): the 1D100 characteristic-loss table.</summary>
    public MajorWoundTable Table { get; }

    /// <summary>
    /// Ch 6, "Fatal Wounds" (p.156): a fatally wounded character survives if medical attention brings
    /// their hit points to 1 or more "in the round they received the fatal wound or the round
    /// immediately after." This is the extra-round offset after the wound round (1 = the round
    /// immediately after), so the rescue window is rounds 0 through this value inclusive. See
    /// <see cref="MajorWoundResolver.SurvivesFatalWound"/>.
    /// </summary>
    public int FatalWoundRescueWindowRounds { get; }

    /// <summary>
    /// Ch 6, "Major Wounds" (p.155): a character at 2 or fewer hit points after a major wound
    /// "collapses immediately from shock and loss of blood and is unconscious for an hour." The
    /// duration of that collapse, in hours.
    /// </summary>
    public int CollapseUnconsciousHours { get; }
}
