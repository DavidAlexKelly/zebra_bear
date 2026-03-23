using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using ZebraBear.Entities;

namespace ZebraBear.Core;

/// <summary>
/// Registry of all entity builders known to the current game.
///
/// The engine registers nothing here by default. The game calls
/// ZebraBearEntities.Register() at startup to populate it.
/// </summary>
public static class EntityRegistry
{
    private static readonly Dictionary<string, IEntityBuilder> _builders =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register a builder. Overwrites any existing builder with the same TypeName.
    /// Call before loading any rooms.
    /// </summary>
    public static void Register(IEntityBuilder builder)
    {
        _builders[builder.TypeName] = builder;
        Console.WriteLine($"[EntityRegistry] Registered '{builder.TypeName}'.");
    }

    /// <summary>
    /// Build an entity from a JSON node.
    /// Returns null (with a warning) if no builder is registered for the type.
    /// </summary>
    public static Entity Build(JsonNode node, string name)
    {
        var type = node["type"]?.GetValue<string>() ?? "";

        if (_builders.TryGetValue(type, out var builder))
            return builder.Build(node, name);

        Console.WriteLine($"[EntityRegistry] No builder registered for type '{type}' " +
                          $"(name='{name}'). Add it to ZebraBearEntities.Register().");
        return null;
    }
}