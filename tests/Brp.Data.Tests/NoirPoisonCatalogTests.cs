using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Reproduces the printed Sample Poisons Table (Ch 8: Equipment, "Poisons", p.221) cell by
/// cell, so a transcription error surfaces as a failing row rather than hiding inside a loop
/// (`docs/source-handling.md`, "the discipline"). Also pins the column-misalignment errata
/// correction for Arsenic and Rattlesnake venom (see <c>poison-catalog.json</c>'s
/// <c>source.errata</c> field for the bbox/render evidence).
/// </summary>
public class NoirPoisonCatalogTests
{
    public static TheoryData<string, string, string, int, string?> Entries => new()
    {
        // id, name, speedOfEffect, potency, symptoms
        { "arsenic", "Arsenic", "½ to 24 hours", 16, null },
        { "belladonna", "Belladonna", "2 hours to 2 days", 16, "Rapid heartbeat, impaired vision, convulsions." },
        { "blackWidowVenom", "Black Widow venom", "2–8 days", 7, "Chills, sweating, nausea." },
        { "cobraVenom", "Cobra venom", "15–60 minutes", 16, "Convulsions, respiratory failure." },
        { "curare", "Curare", "1 combat round", 25, "Muscular paralysis, respiratory failure." },
        { "cyanide", "Cyanide", "1–15 minutes", 20, "Dizziness, convulsions, fainting." },
        { "rattlesnakeVenom", "Rattlesnake venom", "15–60 minutes", 10, null },
        { "scorpionVenom", "Scorpion venom", "24–48 hours", 9, "Intense pain, weakness, hemorrhaging." },
        {
            "sleepingPills", "Sleeping pills", "10–30 minutes", 6,
            "Normal sleep; each additional dose increases chance of respiratory failure by +5%."
        },
        { "strychnine", "Strychnine", "10–20 minutes", 20, "Violent muscle contractions, asphyxiation." },
    };

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_sample_poison_reproduces_its_printed_row(
        string id, string name, string speedOfEffect, int potency, string? symptoms)
    {
        var catalog = NoirPoisonCatalog.Load();

        var entry = catalog.EntryById(new PoisonId(id));

        Assert.Equal(name, entry.Name);
        Assert.Equal(speedOfEffect, entry.SpeedOfEffect);
        Assert.Equal(potency, entry.Potency);
        Assert.Equal(symptoms, entry.Symptoms);
    }

    [Fact]
    public void Exactly_the_ten_printed_rows_load()
    {
        var catalog = NoirPoisonCatalog.Load();

        var ids = catalog.Entries.Keys.Select(id => id.Value).ToHashSet();

        Assert.Equal(
            new HashSet<string>
            {
                "arsenic", "belladonna", "blackWidowVenom", "cobraVenom", "curare",
                "cyanide", "rattlesnakeVenom", "scorpionVenom", "sleepingPills", "strychnine",
            },
            ids);
        Assert.Equal(10, catalog.Entries.Count);
    }

    [Theory]
    [InlineData("arsenic")]
    [InlineData("rattlesnakeVenom")]
    public void The_two_column_misaligned_rows_are_corrected_to_a_bare_pot_number_not_a_time_range(string id)
    {
        // Errata: the printed table drops these two rows' Symptoms text and shifts the
        // following column's value into that cell -- see poison-catalog.json's
        // "source.errata" for the bbox/render evidence. The corrected POT must be a bare
        // number (not left as the misprinted time-range/blank), and Symptoms must be null,
        // matching what the book actually prints for these two cells.
        var catalog = NoirPoisonCatalog.Load();

        var entry = catalog.EntryById(new PoisonId(id));

        Assert.True(entry.Potency > 0);
        Assert.Null(entry.Symptoms);
        Assert.False(string.IsNullOrWhiteSpace(entry.SpeedOfEffect));
    }

    [Fact]
    public void Sleeping_pills_is_the_tables_one_manufactured_modern_drug_entry()
    {
        // Issue #231: modern drugs modeled with the existing poison machinery. The Sample
        // Poisons Table's other nine rows are natural toxins and venoms; Sleeping Pills is the
        // table's one manufactured/pharmaceutical entry.
        var catalog = NoirPoisonCatalog.Load();

        var sleepingPills = catalog.EntryById(new PoisonId("sleepingPills"));

        Assert.Equal(6, sleepingPills.Potency);
        Assert.Contains("respiratory failure", sleepingPills.Symptoms, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_entry_carries_a_non_empty_source_citation()
    {
        var catalog = NoirPoisonCatalog.Load();

        foreach (var entry in catalog.Entries.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Source));
        }
    }
}
