using System.Reflection;
using Brp.Core;

namespace Brp.Core.Tests;

/// <summary>
/// Guards the structural invariants from AGENTS.md. These are not style checks —
/// a game-engine dependency in the core makes the engine unusable for the
/// gamemaster tooling, and ambient randomness makes every roll unreproducible.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Core = typeof(AssemblyMarker).Assembly;

    [Theory]
    [InlineData("UnityEngine")]
    [InlineData("Godot")]
    [InlineData("MonoGame")]
    [InlineData("Microsoft.Xna")]
    public void Core_takes_no_game_engine_dependency(string forbidden)
    {
        var referenced = Core.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Core_targets_the_expected_framework()
    {
        var tfm = Core
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?
            .FrameworkName;

        Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
    }
}
