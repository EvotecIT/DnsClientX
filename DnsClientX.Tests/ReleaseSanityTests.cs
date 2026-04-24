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
    /// Tests release-facing metadata and documentation drift.
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
        /// Ensures README and CLI help cover key operator switches together.
        /// </summary>
        [Fact]
        public async Task CliHelpAndReadme_ShouldDocumentKeyOperatorSwitches() {
            string root = FindRepositoryRoot();
            string readme = File.ReadAllText(CombineRelative(root, "README.md"));
            string help = await GetCliHelpAsync();

            foreach (string cliSwitch in CliOperatorSwitches) {
                Assert.Contains(cliSwitch, help, StringComparison.Ordinal);
                Assert.Contains(cliSwitch, readme, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Ensures exported binary cmdlets stay listed in the PowerShell module manifest.
        /// </summary>
        [Fact]
        public void PowerShellManifest_ShouldExportBinaryCmdlets() {
            string root = FindRepositoryRoot();
            string manifest = File.ReadAllText(CombineRelative(root, "Module", "DnsClientX.psd1"));
            string[] sourceFiles = Directory.GetFiles(CombineRelative(root, "DnsClientX.PowerShell"), "*.cs");

            foreach (string sourceFile in sourceFiles) {
                string source = File.ReadAllText(sourceFile);
                Match match = Regex.Match(source, @"\[Cmdlet\([^,]+,\s*""(?<noun>[^""]+)""", RegexOptions.CultureInvariant);
                if (!match.Success) {
                    continue;
                }

                string verb = ReadCmdletVerb(source);
                string noun = match.Groups["noun"].Value;
                Assert.Contains($"{verb}-{noun}", manifest, StringComparison.Ordinal);
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

                current = Path.Combine(current, segment);
            }

            return current;
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

        private static string ReadCmdletVerb(string source) {
            if (source.Contains("VerbsCommon.Get", StringComparison.Ordinal)) return "Get";
            if (source.Contains("VerbsCommon.Clear", StringComparison.Ordinal)) return "Clear";
            if (source.Contains("VerbsCommon.Find", StringComparison.Ordinal)) return "Find";
            if (source.Contains("VerbsData.ConvertFrom", StringComparison.Ordinal)) return "ConvertFrom";
            if (source.Contains("VerbsDiagnostic.Resolve", StringComparison.Ordinal)) return "Resolve";
            if (source.Contains("VerbsDiagnostic.Test", StringComparison.Ordinal)) return "Test";
            if (source.Contains("VerbsLifecycle.Invoke", StringComparison.Ordinal)) return "Invoke";

            throw new InvalidOperationException("Unsupported cmdlet verb in PowerShell source.");
        }

        private static string FindRepositoryRoot() {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null) {
                if (File.Exists(Path.Combine(directory.FullName, "DnsClientX.sln"))) {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root containing DnsClientX.sln.");
        }
    }
}
