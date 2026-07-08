using System.Text;
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
                new XAttribute("drifterArrivalReservoir", snapshot.DrifterArrivalReservoir),
                snapshot.PlayerFactionId == null
                    ? null
                    : new XAttribute("playerFactionId", snapshot.PlayerFactionId),
                new XElement(
                    "Settlements",
                    snapshot.Settlements.Select(settlement =>
                        new XElement(
                            "Settlement",
                            IdAttributes(settlement.Id),
                            new XAttribute("slug", settlement.Slug),
                            new XAttribute("name", settlement.Name),
                            new XAttribute("factionId", settlement.FactionId),
                            new XAttribute("status", settlement.Status)))),
                new XElement(
                    "Citizens",
                    new XAttribute("format", "compact-v2"),
                    EncodeCitizens(snapshot.Citizens)),
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
                    "Caravans",
                    snapshot.Caravans.Select(caravan =>
                        new XElement(
                            "Caravan",
                            IdAttributes(caravan.Id),
                            new XAttribute("name", caravan.Name),
                            new XAttribute("factionId", caravan.FactionId),
                            new XAttribute("sourceSettlementKind", caravan.SourceSettlementId.Kind),
                            new XAttribute("sourceSettlementId", caravan.SourceSettlementId.Value),
                            new XAttribute("targetSettlementKind", caravan.TargetSettlementId.Kind),
                            new XAttribute("targetSettlementId", caravan.TargetSettlementId.Value),
                            new XAttribute("departTick", caravan.DepartTick),
                            new XAttribute("arrivalTick", caravan.ArrivalTick),
                            new XAttribute("status", caravan.Status)))),
                new XElement(
                    "Missions",
                    snapshot.Missions.Select(mission =>
                        new XElement(
                            "Mission",
                            IdAttributes(mission.Id),
                            new XAttribute("missionKind", mission.Kind),
                            new XAttribute("factionId", mission.FactionId),
                            new XAttribute("originSettlementKind", mission.OriginSettlementId.Kind),
                            new XAttribute("originSettlementId", mission.OriginSettlementId.Value),
                            new XAttribute("targetSettlementKind", mission.TargetSettlementId.Kind),
                            new XAttribute("targetSettlementId", mission.TargetSettlementId.Value),
                            new XAttribute("departTick", mission.DepartTick),
                            new XAttribute("arrivalTick", mission.ArrivalTick),
                            new XAttribute("status", mission.Status),
                            new XAttribute("targetFactionId", mission.TargetFactionId),
                            new XAttribute("amount", mission.Amount)))),
                new XElement(
                    "Ruins",
                    snapshot.Ruins.Select(ruin =>
                        new XElement(
                            "Ruin",
                            IdAttributes(ruin.Id),
                            new XAttribute("originalSettlementKind", ruin.OriginalSettlementId.Kind),
                            new XAttribute("originalSettlementId", ruin.OriginalSettlementId.Value),
                            new XAttribute("slug", ruin.Slug),
                            new XAttribute("name", ruin.Name),
                            new XAttribute("formerFactionId", ruin.FormerFactionId),
                            new XAttribute("claimFactionId", ruin.ClaimFactionId),
                            new XAttribute("salvageBand", ruin.SalvageBand),
                            new XAttribute("dangerBand", ruin.DangerBand),
                            new XAttribute("createdTick", ruin.CreatedTick),
                            new XAttribute("status", ruin.Status),
                            ruin.ReclaimedSettlementId.HasValue
                                ? new XAttribute("reclaimedSettlementKind", ruin.ReclaimedSettlementId.Value.Kind)
                                : null,
                            ruin.ReclaimedSettlementId.HasValue
                                ? new XAttribute("reclaimedSettlementId", ruin.ReclaimedSettlementId.Value.Value)
                                : null,
                            new XAttribute("statusTick", ruin.StatusTick)))),
                new XElement(
                    "Conflicts",
                    snapshot.Conflicts.Select(conflict =>
                        new XElement(
                            "Conflict",
                            IdAttributes(conflict.Id),
                            new XAttribute("factionA", conflict.FactionA),
                            new XAttribute("factionB", conflict.FactionB),
                            new XAttribute("status", conflict.Status),
                            new XAttribute("startedTick", conflict.StartedTick),
                            new XAttribute("statusTick", conflict.StatusTick),
                            new XAttribute("warExhaustionA", conflict.WarExhaustionA),
                            new XAttribute("warExhaustionB", conflict.WarExhaustionB),
                            new XAttribute("truceExpiresTick", conflict.TruceExpiresTick),
                            new XAttribute("refugeesCreated", conflict.RefugeesCreated)))),
                new XElement(
                    "ConflictClaims",
                    snapshot.ConflictClaims.Select(claim =>
                        new XElement(
                            "ConflictClaim",
                            new XAttribute("conflictKind", claim.ConflictId.Kind),
                            new XAttribute("conflictId", claim.ConflictId.Value),
                            new XAttribute("settlementKind", claim.SettlementId.Kind),
                            new XAttribute("settlementId", claim.SettlementId.Value),
                            new XAttribute("claimantFactionId", claim.ClaimantFactionId),
                            new XAttribute("tick", claim.Tick)))),
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
                    "RaidIntelFacts",
                    snapshot.RaidIntelFacts.Select(fact =>
                        new XElement(
                            "RaidIntelFact",
                            IdAttributes(fact.Id),
                            new XAttribute("sourceKind", fact.SourceKind),
                            new XAttribute("factionId", fact.FactionId),
                            new XAttribute("targetKind", fact.TargetKind),
                            new XAttribute("targetKey", fact.TargetKey),
                            new XAttribute("valueBand", fact.ValueBand),
                            new XAttribute("confidence", fact.Confidence),
                            new XAttribute("createdTick", fact.CreatedTick),
                            new XAttribute("expiresTick", fact.ExpiresTick),
                            new XAttribute("combatantDemand", fact.CombatantDemand),
                            new XAttribute("summary", fact.Summary)))),
                new XElement(
                    "RaidPreparations",
                    snapshot.RaidPreparations.Select(preparation =>
                        new XElement(
                            "RaidPreparation",
                            IdAttributes(preparation.Id),
                            new XAttribute("factionId", preparation.FactionId),
                            new XAttribute("sourceSettlementKind", preparation.SourceSettlementId.Kind),
                            new XAttribute("sourceSettlementId", preparation.SourceSettlementId.Value),
                            new XAttribute("armyKind", preparation.ArmyId.Kind),
                            new XAttribute("armyId", preparation.ArmyId.Value),
                            preparation.IntelFactId.HasValue
                                ? new XAttribute("intelFactKind", preparation.IntelFactId.Value.Kind)
                                : null,
                            preparation.IntelFactId.HasValue
                                ? new XAttribute("intelFactId", preparation.IntelFactId.Value.Value)
                                : null,
                            new XAttribute("reason", preparation.Reason),
                            new XAttribute("status", preparation.Status),
                            new XAttribute("reservedCombatants", preparation.ReservedCombatants),
                            new XAttribute("supplyResourceKey", preparation.SupplyResourceKey),
                            new XAttribute("reservedSupplies", preparation.ReservedSupplies),
                            new XAttribute("createdTick", preparation.CreatedTick),
                            new XAttribute("expiresTick", preparation.ExpiresTick),
                            new XAttribute("summary", preparation.Summary)))),
                new XElement(
                    "MaterializationLeases",
                    snapshot.MaterializationLeases.Select(lease =>
                        new XElement(
                            "MaterializationLease",
                            IdAttributes(lease.Id),
                            new XAttribute("citizenKind", lease.CitizenId.Kind),
                            new XAttribute("citizenId", lease.CitizenId.Value),
                            new XAttribute("sourceOwnerKind", lease.SourceOwnerId.Kind),
                            new XAttribute("sourceOwnerId", lease.SourceOwnerId.Value),
                            new XAttribute("returnOwnerKind", lease.ReturnOwnerId.Kind),
                            new XAttribute("returnOwnerId", lease.ReturnOwnerId.Value),
                            new XAttribute("purpose", lease.Purpose),
                            new XAttribute("purposeKey", lease.PurposeKey),
                            new XAttribute("createdTick", lease.CreatedTick),
                            new XAttribute("expiresTick", lease.ExpiresTick),
                            new XAttribute("lifecycle", lease.Lifecycle),
                            lease.PawnThingId.HasValue
                                ? new XAttribute("pawnThingId", lease.PawnThingId.Value)
                                : null))),
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
                            new XAttribute("componentPerAdult", profile.ComponentPerAdult),
                            new XAttribute("archetype", profile.Archetype),
                            new XAttribute("laborEfficiencyPercent", profile.LaborEfficiencyPercent),
                            new XAttribute("economyScalePercent", profile.EconomyScalePercent),
                            new XAttribute("complexityPenaltyPercent", profile.ComplexityPenaltyPercent)))),
                new XElement(
                    "SettlementCapabilities",
                    snapshot.SettlementCapabilities.Select(capability =>
                            new XElement(
                                "SettlementCapability",
                                new XAttribute("settlementKind", capability.SettlementId.Kind),
                                new XAttribute("settlementId", capability.SettlementId.Value),
                                new XAttribute("housingCapacity", capability.HousingCapacity),
                                new XAttribute("foodStorageCapacity", capability.FoodStorageCapacity),
                                new XAttribute("medicineStorageCapacity", capability.MedicineStorageCapacity),
                                new XAttribute("powerCapacity", capability.PowerCapacity),
                                new XAttribute("laboratoryCapacity", capability.LaboratoryCapacity),
                                new XAttribute("animalCapacity", capability.AnimalCapacity),
                                new XAttribute("cropCapacity", capability.CropCapacity),
                                new XAttribute("researchCapacity", capability.ResearchCapacity),
                                new XAttribute("mechanicalCapacity", capability.MechanicalCapacity),
                                new XAttribute("pollutionHandling", capability.PollutionHandling)))),
                new XElement(
                    "SettlementFacilities",
                    snapshot.SettlementFacilities.Select(facility =>
                        new XElement(
                            "SettlementFacility",
                            IdAttributes(facility.Id),
                            new XAttribute("settlementKind", facility.SettlementId.Kind),
                            new XAttribute("settlementId", facility.SettlementId.Value),
                            new XAttribute("facilityKind", facility.Kind),
                            new XAttribute("level", facility.Level),
                            new XAttribute("conditionPercent", facility.ConditionPercent),
                            new XAttribute("builtTick", facility.BuiltTick)))),
                new XElement(
                    "SettlementProjects",
                    snapshot.SettlementProjects.Select(project =>
                        new XElement(
                            "SettlementProject",
                            IdAttributes(project.Id),
                            new XAttribute("settlementKind", project.SettlementId.Kind),
                            new XAttribute("settlementId", project.SettlementId.Value),
                            new XAttribute("projectKind", project.Kind),
                            new XAttribute("status", project.Status),
                            new XAttribute("facilityKind", project.FacilityKind),
                            project.TargetFacilityId.HasValue
                                ? new XAttribute("targetFacilityKind", project.TargetFacilityId.Value.Kind)
                                : null,
                            project.TargetFacilityId.HasValue
                                ? new XAttribute("targetFacilityId", project.TargetFacilityId.Value.Value)
                                : null,
                            new XAttribute("facilityLevel", project.FacilityLevel),
                            new XAttribute("startedTick", project.StartedTick),
                            new XAttribute("completionTick", project.CompletionTick),
                            new XAttribute("steelCost", project.SteelCost),
                            new XAttribute("componentCost", project.ComponentCost)))),
                new XElement(
                    "AnimalCohorts",
                    snapshot.AnimalCohorts.Select(cohort =>
                        new XElement(
                            "AnimalCohort",
                            IdAttributes(cohort.Id),
                            new XAttribute("ownerKind", cohort.OwnerId.Kind),
                            new XAttribute("ownerId", cohort.OwnerId.Value),
                            new XAttribute("animalKind", cohort.AnimalKind),
                            new XAttribute("cohortType", cohort.Type),
                            new XAttribute("count", cohort.Count),
                            new XAttribute("healthPercent", cohort.HealthPercent),
                            new XAttribute("fertilityPercent", cohort.FertilityPercent),
                            new XAttribute("carryingCapacity", cohort.CarryingCapacity),
                            new XAttribute("lastUpdatedTick", cohort.LastUpdatedTick)))),
                new XElement(
                    "SpecialistPools",
                    snapshot.SpecialistPools.Select(specialists =>
                            new XElement(
                                "SpecialistPool",
                                new XAttribute("settlementKind", specialists.SettlementId.Kind),
                                new XAttribute("settlementId", specialists.SettlementId.Value),
                                new XAttribute("farmers", specialists.Farmers),
                                new XAttribute("handlers", specialists.Handlers),
                                new XAttribute("doctors", specialists.Doctors),
                                new XAttribute("researchers", specialists.Researchers),
                                new XAttribute("engineers", specialists.Engineers),
                                new XAttribute("geneticists", specialists.Geneticists),
                                new XAttribute("mechanitors", specialists.Mechanitors),
                                new XAttribute("soldiers", specialists.Soldiers),
                                new XAttribute("diplomats", specialists.Diplomats)))),
                new XElement(
                    "SettlementWealth",
                    snapshot.SettlementWealth.Select(wealth =>
                        new XElement(
                            "Wealth",
                            new XAttribute("settlementKind", wealth.SettlementId.Kind),
                            new XAttribute("settlementId", wealth.SettlementId.Value),
                            new XAttribute("factionId", wealth.FactionId),
                            new XAttribute("silver", wealth.Silver),
                            new XAttribute("materialWealth", wealth.MaterialWealth),
                            new XAttribute("totalWealth", wealth.TotalWealth)))),
                new XElement(
                    "FactionWealth",
                    snapshot.FactionWealth.Select(wealth =>
                        new XElement(
                            "Wealth",
                            new XAttribute("factionId", wealth.FactionId),
                            new XAttribute("silver", wealth.Silver),
                            new XAttribute("materialWealth", wealth.MaterialWealth),
                            new XAttribute("totalWealth", wealth.TotalWealth)))),
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
                    new XAttribute("format", "compact-v2"),
                    EncodeOwnership(snapshot.Ownership)),
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
                    new XAttribute("format", "compact-v2"),
                    EncodeEvents(snapshot.Events)),
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
                                new XAttribute("status", movement.Status),
                                new XAttribute("statusTick", movement.StatusTick)))),
                new XElement(
                    "FactionBehaviors",
                    state.FactionBehaviors
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair =>
                            new XElement(
                                "FactionBehavior",
                                new XAttribute("factionId", pair.Key),
                                new XAttribute("behavior", pair.Value)))),
                new XElement(
                    "FactionRelations",
                    state.FactionRelations
                        .OrderBy(pair => pair.Key.Item1, StringComparer.Ordinal)
                        .ThenBy(pair => pair.Key.Item2, StringComparer.Ordinal)
                        .Select(pair =>
                            new XElement(
                                "Relation",
                                new XAttribute("factionA", pair.Key.Item1),
                                new XAttribute("factionB", pair.Key.Item2),
                                new XAttribute("goodwill", pair.Value)))),
                new XElement(
                    "IrreconcilableFactions",
                    state.IrreconcilableFactions
                        .OrderBy(factionId => factionId, StringComparer.Ordinal)
                        .Select(factionId =>
                            new XElement(
                                "Faction",
                                new XAttribute("factionId", factionId))))));

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
                    RequiredString(element, "factionId"),
                    OptionalEnum(element, "status", SettlementLifecycleStatus.Active)))
                .ToList(),
            DecodeCitizens(RequiredContainer(root, "Citizens")),
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
                    RequiredInt(element, "componentPerAdult"))
                {
                    Archetype = OptionalEnum(element, "archetype", ProductionArchetype.Balanced),
                    LaborEfficiencyPercent = OptionalInt(element, "laborEfficiencyPercent", 100),
                    EconomyScalePercent = OptionalInt(element, "economyScalePercent", 100),
                    ComplexityPenaltyPercent = OptionalInt(element, "complexityPenaltyPercent", 100),
                })
                .ToList(),
            OptionalContainer(root, "FactionRecords")
                .Elements("FactionRecord")
                .Select(element => new WorldFactionRecord(
                    RequiredString(element, "factionId"),
                    RequiredEnum<WorldFactionStatus>(element, "status"),
                    RequiredInt(element, "tick"),
                    RequiredString(element, "reason")))
                .ToList(),
            DecodeOwnership(RequiredContainer(root, "Ownership")),
            RequiredContainer(root, "Resources")
                .Elements("Resource")
                .Select(element => new ResourceStack(
                    ReadEntityId(element, "ownerKind", "ownerId"),
                    RequiredString(element, "resourceKey"),
                    RequiredInt(element, "quantity")))
                .ToList(),
            DecodeEvents(RequiredContainer(root, "Events")),
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
                .ToList())
        {
            PlayerFactionId = OptionalString(root, "playerFactionId"),
            DrifterArrivalReservoir = OptionalInt(root, "drifterArrivalReservoir", 0),
            Caravans = OptionalContainer(root, "Caravans")
                .Elements("Caravan")
                .Select(element => new WorldCaravan(
                    ReadId(element),
                    RequiredString(element, "name"),
                    RequiredString(element, "factionId"),
                    ReadEntityId(element, "sourceSettlementKind", "sourceSettlementId"),
                    ReadEntityId(element, "targetSettlementKind", "targetSettlementId"),
                    RequiredInt(element, "departTick"),
                    RequiredInt(element, "arrivalTick"),
                    RequiredEnum<CaravanStatus>(element, "status")))
                .ToList(),
            Missions = OptionalContainer(root, "Missions")
                .Elements("Mission")
                .Select(element => new WorldMission(
                    ReadId(element),
                    RequiredEnum<WorldMissionKind>(element, "missionKind"),
                    RequiredString(element, "factionId"),
                    ReadEntityId(element, "originSettlementKind", "originSettlementId"),
                    ReadEntityId(element, "targetSettlementKind", "targetSettlementId"),
                    RequiredInt(element, "departTick"),
                    RequiredInt(element, "arrivalTick"),
                    RequiredEnum<WorldMissionStatus>(element, "status"))
                {
                    TargetFactionId = OptionalString(element, "targetFactionId") ?? string.Empty,
                    Amount = OptionalInt(element, "amount", 0),
                })
                .ToList(),
            Ruins = OptionalContainer(root, "Ruins")
                .Elements("Ruin")
                .Select(element => new WorldRuin(
                    ReadId(element),
                    ReadEntityId(element, "originalSettlementKind", "originalSettlementId"),
                    RequiredString(element, "slug"),
                    RequiredString(element, "name"),
                    RequiredString(element, "formerFactionId"),
                    RequiredString(element, "claimFactionId"),
                    RequiredEnum<RuinSalvageBand>(element, "salvageBand"),
                    RequiredEnum<RuinDangerBand>(element, "dangerBand"),
                    RequiredInt(element, "createdTick"),
                    RequiredEnum<RuinStatus>(element, "status"),
                    TryReadEntityId(element, "reclaimedSettlementKind", "reclaimedSettlementId"),
                    OptionalInt(element, "statusTick", RequiredInt(element, "createdTick"))))
                .ToList(),
            Conflicts = OptionalContainer(root, "Conflicts")
                .Elements("Conflict")
                .Select(element => new WorldConflict(
                    ReadId(element),
                    RequiredString(element, "factionA"),
                    RequiredString(element, "factionB"),
                    RequiredEnum<WorldConflictStatus>(element, "status"),
                    RequiredInt(element, "startedTick"),
                    RequiredInt(element, "statusTick"),
                    RequiredInt(element, "warExhaustionA"),
                    RequiredInt(element, "warExhaustionB"),
                    RequiredInt(element, "truceExpiresTick"),
                    OptionalInt(element, "refugeesCreated", 0)))
                .ToList(),
            ConflictClaims = OptionalContainer(root, "ConflictClaims")
                .Elements("ConflictClaim")
                .Select(element => new ConflictClaim(
                    ReadEntityId(element, "conflictKind", "conflictId"),
                    ReadEntityId(element, "settlementKind", "settlementId"),
                    RequiredString(element, "claimantFactionId"),
                    RequiredInt(element, "tick")))
                .ToList(),
            RaidIntelFacts = OptionalContainer(root, "RaidIntelFacts")
                .Elements("RaidIntelFact")
                .Select(element => new RaidIntelFact(
                    ReadId(element),
                    RequiredEnum<IntelSourceKind>(element, "sourceKind"),
                    RequiredString(element, "factionId"),
                    RequiredEnum<RaidIntelTargetKind>(element, "targetKind"),
                    RequiredString(element, "targetKey"),
                    RequiredEnum<RaidIntelValueBand>(element, "valueBand"),
                    RequiredInt(element, "confidence"),
                    RequiredInt(element, "createdTick"),
                    RequiredInt(element, "expiresTick"),
                    RequiredInt(element, "combatantDemand"),
                    RequiredString(element, "summary")))
                .ToList(),
            RaidPreparations = OptionalContainer(root, "RaidPreparations")
                .Elements("RaidPreparation")
                .Select(element => new RaidPreparation(
                    ReadId(element),
                    RequiredString(element, "factionId"),
                    ReadEntityId(element, "sourceSettlementKind", "sourceSettlementId"),
                    ReadEntityId(element, "armyKind", "armyId"),
                    TryReadEntityId(element, "intelFactKind", "intelFactId"),
                    RequiredEnum<RaidIntentReason>(element, "reason"),
                    RequiredEnum<RaidPreparationStatus>(element, "status"),
                    RequiredInt(element, "reservedCombatants"),
                    RequiredString(element, "supplyResourceKey"),
                    RequiredInt(element, "reservedSupplies"),
                    RequiredInt(element, "createdTick"),
                    RequiredInt(element, "expiresTick"),
                    RequiredString(element, "summary")))
                .ToList(),
            MaterializationLeases = OptionalContainer(root, "MaterializationLeases")
                .Elements("MaterializationLease")
                .Select(element => new MaterializationLease(
                    ReadId(element),
                    ReadEntityId(element, "citizenKind", "citizenId"),
                    ReadEntityId(element, "sourceOwnerKind", "sourceOwnerId"),
                    ReadEntityId(element, "returnOwnerKind", "returnOwnerId"),
                    RequiredEnum<MaterializationPurpose>(element, "purpose"),
                    RequiredString(element, "purposeKey"),
                    RequiredInt(element, "createdTick"),
                    RequiredInt(element, "expiresTick"),
                    RequiredEnum<MaterializationLeaseLifecycle>(element, "lifecycle"),
                    TryOptionalInt(element, "pawnThingId")))
                .ToList(),
            SettlementFacilities = OptionalContainer(root, "SettlementFacilities")
                .Elements("SettlementFacility")
                .Select(element => new SettlementFacility(
                    ReadId(element),
                    ReadEntityId(element, "settlementKind", "settlementId"),
                    RequiredEnum<SettlementFacilityKind>(element, "facilityKind"),
                    RequiredInt(element, "level"),
                    RequiredInt(element, "conditionPercent"),
                    RequiredInt(element, "builtTick")))
                .ToList(),
            SettlementProjects = OptionalContainer(root, "SettlementProjects")
                .Elements("SettlementProject")
                .Select(element => new SettlementProject(
                    ReadId(element),
                    ReadEntityId(element, "settlementKind", "settlementId"),
                    RequiredEnum<SettlementProjectKind>(element, "projectKind"),
                    RequiredEnum<SettlementProjectStatus>(element, "status"),
                    RequiredEnum<SettlementFacilityKind>(element, "facilityKind"),
                    TryReadEntityId(element, "targetFacilityKind", "targetFacilityId"),
                    RequiredInt(element, "facilityLevel"),
                    RequiredInt(element, "startedTick"),
                    RequiredInt(element, "completionTick"),
                    RequiredInt(element, "steelCost"),
                    RequiredInt(element, "componentCost")))
                .ToList(),
            AnimalCohorts = OptionalContainer(root, "AnimalCohorts")
                .Elements("AnimalCohort")
                .Select(element => new WorldAnimalCohort(
                    ReadId(element),
                    ReadEntityId(element, "ownerKind", "ownerId"),
                    RequiredString(element, "animalKind"),
                    RequiredEnum<AnimalCohortType>(element, "cohortType"),
                    RequiredInt(element, "count"),
                    RequiredInt(element, "healthPercent"),
                    RequiredInt(element, "fertilityPercent"),
                    RequiredInt(element, "carryingCapacity"),
                    RequiredInt(element, "lastUpdatedTick")))
                .ToList()
        };

        var state = WorldState.FromSnapshot(snapshot);

        foreach (var element in OptionalContainer(root, "SettlementCapabilities").Elements("SettlementCapability"))
        {
            state.RecordSettlementCapability(new SettlementCapability(
                ReadEntityId(element, "settlementKind", "settlementId"),
                RequiredInt(element, "housingCapacity"),
                RequiredInt(element, "foodStorageCapacity"),
                RequiredInt(element, "medicineStorageCapacity"),
                RequiredInt(element, "powerCapacity"),
                RequiredInt(element, "laboratoryCapacity"),
                RequiredInt(element, "animalCapacity"),
                RequiredInt(element, "cropCapacity"),
                RequiredInt(element, "researchCapacity"),
                RequiredInt(element, "mechanicalCapacity"),
                RequiredInt(element, "pollutionHandling")));
        }

        foreach (var element in OptionalContainer(root, "SpecialistPools").Elements("SpecialistPool"))
        {
            state.RecordSpecialistPool(new SpecialistPool(
                ReadEntityId(element, "settlementKind", "settlementId"),
                RequiredInt(element, "farmers"),
                RequiredInt(element, "handlers"),
                RequiredInt(element, "doctors"),
                RequiredInt(element, "researchers"),
                RequiredInt(element, "engineers"),
                RequiredInt(element, "geneticists"),
                RequiredInt(element, "mechanitors"),
                RequiredInt(element, "soldiers"),
                RequiredInt(element, "diplomats")));
        }

        foreach (var element in OptionalContainer(root, "SettlementWealth").Elements("Wealth"))
        {
            state.RecordSettlementWealth(new SettlementWealthSnapshot(
                ReadEntityId(element, "settlementKind", "settlementId"),
                RequiredString(element, "factionId"),
                RequiredInt(element, "silver"),
                RequiredInt(element, "materialWealth"),
                RequiredInt(element, "totalWealth")));
        }

        foreach (var element in OptionalContainer(root, "FactionWealth").Elements("Wealth"))
        {
            state.RecordFactionWealth(new FactionWealthSnapshot(
                RequiredString(element, "factionId"),
                RequiredInt(element, "silver"),
                RequiredInt(element, "materialWealth"),
                RequiredInt(element, "totalWealth")));
        }

        foreach (var element in OptionalContainer(root, "ArmyMovements").Elements("Movement"))
        {
            state.RestoreArmyMovementForLedger(new WorldArmyMovement(
                ReadEntityId(element, "armyKind", "armyId"),
                ReadEntityId(element, "targetKind", "targetId"),
                RequiredInt(element, "departTick"),
                RequiredInt(element, "arrivalTick"),
                RequiredEnum<ArmyMovementStatus>(element, "status"))
            {
                StatusTick = OptionalInt(element, "statusTick", RequiredInt(element, "departTick"))
            });
        }

        foreach (var element in OptionalContainer(root, "FactionBehaviors").Elements("FactionBehavior"))
        {
            state.RestoreFactionBehaviorForLedger(
                RequiredString(element, "factionId"),
                RequiredEnum<FactionBehavior>(element, "behavior"));
        }

        foreach (var element in OptionalContainer(root, "FactionRelations").Elements("Relation"))
        {
            state.SetFactionGoodwillForLedger(
                RequiredString(element, "factionA"),
                RequiredString(element, "factionB"),
                RequiredInt(element, "goodwill"));
        }

        foreach (var element in OptionalContainer(root, "IrreconcilableFactions").Elements("Faction"))
        {
            state.RestoreIrreconcilableFactionForLedger(RequiredString(element, "factionId"));
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

    private static string EncodeCitizens(IEnumerable<WorldCitizen> citizens)
    {
        return string.Join(
            "\n",
            citizens.Select(citizen => string.Join(
                "|",
                citizen.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                EncodeString(citizen.Name),
                citizen.Age.ToString(System.Globalization.CultureInfo.InvariantCulture),
                citizen.Sex,
                EncodeString(citizen.Profession),
                citizen.SettlementId.Kind,
                citizen.SettlementId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                citizen.Status)));
    }

    private static IReadOnlyList<WorldCitizen> DecodeCitizens(XElement element)
    {
        var format = OptionalString(element, "format");
        if (string.Equals(format, "compact-v2", StringComparison.Ordinal))
        {
            return element.Value
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(DecodeCompactCitizen)
                .ToList();
        }

        return element
            .Elements("Citizen")
            .Select(citizenElement => new WorldCitizen(
                ReadId(citizenElement),
                RequiredString(citizenElement, "name"),
                RequiredInt(citizenElement, "age"),
                RequiredEnum<Sex>(citizenElement, "sex"),
                RequiredString(citizenElement, "profession"),
                ReadEntityId(citizenElement, "settlementKind", "settlementId"),
                RequiredEnum<CitizenStatus>(citizenElement, "status")))
            .ToList();
    }

    private static string EncodeOwnership(IEnumerable<OwnershipRecord> ownership)
    {
        return string.Join(
            "\n",
            ownership.Select(record => string.Join(
                "|",
                record.AssetId.Kind,
                record.AssetId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                record.OwnerId.Kind,
                record.OwnerId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static IReadOnlyList<OwnershipRecord> DecodeOwnership(XElement element)
    {
        var format = OptionalString(element, "format");
        if (string.Equals(format, "compact-v2", StringComparison.Ordinal))
        {
            return element.Value
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(DecodeCompactOwnership)
                .ToList();
        }

        return element
            .Elements("Owner")
            .Select(ownerElement => new OwnershipRecord(
                ReadEntityId(ownerElement, "assetKind", "assetId"),
                ReadEntityId(ownerElement, "ownerKind", "ownerId")))
            .ToList();
    }

    private static OwnershipRecord DecodeCompactOwnership(string row)
    {
        var fields = row.Split('|');
        if (fields.Length != 4)
        {
            throw new InvalidOperationException("Compact ownership row has an invalid field count.");
        }

        return new OwnershipRecord(
            EntityId.Create(
                ParseEnum<EntityKind>(fields[0]),
                long.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture)),
            EntityId.Create(
                ParseEnum<EntityKind>(fields[2]),
                long.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static string EncodeEvents(IEnumerable<WorldEvent> events)
    {
        return string.Join(
            "\n",
            events.Select(worldEvent => string.Join(
                "|",
                worldEvent.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                worldEvent.Kind,
                worldEvent.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture),
                worldEvent.SubjectId.HasValue ? worldEvent.SubjectId.Value.Kind.ToString() : string.Empty,
                worldEvent.SubjectId.HasValue ? worldEvent.SubjectId.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                EncodeString(worldEvent.Summary))));
    }

    private static IReadOnlyList<WorldEvent> DecodeEvents(XElement element)
    {
        var format = OptionalString(element, "format");
        if (string.Equals(format, "compact-v2", StringComparison.Ordinal))
        {
            return element.Value
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(DecodeCompactEvent)
                .ToList();
        }

        return element
            .Elements("Event")
            .Select(eventElement => new WorldEvent(
                ReadId(eventElement),
                RequiredEnum<WorldEventKind>(eventElement, "eventKind"),
                RequiredInt(eventElement, "tick"),
                TryReadEntityId(eventElement, "subjectKind", "subjectId"),
                RequiredString(eventElement, "summary")))
            .ToList();
    }

    private static WorldEvent DecodeCompactEvent(string row)
    {
        var fields = row.Split('|');
        if (fields.Length != 6)
        {
            throw new InvalidOperationException("Compact event row has an invalid field count.");
        }

        var subjectId = string.IsNullOrWhiteSpace(fields[3])
            ? (EntityId?)null
            : EntityId.Create(
                ParseEnum<EntityKind>(fields[3]),
                long.Parse(fields[4], System.Globalization.CultureInfo.InvariantCulture));

        return new WorldEvent(
            EntityId.Create(EntityKind.Event, long.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture)),
            ParseEnum<WorldEventKind>(fields[1]),
            int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
            subjectId,
            DecodeString(fields[5]));
    }

    private static WorldCitizen DecodeCompactCitizen(string row)
    {
        var fields = row.Split('|');
        if (fields.Length != 8)
        {
            throw new InvalidOperationException("Compact citizen row has an invalid field count.");
        }

        return new WorldCitizen(
            EntityId.Create(EntityKind.Citizen, long.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture)),
            DecodeString(fields[1]),
            int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
            ParseEnum<Sex>(fields[3]),
            DecodeString(fields[4]),
            EntityId.Create(
                ParseEnum<EntityKind>(fields[5]),
                long.Parse(fields[6], System.Globalization.CultureInfo.InvariantCulture)),
            ParseEnum<CitizenStatus>(fields[7]));
    }

    private static T ParseEnum<T>(string value)
        where T : struct
    {
        return (T)Enum.Parse(typeof(T), value);
    }

    private static string EncodeString(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static string DecodeString(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
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

    private static string? OptionalString(XElement element, string name)
    {
        return element.Attribute(name)?.Value;
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

    private static int? TryOptionalInt(XElement element, string name)
    {
        var attribute = element.Attribute(name);
        return attribute == null
            ? null
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
