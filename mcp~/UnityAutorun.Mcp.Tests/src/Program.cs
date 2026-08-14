using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using UnityAutorun.Mcp;

namespace UnityAutorun.Mcp.Tests
{
    internal static class Program
    {
        private static readonly List<(string Name, Action Test)> Tests =
            new List<(string Name, Action Test)>
            {
                ("incremental merge is idempotent and preserves fields", TestIncrementalMerge),
                ("full map save is creation-only", TestCreationOnlySave),
                ("candidate identity ignores source line movement", TestCandidateIdentityStability),
                ("finalization and no-op patches are byte-stable", TestFinalizationIdempotence)
            };

        private static int Main()
        {
            int failed = 0;
            foreach ((string name, Action test) in Tests)
            {
                try
                {
                    test();
                    Console.WriteLine("PASS " + name);
                }
                catch (Exception exception)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL " + name + ": " + exception);
                }
            }

            Console.WriteLine((Tests.Count - failed) + "/" + Tests.Count + " tests passed.");
            return failed == 0 ? 0 : 1;
        }

        private static void TestIncrementalMerge()
        {
            WithTemporaryDirectory(root =>
            {
                string mapPath = Path.Combine(root, "ui-nav-map.json");
                JsonObject initialPatch = JsonUtil.Obj(
                    ("views", new JsonArray
                    {
                        JsonUtil.Obj(("id", "view.a"), ("name", "UIFormA"))
                    }),
                    ("controls", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("id", "control.a.open"),
                            ("viewId", "view.a"),
                            ("name", "Open_ExButton"),
                            ("autoRun", JsonUtil.Obj(
                                ("buttonName", "Open_ExButton"),
                                ("buttonText", "untitled"),
                                ("delay", 0.5)))
                        )
                    })
                );
                JsonObject created = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", initialPatch)
                ));
                AssertTrue(Boolean(created, "writePerformed"), "Initial patch was not written.");

                string beforeNoOp = File.ReadAllText(mapPath);
                JsonObject noOpPatch = JsonUtil.Obj(
                    ("controls", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("autoRun", JsonUtil.Obj(
                                ("delay", 0.5),
                                ("buttonText", "untitled"),
                                ("buttonName", "Open_ExButton"))),
                            ("name", "Open_ExButton"),
                            ("id", "control.a.open"))
                    })
                );
                JsonObject noOp = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", noOpPatch)
                ));
                AssertFalse(Boolean(noOp, "writePerformed"), "Equivalent patch rewrote the map.");
                AssertEqual(beforeNoOp, File.ReadAllText(mapPath), "Equivalent patch changed file bytes.");

                JsonObject delayPatch = JsonUtil.Obj(
                    ("controls", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("id", "control.a.open"),
                            ("autoRun", JsonUtil.Obj(("delay", 1.25)))
                        )
                    })
                );
                JsonObject rejected = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", delayPatch)
                ));
                AssertFalse(Boolean(rejected, "ok"), "Conflicting update was accepted without authorization.");

                JsonObject updated = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", delayPatch.DeepClone()),
                    ("allowConflicts", true)
                ));
                AssertTrue(Boolean(updated, "writePerformed"), "Authorized update was not written.");
                JsonObject map = ParseObject(File.ReadAllText(mapPath));
                JsonObject control = map["controls"].AsArray()[0].AsObject();
                JsonObject autoRun = control["autoRun"].AsObject();
                AssertEqual("Open_ExButton", Text(autoRun, "buttonName"), "Nested buttonName was lost.");
                AssertEqual("untitled", Text(autoRun, "buttonText"), "Nested buttonText was lost.");
                AssertEqual(1.25, autoRun["delay"].GetValue<double>(), "Nested delay was not updated.");

                JsonObject removePatch = JsonUtil.Obj(
                    ("controls", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("id", "control.a.open"),
                            ("autoRun", new JsonObject { ["buttonText"] = null })
                        )
                    })
                );
                UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", removePatch),
                    ("allowConflicts", true)
                ));
                map = ParseObject(File.ReadAllText(mapPath));
                autoRun = map["controls"].AsArray()[0]["autoRun"].AsObject();
                AssertFalse(autoRun.ContainsKey("buttonText"), "Explicit null did not remove the nested field.");
                AssertEqual("Open_ExButton", Text(autoRun, "buttonName"), "Removing one field changed a sibling field.");
            });
        }

        private static void TestCreationOnlySave()
        {
            WithTemporaryToolRoot((toolRoot, mapPath) =>
            {
                JsonObject map = EmptyMap();
                JsonObject created = UiNavMapWriter.Save(JsonUtil.Obj(("map", map)));
                AssertTrue(Boolean(created, "ok"), "Initial full map creation failed.");
                string original = File.ReadAllText(mapPath);

                JsonObject replacement = EmptyMap();
                replacement["views"].AsArray().Add(JsonUtil.Obj(
                    ("id", "view.replacement"),
                    ("name", "Replacement")));
                JsonObject rejected = UiNavMapWriter.Save(JsonUtil.Obj(("map", replacement)));
                AssertFalse(Boolean(rejected, "ok"), "Existing map accepted a full replacement.");
                AssertEqual("nav_map_exists_use_patch", Text(rejected, "code"), "Unexpected replacement error code.");
                AssertEqual(original, File.ReadAllText(mapPath), "Rejected full replacement changed file bytes.");
            });
        }

        private static void TestCandidateIdentityStability()
        {
            WithTemporaryToolRoot((toolRoot, mapPath) =>
            {
                string assetsRoot = FindAssetsRoot(toolRoot);
                string scripts = Path.Combine(assetsRoot, "Scripts");
                Directory.CreateDirectory(scripts);
                string sourcePath = Path.Combine(scripts, "UIFormA.cs");
                File.WriteAllText(sourcePath, CandidateSource("UIFormB", 0));
                JsonObject first = TraceCandidate(mapPath, "UIFormA", "UIFormB");

                File.WriteAllText(sourcePath, CandidateSource("UIFormB", 8));
                JsonObject second = TraceCandidate(mapPath, "UIFormA", "UIFormB");
                AssertEqual(Text(first, "id"), Text(second, "id"), "Blank lines changed candidate id.");
                AssertEqual(Text(first, "candidateVersion"), Text(second, "candidateVersion"), "Blank lines changed candidate evidence version.");

                File.WriteAllText(sourcePath, CandidateSource("UIFormC", 8));
                JsonObject changed = TraceCandidate(mapPath, "UIFormA", "UIFormC");
                AssertNotEqual(Text(first, "id"), Text(changed, "id"), "A target-view change did not change candidate id.");
            });
        }

        private static void TestFinalizationIdempotence()
        {
            WithTemporaryToolRoot((toolRoot, mapPath) =>
            {
                string assetsRoot = FindAssetsRoot(toolRoot);
                string scripts = Path.Combine(assetsRoot, "Scripts");
                Directory.CreateDirectory(scripts);
                File.WriteAllText(
                    Path.Combine(scripts, "UIFormA.cs"),
                    CandidateSource("UIFormB", 0));
                JsonObject candidate = TraceCandidate(mapPath, "UIFormA", "UIFormB");
                JsonObject patch = JsonUtil.Obj(
                    ("views", new JsonArray
                    {
                        JsonUtil.Obj(("id", "view.a"), ("name", "UIFormA")),
                        JsonUtil.Obj(("id", "view.b"), ("name", "UIFormB"))
                    }),
                    ("transitions", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("id", "transition.a.open.to.b"),
                            ("fromViewId", "view.a"),
                            ("toViewId", "view.b"),
                            ("kind", "interaction"),
                            ("automation", JsonUtil.Obj(("mode", "click"))))
                    }),
                    ("candidateDecisions", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("id", Text(candidate, "id")),
                            ("candidateVersion", Text(candidate, "candidateVersion")),
                            ("outcome", "transition"),
                            ("targetId", "transition.a.open.to.b"))
                    })
                );
                JsonObject merged = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", patch)
                ));
                AssertTrue(Boolean(merged, "ok"), "Fixture map merge failed.");

                JsonObject finalized = Finalize(mapPath, "UIFormA", "UIFormB");
                AssertTrue(Boolean(finalized, "completionGatePassed"), "Initial finalization failed.");
                AssertTrue(Boolean(finalized, "writePerformed"), "Initial finalization did not write the map.");
                string finalizedBytes = File.ReadAllText(mapPath);

                JsonObject repeated = Finalize(mapPath, "UIFormA", "UIFormB");
                AssertTrue(Boolean(repeated, "completionGatePassed"), "Repeated finalization failed.");
                AssertFalse(Boolean(repeated, "writePerformed"), "Repeated finalization rewrote the map.");
                AssertEqual(finalizedBytes, File.ReadAllText(mapPath), "Repeated finalization changed file bytes.");

                JsonObject noOpPatch = JsonUtil.Obj(
                    ("transitions", new JsonArray
                    {
                        JsonUtil.Obj(
                            ("automation", JsonUtil.Obj(("mode", "click"))),
                            ("id", "transition.a.open.to.b"))
                    })
                );
                JsonObject noOp = UiNavMapPatchTools.MergePatch(JsonUtil.Obj(
                    ("mapPath", mapPath),
                    ("patch", noOpPatch)
                ));
                AssertFalse(Boolean(noOp, "writePerformed"), "No-op transition patch rewrote the map.");
                AssertEqual(finalizedBytes, File.ReadAllText(mapPath), "No-op transition patch changed file bytes.");
            });
        }

        private static JsonObject TraceCandidate(
            string mapPath,
            params string[] knownViews)
        {
            var names = new JsonArray();
            foreach (string view in knownViews)
            {
                names.Add(view);
            }

            JsonObject result = UiNavMapSourceScanner.TraceNavigationCalls(JsonUtil.Obj(
                ("mapPath", mapPath),
                ("query", ""),
                ("offset", 0),
                ("limit", 10),
                ("knownViewNames", names)
            ));
            JsonArray items = result["items"].AsArray();
            AssertEqual(1, items.Count, "Expected exactly one navigation candidate.");
            return items[0].AsObject();
        }

        private static JsonObject Finalize(string mapPath, params string[] knownViews)
        {
            var names = new JsonArray();
            foreach (string view in knownViews)
            {
                names.Add(view);
            }

            return UiNavMapSourceScanner.FinalizeGeneration(JsonUtil.Obj(
                ("mapPath", mapPath),
                ("limit", 20),
                ("knownViewNames", names)
            ));
        }

        private static string CandidateSource(string targetView, int leadingBlankLines)
        {
            return new string('\n', leadingBlankLines)
                + "public sealed class UIFormA\n"
                + "{\n"
                + "    private ExButton OpenExButton;\n"
                + "    public void Bind()\n"
                + "    {\n"
                + "        OpenExButton.Set(OnOpen);\n"
                + "    }\n"
                + "\n"
                + "    private void OnOpen()\n"
                + "    {\n"
                + "        OpenUIForm(" + targetView + ");\n"
                + "    }\n"
                + "}\n";
        }

        private static JsonObject EmptyMap()
        {
            return JsonUtil.Obj(
                ("views", new JsonArray()),
                ("controls", new JsonArray()),
                ("transitions", new JsonArray()),
                ("routes", new JsonArray()),
                ("unresolved", new JsonArray()),
                ("candidateDecisions", new JsonArray()));
        }

        private static void WithTemporaryToolRoot(Action<string, string> action)
        {
            WithTemporaryDirectory(root =>
            {
                string assetsRoot = Path.Combine(root, "Assets");
                string toolRoot = Path.Combine(
                    assetsRoot,
                    "Editor",
                    "Unity-Autorun-Tool");
                Directory.CreateDirectory(Path.Combine(toolRoot, "Gen"));
                Directory.CreateDirectory(Path.Combine(toolRoot, "mcp~"));
                string oldToolRoot = Environment.GetEnvironmentVariable(
                    "UNITY_AUTORUN_TOOL_ROOT");
                try
                {
                    Environment.SetEnvironmentVariable(
                        "UNITY_AUTORUN_TOOL_ROOT",
                        toolRoot);
                    action(toolRoot, Path.Combine(toolRoot, "Gen", "ui-nav-map.json"));
                }
                finally
                {
                    Environment.SetEnvironmentVariable(
                        "UNITY_AUTORUN_TOOL_ROOT",
                        oldToolRoot);
                }
            });
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "unity-autorun-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                action(root);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static string FindAssetsRoot(string toolRoot)
        {
            DirectoryInfo directory = new DirectoryInfo(toolRoot);
            while (directory != null
                && !string.Equals(directory.Name, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Temporary Assets root was not found.");
            }

            return directory.FullName;
        }

        private static JsonObject ParseObject(string json)
        {
            return JsonNode.Parse(json).AsObject();
        }

        private static bool Boolean(JsonObject value, string key)
        {
            return value?[key]?.GetValue<bool>() == true;
        }

        private static string Text(JsonObject value, string key)
        {
            return value?[key]?.GetValue<string>();
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            AssertTrue(!condition, message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + " Expected=" + expected + ", actual=" + actual + ".");
            }
        }

        private static void AssertNotEqual<T>(T left, T right, string message)
        {
            if (EqualityComparer<T>.Default.Equals(left, right))
            {
                throw new InvalidOperationException(message + " Value=" + left + ".");
            }
        }
    }
}
