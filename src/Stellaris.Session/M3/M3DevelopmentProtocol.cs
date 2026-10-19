using System.Text.Json.Serialization;
using System.Text.Json;

namespace Stellaris.Session.M3;

// Wire contract для owner-scoped live construction/economy snapshots.
public sealed record M3DevelopmentGate(string Code, string DependencyId);
[JsonConverter(typeof(M3DevelopmentWireConverter))]
public sealed record M3DevelopmentView(string ContentProfileId, IReadOnlyList<M3DevelopmentColonyView> Colonies, IReadOnlyList<M3DevelopmentGate> Gates,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(M3SourceRecipeTableConverter))] IReadOnlyList<M3ConstructionSourceTermView>? SourceRecipeTerms = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<IReadOnlyList<string>>? CatalogSlotSets = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceRecipeEncoding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? SourceRecipeStrings = null);
public sealed record M3DevelopmentColonyView(string ColonyId, int? EconomyPlanetId, int OwnerParticipantId, long Revision, string AuthorityMode, bool IsMobile, string? CarrierId, string? LocationSystemId, IReadOnlyList<M3DevelopmentSlotView> Slots, IReadOnlyList<M3ConstructionCatalogView> Catalog, IReadOnlyList<M3ConstructionInstalledView> Installed, IReadOnlyList<M3ConstructionOrderView> Queue, IReadOnlyList<M3DevelopmentFeatureView> Features, IReadOnlyList<M3DevelopmentJobView> Jobs, IReadOnlyList<M3DevelopmentGate> Gates, string? HomeworldInitializerId = null, string? PlanetClass = null, int? PlanetSize = null, IReadOnlyList<M3StartupDistrictView>? StartupDistricts = null, IReadOnlyList<M3StartupBuildingView>? StartupBuildings = null, int? InitialPlanetSize = null, IReadOnlyList<M3StartupZoneView>? StartupZones = null, IReadOnlyList<M3StartupFeatureView>? StartupFeatures = null, IReadOnlyList<M3StartupDistrictCapView>? StartupDistrictCaps = null, M3DevelopmentEconomyView? Economy = null, M3ConstructionQueuePolicyView? QueuePolicy = null, M3ConstructionDistrictPoolView? DistrictPool = null, IReadOnlyList<M3ConstructionDistrictTypePoolView>? DistrictTypePools = null);
public sealed record M3ConstructionDistrictTypePoolView(string PolicyId, string DefinitionId, string SharedPoolId, bool Uncapped, int? Capacity, int Installed, int Reserved, int? Free, IReadOnlyList<string> MemberDefinitionIds);
public sealed record M3ConstructionDistrictPoolView(string PolicyId, int Capacity, int Installed, int Reserved, int Free);
public sealed record M3ConstructionQueuePolicyView(string PolicyId, int ActiveCapacity, bool CanReorder);
public sealed record M3DevelopmentEconomyView(string ProfileId, string AllocationPolicy, decimal Population, decimal BaseWorkforce, int? ReportMonth, IReadOnlyList<M3DevelopmentEconomyJobView> Jobs, IReadOnlyList<M3DevelopmentEconomyResourceView> Resources, IReadOnlyList<M3DevelopmentCoverageOmission>? CoverageOmissions = null,
    IReadOnlyList<M3DevelopmentJobDefinitionView>? JobDefinitions = null);
public sealed record M3DevelopmentJobDefinitionView(string JobId, string? CategoryId, string SourceSocialClassId, string? LocalizationKey, string? DescriptionKey, string? PluralLocalizationKey, string? ImageDefinitionKey, IReadOnlyList<M3DevelopmentGate> Gates);
public sealed record M3DevelopmentCoverageOmission(string DefinitionId, string JobId, string TermKind, string Reason);
public sealed record M3DevelopmentEconomyJobView(string JobId, string CategoryId, string? ImageDefinitionKey, decimal Capacity, decimal Limit, decimal? Assigned, decimal? BonusWorkforce, decimal? EffectiveWorkforce,
    string? SourceSocialClassId = null, string? SocialClassImageDefinitionKey = null, string? LocalizationKey = null, string? DescriptionKey = null, string? PluralLocalizationKey = null,
    IReadOnlyDictionary<string, decimal>? Produced = null, IReadOnlyDictionary<string, decimal>? Upkeep = null);
