using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Core;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSpeedHudAndTerminalTests
    {
        private const string TerminalGateTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxCampaignTerminalSpeedGate3D";
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void HudDisplaysRequestedAndEffectiveSpeedAsSeparateTruth()
        {
            var speed = new GameSpeedModel();
            var commands = new GrayboxGameSpeedCommandFacade3D(speed);
            commands.RequestSpeed(2);
            speed.SetPaused(GamePauseReason.SystemMenu, true);

            GrayboxDefenseHudView3D hud = Create<GrayboxDefenseHudView3D>(
                "task7-speed-hud");
            MethodInfo apply = typeof(GrayboxDefenseHudView3D).GetMethod(
                "ApplySpeed",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float), typeof(float) },
                null);
            Assert.That(apply, Is.Not.Null,
                "Task 7 needs a read-only HUD speed projection; Task 8 may " +
                "later compose it into the full campaign HUD.");
            apply.Invoke(hud, new object[]
            {
                commands.RequestedSpeed,
                commands.EffectiveSpeed,
            });

            PropertyInfo speedText = typeof(GrayboxDefenseHudView3D)
                .GetProperty(
                    "SpeedText",
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(speedText, Is.Not.Null);
            Assert.That(speedText.PropertyType, Is.EqualTo(typeof(Text)));
            var text = (Text)speedText.GetValue(hud, null);
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Does.Contain("请求 2×"));
            Assert.That(text.text, Does.Contain("有效 0×"),
                "A system-menu pause must not masquerade as a changed request.");
        }

        [Test]
        public void SystemMenuOwnsOnlyItsPauseAndRestoresOpeningRequest()
        {
            var speed = new GameSpeedModel();
            speed.Set(2f);
            speed.SetPaused(GamePauseReason.Advancement, true);
            GrayboxSystemMenuController3D menu = CreateSystemMenu(speed);

            menu.Open();
            Assert.That(speed.IsPaused(GamePauseReason.SystemMenu), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.User), Is.False,
                "Opening the system menu must not acquire tactical pause.");
            Assert.That(speed.RequestedSpeed, Is.EqualTo(2f));
            Assert.That(speed.Speed, Is.Zero);

            menu.RequestSpeed(1);
            Assert.That(speed.RequestedSpeed, Is.EqualTo(1f));
            Assert.That(speed.Speed, Is.Zero,
                "Changing the request cannot bypass the open menu.");
            menu.Close();

            Assert.That(speed.IsPaused(GamePauseReason.SystemMenu), Is.False);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.True,
                "Closing releases only the pause reason owned by the menu.");
            Assert.That(speed.RequestedSpeed, Is.EqualTo(2f),
                "Closing restores the request captured when the menu opened.");
            Assert.That(speed.Speed, Is.Zero,
                "A terminal pause remains authoritative after the menu closes.");
        }

        [Test]
        public void VictoryFreezesEffectiveSpeedAndContinueRestoresSandboxSpeed()
        {
            SingleCityDefenseCampaignModel campaign = ReachVictory();
            var speed = new GameSpeedModel();
            speed.Set(2f);
            object gate = CreateTerminalGate(speed);

            SynchronizeTerminalGate(gate, campaign.Snapshot);

            Assert.That(campaign.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Victory));
            Assert.That(speed.IsPaused(GamePauseReason.CampaignVictory), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.False,
                "Campaign victory must not occupy the civilization " +
                "advancement modal pause reason.");
            Assert.That(speed.IsPaused(GamePauseReason.Defeat), Is.False);
            Assert.That(speed.Speed, Is.Zero);
            Assert.That(speed.RequestedSpeed, Is.EqualTo(2f));
            Assert.That(speed.LastNonZeroSpeed, Is.EqualTo(2f));
            Assert.That(ReadBool(gate, "CanContinueSandbox"), Is.True);
            Assert.That(ReadBool(gate, "BlocksRuleProgress"), Is.True);

            speed.SetPaused(GamePauseReason.User, true);
            Assert.That(InvokeBool(gate, "TryContinueSandbox"), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.CampaignVictory), Is.False);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.False);
            Assert.That(speed.IsPaused(GamePauseReason.User), Is.False,
                "Continue sandbox resumes the restored last non-zero speed.");
            Assert.That(speed.Speed, Is.EqualTo(2f),
                "Victory continuation restores the last non-zero request.");
            Assert.That(ReadBool(gate, "BlocksRuleProgress"), Is.False);
            Assert.That(campaign.IsTerminal, Is.True,
                "Sandbox continuation closes settlement; it does not rewrite " +
                "the authoritative campaign result.");
            campaign.Advance(500f, 2);
            Assert.That(campaign.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Victory),
                "Continuing sandbox cannot generate an eleventh wave.");

            SynchronizeTerminalGate(gate, campaign.Snapshot);
            Assert.That(speed.Speed, Is.EqualTo(2f),
                "Repeated HUD/runtime synchronization must not reopen a " +
                "victory already continued into sandbox.");
            Assert.That(ReadBool(gate, "BlocksRuleProgress"), Is.False);
        }

        [Test]
        public void VictoryContinuationReleasesOnlyCampaignVictoryPause()
        {
            SingleCityDefenseCampaignModel campaign = ReachVictory();
            var speed = new GameSpeedModel();
            speed.SetPaused(GamePauseReason.Advancement, true);
            object gate = CreateTerminalGate(speed);

            SynchronizeTerminalGate(gate, campaign.Snapshot);

            Assert.That(speed.IsPaused(GamePauseReason.CampaignVictory), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.True);
            Assert.That(InvokeBool(gate, "TryContinueSandbox"), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.CampaignVictory), Is.False);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.True,
                "Continuing the sandbox must not close an independently " +
                "owned civilization advancement modal.");
            Assert.That(speed.Speed, Is.Zero);
        }

        [Test]
        public void DefeatFreezesEffectiveSpeedAndCannotContinueInPlace()
        {
            var campaign = new SingleCityDefenseCampaignModel(8f, 8f);
            Assert.That(TriggerCampaign(campaign), Is.True);
            campaign.ApplyCoreDamage(int.MaxValue);
            var speed = new GameSpeedModel();
            speed.Set(2f);
            object gate = CreateTerminalGate(speed);

            SynchronizeTerminalGate(gate, campaign.Snapshot);

            Assert.That(campaign.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Defeat));
            Assert.That(speed.IsPaused(GamePauseReason.Defeat), Is.True);
            Assert.That(speed.IsPaused(GamePauseReason.CampaignVictory), Is.False);
            Assert.That(speed.IsPaused(GamePauseReason.Advancement), Is.False);
            Assert.That(speed.Speed, Is.Zero);
            Assert.That(speed.LastNonZeroSpeed, Is.EqualTo(2f));
            Assert.That(ReadBool(gate, "CanContinueSandbox"), Is.False);
            Assert.That(ReadBool(gate, "BlocksRuleProgress"), Is.True);
            Assert.That(InvokeBool(gate, "TryContinueSandbox"), Is.False,
                "Defeat may later expose checkpoint retry or return-to-title, " +
                "but never in-place sandbox continuation.");
            Assert.That(speed.IsPaused(GamePauseReason.Defeat), Is.True);
            Assert.That(speed.Speed, Is.Zero);
        }

        [Test]
        public void ProductionRuntimeWiresOneTerminalGateClockAndHudProjection()
        {
            string host = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveRuntimeHost3D.cs"));
            StringAssert.Contains(
                "new GrayboxCampaignTerminalSpeedGate3D(Speed)",
                host);
            StringAssert.Contains(
                "defense.ConfigureFormalSpeedRuntime(",
                host);

            MethodInfo configure = typeof(GrayboxDefenseController3D)
                .GetMethod(
                    "ConfigureFormalSpeedRuntime",
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(configure, Is.Not.Null);
            MethodInfo continueSandbox = typeof(GrayboxDefenseController3D)
                .GetMethod(
                    "TryContinueCampaignSandbox",
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(continueSandbox, Is.Not.Null);
            Assert.That(continueSandbox.ReturnType, Is.EqualTo(typeof(bool)));

            string controller = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseController3D.cs"));
            StringAssert.Contains("terminalSpeedGate.Synchronize(", controller);
            StringAssert.Contains("formalRuleClock.SetTerminal(", controller);
            StringAssert.Contains("Time.timeScale = effective", controller,
                "Unity timeScale remains a presentation compatibility mirror.");
            StringAssert.Contains("hud.ApplySpeed(", controller);
        }

        private GrayboxSystemMenuController3D CreateSystemMenu(
            GameSpeedModel speed)
        {
            var settings = new GrayboxDisplaySettingsModel3D(
                new FakeDisplayPlatform(),
                new FakeDisplayStore());
            GrayboxSystemMenuController3D menu =
                Create<GrayboxSystemMenuController3D>("task7-system-menu");
            menu.Configure(speed, settings, new FakeExit());
            return menu;
        }

        private T Create<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name);
            created.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static object CreateTerminalGate(GameSpeedModel speed)
        {
            Type type = RequireType(TerminalGateTypeName);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(GameSpeedModel),
            });
            Assert.That(constructor, Is.Not.Null,
                "The terminal speed gate owns only one shared speed model.");
            return constructor.Invoke(new object[] { speed });
        }

        private static void SynchronizeTerminalGate(
            object gate,
            SingleCityDefenseCampaignSnapshot snapshot)
        {
            MethodInfo synchronize = gate.GetType().GetMethod(
                "Synchronize",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(SingleCityDefenseCampaignSnapshot) },
                null);
            Assert.That(synchronize, Is.Not.Null,
                "Terminal pause must derive from the immutable formal " +
                "campaign snapshot, not HUD text or GameObjects.");
            Assert.That(synchronize.ReturnType, Is.EqualTo(typeof(void)));
            synchronize.Invoke(gate, new object[] { snapshot });
        }

        private static bool ReadBool(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + propertyName);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(property.CanWrite, Is.False);
            return (bool)property.GetValue(owner, null);
        }

        private static bool InvokeBool(object owner, string methodName)
        {
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null,
                owner.GetType().FullName + "." + methodName);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            return (bool)method.Invoke(owner, null);
        }

        private static Type RequireType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            Assert.Fail("Missing Task 7 runtime type: " + fullName);
            return null;
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private static SingleCityDefenseCampaignModel ReachVictory()
        {
            var campaign = new SingleCityDefenseCampaignModel(8f, 8f);
            Assert.That(TriggerCampaign(campaign), Is.True);
            var guard = 0;
            while (!campaign.IsTerminal && guard++ < 20)
            {
                campaign.Advance(500f, 1);
                SingleCityDefenseEnemySnapshot[] enemies =
                    campaign.Snapshot.Enemies.ToArray();
                for (var index = 0; index < enemies.Length; index++)
                {
                    Assert.That(campaign.DefeatEnemy(
                        enemies[index].StableId,
                        BuildingCatalog.MachineGunTurret.Id.Value), Is.True);
                }
                campaign.Advance(.1f, 1);
            }
            Assert.That(campaign.Snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
            return campaign;
        }

        private static bool TriggerCampaign(
            SingleCityDefenseCampaignModel campaign)
        {
            return campaign.NotifyDefenseTowerCompleted(
                "building.instance.task7-machine-gun",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true);
        }

        private sealed class FakeDisplayPlatform :
            IGrayboxDisplaySettingsPlatform
        {
            private static readonly IReadOnlyList<
                GrayboxDisplayResolution3D> Resolutions = new[]
                {
                    new GrayboxDisplayResolution3D(1920, 1080),
                };

            public IReadOnlyList<GrayboxDisplayResolution3D>
                AvailableResolutions => Resolutions;

            public GrayboxDisplaySettings3D Current =>
                new GrayboxDisplaySettings3D(
                    1920,
                    1080,
                    GrayboxWindowMode3D.Windowed);

            public bool TryApply(GrayboxDisplaySettings3D settings)
            {
                return true;
            }
        }

        private sealed class FakeDisplayStore : IGrayboxDisplaySettingsStore
        {
            public bool TryLoad(
                out int version,
                out GrayboxDisplaySettings3D settings)
            {
                version = 0;
                settings = default;
                return false;
            }

            public void Save(
                int version,
                GrayboxDisplaySettings3D settings)
            {
            }
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public void Exit()
            {
            }
        }
    }
}
