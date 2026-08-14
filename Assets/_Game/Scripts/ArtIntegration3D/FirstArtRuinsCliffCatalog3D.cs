using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Content;

namespace WasteCity.ArtIntegration3D
{
    public enum FirstArtRuinsCliffFamily3D
    {
        Ruins = 0,
        Cliff = 1,
    }

    public enum FirstArtRuinsCliffModule3D
    {
        CrackedFloorSlab = 0,
        RubblePileA = 1,
        RubblePileB = 2,
        RebarConcreteBlock = 3,
        BrokenPipe = 4,
        DrainageChannel = 5,
        BoundaryEdge = 6,
        WornMarkingPlate = 7,
        StraightA = 8,
        StraightB = 9,
        InnerCorner = 10,
        OuterCorner = 11,
        EndCap = 12,
        TopCap = 13,
    }

    public sealed class FirstArtRuinsCliffCatalogEntry3D
    {
        private readonly ReadOnlyCollection<string> materialRoles;

        internal FirstArtRuinsCliffCatalogEntry3D(
            string stableId,
            FirstArtRuinsCliffFamily3D family,
            FirstArtRuinsCliffModule3D module,
            string fbxPath,
            string prefabPath,
            Vector3 rootScale,
            Vector3 childOffset,
            Vector3 calibratedBounds,
            int canonicalConnectionMask,
            params string[] materialRoles)
        {
            _ = new StableId(stableId);
            StableId = stableId;
            Family = family;
            Module = module;
            FbxPath = fbxPath;
            PrefabPath = prefabPath;
            RootScale = rootScale;
            ChildOffset = childOffset;
            CalibratedBounds = calibratedBounds;
            CanonicalConnectionMask = canonicalConnectionMask;
            this.materialRoles = Array.AsReadOnly(
                (string[])materialRoles.Clone());
        }

        public string StableId { get; }
        public FirstArtRuinsCliffFamily3D Family { get; }
        public FirstArtRuinsCliffModule3D Module { get; }
        public string FbxPath { get; }
        public string PrefabPath { get; }
        public Vector3 RootScale { get; }
        public Vector3 ChildOffset { get; }
        public Vector3 CalibratedBounds { get; }
        public int BaseRotationYDegrees => 0;
        public int CanonicalConnectionMask { get; }
        public IReadOnlyList<string> MaterialRoles => materialRoles;

        public string SurfaceStableId =>
            Family == FirstArtRuinsCliffFamily3D.Ruins
                ? "world.obstacle.ruins"
                : "world.obstacle.cliff";
    }

    public sealed class FirstArtRuinsCliffMaterialRole3D
    {
        internal FirstArtRuinsCliffMaterialRole3D(
            string name,
            FirstArtRuinsCliffFamily3D family)
        {
            Name = name;
            Family = family;
        }

        public string Name { get; }
        public FirstArtRuinsCliffFamily3D Family { get; }
    }

    public static class FirstArtRuinsCliffCatalog3D
    {
        public const int EntryCount = 14;
        public const int RuinsEntryCount = 8;
        public const int CliffEntryCount = 6;
        public const int MaterialRoleCount = 13;
        public const int NorthConnection = 1;
        public const int EastConnection = 2;
        public const int SouthConnection = 4;
        public const int WestConnection = 8;
        public const string RequiredShaderName =
            "WasteCity/Terrain/FirstPassGeometry";
        public const string GeometryMaterialDirectory =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/";

        private const string TerrainRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/";

