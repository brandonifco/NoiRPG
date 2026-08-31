using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Brp.Core.Primitives;
using Brp.Core.Randomness;

namespace Brp.Core.Dice;

/// <summary>
/// Optional context threaded into a roll. <see cref="DamageBonus"/> supplies the value
/// substituted for the <c>db</c> token (Ch 2's damage bonus modifier). If omitted, <c>db</c>
/// and its halved forms evaluate to zero.
/// </summary>
/// <param name="DamageBonus">
/// The damage bonus expression, e.g. <c>1D4</c> or <c>-1D4</c>. Rolled fresh -- consuming its
/// own entropy draws -- every time a <c>db</c> or half-<c>db</c> term is evaluated.
/// </param>
public readonly record struct DiceContext(DiceExpression? DamageBonus = null);

/// <summary>
/// An immutable, parsed dice notation expression. Parse once, evaluate many times against
/// different <see cref="IEntropySource"/> draws -- the expression itself carries no state and
/// consumes no entropy on its own.
/// <para>
/// Grammar: an optionally-signed first term, followed by zero or more explicitly signed terms.
/// Each term is one of a dice group (<c>NdM</c>, count defaults to 1), a flat integer constant,
/// the damage-bonus token <c>db</c>, or a halved damage-bonus token (<c>db/2</c> or <c>½db</c>).
/// Parsing is case-insensitive and tolerant of whitespace anywhere in the string. See Ch 1
/// (dice notation) and Ch 2 (damage bonus) of the source book.
/// </para>
/// </summary>
public sealed class DiceExpression
{
    private const int MaxDiceCount = 1000;

