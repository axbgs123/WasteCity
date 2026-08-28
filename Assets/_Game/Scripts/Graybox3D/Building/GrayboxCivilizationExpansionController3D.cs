using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WasteCity.Building;
using WasteCity.CivilizationExpansion;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.World;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxCivilizationExpansionController3D :
        MonoBehaviour
    {
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxWorldView3D world;
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxDefenseController3D defense;
        [SerializeField] private GrayboxLeaderController3D leader;
        [SerializeField] private GrayboxCivilizationExpansionView3D view;

        private PrimarySettlementAccount primaryAccount;
        private Func<string> sessionIdProvider;
        private ulong presentationFingerprint;
        private readonly Dictionary<DamageType, float> guardDamageRemainders =
            new Dictionary<DamageType, float>();
        private Transform markerRoot;
        private readonly List<GameObject> worldMarkers =
            new List<GameObject>();
        private Material settlementMarkerMaterial;
        private Material outpostMarkerMaterial;
        private Material squadMarkerMaterial;
        private Material convoyMarkerMaterial;
        private float leaderTravelRemainingSeconds;
        private string leaderTravelDestinationId = string.Empty;

        public CivilizationExpansionRuntime Runtime { get; private set; }
        public bool IsInitialized => Runtime != null;
        public string LastFeedback { get; private set; } = string.Empty;

        public void Configure(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingSession3D session,
            GrayboxDefenseController3D defense,
            GrayboxLeaderController3D leader,
            GrayboxCivilizationExpansionView3D view,
            Func<string> sessionIdProvider)
        {
            this.city = city;
            this.world = world;
            this.session = session;
            this.defense = defense;
            this.leader = leader;
            this.view = view;
            this.sessionIdProvider = sessionIdProvider;
        }

        public void ConfigureSessionIdProvider(Func<string> provider)
        {
            sessionIdProvider = provider ??
                throw new ArgumentNullException(nameof(provider));
        }

        public bool TryInitialize(out string error)
        {
            if (Runtime != null)
            {
                error = string.Empty;
                return true;
            }
            if (city == null || world?.Model == null || session == null ||
                session.CityStorage == null || view == null ||
                !city.TryGetCurrentCell(out int cityX, out int cityY))
            {
                error = "文明扩展场景引用或主城格尚未就绪";
                return false;
            }
            primaryAccount = new PrimarySettlementAccount(session);
            Runtime = new CivilizationExpansionRuntime(
                world.Model,
                cityX,
                cityY,
                primaryAccount);
            Runtime.Army.SetLeaderAssignment(
                leader?.Model?.Recruited == true,
                leader?.Model?.Injured == false);
            BindView();
            Refresh(force: true);
            error = string.Empty;
            return true;
        }

        public void Tick(float ruleDeltaSeconds, bool paused)
        {
            if (!TryInitialize(out _)) return;
            Runtime.EnsureDiplomacySession(sessionIdProvider?.Invoke());
            Runtime.Army.SetLeaderAssignment(
                leader?.Model?.Recruited == true,
                leader?.Model?.Injured == false);
            Runtime.Tick(
                ruleDeltaSeconds,
                paused,
                session.CityStorage,
                CountOperationalBuildings);
            TickLeaderTravel(ruleDeltaSeconds, paused);
            ApplyGuardDamage(ruleDeltaSeconds, paused);
            Refresh(force: false);
        }

        public void TogglePage(GrayboxCivilizationExpansionPage3D page)
        {
            if (!TryInitialize(out string error))
            {
                LastFeedback = error;
                return;
            }
            view.Toggle(page);
            Refresh(force: true);
        }

        public void Close()
        {
            view?.Close();
        }

        public bool ResetForNewProgress(out string error)
        {
            Runtime = null;
            primaryAccount = null;
            presentationFingerprint = 0ul;
            LastFeedback = string.Empty;
            guardDamageRemainders.Clear();
            leaderTravelRemainingSeconds = 0f;
            leaderTravelDestinationId = string.Empty;
            bool initialized = TryInitialize(out error);
            if (initialized)
                Runtime.EnsureDiplomacySession(sessionIdProvider?.Invoke());
            return initialized;
        }

        public void Refresh(bool force)
        {
            if (Runtime == null || view == null) return;
            ulong next = Fingerprint();
            if (!force && next == presentationFingerprint) return;
            presentationFingerprint = next;
            view.Apply(BuildPresentation(view.Page));
            RefreshWorldMarkers();
        }

        private void BindView()
        {
            view.PrimaryRequested -= HandlePrimary;
            view.SecondaryRequested -= HandleSecondary;
            view.TertiaryRequested -= HandleTertiary;
            view.PageChanged -= HandlePageChanged;
            view.PrimaryRequested += HandlePrimary;
            view.SecondaryRequested += HandleSecondary;
            view.TertiaryRequested += HandleTertiary;
            view.PageChanged += HandlePageChanged;
        }

        private void HandlePageChanged(
            GrayboxCivilizationExpansionPage3D page)
        {
            Refresh(force: true);
        }

        private void HandlePrimary()
        {
            switch (view.Page)
            {
                case GrayboxCivilizationExpansionPage3D.Army:
                    Runtime.Army.Commands.Guard();
                    Feedback("默认小队已切换为守卫主城");
                    break;
                case GrayboxCivilizationExpansionPage3D.World:
                    HandleSecondaryCityCommand();
                    break;
                default:
                    AdvanceDiplomacy(ExternalFactionCatalog.AshCaravan.Id.Value);
                    break;
            }
        }

        private void HandleSecondary()
        {
            switch (view.Page)
            {
                case GrayboxCivilizationExpansionPage3D.Army:
                    Runtime.Army.Commands.FollowLeader();
                    Feedback("默认小队已切换为跟随领袖");
                    break;
                case GrayboxCivilizationExpansionPage3D.World:
                    TryEstablishOutpost();
                    break;
                default:
                    AdvanceDiplomacy(
                        ExternalFactionCatalog.CrystalAccord.Id.Value);
                    break;
            }
        }

        private void HandleTertiary()
        {
            switch (view.Page)
            {
                case GrayboxCivilizationExpansionPage3D.Army:
                    if (Runtime.Expedition.Status ==
                            ArmyExpeditionStatus.Outbound ||
                        Runtime.Expedition.Status ==
                            ArmyExpeditionStatus.Returning)
                    {
                        Feedback(Runtime.RetreatExpedition()
                            ? "远征队正在撤回，途中战利品已丢弃"
                            : "当前远征不能撤退");
                    }
                    else
                    {
                        TryStartExpedition();
                    }
                    break;
                case GrayboxCivilizationExpansionPage3D.World:
                    TryDispatchConvoy();
                    break;
                default:
                    TryPoliticalAction();
                    break;
            }
        }

        private void TryStartExpedition()
        {
            if (!TryFindOpenCell(minimumDistance: 6, out int x, out int y))
            {
                Feedback("当前没有合适的已揭示远征格");
                return;
            }
            string sessionId = sessionIdProvider?.Invoke();
            if (Runtime.TryStartExpedition(
                    sessionId,
                    x,
                    y,
                    out string error))
                Feedback("远征队已出发至 " + x + "," + y);
            else
                Feedback(error);
        }

        private void TryEstablishSecondary()
        {
            if (Runtime.WorldLayer.GetSettlement(
                    WorldLayerCatalog.SecondaryCity.Id) != null)
            {
                Feedback("次城已经建立");
                return;
            }
            if (!TryFindOpenCell(8, out int x, out int y))
            {
                Feedback("没有合适的次城落点");
                return;
            }
            bool succeeded = Runtime.WorldLayer.TryEstablishSecondary(
                x,
                y,
                SettlementAutonomyTemplate.Industrial,
                primaryAccount,
                out _,
                out string error);
            Feedback(succeeded
                ? "工业次城已建立，拥有独立 150 容量库存"
                : error);
        }

        private void HandleSecondaryCityCommand()
        {
            SettlementRuntime secondary = Runtime.WorldLayer.GetSettlement(
                WorldLayerCatalog.SecondaryCity.Id);
            if (secondary == null)
            {
                TryEstablishSecondary();
                return;
            }
            Runtime.WorldLayer.TryFocus(secondary.StableId);
            bool remoteUnlocked = session.IsResearchCompleted(
                "core.research.collective-consciousness");
            CharacterLifeRuntime currentLeader = Runtime.FindCharacter(
                Runtime.Politics.CurrentLeaderId);
            bool leaderPresent = currentLeader != null &&
                string.Equals(
                    currentLeader.AssignedSettlementId,
                    secondary.StableId,
                    StringComparison.Ordinal);
            if (Runtime.WorldLayer.TryControlCity(
                    secondary.StableId,
                    remoteUnlocked,
                    leaderPresent))
            {
                Feedback(remoteUnlocked
                    ? "已通过集体意识远程接管次城"
                    : "领袖已接管次城");
                return;
            }
            if (leaderTravelRemainingSeconds > 0f)
            {
                Feedback("领袖调动仍需 " +
                    leaderTravelRemainingSeconds.ToString("0") + " 秒");
                return;
            }
            leaderTravelDestinationId = secondary.StableId;
            leaderTravelRemainingSeconds = 30f;
            Feedback("领袖开始前往次城，30 秒后可接管");
        }

        private void TickLeaderTravel(float deltaSeconds, bool paused)
        {
            if (paused || leaderTravelRemainingSeconds <= 0f ||
                string.IsNullOrWhiteSpace(leaderTravelDestinationId))
                return;
            leaderTravelRemainingSeconds = Mathf.Max(
                0f,
                leaderTravelRemainingSeconds - Mathf.Max(0f, deltaSeconds));
            if (leaderTravelRemainingSeconds > 0f) return;
            SettlementRuntime destination = Runtime.WorldLayer.GetSettlement(
                leaderTravelDestinationId);
            CharacterLifeRuntime currentLeader = Runtime.FindCharacter(
                Runtime.Politics.CurrentLeaderId);
            if (destination != null && currentLeader != null)
            {
                currentLeader.SetPosition(
                    destination.StableId,
                    destination.X,
                    destination.Y);
                Runtime.WorldLayer.TryControlCity(
                    destination.StableId,
                    remoteCommandUnlocked: false,
                    leaderPresent: true);
                LastFeedback = "领袖已到达并接管次城";
            }
            leaderTravelDestinationId = string.Empty;
            Refresh(force: true);
        }

        private void TryEstablishOutpost()
        {
            if (Runtime.WorldLayer.GetSettlement(
                    WorldLayerCatalog.Outpost.Id) != null)
            {
                Feedback("前哨已经建立");
                return;
            }
            if (!TryFindOpenCell(5, out int x, out int y))
            {
                Feedback("没有合适的前哨落点");
                return;
            }
            bool succeeded = Runtime.WorldLayer.TryEstablishOutpost(
                x,
                y,
                primaryAccount,
                out _,
                out string error);
            Feedback(succeeded
                ? "前哨已建立，补给正常时每 12 秒产出 1 石料"
                : error);
        }

        private void TryDispatchConvoy()
        {
            SettlementRuntime secondary = Runtime.WorldLayer.GetSettlement(
                WorldLayerCatalog.SecondaryCity.Id);
            if (secondary == null)
            {
                Feedback("需要先建立次城");
                return;
            }
            var cargo = new[] { new ResourceAmount(ResourceIds.Stone, 10) };
            string escort = Runtime.IsNonDormant(
                    SingleCityArmyModel.DefaultSquadId)
                ? SingleCityArmyModel.DefaultSquadId
                : string.Empty;
            bool succeeded = Runtime.Transport.TryDispatch(
                sessionIdProvider?.Invoke(),
                WorldLayerCatalog.PrimaryCity.Id,
                secondary.StableId,
                cargo,
                escort,
                out string convoyId,
                out string error);
            Feedback(succeeded
                ? "运输队 " + convoyId + " 已装载 10 石料"
                : error);
        }

        private void AdvanceDiplomacy(string factionId)
        {
            DiplomacyFactionSnapshot faction = Runtime.Diplomacy.GetFaction(
                factionId);
            if (faction == null)
            {
                Feedback("外部势力不存在");
                return;
            }
            if (!faction.Contacted)
            {
                Feedback(Runtime.Diplomacy.EstablishContact(
                        factionId,
                        out string contactError)
                    ? "已建立外交接触"
                    : contactError);
                return;
            }
            if (faction.ActiveOffer == null)
            {
                Feedback(Runtime.Diplomacy.TryRefreshOffer(
                        factionId,
                        out _,
                        out string offerError)
                    ? "已收到新的外交报价"
                    : offerError);
                return;
            }
            Feedback(Runtime.Diplomacy.TryAcceptOffer(
                    factionId,
                    primaryAccount,
                    out _,
                    out string settleError)
                ? "外交交易已经原子结算"
                : settleError);
        }

        private void TryPoliticalAction()
        {
            for (var index = 0; index < Runtime.Characters.Count; index++)
            {
                CharacterLifeRuntime character = Runtime.Characters[index];
                if (character.State != CharacterLifeState.Downed) continue;
                if (character.HasActiveRescue)
                {
                    Feedback("城市医疗救援正在进行");
                    return;
                }
                int available = session.CityStorage.GetNetworkAmount(
                    CharacterLifeRuntime.RescueResourceId);
                if (available < CharacterLifeRuntime.RescueBiomassCost ||
                    !session.CityStorage.TrySpendFromNetwork(
                        CharacterLifeRuntime.RescueResourceId,
                        CharacterLifeRuntime.RescueBiomassCost))
                {
                    Feedback("救援需要 2 生物质");
                    return;
                }
                if (!character.TryBeginRescue(
                        CharacterRescueMethod.CityMedical,
                        character.AssignedSettlementId,
                        available,
                        out _,
                        out string rescueError))
                {
                    session.CityStorage.TryCommitBatch(
                        Array.Empty<ResourceAmount>(),
                        new[]
                        {
                            new ResourceAmount(
                                CharacterLifeRuntime.RescueResourceId,
                                CharacterLifeRuntime.RescueBiomassCost),
                        });
                    Feedback(rescueError);
                    return;
                }
                Feedback("城市医疗救援已启动，需保持 4 秒");
                return;
            }
            LeadershipPoliticsRuntime politics = Runtime.Politics;
            if (!politics.IsInterimCouncilActive)
            {
                Feedback(politics.TryDesignateSuccessor(
                        CharacterCatalog.LinXiId,
                        out string error)
                    ? "林溪已被指定为继承人"
                    : error);
                return;
            }
            if (politics.Crisis != null)
            {
                bool resolved = politics.TryResolveCoup(
                    CoupResolution.Concession,
                    primaryAccount,
                    WorldLayerCatalog.PrimaryCity.Id,
                    out _,
                    out string error);
                Feedback(resolved ? "政变危机已通过让步解决" : error);
                return;
            }
            bool selected = politics.TryChooseSuccessor(
                CharacterCatalog.LinXiId,
                forceLowSupport: true,
                out SuccessionCommandResult result,
                out string selectionError);
            Feedback(selected
                ? result == SuccessionCommandResult.Committed
                    ? "林溪已接任文明领袖"
                    : "低支持度强推触发政变危机"
                : selectionError);
        }

        private GrayboxCivilizationExpansionPresentation3D BuildPresentation(
            GrayboxCivilizationExpansionPage3D page)
        {
            switch (page)
            {
                case GrayboxCivilizationExpansionPage3D.World:
                    return WorldPresentation();
                case GrayboxCivilizationExpansionPage3D.Politics:
                    return PoliticsPresentation();
                default:
                    return ArmyPresentation();
            }
        }

        private GrayboxCivilizationExpansionPresentation3D ArmyPresentation()
        {
            var summary = new StringBuilder();
            summary.Append("默认小队  ")
                .Append(Runtime.Army.Units.Count)
                .Append(" / ")
                .Append(SingleCityArmyModel.DefaultSquadMaximumUnits)
                .Append("\n命令：")
                .Append(Runtime.Army.Commands.Command)
                .Append("\n远征：")
                .Append(Runtime.Expedition.Status);
            var details = new StringBuilder();
            for (var index = 0; index < ArmyUnitCatalog.All.Count; index++)
            {
                ArmyUnitDefinition unit = ArmyUnitCatalog.All[index];
                details.Append(unit.ChineseName)
                    .Append("  ")
                    .Append(Runtime.Army.UnitCount(unit.Id))
                    .Append("  制造 ")
                    .Append(Runtime.Army.ManufacturingProgress(unit.Id)
                        .ToString("0.0"))
                    .Append("/")
                    .Append(unit.ManufactureSeconds.ToString("0"))
                    .Append("s\n");
            }
            details.Append("\n").Append(LastFeedback);
            bool active = Runtime.Expedition.Status ==
                ArmyExpeditionStatus.Outbound ||
                Runtime.Expedition.Status == ArmyExpeditionStatus.Returning;
            return new GrayboxCivilizationExpansionPresentation3D(
                "军队与远征",
                summary.ToString(),
                details.ToString(),
                "守卫主城",
                true,
                "跟随领袖",
                true,
                active ? "撤回远征" : "派出远征",
                active || Runtime.Army.Units.Count > 0);
        }

        private GrayboxCivilizationExpansionPresentation3D WorldPresentation()
        {
            WorldLayerRuntimeSnapshot worldSnapshot =
                Runtime.WorldLayer.Capture();
            TransportRuntimeSnapshot transportSnapshot =
                Runtime.Transport.Capture();
            var details = new StringBuilder();
            for (var index = 0; index < worldSnapshot.Settlements.Count; index++)
            {
                SettlementRuntimeSnapshot settlement =
                    worldSnapshot.Settlements[index];
                details.Append(SettlementName(settlement.Kind))
                    .Append("  [")
                    .Append(settlement.X).Append(",")
                    .Append(settlement.Y).Append("]  忠诚 ")
                    .Append(settlement.Loyalty).Append("\n");
            }
            details.Append("\n运输队：")
                .Append(transportSnapshot.Convoys.Count)
                .Append("\n").Append(LastFeedback);
            return new GrayboxCivilizationExpansionPresentation3D(
                "世界层与多城市",
                "查看：" + worldSnapshot.FocusedSettlementId +
                "\n控制：" + worldSnapshot.ControlledCityId +
                "\n地图保持 64×48 v2，不改变现有资源节点",
                details.ToString(),
                Runtime.WorldLayer.GetSettlement(
                    WorldLayerCatalog.SecondaryCity.Id) == null
                        ? "建立工业次城"
                        : leaderTravelRemainingSeconds > 0f
                            ? "领袖调动中（" +
                                leaderTravelRemainingSeconds.ToString("0") +
                                "秒）"
                            : "查看 / 接管次城",
                true,
                "建立前哨",
                Runtime.WorldLayer.GetSettlement(
                    WorldLayerCatalog.Outpost.Id) == null,
                "派出石料运输队",
                Runtime.WorldLayer.GetSettlement(
                    WorldLayerCatalog.SecondaryCity.Id) != null);
        }

        private GrayboxCivilizationExpansionPresentation3D
            PoliticsPresentation()
        {
            var details = new StringBuilder();
            for (var index = 0; index < Runtime.Characters.Count; index++)
            {
                CharacterLifeRuntime character = Runtime.Characters[index];
                details.Append(character.Definition.DisplayName)
                    .Append("  ")
                    .Append(character.State)
                    .Append("  忠诚 ")
                    .Append(character.Loyalty).Append("\n");
            }
            for (var index = 0; index < ExternalFactionCatalog.All.Count;
                 index++)
            {
                DiplomacyFactionSnapshot faction = Runtime.Diplomacy.GetFaction(
                    ExternalFactionCatalog.All[index].Id.Value);
                details.Append(ExternalFactionCatalog.All[index].DisplayName)
                    .Append("  ")
                    .Append(faction.State)
                    .Append("  关系 ")
                    .Append(faction.Relation).Append("\n");
            }
            details.Append("\n").Append(LastFeedback);
            bool hasDowned = false;
            for (var index = 0; index < Runtime.Characters.Count; index++)
                hasDowned |= Runtime.Characters[index].State ==
                    CharacterLifeState.Downed;
            return new GrayboxCivilizationExpansionPresentation3D(
                "角色、内政与外交",
                "当前领袖：" + Runtime.Politics.CurrentLeaderId +
                "\n临时议会：" +
                (Runtime.Politics.IsInterimCouncilActive ? "运行中" : "未启动") +
                "\n文明效率：" +
                Runtime.Politics.EfficiencyMultiplier.ToString("0%"),
                details.ToString(),
                "灰烬商团：接触 / 报价 / 接受",
                true,
                "晶律协定会：接触 / 报价 / 接受",
                true,
                hasDowned
                    ? "启动城市医疗救援"
                    : Runtime.Politics.Crisis != null
                    ? "让步解决政变"
                    : Runtime.Politics.IsInterimCouncilActive
                        ? "推举林溪"
                        : "指定林溪为继承人",
                true);
        }

        private int CountOperationalBuildings(string buildingId)
        {
            int count = 0;
            IReadOnlyList<GrayboxBuildingInstance3D> instances =
                session.Instances;
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance.IsPlayerOwned &&
                    instance.State == GrayboxBuildingInstanceState.Completed &&
                    string.Equals(
                        instance.Placement.Definition.Id.Value,
                        buildingId,
                        StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private void ApplyGuardDamage(float ruleDeltaSeconds, bool paused)
        {
            if (paused || ruleDeltaSeconds <= 0f || defense?.Runtime == null ||
                Runtime.Army.Commands.Command !=
                    FriendlySquadCommandType.Guard)
                return;
            var damagePerType = new Dictionary<DamageType, float>();
            IReadOnlyList<ArmyUnitSnapshot> units = Runtime.Army.Units;
            for (var index = 0; index < units.Count; index++)
            {
                if (!units[index].IsActive) continue;
                ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                    units[index].DefinitionId);
                if (definition == null) continue;
                damagePerType.TryGetValue(
                    definition.DamageType,
                    out float before);
                damagePerType[definition.DamageType] =
                    before + definition.Damage;
            }
            foreach (KeyValuePair<DamageType, float> pair in damagePerType)
            {
                guardDamageRemainders.TryGetValue(
                    pair.Key,
                    out float remainder);
                remainder += pair.Value *
                    Runtime.Army.ResolveSquadDamageMultiplier() *
                    ruleDeltaSeconds;
                int whole = Mathf.FloorToInt(remainder);
                guardDamageRemainders[pair.Key] = remainder - whole;
                if (whole > 0)
                    defense.Runtime.TryApplyArmyGuardDamage(
                        whole,
                        pair.Key);
            }
        }

        private bool TryFindOpenCell(
            int minimumDistance,
            out int cellX,
            out int cellY)
        {
            SettlementRuntime primary = Runtime.WorldLayer.PrimaryCity;
            for (var radius = Math.Max(1, minimumDistance);
                 radius < Math.Max(world.Model.Width, world.Model.Height);
                 radius++)
            {
                for (var y = 0; y < world.Model.Height; y++)
                for (var x = 0; x < world.Model.Width; x++)
                {
                    if (Math.Abs(x - primary.X) + Math.Abs(y - primary.Y) <
                        radius ||
                        !world.Model.IsRevealed(x, y) ||
                        !CityTerrainRules.IsPassable(world.Model.Get(x, y)))
                        continue;
                    bool occupied = false;
                    WorldLayerRuntimeSnapshot snapshot =
                        Runtime.WorldLayer.Capture();
                    for (var index = 0;
                         index < snapshot.Settlements.Count;
                         index++)
                    {
                        if (snapshot.Settlements[index].X == x &&
                            snapshot.Settlements[index].Y == y)
                        {
                            occupied = true;
                            break;
                        }
                    }
                    if (occupied) continue;
                    cellX = x;
                    cellY = y;
                    return true;
                }
            }
            cellX = -1;
            cellY = -1;
            return false;
        }

        private void Feedback(string message)
        {
            LastFeedback = string.IsNullOrWhiteSpace(message)
                ? "操作未完成"
                : message;
            Refresh(force: true);
        }

        private ulong Fingerprint()
        {
            unchecked
            {
                ulong value = Runtime.WorldLayer.Revision;
                value = value * 397ul + Runtime.Transport.Revision;
                value = value * 397ul + Runtime.Army.Commands.Revision;
                value = value * 397ul + (ulong)Runtime.Army.Units.Count;
                value = value * 397ul + (ulong)Runtime.Expedition.Status;
                value = value * 397ul + (ulong)(view?.Page ?? 0);
                value = value * 397ul + (ulong)LastFeedback.GetHashCode();
                return value;
            }
        }

        private void RefreshWorldMarkers()
        {
            for (var index = 0; index < worldMarkers.Count; index++)
                DestroyObject(worldMarkers[index]);
            worldMarkers.Clear();
            markerRoot ??= new GameObject(
                "CivilizationExpansion.WorldMarkers").transform;
            markerRoot.SetParent(transform, false);
            EnsureMarkerMaterials();

            WorldLayerRuntimeSnapshot layer = Runtime.WorldLayer.Capture();
            for (var index = 0; index < layer.Settlements.Count; index++)
            {
                SettlementRuntimeSnapshot settlement =
                    layer.Settlements[index];
                if (settlement.Kind == SettlementKind.PrimaryCity) continue;
                AddMarker(
                    settlement.Kind == SettlementKind.Outpost
                        ? PrimitiveType.Cylinder
                        : PrimitiveType.Cube,
                    "SettlementMarker." + settlement.StableId,
                    settlement.X,
                    settlement.Y,
                    settlement.Kind == SettlementKind.Outpost ? .72f : 1.15f,
                    settlement.Kind == SettlementKind.Outpost
                        ? outpostMarkerMaterial
                        : settlementMarkerMaterial,
                    height: settlement.Kind == SettlementKind.Outpost
                        ? .32f
                        : .72f);
            }
            if (Runtime.Army.Units.Count > 0)
            {
                SettlementRuntime primary = Runtime.WorldLayer.PrimaryCity;
                AddMarker(
                    PrimitiveType.Sphere,
                    "SquadMarker." + SingleCityArmyModel.DefaultSquadId,
                    primary.X + 1,
                    primary.Y,
                    .58f,
                    squadMarkerMaterial,
                    .42f);
            }
            TransportRuntimeSnapshot transport = Runtime.Transport.Capture();
            for (var index = 0; index < transport.Convoys.Count; index++)
            {
                ConvoySnapshot convoy = transport.Convoys[index];
                if (convoy.Status == ConvoyStatus.Delivered ||
                    convoy.Status == ConvoyStatus.Destroyed ||
                    convoy.Path.Count == 0)
                    continue;
                int pathIndex = Mathf.Clamp(
                    convoy.CompletedPathCells,
                    0,
                    convoy.Path.Count - 1);
                AddMarker(
                    PrimitiveType.Cube,
                    "ConvoyMarker." + convoy.StableId,
                    convoy.Path[pathIndex].X,
                    convoy.Path[pathIndex].Y,
                    .42f,
                    convoyMarkerMaterial,
                    .30f);
            }
        }

        private void AddMarker(
            PrimitiveType primitive,
            string markerName,
            int cellX,
            int cellY,
            float scale,
            Material material,
            float height)
        {
            if (world?.Coordinates == null ||
                !world.Coordinates.TryCellToWorld(
                    cellX,
                    cellY,
                    height,
                    out Vector3 position))
                return;
            GameObject marker = GameObject.CreatePrimitive(primitive);
            marker.name = markerName;
            marker.transform.SetParent(markerRoot, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(
                scale,
                primitive == PrimitiveType.Cylinder ? .36f : scale,
                scale);
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) DestroyObject(collider);
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            worldMarkers.Add(marker);
        }

        private void EnsureMarkerMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            settlementMarkerMaterial ??= MarkerMaterial(
                shader, new Color(.23f, .72f, .78f, 1f));
            outpostMarkerMaterial ??= MarkerMaterial(
                shader, new Color(.88f, .64f, .24f, 1f));
            squadMarkerMaterial ??= MarkerMaterial(
                shader, new Color(.42f, .92f, .58f, 1f));
            convoyMarkerMaterial ??= MarkerMaterial(
                shader, new Color(.92f, .84f, .38f, 1f));
        }

        private static Material MarkerMaterial(Shader shader, Color color)
        {
            var material = new Material(shader)
            {
                hideFlags = HideFlags.DontSave,
                color = color,
            };
            return material;
        }

        private static string SettlementName(SettlementKind kind)
        {
            switch (kind)
            {
                case SettlementKind.SecondaryCity: return "次城";
                case SettlementKind.Outpost: return "前哨";
                default: return "主城";
            }
        }

        private void OnDestroy()
        {
            if (view == null) return;
            view.PrimaryRequested -= HandlePrimary;
            view.SecondaryRequested -= HandleSecondary;
            view.TertiaryRequested -= HandleTertiary;
            view.PageChanged -= HandlePageChanged;
            for (var index = 0; index < worldMarkers.Count; index++)
                DestroyObject(worldMarkers[index]);
            worldMarkers.Clear();
            DestroyObject(markerRoot?.gameObject);
            DestroyObject(settlementMarkerMaterial);
            DestroyObject(outpostMarkerMaterial);
            DestroyObject(squadMarkerMaterial);
            DestroyObject(convoyMarkerMaterial);
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private sealed class PrimarySettlementAccount :
            ISettlementInventoryEndpoint,
            ISettlementConstructionAccount,
            IDiplomacyResourceWallet,
            ILeadershipPoliticsResolutionAuthority
        {
            private readonly GrayboxBuildingSession3D session;

            public PrimarySettlementAccount(
                GrayboxBuildingSession3D session)
            {
                this.session = session ??
                    throw new ArgumentNullException(nameof(session));
            }

            public string StableSettlementId =>
                WorldLayerCatalog.PrimaryCity.Id;
            public int Population => session.Population;
            public int AcceptableSpace
            {
                get
                {
                    long total = 0;
                    for (var index = 0; index < ResourceIds.All.Length; index++)
                        total += session.CityStorage.GetNetworkAcceptableSpace(
                            ResourceIds.All[index]);
                    return (int)Math.Min(int.MaxValue, total);
                }
            }

            public int GetAmount(string resourceId) =>
                session.CityStorage.GetNetworkAmount(resourceId);

            public bool TryExtract(IReadOnlyList<ResourceAmount> amounts) =>
                session.CityStorage.TryCommitBatch(
                    amounts,
                    Array.Empty<ResourceAmount>());

            public bool TryAccept(IReadOnlyList<ResourceAmount> amounts) =>
                session.CityStorage.TryCommitBatch(
                    Array.Empty<ResourceAmount>(),
                    amounts);

            public bool TryCommit(
                IReadOnlyList<ResourceAmount> costs,
                int populationCost)
            {
                if (populationCost < 0 || session.Population < populationCost)
                    return false;
                for (var index = 0; index < costs.Count; index++)
                {
                    if (GetAmount(costs[index].ResourceId) <
                        costs[index].Amount)
                        return false;
                }
                if (!TryExtract(costs)) return false;
                if (session.TryRestorePopulation(
                        session.Population - populationCost,
                        session.PopulationCapacity,
                        out _))
                    return true;
                TryAccept(costs);
                return false;
            }

            public bool TryExchange(
                string costResourceId,
                int costAmount,
                string rewardResourceId,
                int rewardAmount,
                out string error)
            {
                bool succeeded = session.CityStorage.TryCommitBatch(
                    new[] { new ResourceAmount(costResourceId, costAmount) },
                    new[] { new ResourceAmount(rewardResourceId, rewardAmount) });
                error = succeeded ? string.Empty : "外交交易所需资源或容量不足";
                return succeeded;
            }

            public bool TrySpendResource(
                string resourceId,
                int amount,
                out string error)
            {
                bool succeeded = session.CityStorage.TrySpendFromNetwork(
                    resourceId,
                    amount);
                error = succeeded ? string.Empty : "政变让步需要 10 合金";
                return succeeded;
            }

            public bool TryAdjustSettlementLoyalty(
                string settlementId,
                int delta,
                out string error)
            {
                error = string.Equals(
                    settlementId,
                    WorldLayerCatalog.PrimaryCity.Id,
                    StringComparison.Ordinal)
                    ? string.Empty
                    : "目标城市不存在";
                return string.IsNullOrEmpty(error);
            }
        }
    }
}
