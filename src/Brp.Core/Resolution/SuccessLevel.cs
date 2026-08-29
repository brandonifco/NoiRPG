namespace Brp.Core.Resolution;

/// <summary>
/// The five degrees of success for an action roll, per Ch 5: System, "Evaluating Success or
/// Failure" (BRP ORC Content Document, p.127): "There are five degrees of success for any
/// type of action roll. Ranked from worst to best, they are as follows: Fumble, Failure,
/// Success, Special Success, Critical Success."
/// <para>
/// The underlying values encode that ranking, so ordinary comparison operators order and
/// compare grades correctly. This matters beyond display: opposed rolls (Issue #12) decide
/// their winner by comparing two <see cref="SuccessLevel"/> values, so the order has to be
/// meaningful, not incidental.
/// </para>
/// </summary>
public enum SuccessLevel
{
    /// <summary>The worst possible result. Ch 5, "Fumble".</summary>
    Fumble = 0,

    /// <summary>An ordinary failed roll. Ch 5, "Failure".</summary>
    Failure = 1,

    /// <summary>An ordinary successful roll. Ch 5, "Success".</summary>
    Success = 2,

    /// <summary>A better-than-average success. Ch 5, "Special Success".</summary>
    Special = 3,

    /// <summary>The best possible result. Ch 5, "Critical Success".</summary>
    Critical = 4,
}
