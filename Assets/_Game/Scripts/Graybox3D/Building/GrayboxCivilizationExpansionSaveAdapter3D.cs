using System;
using System.Collections.Generic;
using WasteCity.CivilizationExpansion;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxCivilizationExpansionSaveAdapter3D
    {
        private readonly GrayboxCivilizationExpansionController3D controller;

        public GrayboxCivilizationExpansionSaveAdapter3D(
            GrayboxCivilizationExpansionController3D controller)
        {
            this.controller = controller ??
                throw new ArgumentNullException(nameof(controller));
        }

        public FormalThreeDCivilizationExpansionSaveData Capture()
        {
            CivilizationExpansionRuntime runtime = RequireRuntime();
            return new FormalThreeDCivilizationExpansionSaveData
            {
                armyLeader = CaptureArmy(runtime),
                worldLayer = CaptureWorld(runtime),
                charactersPolitics = CapturePolitics(runtime),
            };
        }

        public bool TryRestore(
            FormalThreeDCivilizationExpansionSaveData source,
            out string error)
        {
            error = string.Empty;
            if (source == null || !string.Equals(
                    source.configurationSignature,
                    FormalThreeDCivilizationExpansionSaveData
                        .ConfigurationSignature,
                    StringComparison.Ordinal))
            {
                error = "文明扩展存档配置签名无效";
                return false;
            }
            CivilizationExpansionRuntime runtime = RequireRuntime();
            if (!TryRestoreArmy(runtime, source.armyLeader, out error) ||
                !TryRestoreWorld(runtime, source.worldLayer, out error) ||
                !TryRestorePolitics(
                    runtime,
                    source.charactersPolitics,
                    out error))
            {
                return false;
            }
            controller.Refresh(force: true);
            return true;
        }

        private CivilizationExpansionRuntime RequireRuntime()
        {
            if (!controller.TryInitialize(out string error) ||
                controller.Runtime == null)
                throw new InvalidOperationException(error);
            return controller.Runtime;
        }

        private static FormalThreeDArmyLeaderSaveData CaptureArmy(
            CivilizationExpansionRuntime runtime)
        {
            SingleCityArmyPersistenceSnapshot army =
                runtime.Army.CaptureForPersistence();
            FriendlyUnitCommandPersistenceSnapshot command = army.Command;
            var units = new FormalThreeDArmyUnitSaveData[army.Units.Length];
            for (var index = 0; index < units.Length; index++)
            {
                ArmyUnitPersistenceState item = army.Units[index];
                units[index] = new FormalThreeDArmyUnitSaveData
                {
                    stableUnitId = item.StableUnitId,
                    definitionId = item.DefinitionId,
                    squadId = item.SquadId,
                    currentHealth = item.CurrentHealth,
                    dormant = !item.IsActive,
                    maintenanceElapsedSeconds = item.MaintenanceElapsed,
                    maintenanceRemainingSeconds = item.MaintenanceElapsed,
                };
            }
            var manufacturing = new FormalThreeDArmyManufacturingSaveData[
                army.Manufacturing.Length];
            for (var index = 0; index < manufacturing.Length; index++)
            {
                manufacturing[index] =
                    new FormalThreeDArmyManufacturingSaveData
                    {
                        definitionId = army.Manufacturing[index].DefinitionId,
                        progressSeconds =
                            army.Manufacturing[index].ProgressSeconds,
                    };
            }
            var losses = new FormalThreeDArmyLossSaveData[army.Losses.Length];
            for (var index = 0; index < losses.Length; index++)
            {
                losses[index] = new FormalThreeDArmyLossSaveData
                {
                    definitionId = army.Losses[index].DefinitionId,
                    count = army.Losses[index].Count,
                };
            }
            var squad = new FormalThreeDArmySquadSaveData
            {
                stableSquadId = SingleCityArmyModel.DefaultSquadId,
                command = (int)command.Command,
                hasFixedRally = command.HasFixedRally,
                hasExpeditionTarget = command.HasExpeditionTarget,
                rallyFloatX = command.RallyX,
                rallyFloatY = command.RallyY,
                destinationX = command.ExpeditionTargetX,
                destinationY = command.ExpeditionTargetY,
                leaderAssigned = army.LeaderAssigned,
                leaderHealthy = army.LeaderHealthy,
                puppetLosses = command.PuppetLosses,
                behemothLosses = command.BehemothLosses,
                controlledLosses = command.ControlledLosses,
                unitIds = UnitIds(army.Units),
            };
            return new FormalThreeDArmyLeaderSaveData
            {
                nextUnitOrdinal = (ulong)army.NextUnitOrdinal,
                nextExpeditionOrdinal =
                    (ulong)runtime.NextExpeditionOrdinal,
                leaderAssigned = army.LeaderAssigned,
                leaderHealthy = army.LeaderHealthy,
                units = units,
                manufacturing = manufacturing,
                losses = losses,
                squads = new[] { squad },
                expedition = CaptureExpedition(runtime.Expedition),
            };
        }

        private static FormalThreeDArmyExpeditionSaveData CaptureExpedition(
            ArmyExpeditionModel expedition)
        {
            ArmyExpeditionPersistenceSnapshot value =
                expedition.CaptureForPersistence();
            if (value.Status == ArmyExpeditionStatus.Idle) return null;
            var units = new FormalThreeDArmyExpeditionUnitSaveData[
                value.Units.Length];
            for (var index = 0; index < units.Length; index++)
            {
                units[index] = new FormalThreeDArmyExpeditionUnitSaveData
                {
                    stableUnitId = value.Units[index].StableUnitId,
                    definitionId = value.Units[index].DefinitionId,
                    currentHealth = value.Units[index].CurrentHealth,
                    active = value.Units[index].IsActive,
                };
            }
            return new FormalThreeDArmyExpeditionSaveData
            {
                expeditionId = "core.expedition." +
                    value.ExpeditionOrdinal.ToString("D6"),
                squadId = SingleCityArmyModel.DefaultSquadId,
                sessionId = value.SessionId,
                expeditionOrdinal = value.ExpeditionOrdinal,
                phase = (int)value.Status,
                targetX = value.TargetX,
                targetY = value.TargetY,
                outboundDurationSeconds = value.OutboundDurationSeconds,
                returnDurationSeconds = value.ReturnDurationSeconds,
                remainingSeconds = value.RemainingSeconds,
                retreatRequested = value.Retreating,
                leaderHealthy = value.LeaderHealthy,
                hasResolution = value.HasResolution,
                victory = value.Victory,
                armyPower = value.ArmyPower,
                enemyPower = value.EnemyPower,
                enemyDefinitionIds = Clone(value.EnemyDefinitionIds),
                casualtyStableUnitIds = Clone(
                    value.CasualtyStableUnitIds),
                units = units,
                pendingLoot = ToSaveAmounts(value.PendingLoot),
            };
        }

        private static FormalThreeDWorldLayerSaveData CaptureWorld(
            CivilizationExpansionRuntime runtime)
        {
            WorldLayerRuntimeSnapshot world = runtime.WorldLayer.Capture();
            TransportRuntimeSnapshot transport = runtime.Transport.Capture();
            var settlements = new FormalThreeDSettlementSaveData[
                world.Settlements.Count];
            for (var index = 0; index < settlements.Length; index++)
            {
                SettlementRuntimeSnapshot item = world.Settlements[index];
                SettlementRuntime live = runtime.WorldLayer.GetSettlement(
                    item.StableId);
                settlements[index] = new FormalThreeDSettlementSaveData
                {
                    stableSettlementId = item.StableId,
                    kind = (int)item.Kind,
                    x = item.X,
                    y = item.Y,
                    population = item.Population,
                    populationCapacity = item.PopulationCapacity,
                    autonomousTemplate = (int)item.AutonomyTemplate,
                    communicationConnected = item.IsCommunicationActive,
                    supplyConnected = item.IsSupplied,
                    maintenanceConnected = item.IsMaintained,
                    loyalty = item.Loyalty,
                    productionRemainingSeconds =
                        item.AutonomyProgressSeconds,
                    revision = live?.Revision ?? 0ul,
                    inventory = ToSaveAmounts(item.InventoryAmounts),
                };
            }
            var convoys = new FormalThreeDConvoySaveData[
                transport.Convoys.Count];
            for (var index = 0; index < convoys.Length; index++)
            {
                ConvoySnapshot item = transport.Convoys[index];
                convoys[index] = new FormalThreeDConvoySaveData
                {
                    stableConvoyId = item.StableId,
                    sourceSettlementId = item.SourceSettlementId,
                    destinationSettlementId = item.DestinationSettlementId,
                    escortSquadId = item.EscortSquadId,
                    sessionId = item.SessionId,
                    status = (int)item.Status,
                    pathIndex = item.CompletedPathCells,
                    completedPathCells = item.CompletedPathCells,
                    segmentProgress = item.SegmentProgressSeconds,
                    segmentProgressSeconds = item.SegmentProgressSeconds,
                    riskResolved = item.RiskResolved,
                    intercepted = item.Status == ConvoyStatus.Destroyed,
                    appliedRiskPercent = item.AppliedRiskPercent,
                    path = ToSavePoints(item.Path),
                    cargo = ToSaveAmounts(item.Cargo),
                };
            }
            return new FormalThreeDWorldLayerSaveData
            {
                revision = world.Revision,
                nextSettlementOrdinal =
                    (ulong)world.NextSettlementOrdinal,
                nextConvoyOrdinal = (ulong)transport.NextConvoyOrdinal,
                primaryCityId = WorldLayerCatalog.PrimaryCity.Id,
                focusedSettlementId = world.FocusedSettlementId,
                controlledCityId = world.ControlledCityId,
                settlements = settlements,
                convoys = convoys,
            };
        }

        private static FormalThreeDCharactersPoliticsSaveData CapturePolitics(
            CivilizationExpansionRuntime runtime)
        {
            LeadershipPoliticsSnapshot politics = runtime.Politics.Capture();
            DiplomacyRuntimeSnapshot diplomacy = runtime.Diplomacy.Capture();
            var characters = new FormalThreeDCharacterSaveData[
                runtime.Characters.Count];
            var corpses = new List<FormalThreeDCorpseSaveData>();
            for (var index = 0; index < characters.Length; index++)
            {
                CharacterLifeSnapshot item =
                    runtime.Characters[index].Capture();
                characters[index] = new FormalThreeDCharacterSaveData
                {
                    characterId = item.CharacterId,
                    state = (int)item.State,
                    currentHealth = item.CurrentHealth,
                    maximumHealth =
                        runtime.Characters[index].MaximumHealth,
                    x = item.X,
                    y = item.Y,
                    assignedSettlementId = item.AssignedSettlementId,
                    loyalty = item.Loyalty,
                    permanentWoundId = item.PermanentInjuryIds.Count > 0
                        ? item.PermanentInjuryIds[0]
                        : string.Empty,
                    permanentInjuryIds = Copy(item.PermanentInjuryIds),
                    downedRemainingSeconds = item.DownedRemainingSeconds,
                    recoveryRemainingSeconds = item.RecoveryRemainingSeconds,
                    downedElapsedSeconds = item.DownedElapsedSeconds,
                    downCount = item.DownCount,
                    downedCauseId = item.DownedCauseId,
                    equipmentIds = Copy(item.EquipmentIds),
                    rescue = ToSaveRescue(item),
                };
                if (item.Corpse != null)
                {
                    corpses.Add(new FormalThreeDCorpseSaveData
                    {
                        corpseId = "core.corpse." + item.CharacterId,
                        characterId = item.Corpse.CharacterId,
                        settlementId = item.Corpse.SettlementId,
                        x = item.Corpse.X,
                        y = item.Corpse.Y,
                        recovered = item.Corpse.IsRecovered,
                        equipmentIds = Copy(item.Corpse.EquipmentIds),
                    });
                }
            }
            var factions = new FormalThreeDInternalFactionSaveData[
                politics.Factions.Count];
            for (var index = 0; index < factions.Length; index++)
            {
                InternalFactionStateSnapshot item = politics.Factions[index];
                var supports = new FormalThreeDFactionCandidateSupportSaveData[
                    item.CandidateSupports.Count];
                for (var supportIndex = 0;
                     supportIndex < supports.Length;
                     supportIndex++)
                {
                    supports[supportIndex] =
                        new FormalThreeDFactionCandidateSupportSaveData
                        {
                            characterId = item.CandidateSupports[supportIndex]
                                .CharacterId,
                            support = item.CandidateSupports[supportIndex]
                                .Support,
                        };
                }
                factions[index] = new FormalThreeDInternalFactionSaveData
                {
                    factionId = item.FactionId,
                    influence = item.Influence,
                    loyalty = item.Loyalty,
                    candidateSupports = supports,
                };
            }
            var external = new FormalThreeDExternalFactionSaveData[
                diplomacy.Factions.Count];
            for (var index = 0; index < external.Length; index++)
            {
                DiplomacyFactionStateSnapshot item = diplomacy.Factions[index];
                external[index] = new FormalThreeDExternalFactionSaveData
                {
                    factionId = item.FactionId,
                    relation = item.Relation,
                    state = (int)item.State,
                    offerCooldownRemainingSeconds =
                        item.RefreshRemainingSeconds,
                    activeOffer = ToSaveOffer(item.ActiveOffer),
                };
            }
            return new FormalThreeDCharactersPoliticsSaveData
            {
                nextOfferOrdinal = diplomacy.NextOfferOrdinal,
                diplomacySessionId = diplomacy.SessionId,
                convoyInterceptionImmunityCharges =
                    diplomacy.ConvoyInterceptionImmunityCharges,
                currentLeaderId = politics.CurrentLeaderId,
                designatedSuccessorId =
                    politics.DesignatedSuccessorId ?? string.Empty,
                leadershipState = politics.IsInterimCouncilActive ? 1 : 0,
                councilEfficiencyMultiplier = runtime.Politics
                    .EfficiencyMultiplier,
                characters = characters,
                corpses = corpses.ToArray(),
                succession = politics.Crisis == null
                    ? null
                    : new FormalThreeDSuccessionSaveData
                    {
                        phase = 1,
                        selectedCandidateId = politics.Crisis.CandidateId,
                        support = politics.Crisis.Support,
                    },
                internalFactions = factions,
                externalFactions = external,
            };
        }

        private static bool TryRestoreArmy(
            CivilizationExpansionRuntime runtime,
            FormalThreeDArmyLeaderSaveData source,
            out string error)
        {
            if (source == null)
            {
                error = "军队存档缺失";
                return false;
            }
            FormalThreeDArmySquadSaveData squad =
                source.squads != null && source.squads.Length > 0
                    ? source.squads[0]
                    : new FormalThreeDArmySquadSaveData
                    {
                        stableSquadId = SingleCityArmyModel.DefaultSquadId,
                    };
            var command = new FriendlyUnitCommandPersistenceSnapshot(
                squad.hasFixedRally,
                squad.rallyFloatX,
                squad.rallyFloatY,
                (FriendlySquadCommandType)squad.command,
                squad.hasExpeditionTarget,
                squad.destinationX,
                squad.destinationY,
                squad.puppetLosses,
                squad.behemothLosses,
                squad.controlledLosses);
            var units = new ArmyUnitPersistenceState[source.units?.Length ?? 0];
            for (var index = 0; index < units.Length; index++)
            {
                FormalThreeDArmyUnitSaveData item = source.units[index];
                units[index] = new ArmyUnitPersistenceState(
                    item.stableUnitId,
                    item.definitionId,
                    item.squadId,
                    item.currentHealth,
                    item.maintenanceElapsedSeconds,
                    !item.dormant);
            }
            var manufacturing = new ArmyManufacturingPersistenceState[
                source.manufacturing?.Length ?? 0];
            for (var index = 0; index < manufacturing.Length; index++)
            {
                manufacturing[index] = new ArmyManufacturingPersistenceState(
                    source.manufacturing[index].definitionId,
                    source.manufacturing[index].progressSeconds);
            }
            var losses = new ArmyUnitLossPersistenceState[
                source.losses?.Length ?? 0];
            for (var index = 0; index < losses.Length; index++)
            {
                losses[index] = new ArmyUnitLossPersistenceState(
                    source.losses[index].definitionId,
                    source.losses[index].count);
            }
            var snapshot = new SingleCityArmyPersistenceSnapshot(
                checked((int)source.nextUnitOrdinal),
                manufacturing,
                units,
                command,
                source.leaderAssigned || squad.leaderAssigned,
                source.leaderHealthy || squad.leaderHealthy,
                losses);
            if (!runtime.Army.TryPrepareRestoreForPersistence(
                    snapshot,
                    out SingleCityArmyRestorePlan plan,
                    out error) ||
                !runtime.Army.TryCommitRestoreForPersistence(plan, out error))
                return false;
            runtime.RestoreNextExpeditionOrdinal(
                checked((int)source.nextExpeditionOrdinal));
            var expedition = new ArmyExpeditionModel();
            if (source.expedition != null &&
                source.expedition.phase !=
                    (int)ArmyExpeditionStatus.Idle)
            {
                ArmyExpeditionPersistenceSnapshot expeditionSnapshot =
                    ToExpeditionSnapshot(source.expedition);
                if (!expedition.TryPrepareRestoreForPersistence(
                        expeditionSnapshot,
                        out ArmyExpeditionRestorePlan expeditionPlan,
                        out error) ||
                    !expedition.TryCommitRestoreForPersistence(
                        expeditionPlan,
                        out error))
                    return false;
            }
            runtime.ReplaceExpeditionForRestore(expedition);
            error = string.Empty;
            return true;
        }

        private static ArmyExpeditionPersistenceSnapshot ToExpeditionSnapshot(
            FormalThreeDArmyExpeditionSaveData source)
        {
            var units = new ArmyExpeditionUnit[source.units?.Length ?? 0];
            for (var index = 0; index < units.Length; index++)
            {
                units[index] = new ArmyExpeditionUnit(
                    source.units[index].stableUnitId,
                    source.units[index].definitionId,
                    source.units[index].currentHealth,
                    source.units[index].active);
            }
            return new ArmyExpeditionPersistenceSnapshot(
                (ArmyExpeditionStatus)source.phase,
                source.sessionId,
                source.targetX,
                source.targetY,
                source.expeditionOrdinal,
                source.outboundDurationSeconds,
                source.returnDurationSeconds,
                source.remainingSeconds,
                units,
                source.leaderHealthy,
                source.retreatRequested,
                source.enemyDefinitionIds ?? Array.Empty<string>(),
                source.hasResolution,
                source.armyPower,
                source.enemyPower,
                source.casualtyStableUnitIds ?? Array.Empty<string>(),
                ToAmounts(source.pendingLoot),
                source.victory);
        }

        private static bool TryRestoreWorld(
            CivilizationExpansionRuntime runtime,
            FormalThreeDWorldLayerSaveData source,
            out string error)
        {
            if (source == null)
            {
                error = "世界层存档缺失";
                return false;
            }
            if (source.settlements == null || source.settlements.Length == 0)
            {
                error = string.Empty;
                return true;
            }
            var settlements = new SettlementRuntimeSnapshot[
                source.settlements.Length];
            for (var index = 0; index < settlements.Length; index++)
            {
                FormalThreeDSettlementSaveData item =
                    source.settlements[index];
                settlements[index] = new SettlementRuntimeSnapshot(
                    item.stableSettlementId,
                    (SettlementKind)item.kind,
                    item.x,
                    item.y,
                    (SettlementAutonomyTemplate)item.autonomousTemplate,
                    item.population,
                    item.populationCapacity,
                    item.loyalty,
                    item.communicationConnected,
                    item.supplyConnected,
                    item.maintenanceConnected,
                    item.productionRemainingSeconds,
                    ToAmounts(item.inventory),
                    item.revision);
            }
            var world = new WorldLayerRuntimeSnapshot(
                source.revision,
                checked((int)source.nextSettlementOrdinal),
                source.focusedSettlementId,
                source.controlledCityId,
                settlements);
            if (!runtime.WorldLayer.TryRestore(world, out error)) return false;
            var convoys = new ConvoySnapshot[source.convoys?.Length ?? 0];
            for (var index = 0; index < convoys.Length; index++)
            {
                FormalThreeDConvoySaveData item = source.convoys[index];
                convoys[index] = new ConvoySnapshot(
                    item.stableConvoyId,
                    item.sessionId,
                    item.sourceSettlementId,
                    item.destinationSettlementId,
                    ToAmounts(item.cargo),
                    ToPoints(item.path),
                    item.completedPathCells,
                    item.segmentProgressSeconds,
                    item.escortSquadId,
                    item.riskResolved,
                    item.appliedRiskPercent,
                    (ConvoyStatus)item.status);
            }
            return runtime.Transport.TryRestore(
                new TransportRuntimeSnapshot(
                    source.revision,
                    checked((int)source.nextConvoyOrdinal),
                    convoys),
                runtime,
                out error);
        }

        private static bool TryRestorePolitics(
            CivilizationExpansionRuntime runtime,
            FormalThreeDCharactersPoliticsSaveData source,
            out string error)
        {
            if (source == null)
            {
                error = "角色政治存档缺失";
                return false;
            }
            if (source.characters != null && source.characters.Length > 0)
            {
                for (var index = 0; index < source.characters.Length; index++)
                {
                    FormalThreeDCharacterSaveData item =
                        source.characters[index];
                    CharacterLifeRuntime target = runtime.FindCharacter(
                        item.characterId);
                    if (target == null)
                    {
                        error = "角色存档引用了未知角色";
                        return false;
                    }
                    if (!target.TryRestore(
                            ToCharacterSnapshot(item, source.corpses),
                            out error))
                        return false;
                }
            }
            if (source.internalFactions != null &&
                source.internalFactions.Length > 0)
            {
                var factions = new InternalFactionStateSnapshot[
                    source.internalFactions.Length];
                for (var index = 0; index < factions.Length; index++)
                {
                    FormalThreeDInternalFactionSaveData item =
                        source.internalFactions[index];
                    var supports = new FactionCandidateSupportSnapshot[
                        item.candidateSupports?.Length ?? 0];
                    for (var supportIndex = 0;
                         supportIndex < supports.Length;
                         supportIndex++)
                    {
                        supports[supportIndex] =
                            new FactionCandidateSupportSnapshot(
                                item.candidateSupports[supportIndex]
                                    .characterId,
                                item.candidateSupports[supportIndex].support);
                    }
                    factions[index] = new InternalFactionStateSnapshot(
                        item.factionId,
                        item.influence,
                        item.loyalty,
                        supports);
                }
                CoupCrisisSnapshot crisis = source.succession != null &&
                    source.succession.phase == 1
                        ? new CoupCrisisSnapshot(
                            source.succession.selectedCandidateId,
                            source.succession.support)
                        : null;
                if (!runtime.Politics.TryRestore(
                        new LeadershipPoliticsSnapshot(
                            source.currentLeaderId,
                            source.designatedSuccessorId,
                            source.leadershipState == 1,
                            crisis,
                            factions),
                        out error))
                    return false;
            }
            if (source.externalFactions != null &&
                source.externalFactions.Length > 0)
            {
                string sessionId = string.IsNullOrWhiteSpace(
                    source.diplomacySessionId)
                        ? DiplomacyRuntime.DefaultSessionId
                        : source.diplomacySessionId;
                var diplomacy = new DiplomacyRuntime(sessionId);
                var factions = new DiplomacyFactionStateSnapshot[
                    source.externalFactions.Length];
                for (var index = 0; index < factions.Length; index++)
                {
                    FormalThreeDExternalFactionSaveData item =
                        source.externalFactions[index];
                    factions[index] = new DiplomacyFactionStateSnapshot(
                        item.factionId,
                        (DiplomacyRelationshipState)item.state,
                        item.relation,
                        item.offerCooldownRemainingSeconds,
                        ToOffer(item.activeOffer));
                }
                if (!diplomacy.TryRestore(
                        new DiplomacyRuntimeSnapshot(
                            sessionId,
                            source.nextOfferOrdinal,
                            factions,
                            source.convoyInterceptionImmunityCharges),
                        out error))
                    return false;
                runtime.ReplaceDiplomacyForRestore(diplomacy);
            }
            error = string.Empty;
            return true;
        }

        private static CharacterLifeSnapshot ToCharacterSnapshot(
            FormalThreeDCharacterSaveData source,
            IReadOnlyList<FormalThreeDCorpseSaveData> corpses)
        {
            FormalThreeDCorpseSaveData corpse = null;
            if (corpses != null)
            {
                for (var index = 0; index < corpses.Count; index++)
                {
                    if (string.Equals(
                            corpses[index].characterId,
                            source.characterId,
                            StringComparison.Ordinal))
                    {
                        corpse = corpses[index];
                        break;
                    }
                }
            }
            return new CharacterLifeSnapshot(
                source.characterId,
                (CharacterLifeState)source.state,
                source.currentHealth,
                source.loyalty,
                source.assignedSettlementId,
                source.x,
                source.y,
                source.downedRemainingSeconds,
                source.recoveryRemainingSeconds,
                source.downedElapsedSeconds,
                source.downCount,
                source.downedCauseId,
                source.rescue == null ||
                string.IsNullOrWhiteSpace(source.rescue.sourceId)
                    ? null
                    : new CharacterRescueSnapshot(
                        (CharacterRescueMethod)source.rescue.method,
                        source.rescue.sourceId,
                        source.rescue.remainingSeconds,
                        source.rescue.reservedBiomass),
                source.permanentInjuryIds ?? Array.Empty<string>(),
                source.equipmentIds ?? Array.Empty<string>(),
                corpse == null
                    ? null
                    : new CharacterCorpseSnapshot(
                        corpse.characterId,
                        corpse.settlementId,
                        corpse.x,
                        corpse.y,
                        corpse.equipmentIds,
                        corpse.recovered));
        }

        private static FormalThreeDRescueSaveData ToSaveRescue(
            CharacterLifeSnapshot source)
        {
            return source.Rescue == null
                ? null
                : new FormalThreeDRescueSaveData
                {
                    rescueId = "core.rescue." + source.CharacterId,
                    targetCharacterId = source.CharacterId,
                    sourceId = source.Rescue.SourceId,
                    method = (int)source.Rescue.Method,
                    progressSeconds = source.Rescue.RemainingSeconds,
                    remainingSeconds = source.Rescue.RemainingSeconds,
                    reservedBiomass = source.Rescue.ReservedBiomass,
                };
        }

        private static FormalThreeDDiplomacyOfferSaveData ToSaveOffer(
            DiplomacyOfferSnapshot source)
        {
            return source == null ||
                   string.IsNullOrWhiteSpace(source.StableOfferId)
                ? null
                : new FormalThreeDDiplomacyOfferSaveData
                {
                    offerId = source.StableOfferId,
                    factionId = source.FactionId,
                    kind = (int)source.Kind,
                    giveResourceId = source.CostResourceId,
                    giveAmount = source.CostAmount,
                    receiveResourceId = source.RewardResourceId,
                    receiveAmount = source.RewardAmount,
                    grantsConvoyImmunity =
                        source.GrantsConvoyInterceptionImmunity,
                    remainingSeconds = source.RemainingSeconds,
                };
        }

        private static DiplomacyOfferSnapshot ToOffer(
            FormalThreeDDiplomacyOfferSaveData source)
        {
            return source == null ||
                   string.IsNullOrWhiteSpace(source.offerId)
                ? null
                : new DiplomacyOfferSnapshot(
                    source.offerId,
                    source.factionId,
                    (DiplomacyOfferKind)source.kind,
                    source.giveResourceId,
                    source.giveAmount,
                    source.receiveResourceId,
                    source.receiveAmount,
                    source.remainingSeconds);
        }

        private static FormalThreeDResourceAmountSaveData[] ToSaveAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            var result = new FormalThreeDResourceAmountSaveData[
                source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new FormalThreeDResourceAmountSaveData
                {
                    resourceId = source[index].ResourceId,
                    amount = source[index].Amount,
                };
            }
            return result;
        }

        private static ResourceAmount[] ToAmounts(
            IReadOnlyList<FormalThreeDResourceAmountSaveData> source)
        {
            var result = new ResourceAmount[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new ResourceAmount(
                    source[index].resourceId,
                    source[index].amount);
            }
            return result;
        }

        private static FormalThreeDGridPointSaveData[] ToSavePoints(
            IReadOnlyList<WorldGridPoint> source)
        {
            var result = new FormalThreeDGridPointSaveData[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new FormalThreeDGridPointSaveData
                {
                    x = source[index].X,
                    y = source[index].Y,
                };
            }
            return result;
        }

        private static WorldGridPoint[] ToPoints(
            IReadOnlyList<FormalThreeDGridPointSaveData> source)
        {
            var result = new WorldGridPoint[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
                result[index] = new WorldGridPoint(
                    source[index].x,
                    source[index].y);
            return result;
        }

        private static string[] UnitIds(
            IReadOnlyList<ArmyUnitPersistenceState> source)
        {
            var result = new string[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index].StableUnitId;
            return result;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }

        private static string[] Clone(string[] source)
        {
            return source == null
                ? Array.Empty<string>()
                : (string[])source.Clone();
        }
    }
}
