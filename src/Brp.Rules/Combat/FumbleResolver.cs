using Brp.Core.Contests;
using Brp.Core.Randomness;
using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves a fumble by rolling D100 on the appropriate one of Ch 6's four fumble tables
/// (pp.148-149) and returning the structured consequence. A fumble is a
/// <see cref="Core.Resolution.SuccessLevel.Fumble"/> the resolution kernel (#10) has already
/// determined; this piece (F) turns that into a named outcome. It is the fumble-table half of the
/// combat surface whose flag <see cref="AttackDefenseOutcome.AttackerRollsOnFumbleTable"/> /
/// <see cref="AttackDefenseOutcome.DefenderRollsOnFumbleTable"/> raises.
/// <para>
/// Deliberately data-driven (AGENTS.md invariant 7): the tables' rows live in
/// <see cref="FumbleRuleset"/>; this resolver only selects the table, draws rolls, walks the reroll
/// chain, and reports the ally/fallback branch. It applies nothing -- dropping a weapon, weapon
/// hit-point loss, a movement penalty, hitting an ally or oneself are all structured outcomes a
/// caller applies, the same seam #50/#96 use, because no encounter model lives in this layer.
/// </para>
/// </summary>
public static class FumbleResolver
{
    /// <summary>
    /// Selects which of the four fumble tables applies, from the existing combat context: the
    /// fumbling combatant's <see cref="WeaponClass"/> and the <see cref="DefenseType"/> of the
    /// fumbled roll. A natural (unarmed) weapon always uses the combined Natural table; a missile
    /// weapon always uses the Missile Attack table (missile weapons have no parry table); a melee
    /// weapon uses the Attack table for an attack and the Parry table for a parry.
    /// </summary>
    /// <param name="weaponClass">
    /// The class of weapon the fumbling combatant was using (Ch 8, "Weapon Classes", p.196).
    /// <see cref="WeaponClass.Brawl"/> is the natural/unarmed case; the firearm and thrown classes
    /// are missile weapons; <see cref="WeaponClass.Club"/> and <see cref="WeaponClass.Dagger"/> are
    /// melee weapons.
    /// </param>
    /// <param name="defense">
    /// The defensive action the fumbled roll was, or <see cref="DefenseType.None"/> if the fumbled
    /// roll was an attack. Only <see cref="DefenseType.Parry"/> has a fumble table; a fumbled
    /// <see cref="DefenseType.Dodge"/> has none printed and is rejected.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A missile weapon with a parry (missiles cannot parry), or any weapon with a dodge (no dodge
    /// fumble table exists).
    /// </exception>
    public static FumbleTable SelectTable(WeaponClass weaponClass, DefenseType defense)
    {
        if (defense == DefenseType.Dodge)
        {
            throw new ArgumentException(
                "No fumble table covers a dodge; Ch 6 prints attack/parry tables only (pp.148-149).",
                nameof(defense));
        }

        if (weaponClass == WeaponClass.Brawl)
        {
            // The Natural table combines attack and parry, so the action does not matter.
            return FumbleTable.Natural;
        }

        if (IsMissile(weaponClass))
        {
            if (defense == DefenseType.Parry)
            {
                throw new ArgumentException(
                    "A missile weapon cannot parry; Ch 6 prints no missile parry fumble table (p.148).",
                    nameof(defense));
            }

            return FumbleTable.MissileAttack;
        }

        // A melee weapon (Club, Dagger): the action selects attack vs. parry.
        return defense == DefenseType.Parry ? FumbleTable.MeleeParry : FumbleTable.MeleeAttack;
    }

    /// <summary>
    /// Resolves a fumble by selecting the table from <paramref name="weaponClass"/> and
    /// <paramref name="defense"/> (see <see cref="SelectTable"/>) and rolling on it. A convenience
    /// over <see cref="Resolve(FumbleTable, FumbleRuleset, IEntropySource, IFumbleAdjudicator)"/>.
    /// </summary>
    public static FumbleResolution Resolve(
        WeaponClass weaponClass,
        DefenseType defense,
        FumbleRuleset ruleset,
        IEntropySource entropy,
        IFumbleAdjudicator adjudicator) =>
        Resolve(SelectTable(weaponClass, defense), ruleset, entropy, adjudicator);

    /// <summary>
    /// Rolls D100 on the given <paramref name="table"/> and returns the structured consequence,
    /// following the "blow it" (99) and "blow it badly" (00) reroll chain cumulatively and resolving
    /// each hit-ally / weapon-hit-point row's branch. Each roll -- initial and reroll -- consumes one
    /// D100 draw from <paramref name="entropy"/>; the "use result NN-NN" fallbacks consume none (they
    /// reference an existing row, they do not reroll). The <paramref name="adjudicator"/> answers the
    /// <see cref="FumbleDecisionId.AllyInRange"/> call for the hit-ally rows.
    /// </summary>
    public static FumbleResolution Resolve(
        FumbleTable table,
        FumbleRuleset ruleset,
        IEntropySource entropy,
        IFumbleAdjudicator adjudicator)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentNullException.ThrowIfNull(adjudicator);

        var consequences = ruleset.ForTable(table);
        var steps = new List<FumbleStep>();

        // Iterative reroll accounting: start with one roll to make; a "blow it"/"blow it badly" row
        // adds its reroll count to the outstanding total, so a reroll that lands on 99/00 again
        // accumulates further rolls -- the book's "cumulative if rolled again."
        var rollsRemaining = 1;
        while (rollsRemaining > 0)
        {
            rollsRemaining--;

            var roll = entropy.NextD100();
            var row = consequences.ForRoll(roll);
            steps.Add(new FumbleStep(roll, row, ResolveBranch(row, consequences, adjudicator)));

            if (row.Kind == FumbleEffectKind.Reroll)
            {
                rollsRemaining += row.RerollCount
                    ?? throw new InvalidOperationException("A reroll row is missing its reroll count.");
            }
        }

        return new FumbleResolution(table, steps);
    }

    private static FumbleBranchSelection? ResolveBranch(
        FumbleConsequenceRow row, FumbleConsequenceTable table, IFumbleAdjudicator adjudicator)
    {
        if (row.Fallback is not { } fallback)
        {
            return null;
        }

        var fallbackRow = table.ForRoll(fallback.MinimumRoll);
        bool? primaryApplies = fallback.Condition switch
        {
            // "hit nearest ally ... or use result NN-NN if no ally nearby" -- the ally-in-range call.
            FumbleFallbackCondition.NoAllyNearby => adjudicator.IsAllyInRange(),

            // "do 1D6 damage to the weapon's hit points (or use 81-85 if the weapon has no hit
            // points)" -- weapon hit points are not modeled here, so the caller decides; both
            // branches are reported.
            FumbleFallbackCondition.WeaponHasNoHitPoints => null,

            _ => throw new ArgumentOutOfRangeException(
                nameof(row), fallback.Condition, "Unknown fumble fallback condition."),
        };

        return new FumbleBranchSelection(fallback.Condition, primaryApplies, fallbackRow);
    }

    private static bool IsMissile(WeaponClass weaponClass) => weaponClass switch
    {
        WeaponClass.Missile or WeaponClass.Pistol or WeaponClass.Revolver or WeaponClass.Rifle
            or WeaponClass.Shotgun or WeaponClass.SubmachineGun => true,
        WeaponClass.Brawl or WeaponClass.Club or WeaponClass.Dagger => false,
        _ => throw new ArgumentOutOfRangeException(nameof(weaponClass), weaponClass, "Unknown weapon class."),
    };
}
