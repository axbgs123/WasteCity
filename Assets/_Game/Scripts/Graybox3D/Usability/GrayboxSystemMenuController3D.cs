using System;
using UnityEngine;
using WasteCity.Core;

namespace WasteCity.Graybox3D.Usability
{
    public sealed class GrayboxFormalSaveUiResult3D
    {
        public GrayboxFormalSaveUiResult3D(
            bool success,
            string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
    }

    public interface IGrayboxFormalSaveExitCommand3D
    {
        GrayboxFormalSaveUiResult3D SaveAndExit();
    }

    public enum GrayboxSystemMenuPage3D
    {
        Main,
        Settings,
        OperationGuide,
        ExitConfirm
    }

    public sealed class GrayboxSystemMenuController3D : MonoBehaviour
    {
        [SerializeField] private GrayboxSystemMenuView3D view;

        private GameSpeedModel speed;
        private GrayboxGameSpeedCommandFacade3D speedCommands;
        private GrayboxDisplaySettingsModel3D settings;
        private IGrayboxApplicationExit applicationExit;
        private IGrayboxFormalSaveExitCommand3D formalSaveExit;
        private GrayboxSystemMenuPage3D operationGuideReturnPage;
        private float openingRequestedSpeed = 1f;
        private bool ownsSystemMenuPause;
        private bool exitRequested;
        private bool canExitWithoutSaving;

        public bool IsOpen { get; private set; }
        public GrayboxSystemMenuPage3D Page { get; private set; } =
            GrayboxSystemMenuPage3D.Main;
        public GrayboxDisplaySettingsModel3D Settings => settings;
        public GrayboxGameSpeedCommandFacade3D SpeedCommands
        {
            get
            {
                EnsureRuntimeServices();
                return speedCommands;
            }
        }
        public bool CanExitWithoutSaving => canExitWithoutSaving;
        public bool IsTacticalPaused =>
            speed != null && speed.IsPaused(GamePauseReason.User);

        public void Configure(
            GameSpeedModel speed,
            GrayboxDisplaySettingsModel3D settings,
            IGrayboxApplicationExit applicationExit,
            GrayboxSystemMenuView3D view = null)
        {
            ReleasePauseOwnership();
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
            speedCommands = new GrayboxGameSpeedCommandFacade3D(this.speed);
            this.settings = settings ??
                throw new ArgumentNullException(nameof(settings));
            this.applicationExit = applicationExit ??
                throw new ArgumentNullException(nameof(applicationExit));
            if (view != null)
                this.view = view;
            IsOpen = false;
            Page = GrayboxSystemMenuPage3D.Main;
            operationGuideReturnPage = GrayboxSystemMenuPage3D.Settings;
            exitRequested = false;
            canExitWithoutSaving = false;
            this.view?.SetController(this);
            RenderView();
        }

        public void SetView(GrayboxSystemMenuView3D view)
        {
            this.view = view;
            this.view?.SetController(this);
            RenderView();
        }

        public void ConfigureFormalSaveExit(
            IGrayboxFormalSaveExitCommand3D formalSaveExit)
        {
            this.formalSaveExit = formalSaveExit;
        }

        public void ConfigureRuntimeServices(
            GameSpeedModel speed,
            IGrayboxApplicationExit applicationExit,
            GrayboxSystemMenuView3D view = null)
        {
            settings?.Cancel();
            ReleasePauseOwnership();
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
            speedCommands = new GrayboxGameSpeedCommandFacade3D(this.speed);
            this.applicationExit = applicationExit ??
                throw new ArgumentNullException(nameof(applicationExit));
            IsOpen = false;
            Page = GrayboxSystemMenuPage3D.Main;
            canExitWithoutSaving = false;
            exitRequested = false;
            if (view != null)
                this.view = view;
            this.view?.SetController(this);
            ApplyEffectiveSpeed();
            RenderView();
        }

        public void Open()
        {
            EnsureRuntimeServices();
            if (IsOpen) return;
            openingRequestedSpeed = speed.RequestedSpeed;
            ownsSystemMenuPause = true;
            speed.SetPaused(GamePauseReason.SystemMenu, true);
            IsOpen = true;
            Page = GrayboxSystemMenuPage3D.Main;
            canExitWithoutSaving = false;
            settings.Cancel();
            ApplyEffectiveSpeed();
            RenderView();
        }

        public void Continue()
        {
            Close();
        }

        public void ToggleTacticalPause()
        {
            EnsureRuntimeServices();
            speedCommands.ToggleTacticalPause();
            ApplyEffectiveSpeed();
        }

        public void RequestSpeed(int requestedSpeed)
        {
            EnsureRuntimeServices();
            speedCommands.RequestSpeed(requestedSpeed);
            ApplyEffectiveSpeed();
        }

        public void Close()
        {
            settings?.Cancel();
            IsOpen = false;
            Page = GrayboxSystemMenuPage3D.Main;
            canExitWithoutSaving = false;
            ReleasePauseOwnership();
            RenderView();
        }

