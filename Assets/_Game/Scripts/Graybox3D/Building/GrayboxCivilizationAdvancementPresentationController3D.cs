using System;
using System.Globalization;
using System.Text;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public interface IGrayboxCivilizationAdvancementInputBinding3D
    {
        void ConfigureAdvancement(
            GrayboxCivilizationAdvancementView3D view,
            Func<bool> tryAdvance,
            Func<bool> tryContinue);
    }

    public sealed class GrayboxCivilizationAdvancementPresentation3D
    {
        internal GrayboxCivilizationAdvancementPresentation3D(
            string requirementsText,
            string summaryText,
            string fatePreviewText,
            string stageText,
            string hintText,
            bool promptVisible,
            bool canAdvance,
            bool canContinue)
        {
            RequirementsText = requirementsText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            FatePreviewText = fatePreviewText ?? string.Empty;
            StageText = stageText ?? string.Empty;
            HintText = hintText ?? string.Empty;
            PromptVisible = promptVisible;
            CanAdvance = canAdvance;
            CanContinue = canContinue;
        }

        public string RequirementsText { get; }
        public string SummaryText { get; }
        public string FatePreviewText { get; }
        public string StageText { get; }
        public string HintText { get; }
        public bool PromptVisible { get; }
        public bool CanAdvance { get; }
        public bool CanContinue { get; }
    }

    public sealed class
        GrayboxCivilizationAdvancementPresentationController3D : IDisposable
    {
        private readonly FormalCivilizationAscensionRuntime civilization;
        private readonly FormalFateRuntime fate;
        private readonly AdvancementSequenceModel sequence;
        private readonly GrayboxCivilizationAdvancementView3D view;
        private readonly Func<FormalCivilizationAscensionRequirements>
            requirementsProvider;
        private FormalCivilizationAscensionSnapshot renderedCivilization;
        private FormalFateSnapshot renderedFate;
        private AdvancementSequenceSnapshot renderedSequence;
        private FormalCivilizationAscensionRequirements renderedRequirements;
        private GrayboxCivilizationAdvancementPresentation3D projection;

        public GrayboxCivilizationAdvancementPresentationController3D(
            FormalCivilizationAscensionRuntime civilization,
            FormalFateRuntime fate,
            AdvancementSequenceModel sequence,
            GrayboxCivilizationAdvancementView3D view,
            Func<FormalCivilizationAscensionRequirements> requirementsProvider)
        {
            this.civilization = civilization ??
                throw new ArgumentNullException(nameof(civilization));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.sequence = sequence ??
                throw new ArgumentNullException(nameof(sequence));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.requirementsProvider = requirementsProvider ??
                throw new ArgumentNullException(nameof(requirementsProvider));
            view.AdvanceRequested += HandleAdvanceRequested;
            view.ContinueRequested += HandleContinueRequested;
        }

        public event Action AdvanceRequested;
        public event Action ContinueRequested;

        public bool RefreshIfChanged()
        {
            FormalCivilizationAscensionSnapshot civilizationSnapshot =
                civilization.Capture();
            FormalFateSnapshot fateSnapshot = fate.Capture();
            AdvancementSequenceSnapshot sequenceSnapshot = sequence.Capture();
            FormalCivilizationAscensionRequirements requirements =
                requirementsProvider();
            if (ReferenceEquals(renderedCivilization, civilizationSnapshot) &&
                ReferenceEquals(renderedFate, fateSnapshot) &&
                ReferenceEquals(renderedSequence, sequenceSnapshot) &&
                ReferenceEquals(renderedRequirements, requirements))
            {
                return false;
            }

            projection = BuildProjection(
                civilizationSnapshot,
                fateSnapshot,
                sequenceSnapshot,
                requirements);
            if (sequenceSnapshot.IsPresenting) view.Open();
            else if (sequenceSnapshot.Stage ==
                     AdvancementSequenceStage.Continued)
                view.Close();
            view.Apply(projection);
            renderedCivilization = civilizationSnapshot;
            renderedFate = fateSnapshot;
            renderedSequence = sequenceSnapshot;
            renderedRequirements = requirements;
            return true;
        }

        public bool TryOpen()
        {
            RefreshIfChanged();
            if (projection == null || !projection.CanAdvance) return false;
            view.Open();
            view.Apply(projection);
            return true;
        }

        public void ClosePreview()
        {
            if (sequence.Capture().IsPresenting) return;
            view.Close();
            if (projection != null) view.Apply(projection);
        }

        public void Dispose()
        {
            view.AdvanceRequested -= HandleAdvanceRequested;
            view.ContinueRequested -= HandleContinueRequested;
            AdvanceRequested = null;
            ContinueRequested = null;
        }

        private void HandleAdvanceRequested()
        {
            RefreshIfChanged();
            if (projection != null && projection.CanAdvance)
                AdvanceRequested?.Invoke();
        }

        private void HandleContinueRequested()
        {
            RefreshIfChanged();
            if (projection != null && projection.CanContinue)
                ContinueRequested?.Invoke();
        }

        private GrayboxCivilizationAdvancementPresentation3D
            BuildProjection(
                FormalCivilizationAscensionSnapshot civilizationSnapshot,
                FormalFateSnapshot fate,
                AdvancementSequenceSnapshot sequence,
                FormalCivilizationAscensionRequirements requirements)
        {
            bool eligible =
                civilization.CanPrepareAscension(requirements) &&
                sequence.Stage == AdvancementSequenceStage.None;
            var checklist = new StringBuilder();
            if (requirements != null)
            {
                for (var index = 0; index < requirements.Statuses.Count; index++)
                {
                    FormalCivilizationAscensionRequirementStatus status =
                        requirements.Statuses[index];
                    if (index > 0) checklist.Append('\n');
                    checklist.Append(status.IsMet ? "✓  " : "□  ")
                        .Append(status.DisplayName);
                    if (!status.IsMet)
                        checklist.Append(" — ").Append(status.MissingText);
                }
            }
            FormalFateDefinition definition =
                FormalFateCatalog.Find(fate.SelectedId);
            string fatePreview = definition == null
                ? "尚未选择正式命轨"
                : definition.DisplayName + "  Lv." +
                  civilization.TargetFateLevel + "\n" +
                  definition.LevelTwoSummary;
            return new GrayboxCivilizationAdvancementPresentation3D(
                checklist.ToString(),
                "文明 Lv." + civilizationSnapshot.CivilizationLevel +
                " → Lv." + civilization.TargetCivilizationLevel +
                "\n关注度 " + Signed(civilization.AttentionReward) +
                "\n升阶不额外消耗资源",
                fatePreview,
                StageText(sequence),
                eligible ? "U  执行文明升阶" : string.Empty,
                eligible,
                eligible,
                sequence.Stage == AdvancementSequenceStage.Results);
        }

        private string StageText(AdvancementSequenceSnapshot snapshot)
        {
            string remaining = snapshot.Remaining > 0f
                ? "  " + snapshot.Remaining.ToString(
                    "0.0", CultureInfo.InvariantCulture) + " 秒"
                : string.Empty;
            switch (snapshot.Stage)
            {
                case AdvancementSequenceStage.Scanning:
                    return "扫描：确认文明基础" + remaining;
                case AdvancementSequenceStage.Confirmed:
                    return "确认：命轨响应稳定" + remaining;
                case AdvancementSequenceStage.Warning:
                    return "警告：关注度将变化 " +
                        Signed(civilization.AttentionReward) + remaining;
                case AdvancementSequenceStage.Results:
                    return "完成：文明与命轨已升至 Lv." +
                        civilization.TargetCivilizationLevel;
                case AdvancementSequenceStage.Continued:
                    return "文明升阶结果已确认";
                default:
                    return "文明升阶准备就绪";
            }
        }

        private static string Signed(int value)
        {
            return value > 0
                ? "+" + value.ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

    }
}