public sealed record M3DevelopmentEconomyResourceView(string ResourceId, string StoragePolicy, decimal? SourceMaximum, decimal Stock, decimal? Produced, decimal? JobUpkeep, decimal? BuildingUpkeep, decimal? DistrictUpkeep, decimal? ZoneUpkeep, decimal? Net, string? LocalizationKey = null, string? DescriptionKey = null, decimal? EffectiveMaximum = null);
public sealed record M3StartupDistrictView(string InstanceId, string DefinitionId, int Level, string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3StartupZoneView(string InstanceId, string DistrictInstanceId, string SlotInstanceId, string ZoneId, int SlotNumber, string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3StartupBuildingView(string InstanceId, string DefinitionId, string? SlotInstanceId, string? ZoneId, int Order, string? ImageDefinitionKey = null, IReadOnlyList<string>? CompatibleZoneInstanceIds = null, string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3StartupFeatureView(string InstanceId, string DefinitionId, bool IsBlocker, bool IsHidden, string? HiddenByBlockerInstanceId, string? HiddenFeatureInstanceId, int Order, string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3StartupDistrictCapView(string ModifierId, int Value);
public sealed record M3DevelopmentSlotView(string SlotId, string Kind, string? ReservedByOrderId, string? InstalledDefinitionId, IReadOnlyList<M3DevelopmentGate> Gates, string? ZoneInstanceId = null,
    string? DistrictInstanceId = null, string? ZoneSlotDefinitionId = null, IReadOnlyList<string>? RequirementLocalizationKeys = null);
public sealed record M3ConstructionCatalogView(string DefinitionId, string Kind, string? ImageDefinitionKey, string Eligibility, IReadOnlyList<string> SlotIds, M3ConstructionQuoteView? Quote, IReadOnlyList<M3DevelopmentGate> Gates,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? SourceTier = null, IReadOnlyList<string>? Prerequisites = null, IReadOnlyList<string>? UpgradesTo = null,
    IReadOnlyList<string>? UpgradesFrom = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? CompatibleSlotIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<M3ConstructionSourceTermView>? SourceRecipe = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? CanAfford = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UpgradeOperationGate = null,
    string? LocalizationKey = null, string? DescriptionKey = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? SourceRecipeIndices = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CompatibleSlotSetIndex = null);
public sealed record M3ConstructionSourceTermView(string Kind, string TargetId, decimal? Value, string? Expression,
    string ConditionDisposition, string ApplicationScope);

/// <summary>Versioned lossless string table; legacy inline metadata и tuple6 без encoding сохранены.</summary>
public sealed class M3DevelopmentWireConverter : JsonConverter<M3DevelopmentView>
{
    public const string ENCODING = "interned-strings-v1";
    private static string Name(JsonSerializerOptions options, string name) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;

    public override void Write(Utf8JsonWriter writer, M3DevelopmentView value, JsonSerializerOptions options)
    {
        if ((value.SourceRecipeTerms is null) != (value.CatalogSlotSets is null)) throw new JsonException("Shared catalog tables должны передаваться вместе.");
        if (value.SourceRecipeEncoding is not null && (value.SourceRecipeEncoding != ENCODING || value.SourceRecipeStrings is null || value.SourceRecipeTerms is null))
            throw new JsonException("Неверная source recipe encoding/table.");
        writer.WriteStartObject();
        writer.WriteString(Name(options, nameof(value.ContentProfileId)), value.ContentProfileId);
        writer.WritePropertyName(Name(options, nameof(value.Colonies))); JsonSerializer.Serialize(writer, value.Colonies, options);
        writer.WritePropertyName(Name(options, nameof(value.Gates))); JsonSerializer.Serialize(writer, value.Gates, options);
        if (value.CatalogSlotSets is not null) { writer.WritePropertyName(Name(options, nameof(value.CatalogSlotSets))); JsonSerializer.Serialize(writer, value.CatalogSlotSets, options); }
        if (value.SourceRecipeTerms is not null)
        {
            writer.WritePropertyName(Name(options, nameof(value.SourceRecipeTerms)));
            if (value.SourceRecipeEncoding is null) new M3SourceRecipeTableConverter().Write(writer, value.SourceRecipeTerms, options);
            else
            {
                var strings = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var index = 0; index < value.SourceRecipeStrings!.Count; index++) strings.TryAdd(value.SourceRecipeStrings[index], index);
                writer.WriteStartArray();
                foreach (var term in value.SourceRecipeTerms)
                {
                    writer.WriteStartArray(); writer.WriteNumberValue(strings[term.Kind]); writer.WriteNumberValue(strings[term.TargetId]);
                    if (term.Value is { } number) writer.WriteNumberValue(number); else writer.WriteNullValue();
                    if (term.Expression is { } expression) writer.WriteNumberValue(strings[expression]); else writer.WriteNullValue();
                    writer.WriteNumberValue(strings[term.ConditionDisposition]); writer.WriteNumberValue(strings[term.ApplicationScope]); writer.WriteEndArray();
                }
                writer.WriteEndArray();
                writer.WriteString(Name(options, nameof(value.SourceRecipeEncoding)), value.SourceRecipeEncoding);
                writer.WritePropertyName(Name(options, nameof(value.SourceRecipeStrings))); JsonSerializer.Serialize(writer, value.SourceRecipeStrings, options);
            }
        }
        writer.WriteEndObject();
    }

    public override M3DevelopmentView Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        JsonElement Field(string name) => root.GetProperty(Name(options, name));
        JsonElement? Optional(string name) => root.TryGetProperty(Name(options, name), out var field) && field.ValueKind != JsonValueKind.Null ? field : null;
        var encoding = Optional(nameof(M3DevelopmentView.SourceRecipeEncoding))?.GetString();
        var strings = Optional(nameof(M3DevelopmentView.SourceRecipeStrings))?.Deserialize<string[]>(options);
        if (encoding is not null && encoding != ENCODING || encoding == ENCODING && strings is null || encoding is null && strings is not null || strings?.Any(text => text is null) == true)
            throw new JsonException("Неизвестная source recipe encoding или отсутствующая string table.");
        IReadOnlyList<M3ConstructionSourceTermView>? terms = null;
        if (Optional(nameof(M3DevelopmentView.SourceRecipeTerms)) is { } table)
        {
            if (table.ValueKind != JsonValueKind.Array) throw new JsonException("Нужна source recipe table.");
            if (encoding is null)
            {
                var tableReader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(table.GetRawText())); tableReader.Read();
                terms = new M3SourceRecipeTableConverter().Read(ref tableReader, typeof(IReadOnlyList<M3ConstructionSourceTermView>), options);
            }
            else
            {
                string Text(JsonElement cell) => cell.ValueKind == JsonValueKind.Number && cell.TryGetInt32(out var index) && index >= 0 && index < strings!.Length
                    ? strings[index] : throw new JsonException("Source recipe string reference вне таблицы.");
                var restored = new List<M3ConstructionSourceTermView>();
                foreach (var row in table.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 6 || row[2].ValueKind is not (JsonValueKind.Null or JsonValueKind.Number)) throw new JsonException("Неверная source recipe row.");
                    restored.Add(new(Text(row[0]), Text(row[1]), row[2].ValueKind == JsonValueKind.Null ? null : row[2].GetDecimal(), row[3].ValueKind == JsonValueKind.Null ? null : Text(row[3]), Text(row[4]), Text(row[5])));
                }
                terms = Array.AsReadOnly(restored.ToArray());
            }
        }
        if (encoding is not null && terms is null) throw new JsonException("Source recipe encoding без term table.");
        var colonies = Field(nameof(M3DevelopmentView.Colonies)).Deserialize<M3DevelopmentColonyView[]>(options) ?? throw new JsonException("Нет colonies.");
        var slots = Optional(nameof(M3DevelopmentView.CatalogSlotSets))?.Deserialize<string[][]>(options);
        if ((terms is null) != (slots is null)) throw new JsonException("Shared catalog tables должны передаваться вместе.");
        foreach (var entry in colonies.SelectMany(colony => colony.Catalog))
        {
            if (entry.SourceRecipeIndices is { } indices && (terms is null || indices.Any(index => index < 0 || index >= terms.Count)) || terms is not null && entry.SourceRecipe is not null
                || entry.CompatibleSlotSetIndex is { } index && (slots is null || index < 0 || index >= slots.Length) || slots is not null && entry.CompatibleSlotIds is not null)
                throw new JsonException("Source recipe/slot reference неверна или смешана с inline metadata.");
        }
        return new(Field(nameof(M3DevelopmentView.ContentProfileId)).GetString()!, Array.AsReadOnly(colonies),
            Array.AsReadOnly(Field(nameof(M3DevelopmentView.Gates)).Deserialize<M3DevelopmentGate[]>(options)!), terms,
            slots is null ? null : Array.AsReadOnly(slots.Select(slot => (IReadOnlyList<string>)Array.AsReadOnly(slot)).ToArray()), encoding,
            strings is null ? null : Array.AsReadOnly(strings));
    }
}

