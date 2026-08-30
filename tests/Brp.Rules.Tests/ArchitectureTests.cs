using System.Reflection;
using Brp.Rules.Characters;

namespace Brp.Rules.Tests;

/// <summary>
/// Guards AGENTS.md invariant 6: <c>Brp.Rules</c> takes no game-engine dependency.
/// Mirrors <c>Brp.Core.Tests.ArchitectureTests</c>.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Rules = typeof(Character).Assembly;

    [Theory]
    [InlineData("UnityEngine")]
    [InlineData("Godot")]
    [InlineData("MonoGame")]
    [InlineData("Microsoft.Xna")]
    public void Rules_takes_no_game_engine_dependency(string forbidden)
    {
        var referenced = Rules.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rules_targets_the_expected_framework()
    {
        var tfm = Rules
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?
            .FrameworkName;

        Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
    }

    [Fact]
    public void Character_carries_no_spendable_power_point_pool()
    {
        // Scope cut, `orc-scope-filter.md` "Chapter 4: Powers": POW remains a
        // characteristic, but no spendable power-point / Fate Point reservoir exists on
        // the type. A property or field whose name mentions power points would be the
        // most likely place such a reservoir crept back in.
        var members = typeof(Character).GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name);

        Assert.DoesNotContain(members, name =>
            name.Contains("PowerPoint", StringComparison.OrdinalIgnoreCase)
            || name.Contains("FatePoint", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PowerPool", StringComparison.OrdinalIgnoreCase));
    }
}
