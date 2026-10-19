using System.Collections.Frozen;

namespace Stellaris.Core.Economy;

/// <summary>Фактический неизменяемый генотип; не ссылка на редактируемый пресет.</summary>
public sealed class PopulationSpeciesDefinition
{
    public string ClassId { get; }
    public string ArchetypeId { get; }
    public IReadOnlySet<string> Traits { get; }

    public PopulationSpeciesDefinition(string classId, string archetypeId, IEnumerable<string> traits)
    {
        ClassId = Id(classId);
        ArchetypeId = Id(archetypeId);
        ArgumentNullException.ThrowIfNull(traits);
        var copied = traits.Take(257).Select(Id).ToArray();
        if (copied.Length > 256) throw new ArgumentException("Species traits exceed source budget.", nameof(traits));
        Traits = copied.ToFrozenSet(StringComparer.Ordinal);
    }

    private static string Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
        ? value : throw new ArgumentException("Invalid species source identifier.");
}

/// <summary>Зарегистрированный вид мира; принадлежность населения задаётся ссылками групп.</summary>
public sealed class PopulationSpeciesSnapshot
{
    public EntityId Id { get; }
    public EntityId OwnerCountry { get; }
    public PopulationSpeciesDefinition Definition { get; }
    internal PopulationSpeciesSnapshot(EntityId id, EntityId ownerCountry, PopulationSpeciesDefinition definition)
    { Id = id; OwnerCountry = ownerCountry; Definition = definition; }
}