    // Matches one signed term starting at the current position. Alternatives are ordered so
    // the longer, more specific forms (db/2, ½db) are tried before the bare "db" they would
    // otherwise be truncated to.
    private static readonly Regex TokenPattern = new(
        @"\G(?<sign>[+-])?(?<term>DB/2|½DB|DB|\d*D\d+|\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DicePattern = new(
        @"^(?<count>\d*)D(?<sides>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HalfDamageBonusPattern = new(
        @"^(?:DB/2|½DB)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DamageBonusPattern = new(
        @"^DB$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IReadOnlyList<Term> _terms;

    private DiceExpression(string notation, IReadOnlyList<Term> terms)
    {
        Notation = notation;
        _terms = terms;
    }

    /// <summary>
    /// The expression's normalized rendering: uppercase <c>D</c>, explicit dice counts, an
    /// explicit sign on every term after the first, and <c>DB/2</c> as the canonical spelling
    /// for either halved-damage-bonus form. Re-parsing this string always yields the same
    /// <see cref="Notation"/> back.
    /// </summary>
    public string Notation { get; }

    /// <summary>Parses dice notation, throwing on anything invalid.</summary>
    /// <exception cref="FormatException">
    /// The notation is empty, malformed, uses a zero-sided die, or exceeds the dice-count cap.
    /// </exception>
    public static DiceExpression Parse(string notation)
    {
        if (!TryParseCore(notation, out var expression, out var error))
        {
            throw new FormatException(error);
        }

        return expression!;
    }

    /// <summary>Attempts to parse dice notation, returning <see langword="false"/> instead of throwing.</summary>
    public static bool TryParse(string notation, out DiceExpression? expression) =>
        TryParseCore(notation, out expression, out _);

    /// <summary>
    /// Evaluates the expression against <paramref name="entropy"/>, consuming one entropy draw
    /// per die (rejection sampling may consume more), including any dice rolled indirectly
    /// through <see cref="DiceContext.DamageBonus"/> when a <c>db</c> term is present.
    /// </summary>
    public DiceRoll Roll(IEntropySource entropy, DiceContext context = default)
    {
        ArgumentNullException.ThrowIfNull(entropy);

        var results = new List<DiceTermResult>(_terms.Count);
        var rawTotal = 0;
        foreach (var term in _terms)
        {
            var result = term.Evaluate(entropy, context);
            results.Add(result);
            rawTotal += result.Value;
        }

        var total = Math.Max(0, rawTotal);
        return new DiceRoll(total, rawTotal, results);
    }

    /// <summary>
    /// The highest total this expression can produce, with no entropy consumed -- e.g. Ch 6:
    /// Combat, "Critical Success" (p.146): "the maximum possible damage for the weapon used
    /// (6 for 1D6, 9 for 1D8+1, etc.)". Dice terms contribute their highest face when positively
    /// signed and their lowest (1) when negatively signed, since a negative term's least-negative
    /// contribution to the total is what a die of 1 gives it.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The expression contains a <c>db</c> or half-<c>db</c> term. Weapon damage notation never
    /// embeds these (the damage bonus is rolled and added separately -- Ch 6, p.147 footnote
    /// **), so this is a caller-misuse guard rather than a rule this type needs to model.
    /// </exception>
    public int MaximumPossible() => _terms.Sum(term => term.MaximumPossible());

    /// <summary>
    /// The lowest total this expression can produce (before the zero floor <see cref="Roll"/>
    /// applies to a rolled total), with no entropy consumed -- e.g. Ch 7: Spot Rules, "Knockout
    /// Attacks" (p.174): "the target is dealt the minimum damage for the weapon."
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The expression contains a <c>db</c> or half-<c>db</c> term. See <see cref="MaximumPossible"/>.
    /// </exception>
    public int MinimumPossible() => _terms.Sum(term => term.MinimumPossible());

    private static bool TryParseCore(string? notation, out DiceExpression? expression, out string error)
    {
        expression = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(notation))
        {
            error = "Dice notation must not be empty.";
            return false;
        }

        var stripped = Regex.Replace(notation, @"\s+", string.Empty);
        var terms = new List<Term>();
        var position = 0;
        var isFirst = true;

        while (position < stripped.Length)
        {
            var match = TokenPattern.Match(stripped, position);
            if (!match.Success || match.Index != position)
            {
                error = $"'{notation}' is not a valid dice expression: unrecognized text at '{stripped[position..]}'.";
                return false;
            }

            var signGroup = match.Groups["sign"];
            if (!signGroup.Success && !isFirst)
            {
                error = $"'{notation}' is not a valid dice expression: term '{match.Groups["term"].Value}' needs an explicit + or - sign.";
                return false;
            }

            var sign = signGroup.Success && signGroup.Value == "-" ? -1 : 1;

            if (!TryBuildTerm(sign, match.Groups["term"].Value, out var term, out error))
            {
                return false;
            }

            terms.Add(term);
            position += match.Length;
            isFirst = false;
        }

        if (terms.Count == 0)
        {
            error = $"'{notation}' is not a valid dice expression.";
            return false;
        }

        expression = new DiceExpression(BuildNotation(terms), terms);
        return true;
    }

