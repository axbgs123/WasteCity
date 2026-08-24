using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class GrayboxUnifiedRuleClockContractTests
    {
        private const string ClockTypeName =
            "WasteCity.Graybox3D.GrayboxFormalRuleClock3D, " +
            "WasteCity.Graybox3D";
        private const float Tolerance = .0001f;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        [Test]
        public void SharedClockResolvesZeroAndDoubleSpeedExactlyOnce()
        {
            var speed = new GameSpeedModel();
            object clock = CreateClock(speed);

            speed.Set(0f);
            Assert.That(Resolve(clock, .25f), Is.Zero,
                "A requested 0x produces no rule progress.");

            speed.Set(1f);
            Assert.That(Resolve(clock, .25f),
                Is.EqualTo(.25f).Within(Tolerance));

            speed.Set(2f);
            Time.timeScale = 2f;
            Assert.That(Resolve(clock, .25f),
                Is.EqualTo(.5f).Within(Tolerance),
                "The caller supplies unscaled time, so 2x is applied once " +
                "even while Time.timeScale mirrors the same speed.");
        }

        [Test]
        public void SharedClockStopsForMenuExternalAndTerminalPause()
        {
            var speed = new GameSpeedModel();
            speed.Set(2f);
            object clock = CreateClock(speed);

            speed.SetPaused(GamePauseReason.SystemMenu, true);
            Assert.That(Resolve(clock, 1f), Is.Zero);
            speed.SetPaused(GamePauseReason.SystemMenu, false);

            speed.SetPaused(GamePauseReason.Session, true);
            Assert.That(Resolve(clock, 1f), Is.Zero,
                "An external pause reason must stop every rule consumer.");
            speed.SetPaused(GamePauseReason.Session, false);

            Invoke(clock, "SetTerminal", true);
            Assert.That(ReadFloat(clock, "EffectiveSpeed"), Is.Zero);
            Assert.That(Resolve(clock, 1f), Is.Zero,
                "Victory and defeat freeze production, combat, evacuation, " +
                "research, crafting, city transitions and construction.");
            Assert.That(speed.LastNonZeroSpeed, Is.EqualTo(2f),
                "Terminal freeze preserves the speed used by continue-sandbox.");

            Invoke(clock, "SetTerminal", false);
            Assert.That(ReadFloat(clock, "EffectiveSpeed"), Is.EqualTo(2f));
            Assert.That(Resolve(clock, .25f),
                Is.EqualTo(.5f).Within(Tolerance));
        }

        [Test]
        public void ExplicitDevelopmentAccelerationPreservesFormalSpeedAndPause()
        {
            var speed = new GameSpeedModel();
            speed.Set(2f);
            object clock = CreateClock(speed);

            Invoke(clock, "SetDevelopmentAcceleration", 5f);
            Assert.That(ReadFloat(clock, "EffectiveSpeed"), Is.EqualTo(2f),
                "The player-facing speed remains limited to the formal 2x.");
            Assert.That(Resolve(clock, .25f),
                Is.EqualTo(2.5f).Within(Tolerance),
                "Authorized fixtures accelerate shared rule time explicitly.");

            Invoke(clock, "SetTerminal", true);
            Assert.That(Resolve(clock, .25f), Is.Zero,
                "Development acceleration never bypasses terminal pause.");
        }

        [Test]
        public void UnboundCompatibilityFallbackMirrorsUnityScaleOnce()
        {
            Type clockType = Type.GetType(ClockTypeName, throwOnError: true);
            MethodInfo fallback = clockType.GetMethod(
                "ResolveCompatibilityRuleDelta",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(fallback, Is.Not.Null);

            Time.timeScale = 0f;
            Assert.That((float)fallback.Invoke(null, new object[] { .25f }),
                Is.Zero);
            Time.timeScale = 2f;
            Assert.That((float)fallback.Invoke(null, new object[] { .25f }),
                Is.EqualTo(.5f).Within(Tolerance));

            string session = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxBuildingSession3D.cs"));
            string city = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/" +
                "GrayboxMobileCityController3D.cs"));
            StringAssert.Contains("ResolveCompatibilityRuleDelta", session);
            StringAssert.Contains("ResolveCompatibilityRuleDelta", city);
        }

        [Test]
        public void EveryFormalThreeDRuleDriverConsumesUnscaledTimeOnce()
        {
            var updates = new Dictionary<string, string>
            {
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/" +
                    "GrayboxProductionController3D.cs",
                    "private void Update()"
                },
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/" +
                    "GrayboxDefenseController3D.cs",
                    "private void Update()"
                },
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/" +
                    "GrayboxEvacuationController3D.cs",
                    "private void Update()"
                },
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/" +
                    "GrayboxOperationsController3D.cs",
                    "private void Update()"
                },
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/" +
                    "GrayboxConstructionController3D.cs",
                    "private void Update()"
                },
                {
                    "Assets/_Game/Scripts/Graybox3D/" +
                    "GrayboxMobileCityController3D.cs",
                    "private void Update()"
                },
            };

            foreach (KeyValuePair<string, string> entry in updates)
            {
                string source = File.ReadAllText(ProjectPath(entry.Key));
                if (entry.Key.EndsWith(
                        "GrayboxProductionController3D.cs",
                        StringComparison.Ordinal))
                {
                    int controller = source.IndexOf(
                        "public sealed class GrayboxProductionController3D",
                        StringComparison.Ordinal);
                    Assert.That(controller, Is.GreaterThanOrEqualTo(0));
                    source = source.Substring(controller);
                }
                string update = ExtractMethodBlock(source, entry.Value);
                StringAssert.Contains("Time.unscaledDeltaTime", update,
                    entry.Key + " must start from real unscaled frame time.");
                Assert.That(Count(update, "ResolveRuleDelta("), Is.EqualTo(1),
                    entry.Key + " must resolve the shared speed exactly once.");
                StringAssert.DoesNotContain("Time.deltaTime", update,
                    entry.Key + " cannot consume Unity's already-scaled delta.");
                StringAssert.DoesNotContain("Time.timeScale", update,
                    entry.Key + " cannot use the compatibility mirror as truth.");
            }

            string citySource = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/" +
                "GrayboxMobileCityController3D.cs"));
            string fixedUpdate = ExtractMethodBlock(
                citySource,
                "private void FixedUpdate()");
            StringAssert.Contains("Time.fixedUnscaledDeltaTime", fixedUpdate);
            Assert.That(Count(fixedUpdate, "ResolveRuleDelta("), Is.EqualTo(1));
            StringAssert.DoesNotContain("Time.fixedDeltaTime", fixedUpdate);
            StringAssert.DoesNotContain("Time.timeScale", fixedUpdate);
        }

        [Test]
        public void RuntimeHostBindsOneSharedClockToSessionAndCity()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveRuntimeHost3D.cs"));
            string bind = ExtractMethodBlock(
                source,
                "private void BindRuleClock()");

            StringAssert.Contains("session.ConfigureRuleClock(RuleClock)", bind);
            StringAssert.Contains("city.ConfigureRuleClock(RuleClock)", bind);
            StringAssert.DoesNotContain(
                "new GrayboxFormalRuleClock3D",
                bind,
                "The host binds one shared instance instead of constructing " +
                "independent speed owners per subsystem.");
        }

        [Test]
        public void CraftingAndResearchShareOneResolvedOperationsDelta()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxOperationsController3D.cs"));
            string update = ExtractMethodBlock(source, "private void Update()");

            Assert.That(Count(update, "ResolveRuleDelta("), Is.EqualTo(1));
            StringAssert.Contains("crafting.Tick(ruleDeltaSeconds", update);
            StringAssert.Contains("research.Tick(\n                ruleDeltaSeconds",
                update,
                "Crafting and research must not resolve or multiply speed " +
                "independently.");
        }

        [Test]
        public void SessionAppliesOnlyItsDomainMultiplierAfterSharedSpeed()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxBuildingSession3D.cs"));
            string tick = ExtractMethodBlock(
                source,
                "public void TickConstruction(");

            StringAssert.DoesNotContain("Time.", tick,
                "The session consumes an injected rule delta and never reads " +
                "Unity time directly.");
            StringAssert.Contains("RuleTimeContext.Advance(ruleDeltaSeconds)",
                tick,
                "Productivity/development remain session-owned domain " +
                "multipliers after shared game speed is resolved once.");
        }

        private static object CreateClock(GameSpeedModel speed)
        {
            Type type = Type.GetType(ClockTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Missing one shared formal 3D rule-clock adapter: " +
                ClockTypeName + ".");
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(GameSpeedModel),
            });
            Assert.That(constructor, Is.Not.Null,
                "The rule clock must read the authoritative GameSpeedModel.");
            return constructor.Invoke(new object[] { speed });
        }

        private static float Resolve(object clock, float unscaledDeltaSeconds)
        {
            return (float)Invoke(
                clock,
                "ResolveRuleDelta",
                unscaledDeltaSeconds);
        }

        private static object Invoke(
            object owner,
            string methodName,
            params object[] arguments)
        {
            var parameterTypes = new Type[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
                parameterTypes[index] = arguments[index].GetType();
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null,
                owner.GetType().Name + " must expose " + methodName + ".");
            return method.Invoke(owner, arguments);
        }

        private static float ReadFloat(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (float)property.GetValue(owner);
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private static string ExtractMethodBlock(
            string source,
            string declaration)
        {
            int start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), declaration);
            int opening = source.IndexOf('{', start);
            Assert.That(opening, Is.GreaterThanOrEqualTo(0));
            var depth = 0;
            for (var index = opening; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}') depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            throw new AssertionException("Unbalanced method: " + declaration);
        }

        private static int Count(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(
                       value,
                       index,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}
