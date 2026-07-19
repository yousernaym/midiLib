using System;
using System.IO;

namespace Midi.Tests
{
    static class TestFiles
    {
        /// <summary>
        /// Walks up from the test assembly directory to find the repo-root <c>test-files</c> folder.
        /// </summary>
        public static string FindRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "test-files");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "minimal.mid")))
                    return candidate;
            }
            throw new DirectoryNotFoundException(
                "Could not locate test-files/ (expected minimal.mid). Run tests from the Visual Music repo tree.");
        }

        public static string PathTo(string relative) => Path.Combine(FindRoot(), relative);
    }
}
