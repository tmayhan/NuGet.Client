// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetToolTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetToolTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        public void DotnetToolTests_NoPackageReferenceToolRestore_ThrowsError(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = workingDirectory;
                var rid = "win-x86";
                var packages = new List<PackageIdentity>();

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: workingDirectory, packages: packages);
                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1211", result.Item2);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        public void DotnetToolTests_RegularDependencyPackageWithDependenciesToolRestore_ThrowsError(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = "https://api.nuget.org/v3/index.json";
                var rid = "win-x86";
                var packages = new List<PackageIdentity>() { new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("10.0.3")) };

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1212", result.Item2);
                Assert.DoesNotContain("NU1211", result.Item2); // It's the correct dependency count!
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_BasicDotnetToolRestore_Succeeds(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var rid = "win-x64";
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{rid}/a.dll");
                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 0, result.AllOutput);
                // Verify the assets file
                var lockFile = LockFileUtilities.GetLockFile(Path.Combine(testDirectory, projectName, "project.assets.json"), NullLogger.Instance);
                Assert.NotNull(lockFile);
                Assert.Equal(2, lockFile.Targets.Count);
                var ridTargets = lockFile.Targets.Where(e => e.RuntimeIdentifier != null ? e.RuntimeIdentifier.Equals(rid, StringComparison.CurrentCultureIgnoreCase) : false);
                Assert.Equal(1, ridTargets.Count());
                var toolsAssemblies = ridTargets.First().Libraries.First().ToolsAssemblies;
                Assert.Equal(1, toolsAssemblies.Count);
                Assert.Equal($"tools/{tfm}/{rid}/a.dll", toolsAssemblies.First().Path);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_MismatchedRID_Fails(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var projectRID = "win-x64";
                var packageRID = "win-x86";

                var packageName = string.Join("ToolPackage-", tfm, packageRID);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion)};
                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{packageRID}/a.dll");
                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: projectRID,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1202", result.Item2);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_BasicDotnetToolRestore_WithJsonCompatibleAssets_Succeeds(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var rid = "win-x64";
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{rid}/a.dll");
                package.AddFile($"tools/{tfm}/{rid}/Settings.json");

                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 0, result.AllOutput);
                // Verify the assets file
                var lockFile = LockFileUtilities.GetLockFile(Path.Combine(testDirectory, projectName, "project.assets.json"), NullLogger.Instance);
                Assert.NotNull(lockFile);
                Assert.Equal(2, lockFile.Targets.Count);
                var ridTargets = lockFile.Targets.Where(e => e.RuntimeIdentifier != null ? e.RuntimeIdentifier.Equals(rid, StringComparison.CurrentCultureIgnoreCase) : false);
                Assert.Equal(1, ridTargets.Count());
                var toolsAssemblies = ridTargets.First().Libraries.First().ToolsAssemblies;
                Assert.Equal(2, toolsAssemblies.Count);
                Assert.True(toolsAssemblies.Contains($"tools/{tfm}/{rid}/a.dll"));
                Assert.True(toolsAssemblies.Contains($"tools/{tfm}/{rid}/Settings.json"));
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461", "any", "win-x64")]
        [InlineData("netcoreapp2.0", "any", "win-x64")]
        [InlineData("net461", "any", "win-x86")]
        [InlineData("netcoreapp2.0", "any", "win-x86")]
        public void DotnetToolTests_PackageWithRuntimeJson_RuntimeIdentifierAny_Succeeds(string tfm, string packageRID, string projectRID)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var packageName = string.Join("ToolPackage-", tfm, packageRID);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{packageRID}/a.dll");
                package.AddFile($"tools/{tfm}/{packageRID}/Settings.json");
                package.RuntimeJson = GetResource("Dotnet.Integration.Test.compiler.resources.runtime.json", GetType());
                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: projectRID,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 0, result.AllOutput);
                // Verify the assets file
                var lockFile = LockFileUtilities.GetLockFile(Path.Combine(testDirectory, projectName, "project.assets.json"), NullLogger.Instance);
                Assert.NotNull(lockFile);
                Assert.Equal(2, lockFile.Targets.Count);
                var ridTargets = lockFile.Targets.Where(e => e.RuntimeIdentifier != null ? e.RuntimeIdentifier.Equals(projectRID, StringComparison.CurrentCultureIgnoreCase) : false);
                Assert.Equal(1, ridTargets.Count());
                var toolsAssemblies = ridTargets.First().Libraries.First().ToolsAssemblies;
                Assert.Equal(2, toolsAssemblies.Count);
                Assert.True(toolsAssemblies.Contains($"tools/{tfm}/{packageRID}/a.dll"));
                Assert.True(toolsAssemblies.Contains($"tools/{tfm}/{packageRID}/Settings.json"));
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461", "any", "win-x64")]
        [InlineData("netcoreapp2.0", "any", "win-x64")]
        [InlineData("net461", "any", "win-x86")]
        [InlineData("netcoreapp2.0", "any", "win-x86")]
        public void DotnetToolTests_PackageWithoutRuntimeJson_RuntimeIdentifierAny_Fails(string tfm, string packageRID, string projectRID)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var packageName = string.Join("ToolPackage-", tfm, packageRID);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{packageRID}/a.dll");
                package.AddFile($"tools/{tfm}/{packageRID}/Settings.json");
                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: projectRID,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                var framework = NuGetFramework.Parse(tfm);
                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1202", result.AllOutput);
                Assert.Contains($"supports: {tfm} ({framework.DotNetFrameworkName}) / {packageRID}", result.AllOutput);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        public void DotnetToolTests_RegularDependencyAndToolPackageWithDependenciesToolRestore_ThrowsError(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var localSource = Path.Combine(testDirectory, "packageSource");
                var source = "https://api.nuget.org/v3/index.json" + ";" + localSource;
                var rid = "win-x64";
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{rid}/a.dll");
                package.AddFile($"tools/{tfm}/{rid}/Settings.json");

                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(localSource, package);

                var packages = new List<PackageIdentity>() {
                    new PackageIdentity(packageName, packageVersion),
                    new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("10.0.3"))
                };

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("Invalid project-package combination for Newtonsoft.Json 10.0.3", result.Item2);
                Assert.DoesNotContain("Invalid project-package combination for ToolPackage", result.Item2);
                Assert.Contains("NU1211", result.Item2); //count is wrong
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("netcoreapp1.0", "any", "win7-x64")]
        public void DotnetToolTests_ToolWithPlatformPackage_Succeeds(string tfm, string packageRid, string projectRid)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var localSource = Path.Combine(testDirectory, "packageSource");
                var packageName = string.Join("ToolPackage-", tfm, packageRid);
                var platformsPackageName = "PlatformsPackage";
                var packageVersion = NuGetVersion.Parse("1.0.0");

                var platformsPackage = new SimpleTestPackageContext(platformsPackageName, packageVersion.OriginalVersion);
                platformsPackage.Files.Clear();
                platformsPackage.AddFile($"lib/{tfm}/a.dll");
                platformsPackage.PackageType = PackageType.Dependency;
                platformsPackage.UseDefaultRuntimeAssemblies = true;
                platformsPackage.PackageTypes.Add(PackageType.Dependency);
                platformsPackage.RuntimeJson = GetResource("Dotnet.Integration.Test.compiler.resources.runtime.json", GetType());

                var toolPackage = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                toolPackage.Files.Clear();
                toolPackage.AddFile($"tools/{tfm}/{packageRid}/a.dll");
                toolPackage.AddFile($"tools/{tfm}/{packageRid}/Settings.json");
                toolPackage.PackageType = PackageType.DotnetTool;
                toolPackage.UseDefaultRuntimeAssemblies = false;
                toolPackage.PackageTypes.Add(PackageType.DotnetTool);
                toolPackage.Dependencies.Add(platformsPackage);

                SimpleTestPackageUtility.CreatePackages(localSource, toolPackage, platformsPackage);

                var packages = new List<PackageIdentity>() {
                    new PackageIdentity(packageName, packageVersion),
                };
                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: projectRid,
                    source: localSource, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 0, result.AllOutput);
                var lockFile = LockFileUtilities.GetLockFile(Path.Combine(testDirectory, projectName, "project.assets.json"), NullLogger.Instance);
                Assert.NotNull(lockFile);
                Assert.Equal(2, lockFile.Targets.Count);
                var ridTargets = lockFile.Targets.Where(e => e.RuntimeIdentifier != null ? e.RuntimeIdentifier.Equals(projectRid, StringComparison.CurrentCultureIgnoreCase) : false);
                Assert.Equal(1, ridTargets.Count());
                var toolsAssemblies = ridTargets.First().Libraries.First().ToolsAssemblies;
                Assert.Equal(2, toolsAssemblies.Count);
                Assert.Contains($"tools/{tfm}/{packageRid}", toolsAssemblies.First().Path);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_ToolPackageWithIncompatibleToolsAssets_Fails(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var rid = "win-x64";
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/a.dll");
                package.AddFile($"tools/runtimes/{rid}/ar.dll");
                package.AddFile($"lib/{tfm}/b.dll");
                package.AddFile($"lib/{tfm}/c.dll");
                package.AddFile($"lib/{tfm}/d.dll");
                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1202", result.AllOutput);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_ToolsPackageWithExtraPackageTypes_Fails(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var rid = "win-x64";
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{rid}/a.dll");
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                package.PackageTypes.Add(PackageType.Dependency);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1204", result.AllOutput);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_BasicDotnetToolRestoreWithNestedValues_Succeeds(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var rid = "win-x64";
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{rid}/a.dll");
                package.AddFile($"tools/{tfm}/{rid}/tool1/b.dll");
                package.PackageType = PackageType.DotnetTool;
                package.UseDefaultRuntimeAssemblies = false;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 0, result.AllOutput);
                // Verify the assets file
                var lockFile = LockFileUtilities.GetLockFile(Path.Combine(testDirectory, projectName, "project.assets.json"), NullLogger.Instance);
                Assert.NotNull(lockFile);
                Assert.Equal(2, lockFile.Targets.Count);
                var ridTargets = lockFile.Targets.Where(e => e.RuntimeIdentifier != null ? e.RuntimeIdentifier.Equals(rid, StringComparison.CurrentCultureIgnoreCase) : false);
                Assert.Equal(1, ridTargets.Count());
                var toolsAssemblies = ridTargets.First().Libraries.First().ToolsAssemblies;
                Assert.Equal(2, toolsAssemblies.Count);
                var toolsAssemblyPaths = toolsAssemblies.Select(e => e.Path);
                Assert.Contains($"tools/{tfm}/{rid}/a.dll", toolsAssemblyPaths);
                Assert.Contains($"tools/{tfm}/{rid}/tool1/b.dll", toolsAssemblyPaths);
            }
        }

        public static string GetResource(string name, Type type)
        {
            using (var reader = new StreamReader(type.GetTypeInfo().Assembly.GetManifestResourceStream(name)))
            {
                return reader.ReadToEnd();
            }
        }

    }
}
