using System.Xml.Linq;

namespace LivingWorld.Core;

public static class WorldStateCodec
{
    public static string Serialize(WorldState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var snapshot = state.CreateSnapshot();
        var document = new XDocument(
            new XElement(
                "LivingWorldState",
                new XAttribute("version", "1"),
                new XAttribute("worldSeed", snapshot.WorldSeed),
                new XAttribute("currentTick", snapshot.CurrentTick),
                new XElement(
                    "Settlements",
                    snapshot.Settlements.Select(settlement =>
                        new XElement(
                            "Settlement",
                            IdAttributes(settlement.Id),
                            new XAttribute("slug", settlement.Slug),
                            new XAttribute("name", settlement.Name),
                            new XAttribute("factionId", settlement.FactionId)))),
                new XElement(
                    "Citizens",
                    snapshot.Citizens.Select(citizen =>
                        new XElement(
                            "Citizen",
                            IdAttributes(citizen.Id),
                            new XAttribute("name", citizen.Name),
                            new XAttribute("age", citizen.Age),
                            new XAttribute("sex", citizen.Sex),
                            new XAttribute("profession", citizen.Profession),
                            new XAttribute("settlementKind", citizen.SettlementId.Kind),
                            new XAttribute("settlementId", citizen.SettlementId.Value),
                            new XAttribute("status", citizen.Status)))),
                new XElement(
                    "Armies",
                    snapshot.Armies.Select(army =>
                        new XElement(
                            "Army",
                            IdAttributes(army.Id),
                            new XAttribute("name", army.Name),
                            new XAttribute("factionId", army.FactionId),
                            new XAttribute("sourceSettlementKind", army.SourceSettlementId.Kind),
                            new XAttribute("sourceSettlementId", army.SourceSettlementId.Value)))),
                new XElement(
                    "MigrationGroups",
                    snapshot.MigrationGroups.Select(group =>
                        new XElement(
                            "MigrationGroup",
                            IdAttributes(group.Id),
                            new XAttribute("sourceSettlementKind", group.SourceSettlementId.Kind),
                            new XAttribute("sourceSettlementId", group.SourceSettlementId.Value),
                            group.TargetSettlementId.HasValue
                                ? new XAttribute("targetSettlementKind", group.TargetSettlementId.Value.Kind)
                                : null,
                            group.TargetSettlementId.HasValue
                                ? new XAttribute("targetSettlementId", group.TargetSettlementId.Value.Value)
                                : null,
                            new XAttribute("factionId", group.FactionId),
                            new XAttribute("createdTick", group.CreatedTick),
                            new XAttribute("arrivalTick", group.ArrivalTick),
                            new XAttribute("status", group.Status),
                            new XAttribute("reason", group.Reason)))),
                new XElement(
                    "IntelReports",
                    snapshot.IntelReports.Select(report =>
                        new XElement(
                            "IntelReport",
                            IdAttributes(report.Id),
                            new XAttribute("sourceKind", report.SourceKind),
                            new XAttribute("factionId", report.FactionId),
                            new XAttribute("tick", report.Tick),
                            new XAttribute("valueScore", report.ValueScore),
                            new XAttribute("summary", report.Summary)))),
                new XElement(
                    "KnownSettlementInfos",
                    snapshot.KnownSettlementInfos.Select(info =>
                        new XElement(
                            "KnownSettlementInfo",
                            new XAttribute("settlementKind", info.SettlementId.Kind),
                            new XAttribute("settlementId", info.SettlementId.Value),
                            new XAttribute("sourceKind", info.SourceKind),
                            new XAttribute("tick", info.Tick),
                            new XAttribute("confidence", info.Confidence),
                            new XAttribute("populationBand", info.PopulationBand),
                            new XAttribute("food", info.Food),
                            new XAttribute("migration", info.Migration),
                            new XAttribute("production", info.Production),
                            new XAttribute("exactValuesVisible", info.ExactValuesVisible),
                            new XAttribute("summary", info.Summary)))),
                new XElement(
                    "RaidOpportunities",
                    snapshot.RaidOpportunities.Select(opportunity =>
                        new XElement(
                            "RaidOpportunity",
                            IdAttributes(opportunity.Id),
                            new XAttribute("factionId", opportunity.FactionId),
                            new XAttribute("intelReportKind", opportunity.IntelReportId.Kind),
                            new XAttribute("intelReportId", opportunity.IntelReportId.Value),
                            new XAttribute("status", opportunity.Status),
                            new XAttribute("combatantDemand", opportunity.CombatantDemand),
                            new XAttribute("reason", opportunity.Reason)))),
                new XElement(
                    "RaidPawnLinks",
                    snapshot.RaidPawnLinks.Select(link =>
                        new XElement(
                            "RaidPawnLink",
                            new XAttribute("pawnThingId", link.PawnThingId),
                            new XAttribute("citizenKind", link.CitizenId.Kind),
                            new XAttribute("citizenId", link.CitizenId.Value),
                            new XAttribute("armyKind", link.ArmyId.Kind),
                            new XAttribute("armyId", link.ArmyId.Value),
                            new XAttribute("status", link.Status)))),
                new XElement(
                    "RaidOutcomes",
                    snapshot.RaidOutcomes.Select(outcome =>
                        new XElement(
                            "RaidOutcome",
                            new XAttribute("armyKind", outcome.ArmyId.Kind),
                            new XAttribute("armyId", outcome.ArmyId.Value),
                            new XAttribute("sourceSettlementKind", outcome.SourceSettlementId.Kind),
                            new XAttribute("sourceSettlementId", outcome.SourceSettlementId.Value),
                            new XAttribute("factionId", outcome.FactionId),
                            new XAttribute("tick", outcome.Tick),
                            new XAttribute("sent", outcome.Sent),
                            new XAttribute("active", outcome.Active),
                            new XAttribute("dead", outcome.Dead),
                            new XAttribute("returned", outcome.Returned),
                            new XAttribute("prisoner", outcome.Prisoner),
                            new XAttribute("missing", outcome.Missing)))),
                new XElement(
                    "ProductionProfiles",
                    snapshot.ProductionProfiles.Select(profile =>
                        new XElement(
                            "ProductionProfile",
                            new XAttribute("settlementKind", profile.SettlementId.Kind),
                            new XAttribute("settlementId", profile.SettlementId.Value),
                            new XAttribute("biome", profile.Biome),
                            new XAttribute("hilliness", profile.Hilliness),
                            new XAttribute("techLevel", profile.TechLevel),
                            new XAttribute("growingDays", profile.GrowingDays),
                            new XAttribute("rainfall", profile.Rainfall),
                            new XAttribute("averageTemperature", profile.AverageTemperature),
                            new XAttribute("foodPerAdult", profile.FoodPerAdult),
                            new XAttribute("steelPerAdult", profile.SteelPerAdult),
                            new XAttribute("medicinePerAdult", profile.MedicinePerAdult),
                            new XAttribute("componentPerAdult", profile.ComponentPerAdult)))),
                new XElement(
                    "FactionRecords",
                    snapshot.FactionRecords.Select(record =>
                        new XElement(
                            "FactionRecord",
                            new XAttribute("factionId", record.FactionId),
                            new XAttribute("status", record.Status),
                            new XAttribute("tick", record.Tick),
                            new XAttribute("reason", record.Reason)))),
                new XElement(
                    "Ownership",
                    snapshot.Ownership.Select(ownership =>
                        new XElement(
                            "Owner",
                            new XAttribute("assetKind", ownership.AssetId.Kind),
                            new XAttribute("assetId", ownership.AssetId.Value),
                            new XAttribute("ownerKind", ownership.OwnerId.Kind),
                            new XAttribute("ownerId", ownership.OwnerId.Value)))),
                new XElement(
                    "Resources",
                    snapshot.Resources.Select(resource =>
                        new XElement(
                            "Resource",
                            new XAttribute("ownerKind", resource.OwnerId.Kind),
                            new XAttribute("ownerId", resource.OwnerId.Value),
                            new XAttribute("resourceKey", resource.ResourceKey),
                            new XAttribute("quantity", resource.Quantity)))),
                new XElement(
                    "Events",
                    snapshot.Events.Select(worldEvent =>
                        new XElement(
                            "Event",
                            IdAttributes(worldEvent.Id),
                            new XAttribute("eventKind", worldEvent.Kind),
                            new XAttribute("tick", worldEvent.Tick),
                            worldEvent.SubjectId.HasValue
                                ? new XAttribute("subjectKind", worldEvent.SubjectId.Value.Kind)
                                : null,
                            worldEvent.SubjectId.HasValue
                                ? new XAttribute("subjectId", worldEvent.SubjectId.Value.Value)
                                : null,
                            new XAttribute("summary", worldEvent.Summary)))),
                new XElement(
                    "Drifters",
                    snapshot.Drifters.Select(drifter =>
                        new XElement(
                            "Drifter",
                            IdAttributes(drifter.Id),
                            new XAttribute("name", drifter.Name),
                            new XAttribute("age", drifter.Age),
                            new XAttribute("sex", drifter.Sex),
                            new XAttribute("arrivalTick", drifter.ArrivalTick),
                            new XAttribute("combatAptitude", drifter.CombatAptitude),
                            new XAttribute("organizationAptitude", drifter.OrganizationAptitude)))),
                new XElement(
                    "ArmyMovements",
                    state.ArmyMovements
                        .OrderBy(movement => movement.ArmyId.Value)
                        .Select(movement =>
                            new XElement(
                                "Movement",
                                new XAttribute("armyKind", movement.ArmyId.Kind),
                                new XAttribute("armyId", movement.ArmyId.Value),
                                new XAttribute("targetKind", movement.TargetSettlementId.Kind),
                                new XAttribute("targetId", movement.TargetSettlementId.Value),
                                new XAttribute("departTick", movement.DepartTick),
                                new XAttribute("arrivalTick", movement.ArrivalTick),
                                new XAttribute("status", movement.Status)))),
                new XElement(
                    "FactionBehaviors",
                    state.FactionBehaviors
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair =>
                            new XElement(
                                "FactionBehavior",
                                new XAttribute("factionId", pair.Key),
                                new XAttribute("behavior", pair.Value))))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    public static WorldState Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Living World state payload cannot be empty.", nameof(payload));
        }

        var root = XDocument.Parse(payload).Root
            ?? throw new InvalidOperationException("Living World state payload has no root element.");

        var snapshot = new WorldStateSnapshot(
            RequiredInt(root, "worldSeed"),
            RequiredInt(root, "currentTick"),
            RequiredContainer(root, "Settlements")
                .Elements("Settlement")
                .Select(element => new WorldSettlement(
                    ReadId(element),
                    RequiredString(element, "slug"),
                    RequiredString(element, "name"),
                    RequiredString(element, "factionId")))
                .ToList(),
            RequiredContainer(root, "Citizens")
                .Elements("Citizen")
                .Select(element => new WorldCitizen(
                    ReadId(element),
                    RequiredString(element, "name"),
                    RequiredInt(element, "age"),
                    RequiredEnum<Sex>(element, "sex"),
                    RequiredString(element, "profession"),
                    ReadEntityId(element, "settlementKind", "settlementId"),
                    RequiredEnum<CitizenStatus>(element, "status")))
                .ToList(),
            RequiredContainer(root, "Armies")
                .Elements("Army")
                .Select(element => new WorldArmy(
                    ReadId(element),
                    RequiredString(element, "name"),
                    RequiredString(element, "factionId"),
                    ReadEntityId(element, "sourceSettlementKind", "sourceSettlementId")))
                .ToList(),
            OptionalContainer(root, "MigrationGroups")
                .Elements("MigrationGroup")
                .Select(element => new WorldMigrationGroup(
                    ReadId(element),
                    ReadEntityId(element, "sourceSettlementKind", "sourceSettlementId"),
                    TryReadEntityId(element, "targetSettlementKind", "targetSettlementId"),
                    RequiredString(element, "factionId"),
                    RequiredInt(element, "createdTick"),
                    RequiredInt(element, "arrivalTick"),
                    RequiredEnum<MigrationGroupStatus>(element, "status"),
                    RequiredString(element, "reason")))
                .ToList(),
            OptionalContainer(root, "IntelReports")
                .Elements("IntelReport")
                .Select(element => new WorldIntelReport(
                    ReadId(element),
                    RequiredEnum<IntelSourceKind>(element, "sourceKind"),
                    RequiredString(element, "factionId"),
                    RequiredInt(element, "tick"),
                    RequiredInt(element, "valueScore"),
                    RequiredString(element, "summary")))
                .ToList(),
            OptionalContainer(root, "KnownSettlementInfos")
                .Elements("KnownSettlementInfo")
                .Select(element => new KnownSettlementInfo(
                    ReadEntityId(element, "settlementKind", "settlementId"),
                    RequiredEnum<IntelSourceKind>(element, "sourceKind"),
                    RequiredInt(element, "tick"),
                    RequiredEnum<KnowledgeConfidence>(element, "confidence"),
                    RequiredEnum<SettlementPopulationBand>(element, "populationBand"),
                    RequiredEnum<SettlementFoodKnowledge>(element, "food"),
                    RequiredEnum<SettlementMigrationKnowledge>(element, "migration"),
                    OptionalEnum(element, "production", SettlementProductionKnowledge.Unknown),
                    RequiredBool(element, "exactValuesVisible"),
                    RequiredString(element, "summary")))
                .ToList(),
            OptionalContainer(root, "RaidOpportunities")
                .Elements("RaidOpportunity")
                .Select(element => new RaidOpportunity(
                    ReadId(element),
                    RequiredString(element, "factionId"),
                    ReadEntityId(element, "intelReportKind", "intelReportId"),
                    RequiredEnum<RaidOpportunityStatus>(element, "status"),
                    RequiredInt(element, "combatantDemand"),
                    RequiredString(element, "reason")))
                .ToList(),
            OptionalContainer(root, "RaidPawnLinks")
                .Elements("RaidPawnLink")
                .Select(element => new RaidPawnLink(
                    RequiredInt(element, "pawnThingId"),
                    ReadEntityId(element, "citizenKind", "citizenId"),
                    ReadEntityId(element, "armyKind", "armyId"),
                    RequiredEnum<RaidPawnLinkStatus>(element, "status")))
                .ToList(),
            OptionalContainer(root, "RaidOutcomes")
                .Elements("RaidOutcome")
                .Select(element => new WorldRaidOutcome(
                    ReadEntityId(element, "armyKind", "armyId"),
                    ReadEntityId(element, "sourceSettlementKind", "sourceSettlementId"),
                    RequiredString(element, "factionId"),
                    RequiredInt(element, "tick"),
                    RequiredInt(element, "sent"),
                    RequiredInt(element, "active"),
                    RequiredInt(element, "dead"),
                    RequiredInt(element, "returned"),
                    RequiredInt(element, "prisoner"),
                    OptionalInt(element, "missing", 0)))
                .ToList(),
            OptionalContainer(root, "ProductionProfiles")
                .Elements("ProductionProfile")
                .Select(element => new SettlementProductionProfile(
                    ReadEntityId(element, "settlementKind", "settlementId"),
                    RequiredString(element, "biome"),
                    RequiredString(element, "hilliness"),
                    RequiredString(element, "techLevel"),
                    RequiredInt(element, "growingDays"),
                    RequiredInt(element, "rainfall"),
                    RequiredInt(element, "averageTemperature"),
                    RequiredInt(element, "foodPerAdult"),
                    RequiredInt(element, "steelPerAdult"),
                    RequiredInt(element, "medicinePerAdult"),
                    RequiredInt(element, "componentPerAdult")))
                .ToList(),
            OptionalContainer(root, "FactionRecords")
                .Elements("FactionRecord")
                .Select(element => new WorldFactionRecord(
                    RequiredString(element, "factionId"),
                    RequiredEnum<WorldFactionStatus>(element, "status"),
                    RequiredInt(element, "tick"),
                    RequiredString(element, "reason")))
                .ToList(),
            RequiredContainer(root, "Ownership")
                .Elements("Owner")
                .Select(element => new OwnershipRecord(
                    ReadEntityId(element, "assetKind", "assetId"),
                    ReadEntityId(element, "ownerKind", "ownerId")))
                .ToList(),
            RequiredContainer(root, "Resources")
                .Elements("Resource")
                .Select(element => new ResourceStack(
                    ReadEntityId(element, "ownerKind", "ownerId"),
                    RequiredString(element, "resourceKey"),
                    RequiredInt(element, "quantity")))
                .ToList(),
            RequiredContainer(root, "Events")
                .Elements("Event")
                .Select(element => new WorldEvent(
                    ReadId(element),
                    RequiredEnum<WorldEventKind>(element, "eventKind"),
                    RequiredInt(element, "tick"),
                    TryReadEntityId(element, "subjectKind", "subjectId"),
                    RequiredString(element, "summary")))
                .ToList(),
            OptionalContainer(root, "Drifters")
                .Elements("Drifter")
                .Select(element => new Drifter(
                    ReadId(element),
                    RequiredString(element, "name"),
                    RequiredInt(element, "age"),
                    RequiredEnum<Sex>(element, "sex"),
                    RequiredInt(element, "arrivalTick"),
                    OptionalInt(element, "combatAptitude", 0),
                    OptionalInt(element, "organizationAptitude", 0)))
                .ToList());

        var state = WorldState.FromSnapshot(snapshot);

        foreach (var element in OptionalContainer(root, "ArmyMovements").Elements("Movement"))
        {
            state.RestoreArmyMovementForLedger(new WorldArmyMovement(
                ReadEntityId(element, "armyKind", "armyId"),
                ReadEntityId(element, "targetKind", "targetId"),
                RequiredInt(element, "departTick"),
                RequiredInt(element, "arrivalTick"),
                RequiredEnum<ArmyMovementStatus>(element, "status")));
        }

        foreach (var element in OptionalContainer(root, "FactionBehaviors").Elements("FactionBehavior"))
        {
            state.RestoreFactionBehaviorForLedger(
                RequiredString(element, "factionId"),
                RequiredEnum<FactionBehavior>(element, "behavior"));
        }

        return state;
    }

