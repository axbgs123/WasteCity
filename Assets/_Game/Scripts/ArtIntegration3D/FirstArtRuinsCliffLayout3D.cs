using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.ArtIntegration3D
{
    public readonly struct FirstArtRuinsCliffPlacement3D
    {
        internal FirstArtRuinsCliffPlacement3D(
            FirstArtRuinsCliffFamily3D family,
            int catalogIndex,
            int cellX,
            int cellY,
            int neighborMask,
            int quarterTurns,
            Matrix4x4 worldMatrix)
        {
            Family = family;
            CatalogIndex = catalogIndex;
            CellX = cellX;
            CellY = cellY;
            NeighborMask = neighborMask;
            QuarterTurns = quarterTurns;
            WorldMatrix = worldMatrix;
        }

        public FirstArtRuinsCliffFamily3D Family { get; }
        public int CatalogIndex { get; }
        public int CellX { get; }
        public int CellY { get; }
        public int NeighborMask { get; }
        public int QuarterTurns { get; }
        public Matrix4x4 WorldMatrix { get; }
    }

    public static class FirstArtRuinsCliffLayout3D
    {
        private const uint RuinsVariantSalt = 0x6D2B79F5u;
        private const uint RuinsRotationSalt = 0xA511E9B3u;
        private const uint CliffStraightVariantSalt = 0x9E3779B9u;
        private const uint FnvPrime = 16777619u;
        private const uint AvalancheMultiplier = 0x85EBCA6Bu;

        public static IReadOnlyList<FirstArtRuinsCliffPlacement3D> Project(
            WorldMapModel map,
            PlanarCoordinateMapper3D mapper)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));
            if (map.Width != mapper.Width || map.Height != mapper.Height)
            {
                throw new ArgumentException(
                    "The coordinate mapper dimensions must match the world map.",
                    nameof(mapper));
            }

            var placements = new List<FirstArtRuinsCliffPlacement3D>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                WorldTraversalKind traversal = map.Get(x, y).Traversal;
                if (traversal == WorldTraversalKind.Ruins)
                {
                    int catalogIndex = (int)(Hash(
                        map.Width,
                        map.Height,
                        x,
                        y,
                        RuinsVariantSalt) %
                        FirstArtRuinsCliffCatalog3D.RuinsEntryCount);
                    int quarterTurns = (int)(Hash(
                        map.Width,
                        map.Height,
                        x,
                        y,
                        RuinsRotationSalt) % 4u);
                    placements.Add(CreatePlacement(
                        mapper,
                        FirstArtRuinsCliffFamily3D.Ruins,
                        catalogIndex,
                        x,
                        y,
                        0,
                        quarterTurns));
                }
                else if (traversal == WorldTraversalKind.Cliff)
                {
                    int neighborMask = CliffNeighborMask(map, x, y);
                    SelectCliffModule(
                        map,
                        x,
                        y,
                        neighborMask,
                        out FirstArtRuinsCliffModule3D module,
                        out int quarterTurns);
                    placements.Add(CreatePlacement(
                        mapper,
                        FirstArtRuinsCliffFamily3D.Cliff,
                        CatalogIndexFor(module),
                        x,
                        y,
                        neighborMask,
                        quarterTurns));
                }
            }

            return new ReadOnlyCollection<FirstArtRuinsCliffPlacement3D>(
                placements.ToArray());
        }

        private static FirstArtRuinsCliffPlacement3D CreatePlacement(
            PlanarCoordinateMapper3D mapper,
            FirstArtRuinsCliffFamily3D family,
            int catalogIndex,
            int cellX,
            int cellY,
            int neighborMask,
            int quarterTurns)
        {
            if (!mapper.TryCellToWorld(cellX, cellY, 0f, out Vector3 world))
            {
                throw new InvalidOperationException(
                    "A scanned world cell could not be projected by its mapper.");
            }

            FirstArtRuinsCliffCatalogEntry3D entry =
                FirstArtRuinsCliffCatalog3D.Entries[catalogIndex];
            Matrix4x4 placementMatrix = Matrix4x4.TRS(
                world,
                Quaternion.Euler(0f, quarterTurns * 90f, 0f),
                Vector3.one);
            Matrix4x4 calibrationMatrix = Matrix4x4.TRS(
                entry.ChildOffset,
                Quaternion.identity,
                entry.RootScale);
            return new FirstArtRuinsCliffPlacement3D(
                family,
                catalogIndex,
                cellX,
                cellY,
                neighborMask,
                quarterTurns,
                placementMatrix * calibrationMatrix);
        }

        private static int CliffNeighborMask(
            WorldMapModel map,
            int x,
            int y)
        {
            int mask = 0;
            if (IsCliff(map, x, y + 1))
                mask |= FirstArtRuinsCliffCatalog3D.NorthConnection;
            if (IsCliff(map, x + 1, y))
                mask |= FirstArtRuinsCliffCatalog3D.EastConnection;
            if (IsCliff(map, x, y - 1))
                mask |= FirstArtRuinsCliffCatalog3D.SouthConnection;
            if (IsCliff(map, x - 1, y))
                mask |= FirstArtRuinsCliffCatalog3D.WestConnection;
            return mask;
        }

        private static void SelectCliffModule(
            WorldMapModel map,
            int x,
            int y,
            int neighborMask,
            out FirstArtRuinsCliffModule3D module,
            out int quarterTurns)
        {
            switch (neighborMask)
            {
                case FirstArtRuinsCliffCatalog3D.WestConnection:
                    module = FirstArtRuinsCliffModule3D.EndCap;
                    quarterTurns = 0;
                    return;
                case FirstArtRuinsCliffCatalog3D.NorthConnection:
                    module = FirstArtRuinsCliffModule3D.EndCap;
                    quarterTurns = 1;
                    return;
                case FirstArtRuinsCliffCatalog3D.EastConnection:
                    module = FirstArtRuinsCliffModule3D.EndCap;
                    quarterTurns = 2;
                    return;
                case FirstArtRuinsCliffCatalog3D.SouthConnection:
                    module = FirstArtRuinsCliffModule3D.EndCap;
                    quarterTurns = 3;
                    return;
                case FirstArtRuinsCliffCatalog3D.EastConnection |
                     FirstArtRuinsCliffCatalog3D.WestConnection:
                    module = SelectStraightVariant(map, x, y);
                    quarterTurns = 0;
                    return;
                case FirstArtRuinsCliffCatalog3D.NorthConnection |
                     FirstArtRuinsCliffCatalog3D.SouthConnection:
                    module = SelectStraightVariant(map, x, y);
                    quarterTurns = 1;
                    return;
                case FirstArtRuinsCliffCatalog3D.NorthConnection |
                     FirstArtRuinsCliffCatalog3D.WestConnection:
                    module = IsCliff(map, x - 1, y + 1)
                        ? FirstArtRuinsCliffModule3D.OuterCorner
                        : FirstArtRuinsCliffModule3D.InnerCorner;
                    quarterTurns = 0;
                    return;
                case FirstArtRuinsCliffCatalog3D.NorthConnection |
                     FirstArtRuinsCliffCatalog3D.EastConnection:
                    module = IsCliff(map, x + 1, y + 1)
                        ? FirstArtRuinsCliffModule3D.OuterCorner
                        : FirstArtRuinsCliffModule3D.InnerCorner;
                    quarterTurns = 1;
                    return;
                case FirstArtRuinsCliffCatalog3D.EastConnection |
                     FirstArtRuinsCliffCatalog3D.SouthConnection:
                    module = IsCliff(map, x + 1, y - 1)
                        ? FirstArtRuinsCliffModule3D.OuterCorner
                        : FirstArtRuinsCliffModule3D.InnerCorner;
                    quarterTurns = 2;
                    return;
                case FirstArtRuinsCliffCatalog3D.SouthConnection |
                     FirstArtRuinsCliffCatalog3D.WestConnection:
                    module = IsCliff(map, x - 1, y - 1)
                        ? FirstArtRuinsCliffModule3D.OuterCorner
                        : FirstArtRuinsCliffModule3D.InnerCorner;
                    quarterTurns = 3;
                    return;
                default:
                    module = FirstArtRuinsCliffModule3D.TopCap;
                    quarterTurns = 0;
                    return;
            }
        }

        private static FirstArtRuinsCliffModule3D SelectStraightVariant(
            WorldMapModel map,
            int x,
            int y)
        {
            uint variant = Hash(
                map.Width,
                map.Height,
                x,
                y,
                CliffStraightVariantSalt) & 1u;
            return variant == 0u
                ? FirstArtRuinsCliffModule3D.StraightA
                : FirstArtRuinsCliffModule3D.StraightB;
        }

        private static bool IsCliff(WorldMapModel map, int x, int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x < map.Width &&
                   y < map.Height &&
                   map.Get(x, y).Traversal == WorldTraversalKind.Cliff;
        }

        private static int CatalogIndexFor(FirstArtRuinsCliffModule3D module)
        {
            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.Entries.Count;
                 index++)
            {
                if (FirstArtRuinsCliffCatalog3D.Entries[index].Module == module)
                    return index;
            }

            throw new InvalidOperationException(
                "The selected module is missing from the approved catalog.");
        }

        private static uint Hash(
            int width,
            int height,
            int x,
            int y,
            uint salt)
        {
            unchecked
            {
                uint hash = salt;
                hash = (hash ^ (uint)width) * FnvPrime;
                hash = (hash ^ (uint)height) * FnvPrime;
                hash = (hash ^ (uint)x) * FnvPrime;
                hash = (hash ^ (uint)y) * FnvPrime;
                hash ^= hash >> 13;
                hash *= AvalancheMultiplier;
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}
