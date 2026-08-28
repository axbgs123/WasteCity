using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Combat;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxCivilizationWorldMarkerVisual3D
    {
        internal GrayboxCivilizationWorldMarkerVisual3D(
            Production2DVisualClass visualClass,
            string visualContentId,
            Sprite sprite,
            float worldScale,
            float worldHeight,
            Vector2 anchor)
        {
            VisualClass = visualClass;
            VisualContentId = visualContentId ?? string.Empty;
            Sprite = sprite;
            WorldScale = worldScale;
            WorldHeight = worldHeight;
            Anchor = anchor;
        }

        public Production2DVisualClass VisualClass { get; }
        public string VisualContentId { get; }
        public Sprite Sprite { get; }
        public bool UsesProgrammaticFallback => Sprite == null;
        public float WorldScale { get; }
        public float WorldHeight { get; }
        public Vector2 Anchor { get; }
    }

    public sealed class GrayboxCivilizationExpansionVisualPresenter3D
    {
        public const string PrimaryPanelVisualId =
            "core.ui.frame.primary-panel";
        public const string SecondaryCardVisualId =
            "core.ui.frame.secondary-card";
        public const string PrimaryButtonVisualId =
            "core.ui.control.primary-button";
        public const string TerminalDividerVisualId =
            "core.ui.divider.terminal-horizontal";
        public const string ArmyTabVisualId = "core.ui.tab.army";
        public const string WorldTabVisualId = "core.ui.tab.world";
        public const string PoliticsTabVisualId = "core.ui.tab.politics";
        public const string GuardStatusVisualId = "core.ui.status.guard";
        public const string FollowStatusVisualId = "core.ui.status.follow";
        public const string ExpeditionStatusVisualId =
            "core.ui.status.expedition";
        public const string RetreatStatusVisualId =
            "core.ui.status.retreat";
        public const string TransportStatusVisualId =
            "core.ui.status.transport";
        public const string CommunicationStatusVisualId =
            "core.ui.status.communication";
        public const string LoyaltyStatusVisualId =
            "core.ui.status.loyalty";
        public const string RescueStatusVisualId = "core.ui.status.rescue";
        public const string SecondaryCityMarkerVisualId =
            "core.world-marker.secondary-city";
        public const string OutpostMarkerVisualId =
            "core.world-marker.outpost";
        public const string ConvoyMarkerVisualId =
            "core.world-marker.convoy";

        private readonly Func<Production2DVisualClass, string, Sprite> resolver;
        private readonly Dictionary<string, Sprite> spriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public GrayboxCivilizationExpansionVisualPresenter3D(
            Func<Production2DVisualClass, string, Sprite> resolver)
        {
            this.resolver = resolver ??
                throw new ArgumentNullException(nameof(resolver));
        }

        public static GrayboxCivilizationExpansionVisualPresenter3D
            CreateFormal()
        {
            Production2DVisualCatalog3D catalog =
                Resources.Load<Production2DVisualCatalog3D>(
                    Production2DVisualCatalog3D.ResourcesPath);
            var sprites = new Dictionary<string, Sprite>(
                StringComparer.Ordinal);
            if (catalog != null)
            {
                for (var index = 0; index < catalog.Entries.Count; index++)
                {
                    Production2DVisualEntry3D entry = catalog.Entries[index];
                    if (entry == null || entry.Sprite == null ||
                        !string.Equals(
                            entry.Variant,
                            Production2DVisualCatalog3D.DefaultVariant,
                            StringComparison.Ordinal))
                        continue;
                    sprites[Key(entry.VisualClass, entry.ContentId)] =
                        entry.Sprite;
                }
            }
            return new GrayboxCivilizationExpansionVisualPresenter3D(
                (visualClass, contentId) => sprites.TryGetValue(
                    Key(visualClass, contentId),
                    out Sprite sprite)
                        ? sprite
                        : null);
        }

        public static void OrientVerticalBillboard(
            Transform marker,
            Vector3 cameraPosition)
        {
            if (marker == null) return;
            Vector3 direction = cameraPosition - marker.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= .000001f) return;
            marker.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }

        public static string[] ArmyStatusVisuals(
            FriendlySquadCommandType command)
        {
            switch (command)
            {
                case FriendlySquadCommandType.Guard:
                    return new[] { GuardStatusVisualId };
                case FriendlySquadCommandType.FollowLeader:
                    return new[] { FollowStatusVisualId };
                case FriendlySquadCommandType.Expedition:
                    return new[] { ExpeditionStatusVisualId };
                case FriendlySquadCommandType.Retreat:
                    return new[] { RetreatStatusVisualId };
                default:
                    return Array.Empty<string>();
            }
        }

        public static string[] WorldStatusVisuals(
            bool hasActiveTransport,
            bool hasRemoteCommunication)
        {
            if (hasActiveTransport && hasRemoteCommunication)
                return new[]
                {
                    TransportStatusVisualId,
                    CommunicationStatusVisualId,
                };
            if (hasActiveTransport)
                return new[] { TransportStatusVisualId };
            if (hasRemoteCommunication)
                return new[] { CommunicationStatusVisualId };
            return Array.Empty<string>();
        }

        public static string[] PoliticsStatusVisuals(bool hasDowned)
        {
            return hasDowned
                ? new[] { LoyaltyStatusVisualId, RescueStatusVisualId }
                : new[] { LoyaltyStatusVisualId };
        }

        public GrayboxCivilizationWorldMarkerVisual3D DescribeWorldMarker(
            string stableRuntimeId,
            string primaryUnitDefinitionId)
        {
            if (string.Equals(
                    stableRuntimeId,
                    WorldLayerCatalog.SecondaryCity.Id,
                    StringComparison.Ordinal))
            {
                return Marker(
                    Production2DVisualClass.WorldMarker,
                    SecondaryCityMarkerVisualId,
                    1.15f,
                    .72f,
                    new Vector2(.5f, 0f));
            }
            if (string.Equals(
                    stableRuntimeId,
                    WorldLayerCatalog.Outpost.Id,
                    StringComparison.Ordinal))
            {
                return Marker(
                    Production2DVisualClass.WorldMarker,
                    OutpostMarkerVisualId,
                    .72f,
                    .32f,
                    new Vector2(.5f, 0f));
            }
            if (!string.IsNullOrWhiteSpace(stableRuntimeId) &&
                stableRuntimeId.StartsWith(
                    "core.convoy.",
                    StringComparison.Ordinal))
            {
                return Marker(
                    Production2DVisualClass.WorldMarker,
                    ConvoyMarkerVisualId,
                    .42f,
                    .30f,
                    new Vector2(.5f, 0f));
            }
            if (string.Equals(
                    stableRuntimeId,
                    SingleCityArmyModel.DefaultSquadId,
                    StringComparison.Ordinal) &&
                ArmyUnitCatalog.Find(primaryUnitDefinitionId) != null)
            {
                return Marker(
                    Production2DVisualClass.Unit,
                    primaryUnitDefinitionId,
                    .58f,
                    .42f,
                    new Vector2(.5f, 0f));
            }
            return null;
        }

        private GrayboxCivilizationWorldMarkerVisual3D Marker(
            Production2DVisualClass visualClass,
            string contentId,
            float scale,
            float height,
            Vector2 anchor)
        {
            string key = Key(visualClass, contentId);
            if (!spriteCache.TryGetValue(key, out Sprite sprite))
            {
                sprite = resolver(visualClass, contentId);
                spriteCache.Add(key, sprite);
            }
            return new GrayboxCivilizationWorldMarkerVisual3D(
                visualClass,
                contentId,
                sprite,
                scale,
                height,
                anchor);
        }

        private static string Key(
            Production2DVisualClass visualClass,
            string contentId)
        {
            return visualClass + "|" + (contentId ?? string.Empty);
        }
    }
}
