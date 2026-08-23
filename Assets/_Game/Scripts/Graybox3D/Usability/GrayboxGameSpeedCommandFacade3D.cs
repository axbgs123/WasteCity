using System;
using WasteCity.Core;

namespace WasteCity.Graybox3D.Usability
{
    public sealed class GrayboxGameSpeedCommandFacade3D
    {
        private readonly GameSpeedModel speed;

        public GrayboxGameSpeedCommandFacade3D(GameSpeedModel speed)
        {
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
        }

        public float RequestedSpeed =>
            speed.IsPaused(GamePauseReason.User)
                ? 0f
                : Normalize(speed.RequestedSpeed);

        public float EffectiveSpeed =>
            speed.Speed <= 0f
                ? 0f
                : Normalize(speed.Speed);

        public float LastNonZeroSpeed =>
            NormalizeNonZero(speed.LastNonZeroSpeed);

        public GrayboxGameSpeedPersistenceState3D CaptureForPersistence()
        {
            return new GrayboxGameSpeedPersistenceState3D(
                RequestedSpeed,
                LastNonZeroSpeed);
        }

        public bool TryPrepareRestore(
            GrayboxGameSpeedPersistenceState3D state,
            out GrayboxGameSpeedRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (state == null)
            {
                error = "Formal game-speed state is required.";
                return false;
            }
            if (!IsFormalRequestedSpeed(state.RequestedSpeed))
            {
                error = "Requested formal speed must be 0x, 1x, or 2x.";
                return false;
            }
            if (state.LastNonZeroSpeed != 1f &&
                state.LastNonZeroSpeed != 2f)
            {
                error = "Last non-zero formal speed must be 1x or 2x.";
                return false;
            }

            plan = new GrayboxGameSpeedRestorePlan3D(
                this,
                speed.Revision,
                RequestedSpeed,
                LastNonZeroSpeed,
                state.RequestedSpeed,
                state.LastNonZeroSpeed);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            GrayboxGameSpeedRestorePlan3D plan,
            out string error)
        {
            if (plan == null)
            {
                error = "Formal game-speed restore plan is required.";
                return false;
            }
            if (!ReferenceEquals(plan.Owner, this))
            {
                error = "Formal game-speed restore plan has another owner.";
                return false;
            }
            if (plan.Consumed)
            {
                error = "Formal game-speed restore plan is already consumed.";
                return false;
            }
            if (plan.ExpectedRevision != speed.Revision ||
                plan.ExpectedRequestedSpeed != RequestedSpeed ||
                plan.ExpectedLastNonZeroSpeed != LastNonZeroSpeed)
            {
                error = "Game speed changed after restore preparation.";
                return false;
            }

            bool userPaused = plan.RequestedSpeed == 0f;
            float underlyingRequested = userPaused
                ? plan.LastNonZeroSpeed
                : plan.RequestedSpeed;
            if (!speed.TryRestoreSpeedState(
                    underlyingRequested,
                    plan.LastNonZeroSpeed,
                    userPaused,
                    out error))
            {
                return false;
            }
            plan.Consumed = true;
            error = string.Empty;
            return true;
        }

        public void RequestSpeed(int requestedSpeed)
        {
            int normalized = Math.Max(0, Math.Min(2, requestedSpeed));
            if (normalized == 0)
            {
                speed.SetPaused(GamePauseReason.User, true);
                return;
            }

            speed.Set(normalized);
            speed.SetPaused(GamePauseReason.User, false);
        }

        public void ToggleTacticalPause()
        {
            if (speed.IsPaused(GamePauseReason.User))
            {
                speed.Set(LastNonZeroSpeed);
                speed.SetPaused(GamePauseReason.User, false);
                return;
            }

            speed.SetPaused(GamePauseReason.User, true);
        }

        public float ResolveRuleDelta(float unscaledDelta)
        {
            if (float.IsNaN(unscaledDelta) ||
                float.IsInfinity(unscaledDelta) ||
                unscaledDelta <= 0f)
                return 0f;
            return unscaledDelta * EffectiveSpeed;
        }

        private static float Normalize(float value)
        {
            if (value <= 0f) return 0f;
            return value < 1.5f ? 1f : 2f;
        }

        private static float NormalizeNonZero(float value)
        {
            return value < 1.5f ? 1f : 2f;
        }

        private static bool IsFormalRequestedSpeed(float value)
        {
            return value == 0f || value == 1f || value == 2f;
        }
    }
}
