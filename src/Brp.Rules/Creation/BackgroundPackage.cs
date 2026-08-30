using Brp.Core.Skills;

namespace Brp.Rules.Creation;

/// <summary>
/// A Freeform Profession package: a data-defined name plus a professional skill-point
/// allocation, applied during creation exactly like the book's named professions
/// ("Professions A Through Z", pp.17 onward -- "Your character will spend their professional
/// skill points on these skills"), except the skill list and point spend are player/author
/// -defined rather than one of the book's printed entries. Sourced: the "Freeform Professions
/// (Option)" checklist entry (p.229): "Useful for customized, difficult-to-categorize player
/// characters" -- the book gives no further mechanic beyond that description, so a freeform
/// package reuses the ordinary professional-skill-point mechanic rather than inventing a new
/// one. The actual noir packages (ex-cop, ex-journalist, etc.) are Layer 5 content authored
/// against this mechanism; this type and its loaded data are the mechanism only.
/// </summary>
/// <param name="Name">The package's display name.</param>
/// <param name="ProfessionalSkillPoints">
/// Points from the professional skill-point pool pre-assigned to specific skills, keyed by
/// canonical <see cref="SkillId"/>. Must not exceed the ruleset's professional skill-point
/// total; <see cref="CharacterBuilder"/> enforces this at apply time, alongside the pool's
/// remaining budget for any further profession points the builder allocates.
/// </param>
public sealed record BackgroundPackage(string Name, IReadOnlyDictionary<SkillId, int> ProfessionalSkillPoints);