        public void OpenSettings()
        {
            if (!IsOpen) return;
            settings.Cancel();
            Page = GrayboxSystemMenuPage3D.Settings;
            RenderView();
        }

        public void StageResolution(int resolutionIndex)
        {
            EnsureSettingsPage();
            if (resolutionIndex < 0 ||
                resolutionIndex >= settings.AvailableResolutions.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(resolutionIndex));
            settings.StageResolution(
                settings.AvailableResolutions[resolutionIndex]);
            RenderView();
        }

        public void StageWindowMode(GrayboxWindowMode3D windowMode)
        {
            EnsureSettingsPage();
            settings.StageWindowMode(windowMode);
            RenderView();
        }

        public bool ApplySettings()
        {
            EnsureSettingsPage();
            bool applied = settings.Apply();
            RenderView();
            return applied;
        }

        public void CancelSettings()
        {
            if (!IsOpen || Page != GrayboxSystemMenuPage3D.Settings)
                return;
            settings.Cancel();
            Page = GrayboxSystemMenuPage3D.Main;
            RenderView();
        }

        public void RestoreDefaultSettings()
        {
            EnsureSettingsPage();
            settings.RestoreDefaults();
            RenderView();
        }

        public void OpenOperationGuide()
        {
            if (!IsOpen) return;
            operationGuideReturnPage = Page ==
                GrayboxSystemMenuPage3D.Settings
                ? GrayboxSystemMenuPage3D.Settings
                : GrayboxSystemMenuPage3D.Main;
            Page = GrayboxSystemMenuPage3D.OperationGuide;
            RenderView();
        }

        public void BackFromOperationGuide()
        {
            if (!IsOpen ||
                Page != GrayboxSystemMenuPage3D.OperationGuide)
                return;
            Page = operationGuideReturnPage;
            RenderView();
        }

        public void OpenExitConfirmation()
        {
            if (!IsOpen) return;
            Page = GrayboxSystemMenuPage3D.ExitConfirm;
            RenderView();
        }

        public void CancelExit()
        {
            if (!IsOpen || Page != GrayboxSystemMenuPage3D.ExitConfirm)
                return;
            Page = GrayboxSystemMenuPage3D.Main;
            RenderView();
        }

        public void ConfirmExit()
        {
            if (!IsOpen ||
                Page != GrayboxSystemMenuPage3D.ExitConfirm ||
                exitRequested)
                return;
            GrayboxFormalSaveUiResult3D result = formalSaveExit?.SaveAndExit();
            if (result == null)
            {
                view?.SetFormalSaveFeedback(
                    "保存服务尚未就绪，请稍后重试");
                canExitWithoutSaving = true;
                RenderView();
                return;
            }
            view?.SetFormalSaveFeedback(result.Message);
            if (!result.Success)
            {
                canExitWithoutSaving = true;
                RenderView();
                return;
            }
            exitRequested = true;
            applicationExit.Exit();
        }

        public void ConfirmExitWithoutSaving()
        {
            if (!IsOpen ||
                Page != GrayboxSystemMenuPage3D.ExitConfirm ||
                exitRequested || !canExitWithoutSaving)
                return;
            exitRequested = true;
            applicationExit.Exit();
        }

        public void RefreshView()
        {
            RenderView();
        }

        private void Awake()
        {
            view?.SetController(this);
        }

        private void OnDisable()
        {
            DiscardOpenMenu();
        }

        private void OnDestroy()
        {
            DiscardOpenMenu();
            view = null;
            speed = null;
            speedCommands = null;
            settings = null;
            applicationExit = null;
            formalSaveExit = null;
        }

        private void DiscardOpenMenu()
        {
            settings?.Cancel();
            IsOpen = false;
            Page = GrayboxSystemMenuPage3D.Main;
            canExitWithoutSaving = false;
            ReleasePauseOwnership();
            RenderView();
        }

        private void ReleasePauseOwnership()
        {
            if (speed == null || !ownsSystemMenuPause)
                return;
            speed.Set(openingRequestedSpeed);
            speed.SetPaused(GamePauseReason.SystemMenu, false);
            ownsSystemMenuPause = false;
            ApplyEffectiveSpeed();
        }

        private void ApplyEffectiveSpeed()
        {
            if (speed != null)
                Time.timeScale = speed.Speed;
        }

        private void EnsureRuntimeServices()
        {
            speed ??= new GameSpeedModel();
            speedCommands ??=
                new GrayboxGameSpeedCommandFacade3D(speed);
            settings ??= new GrayboxDisplaySettingsModel3D(
                new UnityGrayboxDisplaySettingsPlatform3D(),
                new PlayerPrefsGrayboxDisplaySettingsStore3D());
            applicationExit ??= new UnityGrayboxApplicationExit3D();
        }

        private void EnsureSettingsPage()
        {
            if (!IsOpen || Page != GrayboxSystemMenuPage3D.Settings ||
                settings == null)
                throw new InvalidOperationException(
                    "Display settings are only editable on the Settings page.");
        }

        private void RenderView()
        {
            view?.Render(IsOpen, Page, settings);
        }
    }
}
