using System;
using UnityEngine;
using WasteCity.Defense;

namespace WasteCity.Graybox3D.Building
{
    public readonly struct GrayboxDefenseSettlementCommandResult3D
    {
        public GrayboxDefenseSettlementCommandResult3D(
            bool success,
            string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
    }

    public interface IGrayboxDefenseSettlementCommands3D
    {
        GrayboxDefenseSettlementCommandResult3D Execute(
            SingleCityDefenseSettlementAction action);
    }

    public sealed class GrayboxDefenseSettlementController3D :
        MonoBehaviour
    {
        [SerializeField] private GrayboxDefenseSettlementView3D view;

        private IGrayboxDefenseSettlementCommands3D commands;
        private SingleCityDefenseSettlementSnapshot snapshot;
        private bool actionListenerBound;
        private bool commandExecuting;
        private bool hasPresentedTerminalRevision;
        private ulong lastPresentedTerminalRevision;

        public bool IsOpen => view != null && view.IsOpen;
        public bool IsCommandExecuting => commandExecuting;
        public SingleCityDefenseSettlementSnapshot Snapshot => snapshot;

        public void Configure(
            GrayboxDefenseSettlementView3D configuredView,
            IGrayboxDefenseSettlementCommands3D configuredCommands)
        {
            if (configuredView == null)
                throw new ArgumentNullException(nameof(configuredView));
            if (configuredCommands == null)
                throw new ArgumentNullException(nameof(configuredCommands));

            Close();
            view = configuredView;
            commands = configuredCommands;
        }

        public bool Open(SingleCityDefenseSettlementSnapshot newSnapshot)
        {
            if (newSnapshot == null) return false;
            if (hasPresentedTerminalRevision &&
                lastPresentedTerminalRevision ==
                newSnapshot.TerminalRevision)
            {
                return false;
            }
            if (view == null) return false;

            CloseCurrentPresentation();
            if (!view.Open(newSnapshot)) return false;
            snapshot = newSnapshot;
            hasPresentedTerminalRevision = true;
            lastPresentedTerminalRevision =
                newSnapshot.TerminalRevision;
            BindActionListener();
            return true;
        }

        public GrayboxDefenseSettlementCommandResult3D Execute(
            SingleCityDefenseSettlementAction action)
        {
            if (!IsOpen || snapshot == null)
                return Failed("结算界面未打开");
            if (commandExecuting)
                return Failed("结算操作正在执行，请稍候");
            if (!IsAvailable(action))
                return Failed("当前结算不允许此操作");
            if (commands == null)
                return Failed("结算命令尚未接入");

            commandExecuting = true;
            GrayboxDefenseSettlementCommandResult3D result;
            try
            {
                result = commands.Execute(action);
            }
            catch (Exception exception)
            {
                result = Failed(
                    "结算操作失败：" + exception.Message);
            }
            finally
            {
                commandExecuting = false;
            }

            result = NormalizeFeedback(action, result);
            if (result.Success)
                CloseCurrentPresentation();
            else
                view.SetFeedback(result.Message);
            return result;
        }

        public void Close()
        {
            CloseCurrentPresentation();
        }

        private void OnDestroy()
        {
            CloseCurrentPresentation();
            commands = null;
        }

        private void BindActionListener()
        {
            if (actionListenerBound || view == null) return;
            view.ActionRequested += HandleActionRequested;
            actionListenerBound = true;
        }

        private void UnbindActionListener()
        {
            if (!actionListenerBound) return;
            if (view != null)
                view.ActionRequested -= HandleActionRequested;
            actionListenerBound = false;
        }

        private void CloseCurrentPresentation()
        {
            UnbindActionListener();
            if (view != null) view.Close();
            snapshot = null;
            commandExecuting = false;
        }

        private void HandleActionRequested(
            SingleCityDefenseSettlementAction action)
        {
            Execute(action);
        }

        private bool IsAvailable(
            SingleCityDefenseSettlementAction action)
        {
            if (snapshot?.AvailableActions == null) return false;
            for (var index = 0;
                 index < snapshot.AvailableActions.Count;
                 index++)
            {
                if (snapshot.AvailableActions[index] == action) return true;
            }
            return false;
        }

        private static GrayboxDefenseSettlementCommandResult3D
            NormalizeFeedback(
                SingleCityDefenseSettlementAction action,
                GrayboxDefenseSettlementCommandResult3D result)
        {
            if (!string.IsNullOrWhiteSpace(result.Message)) return result;
            if (!result.Success) return Failed("结算操作未完成");

            switch (action)
            {
                case SingleCityDefenseSettlementAction.ContinueSandbox:
                    return new GrayboxDefenseSettlementCommandResult3D(
                        true,
                        "已继续沙盒模式");
                case SingleCityDefenseSettlementAction.RetryWaveCheckpoint:
                    return new GrayboxDefenseSettlementCommandResult3D(
                        true,
                        "已读取最近波前重试档");
                case SingleCityDefenseSettlementAction.ReturnToTitle:
                    return new GrayboxDefenseSettlementCommandResult3D(
                        true,
                        "正在返回标题界面");
                default:
                    return new GrayboxDefenseSettlementCommandResult3D(
                        true,
                        "结算操作已完成");
            }
        }

        private static GrayboxDefenseSettlementCommandResult3D Failed(
            string message)
        {
            return new GrayboxDefenseSettlementCommandResult3D(
                false,
                message);
        }
    }
}
