using System;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Core;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalGameSpeedPersistenceTests
    {
        private const string StateTypeName =
            "WasteCity.Graybox3D.Usability." +
            "GrayboxGameSpeedPersistenceState3D";
        private const string PlanTypeName =
            "WasteCity.Graybox3D.Usability." +
            "GrayboxGameSpeedRestorePlan3D";
        private const float Tolerance = .0001f;

        [Test]
        public void FormalSpeedPersistenceContractIsPureAndTwoPhase()
        {
            Type stateType = RequireType(StateTypeName);
            Type planType = RequireType(PlanTypeName);
            RequireProperty(stateType, "RequestedSpeed", typeof(float));
            RequireProperty(stateType, "LastNonZeroSpeed", typeof(float));
            RequireMethod(
                "CaptureForPersistence",
                stateType,
                Type.EmptyTypes);
            RequireMethod(
                "TryPrepareRestore",
                typeof(bool),
                stateType,
                planType.MakeByRefType(),
                typeof(string).MakeByRefType());
            RequireMethod(
                "TryCommitRestore",
                typeof(bool),
                planType,
                typeof(string).MakeByRefType());
        }

        [TestCase(0, 2)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        public void SchemaThirtyTwoSpeedStateRoundTripsZeroOneAndTwo(
            int requested,
            int expectedLastNonZero)
        {
            var sourceSpeed = new GameSpeedModel();
            var source = new GrayboxGameSpeedCommandFacade3D(sourceSpeed);
            if (requested == 0)
            {
                source.RequestSpeed(2);
                source.RequestSpeed(0);
            }
            else
            {
                source.RequestSpeed(requested);
            }

            object state = Capture(source);
            Assert.That(ReadFloat(state, "RequestedSpeed"),
                Is.EqualTo(requested).Within(Tolerance));
            Assert.That(ReadFloat(state, "LastNonZeroSpeed"),
                Is.EqualTo(expectedLastNonZero).Within(Tolerance));

            var targetSpeed = new GameSpeedModel();
            var target = new GrayboxGameSpeedCommandFacade3D(targetSpeed);
            Assert.That(TryPrepare(
                target,
                state,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(TryCommit(target, plan, out string commitError),
                Is.True, commitError);
            Assert.That(target.RequestedSpeed,
                Is.EqualTo(requested).Within(Tolerance));
            Assert.That(target.LastNonZeroSpeed,
                Is.EqualTo(expectedLastNonZero).Within(Tolerance));

            if (requested == 0)
            {
                target.ToggleTacticalPause();
                Assert.That(target.RequestedSpeed,
                    Is.EqualTo(expectedLastNonZero).Within(Tolerance),
                    "The Space command must resume the restored last " +
                    "non-zero formal speed.");
            }
        }

        [Test]
        public void RequestedAndEffectiveSpeedStayDistinctUnderExternalPause()
        {
            var speed = new GameSpeedModel();
            var commands = new GrayboxGameSpeedCommandFacade3D(speed);
            commands.RequestSpeed(2);
            speed.SetPaused(GamePauseReason.SystemMenu, true);

            Assert.That(commands.RequestedSpeed, Is.EqualTo(2f));
            Assert.That(commands.EffectiveSpeed, Is.Zero);
            object state = Capture(commands);
            Assert.That(ReadFloat(state, "RequestedSpeed"), Is.EqualTo(2f));
            Assert.That(ReadFloat(state, "LastNonZeroSpeed"), Is.EqualTo(2f));
        }

        [Test]
        public void PrepareIsZeroWriteAndCommitRejectsStaleForeignAndConsumedPlans()
        {
            var source = new GrayboxGameSpeedCommandFacade3D(
                new GameSpeedModel());
            source.RequestSpeed(2);
            object saved = Capture(source);

            var target = new GrayboxGameSpeedCommandFacade3D(
                new GameSpeedModel());
            string before = SpeedFingerprint(target);
            Assert.That(TryPrepare(
                target,
                saved,
                out object stalePlan,
                out string prepareError), Is.True, prepareError);
            Assert.That(SpeedFingerprint(target), Is.EqualTo(before),
                "Prepare must validate both speed fields without writing.");
            target.RequestSpeed(0);
            string paused = SpeedFingerprint(target);
            Assert.That(TryCommit(target, stalePlan, out _), Is.False);
            Assert.That(SpeedFingerprint(target), Is.EqualTo(paused));

            Assert.That(TryPrepare(
                target,
                saved,
                out object validPlan,
                out prepareError), Is.True, prepareError);
            var foreign = new GrayboxGameSpeedCommandFacade3D(
                new GameSpeedModel());
            string foreignBefore = SpeedFingerprint(foreign);
            Assert.That(TryCommit(foreign, validPlan, out _), Is.False);
            Assert.That(SpeedFingerprint(foreign), Is.EqualTo(foreignBefore));

            Assert.That(TryCommit(target, validPlan, out string commitError),
                Is.True, commitError);
            Assert.That(target.RequestedSpeed, Is.EqualTo(2f));
            string committed = SpeedFingerprint(target);
            Assert.That(TryCommit(target, validPlan, out _), Is.False);
            Assert.That(SpeedFingerprint(target), Is.EqualTo(committed));
        }

        [Test]
        public void PauseSaveDomainOwnsSchemaThirtyTwoSpeedCaptureAndRestore()
        {
            var sourceSpeed = new GameSpeedModel();
            var sourceCommands = new GrayboxGameSpeedCommandFacade3D(
                sourceSpeed);
            sourceCommands.RequestSpeed(2);
            sourceCommands.ToggleTacticalPause();
            var sourceDomain = new GrayboxFormalPauseSaveDomain3D(sourceSpeed);
            var payload = new FormalThreeDSaveData
            {
                defenseCampaign =
                    new FormalThreeDDefenseCampaignSaveData(),
            };

            Assert.That(sourceDomain.TryCapture(
                payload,
                out string captureError), Is.True, captureError);
            Assert.That(payload.pause.tacticalPaused, Is.True);
            Assert.That(payload.defenseCampaign.requestedSpeed, Is.Zero);
            Assert.That(payload.defenseCampaign.lastNonZeroSpeed,
                Is.EqualTo(2f));

            var targetSpeed = new GameSpeedModel();
            var targetCommands = new GrayboxGameSpeedCommandFacade3D(
                targetSpeed);
            targetCommands.RequestSpeed(1);
            var targetDomain = new GrayboxFormalPauseSaveDomain3D(targetSpeed);
            Assert.That(targetDomain.TryApply(
                payload,
                out string applyError), Is.True, applyError);
            Assert.That(targetCommands.RequestedSpeed, Is.Zero);
            Assert.That(targetCommands.EffectiveSpeed, Is.Zero);
            Assert.That(targetCommands.LastNonZeroSpeed, Is.EqualTo(2f));

            targetCommands.ToggleTacticalPause();
            Assert.That(targetCommands.RequestedSpeed, Is.EqualTo(2f),
                "Space after restore must resume the persisted 2x speed.");
            Assert.That(targetCommands.EffectiveSpeed, Is.EqualTo(2f));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void PauseSaveDomainRoundTripsRunningSchemaThirtyTwoSpeed(
            int requested)
        {
            var sourceSpeed = new GameSpeedModel();
            var sourceCommands = new GrayboxGameSpeedCommandFacade3D(
                sourceSpeed);
            sourceCommands.RequestSpeed(requested);
            var payload = new FormalThreeDSaveData
            {
                defenseCampaign =
                    new FormalThreeDDefenseCampaignSaveData(),
            };
            var sourceDomain = new GrayboxFormalPauseSaveDomain3D(sourceSpeed);
            Assert.That(sourceDomain.TryCapture(
                payload,
                out string captureError), Is.True, captureError);
            Assert.That(payload.defenseCampaign.requestedSpeed,
                Is.EqualTo(requested));
            Assert.That(payload.defenseCampaign.lastNonZeroSpeed,
                Is.EqualTo(requested));

            var targetSpeed = new GameSpeedModel();
            var targetCommands = new GrayboxGameSpeedCommandFacade3D(
                targetSpeed);
            targetCommands.RequestSpeed(requested == 1 ? 2 : 1);
            var targetDomain = new GrayboxFormalPauseSaveDomain3D(targetSpeed);
            Assert.That(targetDomain.TryApply(
                payload,
                out string applyError), Is.True, applyError);
            Assert.That(targetCommands.RequestedSpeed,
                Is.EqualTo(requested));
            Assert.That(targetCommands.EffectiveSpeed,
                Is.EqualTo(requested));
            Assert.That(targetCommands.LastNonZeroSpeed,
                Is.EqualTo(requested));
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void PauseDomainAcceptsPausedSchemaThirtyOneMigrationDefaults(
            float migratedRequestedSpeed)
        {
            var payload = new FormalThreeDSaveData
            {
                pause = new FormalThreeDPauseSaveData
                {
                    tacticalPaused = true,
                },
                defenseCampaign = new FormalThreeDDefenseCampaignSaveData
                {
                    requestedSpeed = migratedRequestedSpeed,
                    lastNonZeroSpeed = 1f,
                    statistics =
                        new FormalThreeDDefenseCampaignStatisticsSaveData
                        {
                            partialFromMigration = true,
                        },
                },
            };
            var speed = new GameSpeedModel();
            speed.SetPaused(GamePauseReason.SystemMenu, true);
            var commands = new GrayboxGameSpeedCommandFacade3D(speed);
            var domain = new GrayboxFormalPauseSaveDomain3D(speed);

            Assert.That(domain.TryApply(
                payload,
                out string error), Is.True, error);
            Assert.That(commands.RequestedSpeed, Is.Zero);
            Assert.That(commands.LastNonZeroSpeed,
                Is.EqualTo(migratedRequestedSpeed),
                "The migrated requested value is the best available resume " +
                "speed even though schema 31 defaulted lastNonZero to 1x.");
            Assert.That(commands.EffectiveSpeed, Is.Zero,
                "The restored User pause and pre-existing external pause " +
                "must both remain effective.");
            speed.SetPaused(GamePauseReason.SystemMenu, false);
            Assert.That(commands.EffectiveSpeed, Is.Zero,
                "Releasing an external pause must not release User pause.");
            commands.ToggleTacticalPause();
            Assert.That(commands.RequestedSpeed,
                Is.EqualTo(migratedRequestedSpeed));
        }

        [Test]
        public void PauseDomainRejectsPausedCurrentSchemaWithNonZeroRequested()
        {
            var payload = new FormalThreeDSaveData
            {
                pause = new FormalThreeDPauseSaveData
                {
                    tacticalPaused = true,
                },
                defenseCampaign = new FormalThreeDDefenseCampaignSaveData
                {
                    requestedSpeed = 1f,
                    lastNonZeroSpeed = 1f,
                    statistics =
                        new FormalThreeDDefenseCampaignStatisticsSaveData
                        {
                            partialFromMigration = false,
                        },
                },
            };
            var speed = new GameSpeedModel();
            var commands = new GrayboxGameSpeedCommandFacade3D(speed);
            string before = SpeedFingerprint(commands);

            Assert.That(new GrayboxFormalPauseSaveDomain3D(speed).TryApply(
                payload,
                out _), Is.False,
                "Only schema 31 partial-statistics migration may use the " +
                "legacy paused + non-zero requested representation.");
            Assert.That(SpeedFingerprint(commands), Is.EqualTo(before));
        }

        private static object Capture(
            GrayboxGameSpeedCommandFacade3D commands)
        {
            MethodInfo method = RequireMethod(
                "CaptureForPersistence",
                RequireType(StateTypeName),
                Type.EmptyTypes);
            return method.Invoke(commands, null);
        }

        private static bool TryPrepare(
            GrayboxGameSpeedCommandFacade3D commands,
            object state,
            out object plan,
            out string error)
        {
            Type stateType = RequireType(StateTypeName);
            Type planType = RequireType(PlanTypeName);
            MethodInfo method = RequireMethod(
                "TryPrepareRestore",
                typeof(bool),
                stateType,
                planType.MakeByRefType(),
                typeof(string).MakeByRefType());
            object[] arguments = { state, null, null };
            bool result = (bool)method.Invoke(commands, arguments);
            plan = arguments[1];
            error = arguments[2] as string;
            return result;
        }

        private static bool TryCommit(
            GrayboxGameSpeedCommandFacade3D commands,
            object plan,
            out string error)
        {
            Type planType = RequireType(PlanTypeName);
            MethodInfo method = RequireMethod(
                "TryCommitRestore",
                typeof(bool),
                planType,
                typeof(string).MakeByRefType());
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(commands, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(GrayboxGameSpeedCommandFacade3D).Assembly
                .GetType(fullName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Missing formal game-speed persistence type " + fullName);
            return type;
        }

        private static MethodInfo RequireMethod(
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = typeof(GrayboxGameSpeedCommandFacade3D)
                .GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    parameters,
                    null);
            Assert.That(method, Is.Not.Null,
                "Missing formal game-speed method " + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
            return method;
        }

        private static PropertyInfo RequireProperty(
            Type owner,
            string name,
            Type propertyType)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType));
            Assert.That(property.CanRead, Is.True);
            return property;
        }

        private static float ReadFloat(object owner, string propertyName)
        {
            PropertyInfo property = RequireProperty(
                owner.GetType(),
                propertyName,
                typeof(float));
            return (float)property.GetValue(owner);
        }

        private static string SpeedFingerprint(
            GrayboxGameSpeedCommandFacade3D commands)
        {
            return commands.RequestedSpeed + "|" +
                commands.EffectiveSpeed + "|" +
                commands.LastNonZeroSpeed;
        }
    }
}
