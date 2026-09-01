using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 8: Equipment, "Poisons" (p.221) -- the named poison/drug catalog feeds its POT straight
/// into the existing Ch 7 poison machinery (<see cref="PoisonResolver"/>/<see cref="PoisonRuleset"/>)
/// with no parallel mechanic: a catalog entry's <see cref="PoisonCatalogEntry.Potency"/> is
/// exactly the <c>poisonPotency</c> argument <see cref="PoisonResolver.ResolvePoison"/> already
/// takes. Issue #231 (modern drugs, POT vs CON on the resistance table).
/// </summary>
public class PoisonCatalogTests
{
    private static readonly PoisonRuleset Poison = NoirInjuryRuleset.Load().Poison;

    [Fact]
    public void A_catalog_entrys_potency_resolves_through_the_existing_poison_resolver()
    {
        var catalog = NoirPoisonCatalog.Load();
        var sleepingPills = catalog.EntryById(new PoisonId("sleepingPills"));

        // POT 6 vs CON 10 => chance 30; roll 20 succeeds (overcomes CON, full POT damage).
        var outcome = PoisonResolver.ResolvePoison(
            poisonPotency: sleepingPills.Potency,
            constitution: 10,
            effectiveAntidotePotency: 0,
            Poison,
            new FixedEntropySource(20));

        Assert.True(outcome.Overcame);
        Assert.Equal(sleepingPills.Potency, outcome.Damage);
    }

    [Fact]
    public void A_catalog_entrys_potency_resolves_to_half_pot_when_resisted()
    {
        var catalog = NoirPoisonCatalog.Load();
        var curare = catalog.EntryById(new PoisonId("curare"));

        // POT 25 vs CON 20 => chance 75; roll 80 fails (does not overcome).
        var outcome = PoisonResolver.ResolvePoison(
            poisonPotency: curare.Potency,
            constitution: 20,
            effectiveAntidotePotency: 0,
            Poison,
            new FixedEntropySource(80));

        Assert.False(outcome.Overcame);
        Assert.Equal(13, outcome.Damage); // half of 25, rounded up.
    }

    [Fact]
    public void Every_catalog_entry_has_a_positive_potency_valid_for_the_poison_resolver()
    {
        var catalog = NoirPoisonCatalog.Load();

        foreach (var entry in catalog.Entries.Values)
        {
            Assert.True(entry.Potency > 0);
        }
    }
}
