using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectQualityIntegrationTests
    {
        private static readonly string[] EnvironmentNames =
        {
            "WASTECITY_QUALITY_TEST_RESULTS", "WASTECITY_QUALITY_ANALYSIS_OUTPUT",
            "WASTECITY_QUALITY_VERIFIED_SHA", "WASTECITY_QUALITY_VERIFIED_AT",
            "WASTECITY_QUALITY_EDITMODE_RESULTS", "WASTECITY_QUALITY_PLAYMODE_RESULTS",
            "WASTECITY_QUALITY_COMPILE_LOG", "WASTECITY_QUALITY_BUILD_SUMMARY",
            "WASTECITY_QUALITY_HUMAN_PLAYTEST", "WASTECITY_QUALITY_CHANGED_PATHS",
        };

        private static readonly string[] GeneratedDocumentPaths =
        {
            ProjectDocumentationGenerator.ProjectInventoryPath,
            ProjectDocumentationGenerator.TestInventoryPath,
            ProjectDocumentationGenerator.VerificationPath,
            ProjectDocumentationGenerator.AttentionPath,
        };

        private readonly Dictionary<string, string> savedEnvironment = new Dictionary<string, string>();
        private string fixtureDirectory;

        [SetUp]
        public void SetUp()
        {
            fixtureDirectory = Path.Combine(Path.GetTempPath(), "wastecity-project-quality-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureDirectory);
            foreach (string name in EnvironmentNames)
            {
                savedEnvironment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (KeyValuePair<string, string> value in savedEnvironment)
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            if (Directory.Exists(fixtureDirectory)) Directory.Delete(fixtureDirectory, true);
        }

        [Test]
        public void PublicBatchMethods_AreStaticParameterlessVoid()
        {
            foreach (string name in new[]
            {
                "GenerateDocumentation", "ValidateDocumentation", "AnalyzeTestResults", "RecordVerification",
            })
            {
                MethodInfo method = typeof(ProjectQualityTools).GetMethod(name,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                Assert.That(method, Is.Not.Null, name + " must be public static");
                Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), name + " must return void");
                Assert.That(method.GetParameters(), Is.Empty, name + " must be parameterless");
            }
        }

        [Test]
        public void MenuAndBatchSource_UseOnlyTheRequiredSafeEntryPoints()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot(),
                "Assets/_Game/Editor/ProjectQuality/ProjectQualityTools.cs"));
            foreach (string name in new[] { "Generate Documentation", "Validate Documentation", "Analyze Test Results", "Record Verification" })
                StringAssert.Contains("WasteCity/Project Quality/" + name, source);
            foreach (string environment in EnvironmentNames) StringAssert.Contains(environment, source);
            StringAssert.DoesNotContain("git", source.ToLowerInvariant());
            StringAssert.DoesNotContain("process.", source.ToLowerInvariant());
        }

        [Test]
        public void AnalyzeTestResults_MissingRequiredVariablesNamesTheMissingVariables()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ProjectQualityTools.AnalyzeTestResults());

            StringAssert.Contains("WASTECITY_QUALITY_TEST_RESULTS", error.Message);
            StringAssert.Contains("WASTECITY_QUALITY_ANALYSIS_OUTPUT", error.Message);
        }

        [Test]
        public void AnalyzeTestResults_RejectsEveryProjectPathAndLeavesRepositoryBytesUnchanged()
        {
            string results = Write("result.xml", "<test-run result=\"Passed\" total=\"1\" passed=\"1\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_TEST_RESULTS", results);
            foreach (string rejectedOutput in new[]
            {
                Path.Combine(ProjectRoot(), "Assets", "quality-analysis-rejected.txt"),
                Path.Combine(ProjectRoot(), "Packages", "quality-analysis-rejected.txt"),
                Path.Combine(ProjectRoot(), "ProjectSettings", "quality-analysis-rejected.txt"),
                Path.Combine(ProjectRoot(), "Docs", "Generated", "Latest-Verification-ZH.md"),
                Path.Combine(ProjectRoot(), "Docs", "Engineering", "project-quality-catalog.json"),
                Path.Combine(ProjectRoot(), "README.md"),
            })
            {
                Dictionary<string, string> before = HashRepositoryFiles(ProjectRoot());
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", rejectedOutput);
                bool existed = File.Exists(rejectedOutput);
                byte[] original = existed ? File.ReadAllBytes(rejectedOutput) : null;
                try
                {
                    InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                        () => ProjectQualityTools.AnalyzeTestResults());
                    StringAssert.Contains("WASTECITY_QUALITY_ANALYSIS_OUTPUT", error.Message);
                }
                finally
                {
                    RestoreExactFile(rejectedOutput, existed, original);
                }
                CollectionAssert.IsEmpty(ChangedPaths(before, HashRepositoryFiles(ProjectRoot())));
            }

            string output = Path.Combine(fixtureDirectory, "analysis.txt");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", output);
            ProjectQualityTools.AnalyzeTestResults();
            Assert.That(File.Exists(output), Is.True);
            StringAssert.Contains("测试结果摘要", File.ReadAllText(output));
        }

        [Test]
        public void AnalyzeTestResults_RejectsReparsePointAncestorsWithoutWritingProtectedRoots()
        {
            RequireNativeSymlinkSupport();
            string results = Write("result.xml", StandardNUnitXml("Passed", 1, 1, 0, 0));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_TEST_RESULTS", results);
            foreach (string protectedRoot in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string link = Path.Combine(fixtureDirectory, "link-" + protectedRoot);
                if (CreateNativeSymbolicLink(Path.Combine(ProjectRoot(), protectedRoot), link) != 0)
                    Assert.Ignore("symbolic links are unavailable on this platform");
                string target = Path.Combine(link, "quality-analysis-" + Guid.NewGuid().ToString("N") + ".txt");
                Dictionary<string, string> before = HashRepositoryFiles(ProjectRoot());
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", target);

                try
                {
                    Assert.Throws<InvalidOperationException>(() => ProjectQualityTools.AnalyzeTestResults());
                    Assert.That(File.Exists(target), Is.False);
                }
                finally
                {
                    if (File.Exists(target)) File.Delete(target);
                }
                CollectionAssert.IsEmpty(ChangedPaths(before, HashRepositoryFiles(ProjectRoot())));
            }
        }

        [Test]
        public void SymlinkFixture_HasWindowsGateBeforeNativeInterop()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot(),
                "Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs"));
            const string signature = "private static void RequireNativeSymlinkSupport()";
            int stringLiteral = source.IndexOf(signature, StringComparison.Ordinal);
            int declaration = source.IndexOf(signature, stringLiteral + signature.Length, StringComparison.Ordinal);
            Assert.That(declaration, Is.GreaterThanOrEqualTo(0));
            string body = source.Substring(declaration);
            int windows = body.IndexOf("RuntimeInformation.IsOSPlatform(OSPlatform.Windows)", StringComparison.Ordinal);
            int ignored = body.IndexOf("Assert.Ignore", StringComparison.Ordinal);
            Assert.That(windows, Is.GreaterThanOrEqualTo(0));
            Assert.That(ignored, Is.GreaterThan(windows));
            int fixture = source.IndexOf("public void AnalyzeTestResults_RejectsReparsePointAncestorsWithoutWritingProtectedRoots()",
                StringComparison.Ordinal);
            int gate = source.IndexOf("RequireNativeSymlinkSupport();", fixture, StringComparison.Ordinal);
            int native = source.IndexOf("CreateNativeSymbolicLink(", gate, StringComparison.Ordinal);
            Assert.That(gate, Is.GreaterThan(fixture));
            Assert.That(native, Is.GreaterThan(gate));
        }

        [Test]
        public void RecordVerification_MissingEveryExplicitValueNamesEachVariable()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ProjectQualityTools.RecordVerification());

            foreach (string name in EnvironmentNames.Where(name => name != "WASTECITY_QUALITY_TEST_RESULTS" &&
                name != "WASTECITY_QUALITY_ANALYSIS_OUTPUT" && name != "WASTECITY_QUALITY_CHANGED_PATHS"))
                StringAssert.Contains(name, error.Message);
        }

        [Test]
        public void RecordVerification_ParsesSuppliedEvidenceAndOnlyChangesLatestVerification()
        {
            string verification = Path.Combine(ProjectRoot(), ProjectDocumentationGenerator.VerificationPath);
            string original = File.ReadAllText(verification);
            Dictionary<string, string> before = HashFiles(ProjectRoot(), true);
            try
            {
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_SHA", "0123456789abcdef0123456789abcdef01234567");
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_AT", "2026-08-12T12:00:00+08:00");
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_EDITMODE_RESULTS", Write("edit.xml",
                    StandardNUnitXml("Passed", 2, 2, 0, 0)));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_PLAYMODE_RESULTS", Write("play.xml",
                    StandardNUnitXml("Passed", 1, 1, 0, 0)));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_COMPILE_LOG", Write("compile.log", "Compilation succeeded\n"));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_BUILD_SUMMARY", Write("builds.json",
                    "{\"Builds\":[{\"Name\":\"Windows 3D\",\"Status\":\"NotRequired\",\"EvidenceLogPath\":\"" +
                    EscapeJson(Write("build.log", "runtime inputs unchanged\n")) + "\",\"Reason\":\"runtime/build inputs unchanged\"}]}"));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_HUMAN_PLAYTEST", "未进行");

                ProjectQualityTools.RecordVerification();

                StringAssert.Contains("2/2", File.ReadAllText(verification));
                Dictionary<string, string> after = HashFiles(ProjectRoot(), true);
                CollectionAssert.AreEquivalent(new[] { ProjectDocumentationGenerator.VerificationPath },
                    ChangedPaths(before, after));
            }
            finally
            {
                File.WriteAllText(verification, original, new UTF8Encoding(false));
            }
        }

        [Test]
        public void RecordVerification_RejectsFailedTestEvidenceWithoutChangingLatestVerification()
        {
            ConfigureValidRecordEvidence(StandardNUnitXml("Failed", 1, 0, 1, 0));
            AssertRecordRejectedWithoutWrite();
        }

        [Test]
        public void RecordVerification_RejectsFailedBuildWithoutChangingLatestVerification()
        {
            ConfigureValidRecordEvidence(StandardNUnitXml("Passed", 1, 1, 0, 0), "Failed");
            AssertRecordRejectedWithoutWrite();
        }

        [Test]
        public void RecordVerification_RejectsZeroCaseClaimWithoutChangingLatestVerification()
        {
            ConfigureValidRecordEvidence("<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\" />");
            AssertRecordRejectedWithoutWrite();
        }

        [TestCase("<test-run result=\"Passed\" total=\"2\" passed=\"2\" failed=\"0\" skipped=\"0\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>")]
        [TestCase("<test-run result=\"Passed\" total=\"1\" passed=\"0\" failed=\"0\" skipped=\"1\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>")]
        [TestCase("<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Skip\" result=\"Skipped\" /></test-run>")]
        public void RecordVerification_RejectsSummaryMismatchWithoutChangingLatestVerification(string xml)
        {
            ConfigureValidRecordEvidence(xml);
            AssertRecordRejectedWithoutWrite();
        }

        [Test]
        public void GenerateThenValidate_ChangesOnlyGeneratedFilesAndUsesExplicitChangedPaths()
        {
            string changedPaths = Write("changed-paths.txt",
                "Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs\n");
            var originalGeneratedFiles = new Dictionary<string, byte[]>();
            foreach (string relativePath in GeneratedDocumentPaths)
                originalGeneratedFiles[relativePath] = File.ReadAllBytes(Path.Combine(ProjectRoot(), relativePath));
            var originalEnvironment = new Dictionary<string, string>();
            foreach (string name in EnvironmentNames)
                originalEnvironment[name] = Environment.GetEnvironmentVariable(name);

            try
            {
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_CHANGED_PATHS", changedPaths);
                Dictionary<string, string> before = HashFiles(ProjectRoot(), false);
                ProjectQualityTools.GenerateDocumentation();
                Dictionary<string, string> afterGeneration = HashFiles(ProjectRoot(), false);
                Assert.That(ChangedPaths(before, afterGeneration).All(path => path.StartsWith("Docs/Generated/", StringComparison.Ordinal)), Is.True);
                StringAssert.Contains("Docs/Generated/Project-Inventory-ZH.md", File.ReadAllText(Path.Combine(ProjectRoot(),
                    ProjectDocumentationGenerator.AttentionPath)));

                Dictionary<string, string> beforeValidation = HashFiles(ProjectRoot(), true);
                ProjectQualityTools.ValidateDocumentation();
                CollectionAssert.IsEmpty(ChangedPaths(beforeValidation, HashFiles(ProjectRoot(), true)));
            }
            finally
            {
                foreach (KeyValuePair<string, byte[]> file in originalGeneratedFiles)
                    File.WriteAllBytes(Path.Combine(ProjectRoot(), file.Key), file.Value);
                foreach (KeyValuePair<string, string> value in originalEnvironment)
                    Environment.SetEnvironmentVariable(value.Key, value.Value);
            }
        }

        [Test]
        public void PermanentCompletionGate_RequiresQualityContractsAndDiscoverabilityLinks()
        {
            string agents = ReadGuide("AGENTS.md");
            string completionGate = MarkdownSectionBody(agents, "## 开发完成门", "## 用户反馈处理");
            AssertCompletionGateStep(completionGate, 1, "审查范围", "稳定需求 ID");
            AssertCompletionGateStep(completionGate, 2, "生产文件必须归入功能组", "推荐复用目录", "对应测试");
            AssertCompletionGateStep(completionGate, 3, "WASTECITY_QUALITY_CHANGED_PATHS", "UTF-8", "文档关注提醒");
            AssertCompletionGateStep(completionGate, 4, "ProjectQualityTools.GenerateDocumentation");
            AssertCompletionGateStep(completionGate, 5, "ProjectQualityTools.ValidateDocumentation", "只读");
            AssertCompletionGateStep(completionGate, 6, "按改动风险", "完整测试", "无界面编译", "Windows 构建", "独立运行");
            AssertCompletionGateStep(completionGate, 7, "ProjectQualityTools.AnalyzeTestResults", "ProjectQualityTools.RecordVerification");
            AssertCompletionGateStep(completionGate, 8, "Docs/06-User-Feedback-and-Change-Control-ZH.md", "人工试玩", "审批");
            AssertCompletionGateStep(completionGate, 9, "精确文件列表", "受保护文件");
            StringAssert.Contains("自动工具不得改变玩法审批或人工验收结论", completionGate);
            StringAssert.Contains("生成的技术文档不是", agents);

            string feedback = MarkdownSectionBody(agents, "## 用户反馈处理", "## 仓库安全");
            AssertOrderedText(feedback, "先在对话中澄清", "向用户展示摘要", "用户明确确认", "更新 `Docs/06");
            foreach (string requirement in new[] { "修改位置", "目标规则", "边界", "验收", "原文差异", "稳定编号", "审批状态" })
                StringAssert.Contains(requirement, feedback);
            StringAssert.DoesNotContain("先更新 `Docs/06-User-Feedback-and-Change-Control-ZH.md`", feedback);

            string readme = ReadGuide("README.md");
            string docsIndex = ReadGuide("Docs/README.md");
            foreach (string document in new[]
            {
                "Docs/06-User-Feedback-and-Change-Control-ZH.md",
                "Docs/07-Project-Use-and-Development-Guide-ZH.md",
                "Docs/08-Testing-and-Bug-Location-Guide-ZH.md",
                "Docs/09-Reusable-Project-Catalog-ZH.md",
            }) AssertMarkdownLinkExists(readme, "README.md", document);
            foreach (string appendix in new[]
            {
                "Docs/Generated/Project-Inventory-ZH.md",
                "Docs/Generated/Test-Inventory-ZH.md",
                "Docs/Generated/Latest-Verification-ZH.md",
                "Docs/Generated/Documentation-Attention-ZH.md",
            }) AssertMarkdownLinkExists(readme, "README.md", appendix);

            foreach (string document in new[]
            {
                "06-User-Feedback-and-Change-Control-ZH.md",
                "07-Project-Use-and-Development-Guide-ZH.md",
                "08-Testing-and-Bug-Location-Guide-ZH.md",
                "09-Reusable-Project-Catalog-ZH.md",
                "Generated/Project-Inventory-ZH.md",
                "Generated/Test-Inventory-ZH.md",
                "Generated/Latest-Verification-ZH.md",
                "Generated/Documentation-Attention-ZH.md",
            }) AssertMarkdownLinkExists(docsIndex, "Docs/README.md", document);
            AssertAllMarkdownLinksResolve(readme, "README.md");
            AssertAllMarkdownLinksResolve(docsIndex, "Docs/README.md");
            StringAssert.Contains("WASTECITY_QUALITY_CHANGED_PATHS", readme);
            StringAssert.Contains("git diff --name-only \"$REVIEW_BASE\"...HEAD", readme);
            StringAssert.Contains("ProjectQualityTools.GenerateDocumentation", readme);
            StringAssert.Contains("本次没有仓库变更", readme);

            string roadmap = ReadGuide("Docs/05-Formal-Development-Roadmap-ZH.md");
            string changeControl = ReadGuide("Docs/06-User-Feedback-and-Change-Control-ZH.md");
            StringAssert.Contains("| `DOC-0001` | 文档变更 | 项目测试、可复用目录与自动文档维护规范 | `P1` | `已明确` | `已批准` | `已实现待验证`", changeControl);
            StringAssert.Contains("普通中文指南、测试定位、复用目录、自动清单与完成门已实现并通过机器验证；等待用户复验", changeControl);
            StringAssert.Contains("DOC-0001`：项目测试、可复用目录与自动文档维护规范；工具、主文档和机器验证已完成，等待用户验证。", roadmap);
            StringAssert.DoesNotContain("DOC-0001`：项目测试、可复用目录与自动文档维护规范；工具和主文档仍在实施中，未验证。", roadmap);
            StringAssert.Contains("DOC-0001", roadmap);
            StringAssert.Contains("质量", roadmap);
            StringAssert.Contains("不计入玩法完成度", roadmap);
            StringAssert.Contains("主 GDD 正式版整体：约 **50%**", roadmap);
            StringAssert.Contains("正式美术、动画与音频：约 **8%**", roadmap);
        }

        [Test]
        public void GenerateDocumentation_UsesExplicitChangedPathListAndRestoresGeneratedEvidence()
        {
            string changedPaths = Write("explicit-changed-paths.txt",
                "Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs\n");
            string attentionPath = Path.Combine(ProjectRoot(), ProjectDocumentationGenerator.AttentionPath);
            var originalGeneratedFiles = new Dictionary<string, byte[]>();
            foreach (string relativePath in GeneratedDocumentPaths)
                originalGeneratedFiles[relativePath] = File.ReadAllBytes(Path.Combine(ProjectRoot(), relativePath));
            var originalEnvironment = new Dictionary<string, string>();
            foreach (string name in EnvironmentNames)
                originalEnvironment[name] = Environment.GetEnvironmentVariable(name);

            try
            {
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_CHANGED_PATHS", changedPaths);
                ProjectQualityTools.GenerateDocumentation();

                string attention = File.ReadAllText(attentionPath);
                StringAssert.Contains("Docs/Generated/Project-Inventory-ZH.md", attention);
                StringAssert.Contains("Docs/Generated/Test-Inventory-ZH.md", attention);
                StringAssert.Contains("项目质量目录或测试映射变化后", attention);
            }
            finally
            {
                foreach (KeyValuePair<string, byte[]> file in originalGeneratedFiles)
                    File.WriteAllBytes(Path.Combine(ProjectRoot(), file.Key), file.Value);
                foreach (KeyValuePair<string, string> value in originalEnvironment)
                    Environment.SetEnvironmentVariable(value.Key, value.Value);
            }
        }

        [Test]
        public void ScopedChangedPathCollection_IncludesOnlyTheApprovedUntrackedFixture()
        {
            string root = ProjectRoot();
            string fixtureRelativePath = "task8-scoped-changed-path-" + Guid.NewGuid().ToString("N") + ".txt";
            string fixturePath = Path.Combine(root, fixtureRelativePath);
            try
            {
                File.WriteAllText(fixturePath, "scoped fixture\n", new UTF8Encoding(false));
                string[] collected = CollectScopedGitPaths(root, fixtureRelativePath);

                CollectionAssert.Contains(collected, fixtureRelativePath);
                CollectionAssert.DoesNotContain(collected,
                    "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_BaseColor.png.meta");
                CollectionAssert.DoesNotContain(collected, "ProjectSettings/PackageManagerSettings.asset");
                CollectionAssert.DoesNotContain(collected, "ProjectSettings/URPProjectSettings.asset");

                string readme = ReadGuide("README.md");
                StringAssert.Contains("### A. 已提交审查", readme);
                StringAssert.Contains("git diff --name-only \"$REVIEW_BASE\"...HEAD -- \"${TASK_PATHS[@]}\"", readme);
                StringAssert.Contains("### B. 提交前正常开发", readme);
                StringAssert.Contains("TASK_PATHS=(\"精确路径1\" \"精确路径2\")", readme);
                StringAssert.Contains("git diff --cached --name-only -- \"${TASK_PATHS[@]}\"", readme);
                StringAssert.Contains("git diff --name-only -- \"${TASK_PATHS[@]}\"", readme);
                StringAssert.Contains("git ls-files --others --exclude-standard -- \"${TASK_PATHS[@]}\"", readme);
                StringAssert.Contains("LC_ALL=C sort -u", readme);
                StringAssert.Contains("生成前输入清单", readme);
                StringAssert.Contains("最终暂存清单", readme);
                StringAssert.Contains("PowerShell", readme);
            }
            finally
            {
                if (File.Exists(fixturePath)) File.Delete(fixturePath);
            }
        }

        [Test]
        public void PlainChineseHumanGuides_HaveOrderedSectionsResolvedLinksAndPlainLanguage()
        {
            string userGuide = ReadGuide("Docs/07-Project-Use-and-Development-Guide-ZH.md");
            string bugGuide = ReadGuide("Docs/08-Testing-and-Bug-Location-Guide-ZH.md");
            string reuseGuide = ReadGuide("Docs/09-Reusable-Project-Catalog-ZH.md");

            AssertGuideContract(userGuide, "Docs/07-Project-Use-and-Development-Guide-ZH.md",
                "# 废土移动城市使用与开发入门", "## 这份说明适合谁看", new[]
                {
                    "这份说明适合谁看", "游戏目前能做什么", "明确尚未完成的内容", "怎样打开默认 3D 游戏",
                    "两个场景的区别", "主要按键和界面", "开发修改器只在开发版本出现", "想修改某个功能先看哪里",
                    "新电脑交接最短路径", "出问题时先做什么", "技术清单链接",
                }, "06-User-Feedback-and-Change-Control-ZH.md", "Generated/Project-Inventory-ZH.md");
            StringAssert.Contains("默认 3D", userGuide);
            StringAssert.Contains("冻结 2D", userGuide);
            StringAssert.Contains("不是新功能模板", userGuide);

            AssertGuideContract(bugGuide, "Docs/08-Testing-and-Bug-Location-Guide-ZH.md",
                "# 废土移动城市测试与 Bug 定位指南", "## 适合谁看", new[]
                {
                    "适合谁看", "测试是什么，不是什么", "五层检查", "按功能选择测试", "怎样读失败定位报告",
                    "明天试玩记录模板", "Bug 修复流程", "偶发失败不能直接忽略", "什么情况下要构建 Windows", "给开发者/AI 的命令入口",
                }, "06-User-Feedback-and-Change-Control-ZH.md", "Generated/Test-Inventory-ZH.md");
            StringAssert.Contains("复现失败 → 失败测试 → 最小修复 → 单功能检查 → 相关检查 → 完整回归 → 人工确认", bugGuide);
            StringAssert.Contains("自动报告只是排查起点", bugGuide);
            StringAssert.Contains("失败报告中的“只重跑这个失败”", bugGuide);
            StringAssert.Contains("精确测试文件与测试类", bugGuide);
            StringAssert.Contains("-testFilter 'WasteCity.Tests.CityPathfinderTests|WasteCity.Tests.CityTerrainRulesTests'", bugGuide);
            StringAssert.Contains("PROJECT_ROOT=\"$(git rev-parse --show-toplevel)\"", bugGuide);
            StringAssert.Contains("mkdir -p /tmp/wastecity-project-quality", bugGuide);
            StringAssert.DoesNotContain("只适用于 macOS/Linux", bugGuide);
            string macOsCommands = MarkdownSectionBody(bugGuide, "### macOS", "### Linux");
            StringAssert.Contains("只适用于 macOS", macOsCommands);
            StringAssert.Contains(".app/Contents/MacOS/Unity", macOsCommands);
            string linuxCommands = MarkdownSectionBody(bugGuide, "### Linux", "### Windows");
            StringAssert.Contains("只适用于 Linux", linuxCommands);
            StringAssert.Contains("UNITY_BIN=\"$HOME/Unity/Hub/Editor/2022.3.62f1/Editor/Unity\"", linuxCommands);
            StringAssert.Contains("按实际安装路径替换", linuxCommands);
            StringAssert.Contains("find \"$HOME/Unity/Hub/Editor\"", linuxCommands);
            StringAssert.DoesNotContain(".app/Contents/MacOS/Unity", linuxCommands);
            string windowsCommands = MarkdownSectionBody(bugGuide, "### Windows", "</details>");
            StringAssert.Contains("Unity Test Runner", windowsCommands);
            StringAssert.DoesNotContain("/Users/baiyan1", bugGuide);
            StringAssert.DoesNotContain("复制对应的 `-testFilter`", bugGuide);

            AssertGuideContract(reuseGuide, "Docs/09-Reusable-Project-Catalog-ZH.md",
                "# 废土移动城市可复用项目目录", "## 适合谁看", new[]
                {
                    "适合谁看", "五级复用说明", "内容与稳定编号", "世界、城市和坐标", "建造与撤离", "UI 与输入",
                    "资源、研究、人口、战斗和存档", "3D 表现与美术", "场景、构建与检查工具", "冻结或禁止用于新功能的旧内容",
                }, "06-User-Feedback-and-Change-Control-ZH.md", "Generated/Project-Inventory-ZH.md");
        }

        [Test]
        public void ReuseGuide_RendersEveryCommittedCatalogEntryWithExactFields()
        {
            string content = ReadGuide("Docs/09-Reusable-Project-Catalog-ZH.md");
            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromFile(Path.Combine(ProjectRoot(),
                "Docs/Engineering/project-quality-catalog.json"));
            Assert.That(catalog.ReuseEntries, Has.Length.EqualTo(37));

            foreach (ProjectReuseEntry entry in catalog.ReuseEntries)
            {
                string body = ReuseEntryBody(content, entry);
                Assert.That(entry.TypeNames, Is.Not.Empty, entry.Id + " needs catalog code names");
                Assert.That(string.IsNullOrWhiteSpace(entry.UseSummary), Is.False, entry.Id + " needs catalog reuse guidance");
                StringAssert.Contains("能解决什么：", body);
                StringAssert.Contains("在哪里：", body);
                StringAssert.Contains("怎么复用：", body);
                StringAssert.Contains("不能负责什么：", body);
                StringAssert.Contains("改后跑哪组测试：", body);
                foreach (string assetPath in entry.AssetPaths) StringAssert.Contains(assetPath, body);
                foreach (string testPath in entry.RequiredTestFiles)
                    StringAssert.Contains(Path.GetFileNameWithoutExtension(testPath), body);
                foreach (string typeName in entry.TypeNames) StringAssert.Contains(typeName, body);
                StringAssert.Contains("怎么复用：" + entry.UseSummary, body);
                StringAssert.Contains(entry.BoundarySummary, body);
            }
        }

        private string Write(string name, string content)
        {
            string path = Path.Combine(fixtureDirectory, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        private static string ReadGuide(string relativePath)
        {
            string path = Path.Combine(ProjectRoot(), relativePath);
            Assert.That(File.Exists(path), Is.True, relativePath + " must exist");
            return File.ReadAllText(path);
        }

        private static void AssertGuideContract(string content, string relativePath, string title, string firstHeading,
            string[] orderedSections, params string[] requiredLinks)
        {
            StringAssert.StartsWith(title + "\n", content);
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int titleLine = Array.IndexOf(lines, title);
            int firstContentLine = titleLine + 1;
            while (firstContentLine < lines.Length && string.IsNullOrWhiteSpace(lines[firstContentLine])) firstContentLine++;
            Assert.That(firstContentLine, Is.LessThan(lines.Length));
            Assert.That(lines[firstContentLine], Is.EqualTo(firstHeading));
            int firstParagraphLine = firstContentLine + 1;
            while (firstParagraphLine < lines.Length && string.IsNullOrWhiteSpace(lines[firstParagraphLine])) firstParagraphLine++;
            Assert.That(firstParagraphLine, Is.LessThan(lines.Length));
            Assert.That(lines[firstParagraphLine].StartsWith("#", StringComparison.Ordinal), Is.False,
                relativePath + " must explain who it is for immediately after its title");

            int previous = -1;
            foreach (string section in orderedSections)
            {
                int index = content.IndexOf("## " + section + "\n", StringComparison.Ordinal);
                Assert.That(index, Is.GreaterThan(previous), relativePath + " is missing or reorders section " + section);
                previous = index;
            }
            foreach (string link in requiredLinks) AssertMarkdownLinkExists(content, relativePath, link);
            AssertAllMarkdownLinksResolve(content, relativePath);
            AssertPlainTermsAndCautiousLanguage(content);
        }

        private static void AssertPlainTermsAndCautiousLanguage(string content)
        {
            AssertFirstUseExplained(content, "EditMode", "不启动游戏");
            AssertFirstUseExplained(content, "PlayMode", "启动游戏");
            AssertFirstUseExplained(content, "组件", "挂在");
            AssertFirstUseExplained(content, "程序集", "一起编译");
            AssertFirstUseExplained(content, "稳定 ID", "不随改名");
            Assert.That(Regex.IsMatch(content, @"\b\d+/\d+\b"), Is.False);
            StringAssert.DoesNotContain("一定是", content);
            StringAssert.DoesNotContain("根因就是", content);
        }

        private static void AssertFirstUseExplained(string content, string term, string explanation)
        {
            int firstUse = content.IndexOf(term, StringComparison.Ordinal);
            Assert.That(firstUse, Is.GreaterThanOrEqualTo(0), term + " must be introduced in every human guide");
            int nearbyEnd = Math.Min(content.Length, firstUse + term.Length + 80);
            StringAssert.Contains(explanation, content.Substring(firstUse, nearbyEnd - firstUse),
                term + " needs a nearby plain-Chinese explanation on first use");
        }

        private static void AssertMarkdownLinkExists(string content, string sourcePath, string expectedTarget)
        {
            Match match = MarkdownLinks(content).FirstOrDefault(value => value.Groups["target"].Value == expectedTarget);
            Assert.That(match, Is.Not.Null, sourcePath + " must use a real Markdown link to " + expectedTarget);
        }

        private static void AssertAllMarkdownLinksResolve(string content, string sourcePath)
        {
            string root = ProjectRoot();
            string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            foreach (Match match in MarkdownLinks(content))
            {
                string target = match.Groups["target"].Value;
                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("#", StringComparison.Ordinal)) continue;
                string relativeTarget = target.Split('#')[0].Replace('/', Path.DirectorySeparatorChar);
                string resolved = Path.GetFullPath(Path.Combine(root, sourceDirectory, relativeTarget));
                Assert.That(resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True,
                    sourcePath + " link escapes the project: " + target);
                Assert.That(File.Exists(resolved), Is.True, sourcePath + " link target does not exist: " + target);
            }
        }

        private static MatchCollection MarkdownLinks(string content)
        {
            return Regex.Matches(content, @"(?<!!)\[[^\]]+\]\((?<target>[^\s)]+)\)");
        }

        private static string MarkdownSectionBody(string content, string heading, string nextHeading)
        {
            int start = content.IndexOf(heading + "\n", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "missing Markdown section " + heading);
            int bodyStart = start + heading.Length;
            int end = content.IndexOf(nextHeading, bodyStart, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(bodyStart), "missing next Markdown section " + nextHeading);
            return content.Substring(bodyStart, end - bodyStart);
        }

        private static void AssertCompletionGateStep(string content, int number, params string[] requiredTerms)
        {
            string heading = number + ". ";
            int start = content.IndexOf(heading, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "missing completion-gate step " + number);
            int next = content.IndexOf((number + 1) + ". ", start + heading.Length, StringComparison.Ordinal);
            if (number < 9) Assert.That(next, Is.GreaterThan(start), "completion-gate steps are reordered after " + number);
            string step = next < 0 ? content.Substring(start) : content.Substring(start, next - start);
            foreach (string term in requiredTerms) StringAssert.Contains(term, step, "step " + number + " is incomplete");
        }

        private static void AssertOrderedText(string content, params string[] values)
        {
            int previous = -1;
            foreach (string value in values)
            {
                int index = content.IndexOf(value, StringComparison.Ordinal);
                Assert.That(index, Is.GreaterThan(previous), "missing or reordered text: " + value);
                previous = index;
            }
        }

        private static string[] CollectScopedGitPaths(string projectRoot, string approvedPath)
        {
            return new[]
            {
                RunGit(projectRoot, "diff", "--name-only", "HEAD", "--", approvedPath),
                RunGit(projectRoot, "diff", "--cached", "--name-only", "--", approvedPath),
                RunGit(projectRoot, "diff", "--name-only", "--", approvedPath),
                RunGit(projectRoot, "ls-files", "--others", "--exclude-standard", "--", approvedPath),
            }.SelectMany(output => output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                .Select(path => path.Replace('\\', '/')).Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static string RunGit(string projectRoot, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Arguments = string.Join(" ", arguments.Select(GitArgument).ToArray()),
            };
            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.EqualTo(0), "git command failed: " + error);
                return output;
            }
        }

        private static string GitArgument(string value)
        {
            Assert.That(Regex.IsMatch(value ?? string.Empty, "^[A-Za-z0-9_./-]+$"), Is.True,
                "test git arguments must be fixed safe repository-relative tokens");
            return value;
        }

        private static string ReuseEntryBody(string content, ProjectReuseEntry entry)
        {
            string heading = "### " + entry.ChineseName + "（" + ReuseLevelName(entry.ReuseLevel) + "）";
            int start = content.IndexOf(heading + "\n", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "reuse guide is missing heading " + heading);
            int bodyStart = start + heading.Length;
            int next = content.IndexOf("\n### ", bodyStart, StringComparison.Ordinal);
            return next < 0 ? content.Substring(bodyStart) : content.Substring(bodyStart, next - bodyStart);
        }

        private static string ReuseLevelName(ProjectReuseLevel level)
        {
            switch (level)
            {
                case ProjectReuseLevel.Recommended: return "推荐复用";
                case ProjectReuseLevel.ReviewBeforeReuse: return "复用前审查";
                case ProjectReuseLevel.SceneOnly: return "仅限场景";
                case ProjectReuseLevel.FrozenRegression: return "冻结回归";
                case ProjectReuseLevel.ProhibitedForNewWork: return "禁止用于新功能";
                default: throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        private void ConfigureValidRecordEvidence(string editXml, string buildStatus = "NotRequired")
        {
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_SHA", "0123456789abcdef0123456789abcdef01234567");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_AT", "2026-08-12T12:00:00+08:00");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_EDITMODE_RESULTS", Write("edit.xml", editXml));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_PLAYMODE_RESULTS", Write("play.xml",
                StandardNUnitXml("Passed", 1, 1, 0, 0)));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_COMPILE_LOG", Write("compile.log", "Compilation succeeded\n"));
            string reason = buildStatus == "NotRequired" ? ",\"Reason\":\"runtime/build inputs unchanged\"" : string.Empty;
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_BUILD_SUMMARY", Write("builds.json",
                "{\"Builds\":[{\"Name\":\"Windows 3D\",\"Status\":\"" + buildStatus + "\",\"EvidenceLogPath\":\"" +
                EscapeJson(Write("build.log", "build evidence\n")) + "\"" + reason + "}]}"));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_HUMAN_PLAYTEST", "未进行");
        }

        private static void AssertRecordRejectedWithoutWrite()
        {
            string verification = Path.Combine(ProjectRoot(), ProjectDocumentationGenerator.VerificationPath);
            byte[] before = File.ReadAllBytes(verification);
            try
            {
                Assert.Throws<InvalidOperationException>(() => ProjectQualityTools.RecordVerification());
            }
            finally
            {
                File.WriteAllBytes(verification, before);
            }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(verification));
        }

        private static void RestoreExactFile(string path, bool existed, byte[] bytes)
        {
            if (existed) File.WriteAllBytes(path, bytes);
            else if (File.Exists(path)) File.Delete(path);
        }

        private static string StandardNUnitXml(string runResult, int total, int passed, int failed, int skipped)
        {
            var cases = new StringBuilder();
            int index = 0;
            AppendCases(cases, "Passed", passed, ref index);
            AppendCases(cases, "Failed", failed, ref index);
            AppendCases(cases, "Skipped", skipped, ref index);
            return "<test-run result=\"" + runResult + "\" total=\"" + total + "\" passed=\"" + passed +
                "\" failed=\"" + failed + "\" skipped=\"" + skipped + "\"><test-suite>" + cases + "</test-suite></test-run>";
        }

        private static void AppendCases(StringBuilder builder, string result, int count, ref int index)
        {
            for (int value = 0; value < count; value++)
            {
                index++;
                builder.Append("<test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Case")
                    .Append(index).Append("\" result=\"").Append(result).Append("\"");
                if (result == "Failed") builder.Append("><failure><message>failed</message></failure></test-case>");
                else builder.Append(" />");
            }
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static Dictionary<string, string> HashFiles(string root, bool includeGenerated)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string directory in new[] { "Assets", "Packages", "ProjectSettings", "Docs" })
            {
                string fullDirectory = Path.Combine(root, directory);
                if (!Directory.Exists(fullDirectory)) continue;
                foreach (string path in Directory.EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    if (!includeGenerated && relative.StartsWith("Docs/Generated/", StringComparison.Ordinal)) continue;
                    using (SHA256 hash = SHA256.Create())
                        values[relative] = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));
                }
            }
            return values;
        }

        private static Dictionary<string, string> HashRepositoryFiles(string root)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Concat(new[] { "Assets", "Packages", "ProjectSettings", "Docs" }.SelectMany(directory =>
                {
                    string fullDirectory = Path.Combine(root, directory);
                    return Directory.Exists(fullDirectory)
                        ? Directory.EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories)
                        : Enumerable.Empty<string>();
                })))
            {
                string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                using (SHA256 hash = SHA256.Create())
                    values[relative] = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));
            }
            return values;
        }

        private static string[] ChangedPaths(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            return before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal)
                .Where(path => !before.ContainsKey(path) || !after.ContainsKey(path) || before[path] != after[path])
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void RequireNativeSymlinkSupport()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Ignore("libc.symlink is not available on Windows");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Assert.Ignore("no supported native symbolic-link API is available on this platform");
        }

        private static int CreateNativeSymbolicLink(string target, string linkPath)
        {
            try
            {
                return CreateSymbolicLink(target, linkPath);
            }
            catch (DllNotFoundException)
            {
                Assert.Ignore("libc symbolic-link API is unavailable on this platform");
                return -1;
            }
            catch (EntryPointNotFoundException)
            {
                Assert.Ignore("libc symbolic-link entry point is unavailable on this platform");
                return -1;
            }
        }

        [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
        private static extern int CreateSymbolicLink(string target, string linkPath);
    }
}
