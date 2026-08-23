using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class FormalSaveDestroyedRuinSchema32Tests
    {
        private const string BuildingSessionSourcePath =
            "Assets/_Game/Scripts/Graybox3D/Building/" +
            "GrayboxBuildingSession3D.cs";

        [Test]
        public void IDEA0017_BuildingLifecycleSerializationUsesStableZeroToThreeValues()
        {
            Assert.That(
                Enum.GetUnderlyingType(typeof(GrayboxBuildingInstanceState)),
                Is.EqualTo(typeof(int)));
            Assert.That(
                (int)GrayboxBuildingInstanceState.UnderConstruction,
                Is.EqualTo(0));
            Assert.That(
                (int)GrayboxBuildingInstanceState.Completed,
                Is.EqualTo(1));
            Assert.That(
                (int)GrayboxBuildingInstanceState.AbandonedRuin,
                Is.EqualTo(2));
            Assert.That(
                (int)GrayboxBuildingInstanceState.DestroyedRuin,
                Is.EqualTo(3));
            Assert.That(
                Enum.GetValues(typeof(GrayboxBuildingInstanceState)).Length,
                Is.EqualTo(4),
                "Schema 32 freezes exactly four persisted building states.");
        }

        [Test]
        public void IDEA0017_PersistedBuildingLifecycleValuesAreExplicitlyDeclared()
        {
            string source = File.ReadAllText(ProjectPath(
                BuildingSessionSourcePath));
            const string explicitDeclaration =
                @"\bUnderConstruction\s*=\s*0\s*," +
                @"\s*Completed\s*=\s*1\s*," +
                @"\s*AbandonedRuin\s*=\s*2\s*," +
                @"\s*DestroyedRuin\s*=\s*3\b";

            Assert.That(
                Regex.IsMatch(
                    source,
                    explicitDeclaration,
                    RegexOptions.CultureInvariant),
                Is.True,
                "Persisted schema enum values must be explicit so future " +
                "reordering cannot silently change save data.");
        }

        [Test]
        public void IDEA0017_SchemaThirtyTwoAcceptsConsistentDestroyedRuin()
        {
            DestroyedRuinFixture fixture = CreateDestroyedRuinFixture();

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(fixture.Envelope);

            Assert.That(result.IsValid, Is.True,
                result.Error + " at " + result.FieldPath + ": " +
                result.Message);
        }

        [TestCase("owner", "isPlayerOwned")]
        [TestCase("lock", "evacuationLockedCrossCheck")]
        [TestCase("remaining", "constructionRemainingSeconds")]
        [TestCase("state", "state")]
        public void IDEA0017_SchemaThirtyTwoRejectsInvalidDestroyedRuinLifecycle(
            string mutation,
            string expectedField)
        {
            DestroyedRuinFixture fixture = CreateDestroyedRuinFixture();
            switch (mutation)
            {
                case "owner":
                    fixture.Building.isPlayerOwned = true;
                    break;
                case "lock":
                    fixture.Building.evacuationLockedCrossCheck = true;
                    break;
                case "remaining":
                    fixture.Building.constructionRemainingSeconds = .5f;
                    break;
                case "state":
                    fixture.Building.state = 4;
                    break;
                default:
                    Assert.Fail("Unknown lifecycle mutation: " + mutation);
                    break;
            }
            Rehash(fixture.Envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(fixture.Envelope);

            Assert.That(result.IsValid, Is.False,
                "Schema 32 accepted invalid DestroyedRuin mutation: " +
                mutation);
            Assert.That(
                result.FieldPath,
                Is.EqualTo(
                    "formal3D.buildings.instances[" +
                    fixture.BuildingIndex + "]." + expectedField),
                "The validator must reach the precise lifecycle invariant " +
                "instead of rejecting the legal DestroyedRuin enum value.");
        }

        private static DestroyedRuinFixture CreateDestroyedRuinFixture()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope, Is.Not.Null);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(32));

            FormalSaveEnvelope envelope = decoded.Envelope;
            FormalThreeDBuildingsSaveData buildings =
                envelope.formal3D.buildings;
            int ordinal = buildings.nextStableInstanceOrdinal;
            string stableInstanceId =
                "building.instance." + ordinal.ToString("D6");
            var ruin = new FormalThreeDBuildingInstanceSaveData
            {
                stableInstanceId = stableInstanceId,
                definitionId = BuildingCatalog.Wall.Id.Value,
                site = (int)BuildingSite.Ground,
                x = 20,
                y = 20,
                orientation = (int)BuildingOrientation.East,
                state = (int)GrayboxBuildingInstanceState.DestroyedRuin,
                constructionRemainingSeconds = 0f,
                isPlayerOwned = false,
                boundResourceNodeId = string.Empty,
                boundNodeX = -1,
                boundNodeY = -1,
                footprintWidth = BuildingCatalog.Wall.Width,
                footprintHeight = BuildingCatalog.Wall.Height,
                evacuationLockedCrossCheck = false,
            };
            int buildingIndex = buildings.instances.Length;
            buildings.instances = Append(buildings.instances, ruin);
            buildings.nextStableInstanceOrdinal = ordinal + 1;

            FormalThreeDDefenseCampaignSaveData campaign =
                envelope.formal3D.defenseCampaign;
            campaign.buildingHealthStates = Append(
                campaign.buildingHealthStates,
                new FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                {
                    stableInstanceId = stableInstanceId,
                    currentHealth = 0,
                    isDestroyed = true,
                });
            Rehash(envelope);
            return new DestroyedRuinFixture(envelope, ruin, buildingIndex);
        }

        private static T[] Append<T>(T[] source, T value)
        {
            Assert.That(source, Is.Not.Null);
            var result = new T[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = value;
            return result;
        }

        private static void Rehash(FormalSaveEnvelope envelope)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
        }

        private static string ReadFixture(string fileName)
        {
            return File.ReadAllText(ProjectPath(Path.Combine(
                "Assets/_Game/Tests/Fixtures/Persistence",
                fileName)));
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            return Path.Combine(projectRoot, relativePath);
        }

        private readonly struct DestroyedRuinFixture
        {
            public DestroyedRuinFixture(
                FormalSaveEnvelope envelope,
                FormalThreeDBuildingInstanceSaveData building,
                int buildingIndex)
            {
                Envelope = envelope;
                Building = building;
                BuildingIndex = buildingIndex;
            }

            public FormalSaveEnvelope Envelope { get; }
            public FormalThreeDBuildingInstanceSaveData Building { get; }
            public int BuildingIndex { get; }
        }
    }
}
