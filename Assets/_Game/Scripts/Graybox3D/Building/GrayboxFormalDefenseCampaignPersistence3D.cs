using System;
using System.Collections.Generic;
using WasteCity.Defense;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFormalDefenseCampaignPersistenceState3D
    {
        internal GrayboxFormalDefenseCampaignPersistenceState3D(
            SingleCityDefenseCampaignPersistenceState campaign,
            SingleCityDefenseTowerPersistenceState[] towers,
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
                buildingHealth)
        {
            Campaign = campaign ??
                throw new ArgumentNullException(nameof(campaign));
            Towers = Array.AsReadOnly(towers ??
                Array.Empty<SingleCityDefenseTowerPersistenceState>());
            BuildingHealth = Array.AsReadOnly(buildingHealth ??
                Array.Empty<
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData>());
        }

        public SingleCityDefenseCampaignPersistenceState Campaign { get; }
        public IReadOnlyList<SingleCityDefenseTowerPersistenceState> Towers
        {
            get;
        }
        public IReadOnlyList<
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData>
            BuildingHealth { get; }
    }

    public sealed class GrayboxFormalDefenseCampaignRestorePlan3D
    {
        internal GrayboxFormalDefenseCampaignRestorePlan3D(
            GrayboxDefenseRuntime3D owner,
            ulong expectedGeneration,
            ulong expectedFingerprint,
            SingleCityDefenseCampaignRestorePlan campaignPlan,
            SingleCityDefenseTowerCombatModel[] towers,
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] health,
            GrayboxBuildingInstance3D[] instances)
        {
            Owner = owner;
            ExpectedGeneration = expectedGeneration;
            ExpectedFingerprint = expectedFingerprint;
            CampaignPlan = campaignPlan;
            Towers = towers;
            Health = health;
            Instances = instances;
        }

        internal GrayboxDefenseRuntime3D Owner { get; }
        internal ulong ExpectedGeneration { get; }
        internal ulong ExpectedFingerprint { get; }
        internal SingleCityDefenseCampaignRestorePlan CampaignPlan { get; }
        internal SingleCityDefenseTowerCombatModel[] Towers { get; }
        internal FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
            Health { get; }
        internal GrayboxBuildingInstance3D[] Instances { get; }
        internal bool Consumed { get; set; }
    }
}
