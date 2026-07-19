using System;
using System.IO;

namespace Midi.Tests
{
    static class TestFiles
    {
        static readonly string[] RelativeRoots =
        {
            "test-files",
            Path.Combine("libRemuxer", "test-files"),
            Path.Combine("Dependencies", "midiLib", "test-files"),
            Path.Combine("Dependencies", "Media", "test-files"),
            Path.Combine("Dependencies", "MidMix", "test-files"),
            Path.Combine("Dependencies", "Remuxer", "libRemuxer", "test-files"),
        };

        /// <summary>
        /// Walks up from the test assembly directory and resolves a fixture relative path
        /// under a local or monorepo <c>test-files</c> tree (first hit wins).
        /// </summary>
        public static string PathTo(string relative)
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                foreach (var root in RelativeRoots)
                {
                    string candidate = Path.Combine(dir.FullName, root, relative);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            throw new DirectoryNotFoundException(
                "Could not locate test fixture '" + relative + "' under any test-files/ tree.");
        }
    }
}