/// <summary>Только shared table: шесть позиций вместо повторяющихся JSON имён каждого поля.</summary>
public sealed class M3SourceRecipeTableConverter : JsonConverter<IReadOnlyList<M3ConstructionSourceTermView>>
{
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<M3ConstructionSourceTermView> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var term in value)
        {
            writer.WriteStartArray(); writer.WriteStringValue(term.Kind); writer.WriteStringValue(term.TargetId);
            if (term.Value is { } number) writer.WriteNumberValue(number); else writer.WriteNullValue();
            writer.WriteStringValue(term.Expression); writer.WriteStringValue(term.ConditionDisposition); writer.WriteStringValue(term.ApplicationScope);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    public override IReadOnlyList<M3ConstructionSourceTermView> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Нужна таблица source recipe.");
        var terms = new List<M3ConstructionSourceTermView>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 6 || row[0].ValueKind != JsonValueKind.String || row[1].ValueKind != JsonValueKind.String
                || row[2].ValueKind is not (JsonValueKind.Null or JsonValueKind.Number) || row[3].ValueKind is not (JsonValueKind.Null or JsonValueKind.String)
                || row[4].ValueKind != JsonValueKind.String || row[5].ValueKind != JsonValueKind.String) throw new JsonException("Неверная source recipe row.");
            terms.Add(new(row[0].GetString()!, row[1].GetString()!, row[2].ValueKind == JsonValueKind.Null ? null : row[2].GetDecimal(), row[3].GetString(), row[4].GetString()!, row[5].GetString()!));
        }
        return Array.AsReadOnly(terms.ToArray());
    }
}
public sealed record M3ConstructionQuoteView(string PolicyId, IReadOnlyDictionary<string, decimal> Costs, decimal DurationDays, IReadOnlyDictionary<string, decimal> InfrastructureUpkeep, IReadOnlyDictionary<string, decimal> JobCapacity);
public sealed record M3ConstructionInstalledView(string SlotId, string DefinitionId, string Kind, string? ImageDefinitionKey, IReadOnlyDictionary<string, decimal> InfrastructureUpkeep, IReadOnlyDictionary<string, decimal> JobCapacity,
    IReadOnlyList<M3ConstructionUpgradeView>? Upgrades = null, string? LocalizationKey = null, string? DescriptionKey = null,
    bool? Enabled = null, M3ConstructionToggleView? Toggle = null, M3ConstructionDemolitionView? Demolition = null,
    IReadOnlyList<M3ConstructionZoneReplacementView>? ZoneReplacements = null);