    private static bool TryBuildTerm(int sign, string text, out Term term, out string error)
    {
        term = null!;
        error = string.Empty;

        if (HalfDamageBonusPattern.IsMatch(text))
        {
            term = new Term(TermKind.HalfDamageBonus, sign, 0, 0, 0);
            return true;
        }

        if (DamageBonusPattern.IsMatch(text))
        {
            term = new Term(TermKind.DamageBonus, sign, 0, 0, 0);
            return true;
        }

        var diceMatch = DicePattern.Match(text);
        if (diceMatch.Success)
        {
            var countText = diceMatch.Groups["count"].Value;
            long count = 1;
            if (countText.Length > 0 &&
                !long.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count))
            {
                error = $"Dice count '{countText}' is too large.";
                return false;
            }

            var sidesText = diceMatch.Groups["sides"].Value;
            if (!long.TryParse(sidesText, NumberStyles.None, CultureInfo.InvariantCulture, out var sides))
            {
                error = $"Die sides '{sidesText}' is too large.";
                return false;
            }

            if (count < 1)
            {
                error = $"Dice count must be at least 1 (got {count}).";
                return false;
            }

            if (count > MaxDiceCount)
            {
                error = $"Dice count {count} exceeds the maximum of {MaxDiceCount}.";
                return false;
            }

            if (sides < 1 || sides > int.MaxValue)
            {
                error = $"A die must have at least 1 side (got {sides}).";
                return false;
            }

            term = new Term(TermKind.Dice, sign, (int)count, (int)sides, 0);
            return true;
        }

        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var constant) &&
            constant <= int.MaxValue)
        {
            term = new Term(TermKind.Constant, sign, 0, 0, (int)constant);
            return true;
        }

        error = $"'{text}' is not a recognized dice term.";
        return false;
    }

    private static string BuildNotation(IReadOnlyList<Term> terms)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < terms.Count; i++)
        {
            var term = terms[i];
            if (i == 0)
            {
                if (term.Sign < 0)
                {
                    builder.Append('-');
                }
            }
            else
            {
                builder.Append(term.Sign < 0 ? '-' : '+');
            }

            builder.Append(term.Bare);
        }

        return builder.ToString();
    }

    private enum TermKind
    {
        Dice,
        Constant,
        DamageBonus,
        HalfDamageBonus,
    }

    private sealed record Term(TermKind Kind, int Sign, int Count, int Sides, int Constant)
    {
        public string Bare => Kind switch
        {
            TermKind.Dice => $"{Count}D{Sides}",
            TermKind.Constant => Constant.ToString(CultureInfo.InvariantCulture),
            TermKind.DamageBonus => "DB",
            TermKind.HalfDamageBonus => "DB/2",
            _ => throw new InvalidOperationException($"Unknown term kind {Kind}."),
        };

        public DiceTermResult Evaluate(IEntropySource entropy, DiceContext context)
        {
            var notation = Sign < 0 ? $"-{Bare}" : Bare;

            switch (Kind)
            {
                case TermKind.Dice:
                    {
                        var faces = new List<int>(Count);
                        var sum = 0;
                        for (var i = 0; i < Count; i++)
                        {
                            var face = entropy.NextDie(Sides);
                            faces.Add(face);
                            sum += face;
                        }

                        return new DiceTermResult(notation, Sign * sum, faces);
                    }

                case TermKind.Constant:
                    return new DiceTermResult(notation, Sign * Constant, Array.Empty<int>());

                case TermKind.DamageBonus:
                    {
                        var (rawTotal, faces) = RollDamageBonus(context.DamageBonus, entropy);
                        return new DiceTermResult(notation, Sign * rawTotal, faces);
                    }

                case TermKind.HalfDamageBonus:
                    {
                        var (rawTotal, faces) = RollDamageBonus(context.DamageBonus, entropy);
                        var half = Rounding.Divide(rawTotal, 2, RoundingMode.Up);
                        return new DiceTermResult(notation, Sign * half, faces);
                    }

                default:
                    throw new InvalidOperationException($"Unknown term kind {Kind}.");
            }
        }

        public int MaximumPossible() => Kind switch
        {
            TermKind.Dice => Sign > 0 ? Sign * Count * Sides : Sign * Count,
            TermKind.Constant => Sign * Constant,
            _ => throw new NotSupportedException(
                $"'{Bare}' has no context-free maximum -- damage-bonus terms are rolled and added separately."),
        };

        public int MinimumPossible() => Kind switch
        {
            TermKind.Dice => Sign > 0 ? Sign * Count : Sign * Count * Sides,
            TermKind.Constant => Sign * Constant,
            _ => throw new NotSupportedException(
                $"'{Bare}' has no context-free minimum -- damage-bonus terms are rolled and added separately."),
        };

        private static (int RawTotal, IReadOnlyList<int> Faces) RollDamageBonus(
            DiceExpression? damageBonus, IEntropySource entropy)
        {
            if (damageBonus is null)
            {
                return (0, Array.Empty<int>());
            }

            // A fresh, db-less context: a damage bonus expression referencing "db" itself
            // (which should not happen in ruleset data) evaluates that inner db as zero
            // rather than recursing.
            var roll = damageBonus.Roll(entropy);
            var faces = roll.Terms.SelectMany(t => t.Faces).ToArray();
            return (roll.RawTotal, faces);
        }
    }
}