        private static readonly ReadOnlyCollection<FirstArtRuinsCliffCatalogEntry3D>
            entries = Array.AsReadOnly(new[]
            {
                Ruins(
                    "art.ruins.cracked-floor-slab",
                    FirstArtRuinsCliffModule3D.CrackedFloorSlab,
                    "SM_Ruins_CrackedFloorSlab",
                    "PF_Ruins_CrackedFloorSlab",
                    V(0.736612445, 0.736612445, 0.736612445),
                    V(0.005478553, 0.000000058, 0.033530462),
                    V(0.9, 0.098776901, 0.777492438),
                    "MAT_Ruins_Concrete", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_DrainDark", "MAT_Ruins_Dust",
                    "MAT_Ruins_DustFilm"),
                Ruins(
                    "art.ruins.rubble-pile-a",
                    FirstArtRuinsCliffModule3D.RubblePileA,
                    "SM_Ruins_RubblePile_A",
                    "PF_Ruins_RubblePile_A",
                    V(0.930280613, 0.930280613, 0.930280613),
                    V(-0.004548970, 0.000000063, -0.026104246),
                    V(0.9, 0.232141809, 0.719227462),
                    "MAT_Ruins_Dust", "MAT_Ruins_Concrete",
                    "MAT_Ruins_Aggregate"),
                Ruins(
                    "art.ruins.rubble-pile-b",
                    FirstArtRuinsCliffModule3D.RubblePileB,
                    "SM_Ruins_RubblePile_B",
                    "PF_Ruins_RubblePile_B",
                    V(0.643474271, 0.643474271, 0.643474271),
                    V(-0.001435036, 0.000000033, -0.000106106),
                    V(0.9, 0.159823928, 0.405880371),
                    "MAT_Ruins_Concrete", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_Dust", "MAT_Ruins_Rust"),
                Ruins(
                    "art.ruins.rebar-concrete-block",
                    FirstArtRuinsCliffModule3D.RebarConcreteBlock,
                    "SM_Ruins_RebarConcreteBlock",
                    "PF_Ruins_RebarConcreteBlock",
                    V(0.856820909, 0.856820909, 0.856820909),
                    V(-0.069655344, 0.000000059, -0.017981657),
                    V(0.9, 0.398876840, 0.686297511),
                    "MAT_Ruins_Concrete", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_DustFilm", "MAT_Ruins_Dust",
                    "MAT_Ruins_Rust"),
                Ruins(
                    "art.ruins.broken-pipe",
                    FirstArtRuinsCliffModule3D.BrokenPipe,
                    "SM_Ruins_BrokenPipe",
                    "PF_Ruins_BrokenPipe",
                    V(0.975516330, 0.975516330, 0.975516330),
                    V(0.002470289, 0.000000051, 0.032442769),
                    V(0.9, 0.641087333, 0.697020324),
                    "MAT_Ruins_Concrete", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_Rust", "MAT_Ruins_DrainDark",
                    "MAT_Ruins_DustFilm", "MAT_Ruins_Dust"),
                Ruins(
                    "art.ruins.drainage-channel",
                    FirstArtRuinsCliffModule3D.DrainageChannel,
                    "SM_Ruins_DrainageChannel",
                    "PF_Ruins_DrainageChannel",
                    V(0.818181800, 0.818181800, 0.818181800),
                    V(0, 0.000000043, -0.005614461),
                    V(0.9, 0.182575367, 0.519253806),
                    "MAT_Ruins_DrainDark", "MAT_Ruins_Concrete",
                    "MAT_Ruins_Aggregate", "MAT_Ruins_Dust",
                    "MAT_Ruins_DustFilm"),
                Ruins(
                    "art.ruins.boundary-edge",
                    FirstArtRuinsCliffModule3D.BoundaryEdge,
                    "SM_Ruins_BoundaryEdge",
                    "PF_Ruins_BoundaryEdge",
                    V(0.743841862, 0.743841862, 0.743841862),
                    V(0.011133321, 0.000000045, -0.005296984),
                    V(0.9, 0.154912706, 0.541050135),
                    "MAT_Ruins_DarkFloor", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_Concrete", "MAT_Ruins_DrainDark",
                    "MAT_Ruins_Dust", "MAT_Ruins_DustFilm"),
                Ruins(
                    "art.ruins.worn-marking-plate",
                    FirstArtRuinsCliffModule3D.WornMarkingPlate,
                    "SM_Ruins_WornMarkingPlate",
                    "PF_Ruins_WornMarkingPlate",
                    V(0.813866507, 0.813866507, 0.813866507),
                    V(-0.003272742, 0.000000052, 0.012372339),
                    V(0.9, 0.054904969, 0.661674072),
                    "MAT_Ruins_DarkFloor", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_Marking", "MAT_Ruins_DrainDark",
                    "MAT_Ruins_Concrete", "MAT_Ruins_Dust",
                    "MAT_Ruins_DustFilm"),
                Cliff(
                    "art.cliff.straight-a",
                    FirstArtRuinsCliffModule3D.StraightA,
                    "SM_Cliff_Straight_A",
                    "PF_Cliff_Straight_A",
                    10,
                    V(0.326633299, 0.600246292, 0.326633299),
                    V(0.001620335, 0.000000051, 0.047667824),
                    V(0.9, 0.9, 0.432732677)),
                Cliff(
                    "art.cliff.straight-b",
                    FirstArtRuinsCliffModule3D.StraightB,
                    "SM_Cliff_Straight_B",
                    "PF_Cliff_Straight_B",
                    10,
                    V(0.332272719, 0.596549113, 0.332272719),
                    V(0.010047115, 0.000000051, 0.052382713),
                    V(0.9, 0.9, 0.452821658)),
                Cliff(
                    "art.cliff.inner-corner",
                    FirstArtRuinsCliffModule3D.InnerCorner,
                    "SM_Cliff_InnerCorner",
                    "PF_Cliff_InnerCorner",
                    9,
                    V(0.332957051, 0.599471748, 0.332957051),
                    V(0.007434510, 0.000000134, -0.093297475),
                    V(0.9, 0.9, 0.724235948)),
                Cliff(
                    "art.cliff.outer-corner",
                    FirstArtRuinsCliffModule3D.OuterCorner,
                    "SM_Cliff_OuterCorner",
                    "PF_Cliff_OuterCorner",
                    9,
                    V(0.391098892, 0.596748804, 0.391098892),
                    V(0.115721460, 0.000000134, -0.089152067),
                    V(0.867786007, 0.9, 0.9)),
                Cliff(
                    "art.cliff.end-cap",
                    FirstArtRuinsCliffModule3D.EndCap,
                    "SM_Cliff_EndCap",
                    "PF_Cliff_EndCap",
                    8,
                    V(0.369910121, 0.600328434, 0.369910121),
                    V(0.010333406, 0.000000050, 0.055235103),
                    V(0.9, 0.9, 0.489557935)),
                Cliff(
                    "art.cliff.top-cap",
                    FirstArtRuinsCliffModule3D.TopCap,
                    "SM_Cliff_TopCap",
                    "PF_Cliff_TopCap",
                    15,
                    V(0.327494533, 0.600163695, 0.327494533),
                    V(-0.004963322, 0.000000118, 0.053226166),
                    V(0.858200781, 0.9, 0.9)),
            });

        private static readonly ReadOnlyCollection<FirstArtRuinsCliffMaterialRole3D>
            materialRoles = Array.AsReadOnly(new[]
            {
                Role("MAT_Ruins_Concrete", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_Aggregate", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_DustFilm", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_Dust", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_DarkFloor", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_DrainDark", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_Rust", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Ruins_Marking", FirstArtRuinsCliffFamily3D.Ruins),
                Role("MAT_Cliff_Strata", FirstArtRuinsCliffFamily3D.Cliff),
                Role("MAT_Cliff_Fracture", FirstArtRuinsCliffFamily3D.Cliff),
                Role("MAT_Cliff_Dust", FirstArtRuinsCliffFamily3D.Cliff),
                Role("MAT_Cliff_Rubble", FirstArtRuinsCliffFamily3D.Cliff),
                Role("MAT_Cliff_Mineral", FirstArtRuinsCliffFamily3D.Cliff),
            });

        public static IReadOnlyList<FirstArtRuinsCliffCatalogEntry3D> Entries =>
            entries;

        public static IReadOnlyList<FirstArtRuinsCliffMaterialRole3D> MaterialRoles =>
            materialRoles;

        public static bool TryGetEntry(
            string stableId,
            out FirstArtRuinsCliffCatalogEntry3D entry)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].StableId, stableId, StringComparison.Ordinal))
                {
                    entry = entries[index];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public static bool TryGetMaterialRole(
            string name,
            out FirstArtRuinsCliffMaterialRole3D role)
        {
            for (int index = 0; index < materialRoles.Count; index++)
            {
                if (string.Equals(materialRoles[index].Name, name, StringComparison.Ordinal))
                {
                    role = materialRoles[index];
                    return true;
                }
            }

            role = null;
            return false;
        }

        private static FirstArtRuinsCliffCatalogEntry3D Ruins(
            string stableId,
            FirstArtRuinsCliffModule3D module,
            string fbxName,
            string prefabName,
            Vector3 rootScale,
            Vector3 childOffset,
            Vector3 calibratedBounds,
            params string[] roles)
        {
            return new FirstArtRuinsCliffCatalogEntry3D(
                stableId,
                FirstArtRuinsCliffFamily3D.Ruins,
                module,
                TerrainRoot + "Ruins/Models/" + fbxName + ".fbx",
                TerrainRoot + "Ruins/Runtime/Prefabs/" + prefabName + ".prefab",
                rootScale,
                childOffset,
                calibratedBounds,
                0,
                roles);
        }

        private static FirstArtRuinsCliffCatalogEntry3D Cliff(
            string stableId,
            FirstArtRuinsCliffModule3D module,
            string fbxName,
            string prefabName,
            int canonicalConnectionMask,
            Vector3 rootScale,
            Vector3 childOffset,
            Vector3 calibratedBounds)
        {
            return new FirstArtRuinsCliffCatalogEntry3D(
                stableId,
                FirstArtRuinsCliffFamily3D.Cliff,
                module,
                TerrainRoot + "Cliff/Models/" + fbxName + ".fbx",
                TerrainRoot + "Cliff/Runtime/Prefabs/" + prefabName + ".prefab",
                rootScale,
                childOffset,
                calibratedBounds,
                canonicalConnectionMask,
                "MAT_Cliff_Strata", "MAT_Cliff_Fracture",
                "MAT_Cliff_Dust", "MAT_Cliff_Rubble",
                "MAT_Cliff_Mineral");
        }

        private static FirstArtRuinsCliffMaterialRole3D Role(
            string name,
            FirstArtRuinsCliffFamily3D family)
        {
            return new FirstArtRuinsCliffMaterialRole3D(name, family);
        }

        private static Vector3 V(double x, double y, double z)
        {
            return new Vector3((float)x, (float)y, (float)z);
        }
    }
}