    private static object[] IdAttributes(EntityId id)
    {
        return new object[]
        {
            new XAttribute("kind", id.Kind),
            new XAttribute("id", id.Value)
        };
    }

    private static EntityId ReadId(XElement element)
    {
        return ReadEntityId(element, "kind", "id");
    }

    private static EntityId? TryReadEntityId(XElement element, string kindAttribute, string valueAttribute)
    {
        if (element.Attribute(kindAttribute) == null || element.Attribute(valueAttribute) == null)
        {
            return null;
        }

        return ReadEntityId(element, kindAttribute, valueAttribute);
    }

    private static EntityId ReadEntityId(XElement element, string kindAttribute, string valueAttribute)
    {
        return EntityId.Create(
            RequiredEnum<EntityKind>(element, kindAttribute),
            RequiredLong(element, valueAttribute));
    }

    private static XElement RequiredContainer(XElement element, string name)
    {
        return element.Element(name)
            ?? throw new InvalidOperationException($"Living World state payload is missing '{name}'.");
    }

    private static XElement OptionalContainer(XElement element, string name)
    {
        return element.Element(name) ?? new XElement(name);
    }

    private static string RequiredString(XElement element, string name)
    {
        return element.Attribute(name)?.Value
            ?? throw new InvalidOperationException($"Living World state payload is missing '{name}'.");
    }

    private static int RequiredInt(XElement element, string name)
    {
        return int.Parse(RequiredString(element, name), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int OptionalInt(XElement element, string name, int fallback)
    {
        var attribute = element.Attribute(name);
        return attribute == null
            ? fallback
            : int.Parse(attribute.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool RequiredBool(XElement element, string name)
    {
        return bool.Parse(RequiredString(element, name));
    }

    private static long RequiredLong(XElement element, string name)
    {
        return long.Parse(RequiredString(element, name), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static T RequiredEnum<T>(XElement element, string name)
        where T : struct
    {
        return (T)Enum.Parse(typeof(T), RequiredString(element, name));
    }

    private static T OptionalEnum<T>(XElement element, string name, T fallback)
        where T : struct
    {
        var attribute = element.Attribute(name);
        return attribute == null
            ? fallback
            : (T)Enum.Parse(typeof(T), attribute.Value);
    }
}