public sealed record M3ConstructionRemovedBuildingView(string SlotId, string DefinitionId, string? LocalizationKey);
public sealed record M3ConstructionZoneReplacementView(string SourceDefinitionId, string DefinitionId, string SlotId, string ZoneInstanceId,
    long ExpectedColonyRevision, string PreviewId, IReadOnlyList<M3ConstructionRemovedBuildingView> RemovedBuildings,
    M3ConstructionQuoteView? Quote, bool? CanAfford, bool CanEnqueue, IReadOnlyList<M3DevelopmentGate> Gates,
    string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3ConstructionZoneReplacementOrderView(string SourceDefinitionId, string ZoneInstanceId, IReadOnlyList<M3ConstructionRemovedBuildingView> RemovedBuildings);
public sealed record M3ConstructionBuildingReplacementOrderView(string SourceDefinitionId, string ZoneInstanceId);
public sealed record M3ConstructionDemolitionView(bool CanDemolish, IReadOnlyList<M3DevelopmentGate> Gates);
public sealed record M3ConstructionToggleView(bool CanToggle, bool TargetEnabled, IReadOnlyList<M3DevelopmentGate> Gates);
public sealed record M3ConstructionUpgradeView(string SourceDefinitionId, string DefinitionId, string SlotId, string ZoneInstanceId, long ExpectedColonyRevision,
    M3ConstructionQuoteView? Quote, string Eligibility, bool? CanAfford, bool CanEnqueue, string? CommandType, IReadOnlyList<M3DevelopmentGate> Gates, string? ImageDefinitionKey = null, string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3ConstructionOrderView(string OrderId, string SlotId, string DefinitionId, string Kind, string Status, IReadOnlyDictionary<string, decimal> PaidCosts, decimal CompletedWork, decimal RequiredWork, string WorkUnit, M3ConstructionRefundView? RefundQuote, IReadOnlyList<M3DevelopmentGate> Gates,
    string? UpgradeFromDefinitionId = null, string? ZoneInstanceId = null, string? LocalizationKey = null, string? DescriptionKey = null, string? CancellationReason = null,
    M3ConstructionZoneReplacementOrderView? ZoneReplacement = null, string? ExecutionState = null,
    string? WorkEstimatePolicyId = null, decimal? DailyWork = null, decimal? RemainingWorkDays = null,
    M3ConstructionBuildingReplacementOrderView? BuildingReplacement = null);
public sealed record M3ConstructionRefundView(string PolicyId, IReadOnlyDictionary<string, decimal> Amounts,
    string? PreviewId = null, IReadOnlyDictionary<string, decimal>? Losses = null);
public sealed record M3DevelopmentFeatureView(string InstanceId, string DefinitionId, string? ImageDefinitionKey, bool Clearable, string? ClearanceDefinitionId, IReadOnlyList<M3DevelopmentGate> Gates, string? ClearanceSlotId = null, string? LocalizationKey = null, string? DescriptionKey = null);
public sealed record M3DevelopmentJobView(string JobId, string CategoryId, string? ImageDefinitionKey, decimal? Capacity, decimal? Assigned, decimal? Workforce, IReadOnlyList<M3DevelopmentGate> Gates, string? LocalizationKey = null, string? DescriptionKey = null);
