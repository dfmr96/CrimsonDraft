#nullable enable

using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    // Stamps the current git commit's short hash into Resources/BuildInfo.txt right before
    // every build, so a shared build always shows exactly which commit it came from --
    // read back at runtime by VersionLabelController. The file is regenerated per build and
    // gitignored; it simply won't exist until the first build is made on a machine.
    public sealed class BuildInfoGenerator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var hash = GetGitShortHash();

            var resourcesDir = Path.Combine(Application.dataPath, "Resources");
            Directory.CreateDirectory(resourcesDir);
            File.WriteAllText(Path.Combine(resourcesDir, "BuildInfo.txt"), hash);

            AssetDatabase.Refresh();
        }

        private static string GetGitShortHash()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = "rev-parse --short HEAD",
                    WorkingDirectory       = repoRoot,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                });

                var output = process!.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 && output.Length > 0 ? output : "unknown";
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"BuildInfoGenerator: couldn't read git hash ({e.Message}).");
                return "unknown";
            }
        }
    }
}
