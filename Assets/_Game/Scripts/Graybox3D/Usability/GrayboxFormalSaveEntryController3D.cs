using UnityEngine;
using UnityEngine.SceneManagement;
using WasteCity.Core;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;

namespace WasteCity.Graybox3D.Usability
{
    [DefaultExecutionOrder(10000)]
    public sealed class GrayboxFormalSaveEntryController3D :
        MonoBehaviour,
        IGrayboxFormalSaveExitCommand3D
    {
        private const string AutomaticCheckpointWarning =
            "自动存档失败，当前进度尚未保存";

        [SerializeField]
        private GrayboxFormalSaveRuntimeHost3D runtimeHost;
        [SerializeField] private GrayboxSystemMenuView3D view;
        [SerializeField]
        private GrayboxSystemMenuController3D systemMenu;
        [SerializeField]
        private GrayboxUsabilityInputCoordinator3D inputCoordinator;

        private bool slotRequiresOverwriteConfirmation;

        public bool IsStartPageOpen { get; private set; }
        public bool CanContinue { get; private set; }
        public bool IsNewGameConfirmationOpen { get; private set; }
        public string FeedbackMessage { get; private set; } = string.Empty;
        public bool IsRuntimeReady { get; private set; }
        public bool BlocksGameplayInput => IsStartPageOpen;

        public void RefreshView()
        {
            view?.RenderStartPage(
                IsStartPageOpen,
                CanContinue,
                IsNewGameConfirmationOpen,
                FeedbackMessage);
            view?.SetCheckpointWarning(
                runtimeHost != null && runtimeHost.HasCheckpointWarning
                    ? AutomaticCheckpointWarning
                    : string.Empty);
        }

        public void RequestContinue()
        {
            if (!IsStartPageOpen || !CanContinue || runtimeHost == null)
                return;

            bool succeeded = runtimeHost.TryContinue();
            ApplyCommandFeedback(
                succeeded,
                runtimeHost.LastStoreResult,
                runtimeHost.LastCoordinatorResult);
            if (succeeded)
                EnterGameplay();
            else
                RefreshView();
        }

        public void RequestNewGame()
        {
            if (!IsStartPageOpen) return;
            if (slotRequiresOverwriteConfirmation)
            {
                IsNewGameConfirmationOpen = true;
                RefreshView();
                return;
            }
            StartNewProgress();
        }

        public void ConfirmNewGame()
        {
            if (!IsStartPageOpen || !IsNewGameConfirmationOpen)
                return;
            StartNewProgress();
        }

        public void CancelNewGame()
        {
            if (!IsStartPageOpen || !IsNewGameConfirmationOpen)
                return;
            IsNewGameConfirmationOpen = false;
            RefreshView();
        }

        public GrayboxFormalSaveUiResult3D SaveAndExit()
        {
            if (!IsRuntimeReady || runtimeHost == null)
            {
                return new GrayboxFormalSaveUiResult3D(
                    false,
                    FeedbackMessage);
            }

            bool succeeded = runtimeHost.TrySaveAndExit();
            GrayboxFormalSaveUiResult3D result = ApplyCommandFeedback(
                succeeded,
                runtimeHost.LastStoreResult,
                runtimeHost.LastCoordinatorResult);
            RefreshView();
            return result;
        }

        public GrayboxFormalSaveUiResult3D RetryWaveCheckpoint()
        {
            if (!IsRuntimeReady || runtimeHost == null)
            {
                return new GrayboxFormalSaveUiResult3D(
                    false,
                    "正式 3D 存档服务尚未就绪");
            }

            bool succeeded = runtimeHost.TryRetryWaveCheckpoint();
            GrayboxFormalSaveUiResult3D result =
                !succeeded && runtimeHost.LastCoordinatorResult != null &&
                !runtimeHost.LastCoordinatorResult.Success
                    ? MapCoordinatorResult(
                        runtimeHost.LastCoordinatorResult)
                    : new GrayboxFormalSaveUiResult3D(
                        succeeded,
                        runtimeHost.LastWaveRetryStoreResult?.Message);
            FeedbackMessage = result.Message;
            RefreshView();
            return result;
        }

        public bool ReturnToTitle()
        {
            if (runtimeHost == null) return false;
            runtimeHost.Speed.SetPaused(GamePauseReason.Title, true);
            Time.timeScale = runtimeHost.Speed.Speed;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
                SceneManager.LoadScene(activeScene.buildIndex);
            else if (!string.IsNullOrWhiteSpace(activeScene.name))
                SceneManager.LoadScene(activeScene.name);
            else
                return false;
            return true;
        }

        private void Awake()
        {
            IsStartPageOpen = true;
            IsRuntimeReady = false;
            IsNewGameConfirmationOpen = false;

            view?.ConfigureFormalSaveEntry(this);
            systemMenu?.ConfigureFormalSaveExit(this);
            inputCoordinator?.ConfigureFormalSaveEntry(this);
            if (runtimeHost != null)
            {
                runtimeHost.CheckpointWarningChanged +=
                    OnCheckpointWarningChanged;
                runtimeHost.Speed.SetPaused(GamePauseReason.Title, true);
                Time.timeScale = runtimeHost.Speed.Speed;
                systemMenu?.ConfigureRuntimeServices(
                    runtimeHost.Speed,
                    new UnityGrayboxApplicationExit3D(),
                    view);
                ApplyProbe(runtimeHost.Probe());
            }
            RefreshView();
        }

        private void OnDestroy()
        {
            if (runtimeHost != null)
                runtimeHost.CheckpointWarningChanged -=
                    OnCheckpointWarningChanged;
        }

        private void OnCheckpointWarningChanged(bool hasWarning)
        {
            view?.SetCheckpointWarning(
                hasWarning
                    ? AutomaticCheckpointWarning
                    : string.Empty);
        }

        private void Start()
        {
            if (runtimeHost != null && runtimeHost.TryInitialize())
                return;
            CanContinue = false;
            FeedbackMessage = "正式 3D 存档服务尚未就绪";
            RefreshView();
        }

        private void StartNewProgress()
        {
            if (runtimeHost == null || !runtimeHost.TryStartNewProgress())
            {
                if (runtimeHost != null && !string.IsNullOrWhiteSpace(
                        runtimeHost.LastStartNewProgressError))
                {
                    FeedbackMessage =
                        "无法开始新进度：" +
                        runtimeHost.LastStartNewProgressError;
                    RefreshView();
                    return;
                }
                ApplyCommandFeedback(
                    false,
                    runtimeHost?.LastStoreResult,
                    runtimeHost?.LastCoordinatorResult);
                RefreshView();
                return;
            }

            ApplyCommandFeedback(
                true,
                runtimeHost.LastStoreResult,
                runtimeHost.LastCoordinatorResult);
            EnterGameplay();
        }

        private void EnterGameplay()
        {
            IsRuntimeReady = true;
            IsStartPageOpen = false;
            IsNewGameConfirmationOpen = false;
            CanContinue = false;
            runtimeHost.Speed.SetPaused(GamePauseReason.Title, false);
            Time.timeScale = runtimeHost.Speed.Speed;
            RefreshView();
        }

        private void ApplyProbe(FormalSaveStoreResult result)
        {
            CanContinue = result != null &&
                result.CanContinue &&
                result.PayloadKind == FormalSavePayloadKind.Formal3D;
            slotRequiresOverwriteConfirmation = result != null &&
                (result.Code == FormalSaveStoreCode.Legacy2DOnly ||
                 result.Code == FormalSaveStoreCode.LoadSucceeded ||
                 result.Code == FormalSaveStoreCode.BackupRecovered ||
                 result.Code ==
                     FormalSaveStoreCode.UnsupportedFutureSchema ||
                 result.Code == FormalSaveStoreCode.CorruptNoBackup ||
                 result.Code == FormalSaveStoreCode.DiskReadFailed);
            FeedbackMessage = result?.Message ?? string.Empty;
        }

        private GrayboxFormalSaveUiResult3D ApplyCommandFeedback(
            bool succeeded,
            FormalSaveStoreResult storeResult,
            GrayboxFormalSaveCoordinatorResult3D coordinatorResult)
        {
            GrayboxFormalSaveUiResult3D result;
            if (!succeeded && coordinatorResult != null &&
                !coordinatorResult.Success)
            {
                result = MapCoordinatorResult(coordinatorResult);
            }
            else if (!succeeded && storeResult != null &&
                     !storeResult.Success)
            {
                result = MapStoreResult(storeResult);
            }
            else if (!succeeded)
            {
                result = new GrayboxFormalSaveUiResult3D(
                    false,
                    "正式 3D 存档服务尚未就绪");
            }
            else if (storeResult != null &&
                (!storeResult.Success || coordinatorResult == null ||
                 coordinatorResult.Success))
            {
                result = MapStoreResult(storeResult);
            }
            else if (coordinatorResult != null)
            {
                result = MapCoordinatorResult(coordinatorResult);
            }
            else
            {
                result = new GrayboxFormalSaveUiResult3D(
                    succeeded,
                    FeedbackMessage);
            }
            FeedbackMessage = result.Message;
            return result;
        }

        private GrayboxFormalSaveUiResult3D MapStoreResult(
            FormalSaveStoreResult result)
        {
            return new GrayboxFormalSaveUiResult3D(
                result != null && result.Success,
                result?.Message);
        }

        private GrayboxFormalSaveUiResult3D MapCoordinatorResult(
            GrayboxFormalSaveCoordinatorResult3D result)
        {
            return new GrayboxFormalSaveUiResult3D(
                result != null && result.Success,
                result?.Message);
        }
    }
}
