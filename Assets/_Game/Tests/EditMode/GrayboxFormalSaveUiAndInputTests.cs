using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Persistence;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveUiAndInputTests
    {
        private const string EntryTypeName =
            "WasteCity.Graybox3D.Usability." +
            "GrayboxFormalSaveEntryController3D";
        private const string EntrySourcePath =
            "Assets/_Game/Scripts/Graybox3D/Usability/" +
            "GrayboxFormalSaveEntryController3D.cs";
        private const string RuntimeHostSourcePath =
            "Assets/_Game/Scripts/Graybox3D/Building/" +
            "GrayboxFormalSaveRuntimeHost3D.cs";

        [Test]
        public void IDEA0015_EntryExposesStablePlayerVisibleStartupState()
        {
            Type entryType = ResolveEntryType();

            AssertPublicReadableProperty(
                entryType,
                "IsStartPageOpen",
                typeof(bool));
            AssertPublicReadableProperty(
                entryType,
                "CanContinue",
                typeof(bool));
            AssertPublicReadableProperty(
                entryType,
                "IsNewGameConfirmationOpen",
                typeof(bool));
            AssertPublicReadableProperty(
                entryType,
                "FeedbackMessage",
                typeof(string));
            AssertPublicReadableProperty(
                entryType,
                "IsRuntimeReady",
                typeof(bool));
        }

        [Test]
        public void IDEA0015_StartAndSaveControlsUseStableFormalInputIds()
        {
            var controls = GrayboxSystemMenuView3D
                .ResolveVisibleControlIds(false);

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Start.Continue",
                    "Start.NewGame",
                    "Start.NewGameConfirm",
                    "Start.NewGameCancel",
                    "FormalSave.Feedback",
                    "Exit.SaveAndQuit",
                    "Exit.Cancel",
                },
                controls);
            Assert.That(controls, Does.Not.Contain("Exit.Confirm"));
        }

        [Test]
        public void IDEA0015_EntryConsumesStructuredResultsWithoutSaveDtos()
        {
            Type entryType = ResolveEntryType();
            Type[] referencedTypes = entryType
                .GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Concat(entryType.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters()
                        .Select(parameter => parameter.ParameterType)
                        .Append(method.ReturnType)))
                .ToArray();

            Assert.That(
                referencedTypes,
                Does.Contain(typeof(FormalSaveStoreResult)),
                "启动入口必须消费统一存档服务的结构化结果。");
            Assert.That(
                referencedTypes,
                Does.Contain(typeof(GrayboxFormalSaveCoordinatorResult3D)),
                "启动入口必须消费 3D 协调器的结构化结果。");
            Assert.That(
                referencedTypes.Contains(typeof(FormalSaveEnvelope)),
                Is.False,
                "UGUI 输入层不得持有正式存档 DTO。");
            Assert.That(
                referencedTypes.Any(type =>
                    type.FullName != null &&
                    type.FullName.Contains("FormalThreeDSaveData")),
                Is.False,
                "UGUI 输入层不得持有领域 payload。");

            string source = File.ReadAllText(EntrySourcePath);
            StringAssert.DoesNotContain("System.IO", source);
            StringAssert.DoesNotContain("File.", source);
            StringAssert.DoesNotContain("PlayerPrefs", source);
            StringAssert.Contains("FormalSavePayloadKind.Formal3D", source);
            StringAssert.Contains("Legacy2DOnly", source);
            StringAssert.Contains("CanContinue", source);
        }

        [Test]
        public void IDEA0015_InputCoordinatorOwnsOneSerializedStartupGate()
        {
            Type entryType = ResolveEntryType();
            FieldInfo field = typeof(GrayboxUsabilityInputCoordinator3D)
                .GetField(
                    "formalSaveEntry",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(entryType));
            Assert.That(
                field.GetCustomAttribute<SerializeField>(),
                Is.Not.Null,
                "启动页输入门必须由场景稳定序列化接线。");
        }

        [Test]
        public void IDEA0015_PlayerFacingMessagesComeFromStructuredResults()
        {
            string storeSource = File.ReadAllText(
                "Assets/_Game/Scripts/Persistence/FormalSaveStore.cs");
            string entrySource = File.ReadAllText(EntrySourcePath);

            foreach (string message in new[]
                     {
                         "主存档损坏，已恢复备份",
                         "检测到旧版 2D 存档，不能直接用于当前 3D 游戏",
                         "保存失败，原存档未被覆盖",
                     })
            {
                StringAssert.Contains(message, storeSource, message);
            }
            StringAssert.Contains(".Message", entrySource);
        }

        [Test]
        public void IDEA0020_NewGameFailureDoesNotReuseTheEarlierProbeMessage()
        {
            string source = File.ReadAllText(EntrySourcePath);
            string start = ExtractMethodBlock(
                source,
                "private void StartNewProgress(");

            int diagnostic = start.IndexOf(
                "LastStartNewProgressError",
                StringComparison.Ordinal);
            int storeFallback = start.IndexOf(
                "ApplyCommandFeedback(",
                StringComparison.Ordinal);
            Assert.That(diagnostic, Is.GreaterThanOrEqualTo(0));
            Assert.That(storeFallback, Is.GreaterThan(diagnostic));
        }

        [Test]
        public void IDEA0015_CheckpointWarningUsesStructuredStateAndUsabilityOwnsPlayerCopy()
        {
            Type hostType = typeof(GrayboxFormalSaveRuntimeHost3D);
            PropertyInfo warningState = hostType.GetProperty(
                "HasCheckpointWarning",
                BindingFlags.Instance | BindingFlags.Public);
            EventInfo warningChanged = hostType.GetEvent(
                "CheckpointWarningChanged",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(
                warningState,
                Is.Not.Null,
                "Building 运行时必须只暴露结构化的检查点警告状态。");
            Assert.That(warningState.PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(warningState.CanRead, Is.True);
            Assert.That(
                warningChanged,
                Is.Not.Null,
                "Building 运行时必须发布结构化的检查点警告变化事件。");
            Assert.That(
                warningChanged.EventHandlerType,
                Is.EqualTo(typeof(Action<bool>)));
            Assert.That(
                hostType.GetProperty(
                    "CheckpointWarningMessage",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null,
                "Building 运行时不得向 UI 暴露玩家文案。");

            const string playerCopy =
                "自动存档失败，当前进度尚未保存";
            string hostSource = File.ReadAllText(RuntimeHostSourcePath);
            string entrySource = File.ReadAllText(EntrySourcePath);

            StringAssert.DoesNotContain(
                playerCopy,
                hostSource,
                "玩家文案不得由 Building 层持有。");
            StringAssert.Contains(
                playerCopy,
                entrySource,
                "Usability 入口必须把结构化状态映射为固定玩家文案。");
        }

        private static Type ResolveEntryType()
        {
            Type type = typeof(GrayboxSystemMenuController3D).Assembly
                .GetType(EntryTypeName, false);
            Assert.That(
                type,
                Is.Not.Null,
                EntryTypeName + " must exist for the formal 3D entry flow.");
            Assert.That(type.IsSubclassOf(typeof(MonoBehaviour)), Is.True);
            return type;
        }

        private static string ExtractMethodBlock(
            string source,
            string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int open = source.IndexOf('{', start);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), signature);
            var depth = 0;
            for (var index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                if (source[index] != '}') continue;
                depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            Assert.Fail("Method block is incomplete: " + signature);
            return string.Empty;
        }

        private static void AssertPublicReadableProperty(
            Type owner,
            string name,
            Type propertyType)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType), name);
            Assert.That(property.CanRead, Is.True, name);
        }
    }
}
