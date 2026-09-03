using System;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxLocalHasteDomain3D
    {
        None = 0,
        Production = 1,
        Research = 2,
        Defense = 3,
    }

    public sealed class GrayboxLocalHasteController3D
    {
        private readonly FormalFateRuntime fate;
        private readonly LocalHasteRuntime runtime;

        public GrayboxLocalHasteController3D(
            FormalFateRuntime fate,
            LocalHasteRuntime runtime)
        {
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public GrayboxLocalHasteDomain3D SelectedDomain =>
            ParseDomain(runtime.Capture().TargetId);

        public bool TrySelectDomain(
            GrayboxLocalHasteDomain3D domain,
            out string error)
        {
            if (!IsSelected() || domain == GrayboxLocalHasteDomain3D.None)
            {
                error = "只有选择局部时加后才能指定加速区域";
                return false;
            }
            return runtime.TrySelectTarget(DomainId(domain), out error);
        }

        public bool TryStart(out string error)
        {
            if (!IsSelected())
            {
                error = "当前命轨不是局部时加";
                return false;
            }
            return runtime.TryStart(out error);
        }

        public bool TryStop()
        {
            return runtime.TryStop();
        }

        public float MultiplierFor(GrayboxLocalHasteDomain3D domain)
        {
            LocalHasteSnapshot snapshot = runtime.Capture();
            return IsSelected() && snapshot.Active &&
                   domain != GrayboxLocalHasteDomain3D.None &&
                   domain == ParseDomain(snapshot.TargetId)
                ? runtime.Multiplier
                : 1f;
        }

        public static bool IsTargetEligible(
            GrayboxLocalHasteDomain3D domain,
            bool productionEligible,
            bool researchEligible,
            bool defenseEligible)
        {
            switch (domain)
            {
                case GrayboxLocalHasteDomain3D.Production:
                    return productionEligible;
                case GrayboxLocalHasteDomain3D.Research:
                    return researchEligible;
                case GrayboxLocalHasteDomain3D.Defense:
                    return defenseEligible;
                default:
                    return false;
            }
        }

        public bool Tick(
            float ruleDeltaSeconds,
            bool globallyPaused,
            bool targetEligible,
            out LocalHasteTickProjection projection,
            out string error)
        {
            if (!IsSelected())
            {
                runtime.TryStop();
                return runtime.Tick(
                    0f,
                    globallyPaused: true,
                    out projection,
                    out error);
            }
            if (runtime.IsActive && !targetEligible)
                runtime.TryStop();
            return runtime.Tick(
                ruleDeltaSeconds,
                globallyPaused,
                out projection,
                out error);
        }

        private bool IsSelected()
        {
            FormalFateSnapshot snapshot = fate.Capture();
            return snapshot.Level >= 1 && string.Equals(
                snapshot.SelectedId,
                FormalFateCatalog.LocalHasteId,
                StringComparison.Ordinal);
        }

        private static string DomainId(GrayboxLocalHasteDomain3D domain)
        {
            switch (domain)
            {
                case GrayboxLocalHasteDomain3D.Production:
                    return "production";
                case GrayboxLocalHasteDomain3D.Research:
                    return "research";
                case GrayboxLocalHasteDomain3D.Defense:
                    return "defense";
                default:
                    return string.Empty;
            }
        }

        private static GrayboxLocalHasteDomain3D ParseDomain(string value)
        {
            switch (value)
            {
                case "production":
                    return GrayboxLocalHasteDomain3D.Production;
                case "research":
                    return GrayboxLocalHasteDomain3D.Research;
                case "defense":
                    return GrayboxLocalHasteDomain3D.Defense;
                default:
                    return GrayboxLocalHasteDomain3D.None;
            }
        }
    }
}
