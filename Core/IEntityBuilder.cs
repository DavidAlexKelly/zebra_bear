using System.Text.Json.Nodes;
using ZebraBear.Entities;

namespace ZebraBear.Core;

/// <summary>
/// Plugin interface for entity types that can be loaded from JSON.
///
/// To add a new entity type without touching GameLoader:
///   1. Create a class that implements IEntityBuilder.
///   2. Register it once at startup:
///        EntityRegistry.Register(new MyEntityBuilder());
///   3. Add objects of that type to rooms.json using the matching "type" value.
///
/// Example:
///   public class PillarBuilder : IEntityBuilder
///   {
///       public string TypeName => "pillar";
///       public Entity Build(JsonNode node, string name, string[] dialogue) { ... }
///   }
/// </summary>
public interface IEntityBuilder
{
    /// <summary>
    /// The value of the "type" field in rooms.json that triggers this builder.
    /// Must be unique. E.g. "pillar", "billboard", "shelf".
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Construct and return an Entity from the JSON node.
    /// name and dialogue are pre-parsed for convenience.
    /// Return null to silently skip (e.g. unsupported platform variant).
    /// </summary>
    Entity Build(JsonNode node, string name, string[] dialogue);
}