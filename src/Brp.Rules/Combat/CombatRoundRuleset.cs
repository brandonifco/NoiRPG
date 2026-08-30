namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined parameters the combat round and its DEX-rank ordering read (AGENTS.md
/// invariant 7: rules values are data, not constants). Every value is sourced or marked as a
/// house interpretation on its own member below. Loaded from
/// <c>combat-round-ruleset.json</c> by <c>Brp.Data.NoirCombatRoundRuleset.Load()</c>.
/// </summary>
public sealed class CombatRoundRuleset
{
    /// <summary>Creates a combat-round ruleset from data-defined values.</summary>
    public CombatRoundRuleset(
        IReadOnlyList<CombatRoundPhase> phases,
        string dexRankSourceCharacteristic,
        bool dexRankOrderedDescending,
        IReadOnlyList<WeaponTypeTier> weaponTypeTiebreakOrder,
        IReadOnlyList<MovementTier> movementTiers,
        int drawWeaponDexRankPenalty,
        int multipleActionDexRankPenalty,
        int dexRankFloor)
    {
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentException.ThrowIfNullOrWhiteSpace(dexRankSourceCharacteristic);
        ArgumentNullException.ThrowIfNull(weaponTypeTiebreakOrder);
        ArgumentNullException.ThrowIfNull(movementTiers);
        ArgumentOutOfRangeException.ThrowIfLessThan(drawWeaponDexRankPenalty, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(multipleActionDexRankPenalty, 0);

        if (phases.Count == 0)
        {
            throw new ArgumentException("A combat round must have at least one phase.", nameof(phases));
        }

        if (weaponTypeTiebreakOrder.Count == 0)
        {
            throw new ArgumentException(
                "The weapon-type tiebreak order must not be empty.", nameof(weaponTypeTiebreakOrder));
        }

        Phases = phases;
        DexRankSourceCharacteristic = dexRankSourceCharacteristic;
        DexRankOrderedDescending = dexRankOrderedDescending;
        WeaponTypeTiebreakOrder = weaponTypeTiebreakOrder;
        MovementTiers = movementTiers;
        DrawWeaponDexRankPenalty = drawWeaponDexRankPenalty;
        MultipleActionDexRankPenalty = multipleActionDexRankPenalty;
        DexRankFloor = dexRankFloor;
    }

    /// <summary>
    /// Ch 6, "Combat Round Phases" (p.142), with the book's Powers phase omitted -- see
    /// <see cref="CombatRoundPhase"/>'s remarks and <c>docs/decisions/0015-combat-round.md</c>.
    /// </summary>
    public IReadOnlyList<CombatRoundPhase> Phases { get; }

    /// <summary>
    /// Ch 6, p.142: "your character can perform actions ... in an order usually determined by
    /// their DEX characteristic." Corroborated by the spellcasting examples (Ch 4: Powers, p.57 --
    /// cut from this codebase per the scope filter, but still useful for reading the mechanic):
    /// "a magician with DEX 15 ... casts at DEX rank 14" (DEX − 1), which only works arithmetically
    /// if DEX rank starts numerically
    /// equal to the DEX characteristic itself, not a derived or scaled value. This field records
    /// which characteristic that identity mapping reads; it is metadata for provenance, not a
    /// formula -- callers supply the already-read DEX value to <see cref="EffectiveDexRankCalculator"/>.
    /// </summary>
    public string DexRankSourceCharacteristic { get; }

    /// <summary>
    /// Ch 6, p.142: "higher DEX characters act before characters with lower DEX." The book states
    /// no alternative ordering direction; this flag exists so the direction is read from data
    /// (invariant 7) rather than hardcoded into the sort in <see cref="CombatRound"/>.
    /// </summary>
    public bool DexRankOrderedDescending { get; }

    /// <summary>
    /// Ch 6, "Action" (p.143), in tiebreak-priority order: missile, then long, then medium, then
    /// short/unarmed. See <see cref="WeaponTypeTier"/> for why this is four tiers, not five.
    /// </summary>
    public IReadOnlyList<WeaponTypeTier> WeaponTypeTiebreakOrder { get; }

    /// <summary>
    /// Ch 6, "Move" (p.144): the 6-15m (1/2 DEX rank) and 16-29m (1/4 DEX rank) movement tiers.
    /// </summary>
    public IReadOnlyList<MovementTier> MovementTiers { get; }

    /// <summary>
    /// Ch 6, "Noncombat Action" (p.144): "An unengaged character can attempt the use of a skill or
    /// power or do some other action not requiring a skill check, such as drawing a weapon or
    /// opening a door. ... These actions, if combined with combat actions or multiple non-combat
    /// actions, incur a DEX rank penalty of 5 per action." The book states one number (5) for both
    /// combining a noncombat action (e.g. drawing a weapon) with another action and for spacing
    /// successive attacks (see <see cref="MultipleActionDexRankPenalty"/>); this ruleset keeps them
    /// as separate data fields -- both sourced to the same printed value today -- so a future
    /// ruleset could diverge them without this being mistaken for two independently-stated rules.
    /// </summary>
    public int DrawWeaponDexRankPenalty { get; }

    /// <summary>
    /// Ch 6, "Attack" (p.144): "If your character can perform more than one action in a round
    /// (some weapons allow for multiple attacks, and combat skill levels in excess of 100% also
    /// allow multiple attacks), each attack should be separated by 5 DEX ranks. The first action
    /// is at the full DEX rank; the second is at DEX rank -5; the third at DEX rank -10; etc."
    /// This ruleset models only the spacing arithmetic; what triggers a combatant having more than
    /// one action (multi-attack weapons, &gt;100% skill) is out of scope for this piece -- see
    /// <c>docs/decisions/0015-combat-round.md</c>'s seam note for piece C.
    /// </summary>
    public int MultipleActionDexRankPenalty { get; }

    /// <summary>
    /// Ch 6, "Attack" (p.144): "Your character cannot act on DEX rank 0, so any actions that would
    /// occur below DEX rank 1 are lost." The floor value itself (0) is unactable, and every rank at
    /// or below it is lost -- see <see cref="EffectiveDexRankCalculator.Compute"/>.
    /// </summary>
    public int DexRankFloor { get; }
}
