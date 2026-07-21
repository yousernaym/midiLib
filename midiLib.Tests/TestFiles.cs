using System;
using System.IO;

namespace Midi.Tests
{
    static class TestFiles
    {
        /// <summary>
        /// Resolves a fixture under the test assembly's copied <c>test-files/</c> tree
        /// (see Content items in the test csproj).
        /// </summary>
        public static string PathTo(string relative)
        {
            string candidate = Path.Combine(AppContext.BaseDirectory, "test-files", relative);
            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException(
                    "Could not locate test fixture '" + relative + "' under " +
                    Path.Combine(AppContext.BaseDirectory, "test-files") + ".");
            }
            return candidate;
        }
    }
}
