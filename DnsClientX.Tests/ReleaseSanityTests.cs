using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests release-facing metadata and executable command help.
    /// </summary>
    [Collection("NoParallel")]
    public class ReleaseSanityTests {
        private static readonly string[] CliOperatorSwitches = {
            "--format",
            "--short",
            "--txt-concat",
            "--question",
            "--answer",
            "--authority",
            "--additional",
            "--reverse",
            "--axfr",
            "--transfer-summary",
            "--resolver-file",
            "--resolver-url",
            "--resolver-validate",
            "--probe-save",
            "--benchmark-save",
            "--resolver-select",
            "--resolver-use",
            "--stamp-info"
        };

        /// <summary>
        /// Ensures release-facing project versions stay aligned.
        /// </summary>
        [Fact]
        public void ReleaseVersions_ShouldStayAligned() {
            string root = FindRepositoryRoot();
            string coreVersion = ReadProjectProperty(CombineRelative(root, "DnsClientX", "DnsClientX.csproj"), "VersionPrefix");

            Assert.Equal(coreVersion, ReadProjectProperty(CombineRelative(root, "DnsClientX.Cli", "DnsClientX.Cli.csproj"), "VersionPrefix"));
            Assert.Equal(coreVersion, ReadProjectProperty(CombineRelative(root, "DnsClientX.Cli", "DnsClientX.Cli.csproj"), "AssemblyVersion"));
            Assert.Equal(coreVersion, ReadProjectProperty(CombineRelative(root, "DnsClientX.Cli", "DnsClientX.Cli.csproj"), "FileVersion"));

            Assert.Equal(coreVersion, ReadProjectProperty(CombineRelative(root, "DnsClientX.PowerShell", "DnsClientX.PowerShell.csproj"), "VersionPrefix"));
            Assert.Equal(coreVersion, ReadProjectProperty(CombineRelative(root, "DnsClientX.PowerShell", "DnsClientX.PowerShell.csproj"), "AssemblyVersion"));
            Assert.Equal(coreVersion, ReadProjectProperty(CombineRelative(root, "DnsClientX.PowerShell", "DnsClientX.PowerShell.csproj"), "FileVersion"));

            Assert.Equal(coreVersion, ReadPowerShellManifestVersion(CombineRelative(root, "Module", "DnsClientX.psd1")));
        }

        /// <summary>
        /// Ensures executable CLI help lists the supported operator switches.
        /// </summary>
        [Fact]
        public async Task CliHelp_ShouldDocumentKeyOperatorSwitches() {
            string help = await GetCliHelpAsync();

            foreach (string cliSwitch in CliOperatorSwitches) {
                Assert.Contains(cliSwitch, help, StringComparison.Ordinal);
            }
        }

        private static async Task<string> GetCliHelpAsync() {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "--help" } })!;
                int exitCode = await task;
                Assert.Equal(0, exitCode);
                return output.ToString();
            } finally {
                Console.SetOut(originalOut);
            }
        }

        private static string CombineRelative(string basePath, params string[] segments) {
            string current = basePath;
            foreach (string segment in segments) {
                if (string.IsNullOrWhiteSpace(segment)) {
                    throw new ArgumentException("Path segment cannot be empty.", nameof(segments));
                }

                if (Path.IsPathRooted(segment)) {
                    throw new ArgumentException($"Path segment must be relative: {segment}", nameof(segments));
                }

                current = JoinPath(current, segment);
            }

            return current;
        }

        private static string JoinPath(string left, string right) {
            return left.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   left.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? left + right
                : left + Path.DirectorySeparatorChar + right;
        }

        private static string ReadProjectProperty(string path, string propertyName) {
            XDocument document = XDocument.Load(path);
            string? value = document.Root?
                .Elements("PropertyGroup")
                .Elements(propertyName)
                .Select(element => element.Value.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            Assert.False(string.IsNullOrWhiteSpace(value), $"Missing {propertyName} in {path}.");
            return value!;
        }

        private static string ReadPowerShellManifestVersion(string path) {
            string content = File.ReadAllText(path);
            Match match = Regex.Match(content, @"ModuleVersion\s*=\s*'(?<version>[^']+)'", RegexOptions.CultureInvariant);
            Assert.True(match.Success, $"Missing ModuleVersion in {path}.");
            return match.Groups["version"].Value;
        }

        private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") {
            // The compiler's source path locates the checkout when the test host runs from external artifacts.
            foreach (string start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(sourcePath)! }) {
                DirectoryInfo? directory = new DirectoryInfo(start);
                while (directory != null) {
                    if (File.Exists(CombineRelative(directory.FullName, "DnsClientX.sln"))) {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not find repository root containing DnsClientX.sln.");
        }
    }
}
