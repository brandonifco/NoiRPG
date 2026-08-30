namespace Brp.Rules.Advancement;

/// <summary>
/// Which gate <see cref="ExperienceSystem.RecordUse"/> applies to a real-stakes check. Names
/// and behavior are kept in step with <c>tools/advancement_sim.py</c>'s two simulated
/// variants ("A RAW (tick on success)" and "B tick on use") so the simulation stays a valid
/// sanity check against this implementation.
/// </summary>
public enum ExperiencePolicy
{
    /// <summary>
    /// NoiRPG's locked default (`noir-rpg-framework.md` v0.2, `AGENTS.md`): a real-stakes
    /// check ticks whether it succeeded or failed. A deliberate house-rule deviation from
    /// BRP RAW -- validated by <c>tools/advancement_sim.py</c> across 10,000 simulated
    /// characters -- adopted because RAW ticks are "nearly invisible at video-game length"
    /// and "starve low skills, which rarely succeed and so rarely tick."
    /// </summary>
    TickOnUse,

    /// <summary>
    /// Ch 5: System, "Skill Improvement" (p.138): "If a skill is used successfully, you
    /// almost always get an experience check" -- a tick requires success. The RAW toggle,
    /// kept so <c>tools/advancement_sim.py</c>'s "RAW" scenario stays reproducible.
    /// </summary>
    RawTickOnSuccess,
}
