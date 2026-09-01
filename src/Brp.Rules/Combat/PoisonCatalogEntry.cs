namespace Brp.Rules.Combat;

/// <summary>
/// One named entry of Ch 8: Equipment, "Poisons" -- the Sample Poisons Table (p.221): a real
/// or manufactured substance (poison, venom, or -- Sleeping Pills -- a modern drug) with the
/// POT rating <see cref="PoisonResolver"/> matches against a target's CON. This is a data
/// catalog only; it introduces no new mechanic. Resolving one dose is
/// <c>PoisonResolver.ResolvePoison(entry.Potency, constitution, ...)</c>, the same path that
/// resolves any other poison (<c>docs/decisions/0019-injury-spot-rules.md</c>).
/// </summary>
/// <param name="Id">The stable ruleset identifier.</param>
/// <param name="Name">The display name, as printed in the book.</param>
/// <param name="SpeedOfEffect">
/// The printed onset-to-effect time range (e.g. "10-30 minutes"), as flavor/pacing text for the
/// gamemaster. Free text, not a parsed duration -- the mechanical onset delay a poison actually
/// uses is the generic fast/slow default from <see cref="PoisonRuleset"/>
/// (<see cref="PoisonResolver.ResolveOnset"/>), which this table does not override per-entry.
/// </param>
/// <param name="Potency">The POT rating matched against CON on the resistance table.</param>
/// <param name="Symptoms">
/// The printed symptoms/flavor text, or <see langword="null"/> where the book's own table
/// prints no symptoms for the entry (see <see cref="Source"/> for the two rows affected by the
/// table's column-misalignment misprint).
/// </param>
/// <param name="Source">The book table (and any correction note) this entry was transcribed from.</param>
public sealed record PoisonCatalogEntry(
    PoisonId Id,
    string Name,
    string SpeedOfEffect,
    int Potency,
    string? Symptoms,
    string Source);
