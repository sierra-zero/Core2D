///////////////////////////////////////////////////////////////////////////////
// ADDINS
///////////////////////////////////////////////////////////////////////////////

#addin "nuget:?package=Polly&version=4.2.0"

///////////////////////////////////////////////////////////////////////////////
// TOOLS
///////////////////////////////////////////////////////////////////////////////

#tool "nuget:?package=xunit.runner.console&version=2.1.0"

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Polly;

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var isRunningOnAppVeyor = BuildSystem.AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals("Core2D/Core2D", BuildSystem.AppVeyor.Environment.Repository.Name);
var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals("master", BuildSystem.AppVeyor.Environment.Repository.Branch);
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag 
               && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);
var isReleasable = StringComparer.OrdinalIgnoreCase.Equals("AnyCPU", platform) 
                   && StringComparer.OrdinalIgnoreCase.Equals("Release", configuration);
var isMyGetRelease = !isTagged && isReleasable;
var isNuGetRelease = isTagged && isReleasable;
var isAnyCPU = StringComparer.OrdinalIgnoreCase.Equals(platform, "AnyCPU");

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = ParseAssemblyInfo("./src/Core2D.Shared/SharedAssemblyInfo.cs").AssemblyVersion;

if (isRunningOnAppVeyor)
{
    if (isTagged)
    {
        // Use Tag Name as version
        version = BuildSystem.AppVeyor.Environment.Repository.Tag.Name;
    }
    else
    {
        // Use AssemblyVersion with AppVeyor Build as version
        version += "-build" + EnvironmentVariable("APPVEYOR_BUILD_NUMBER") + "-alpha";
    }
}

///////////////////////////////////////////////////////////////////////////////
// SOLUTIONS
///////////////////////////////////////////////////////////////////////////////

var msBuildSolution = "./Core2D.sln";
var xBuildSolution = "./Core2D.mono.sln";

///////////////////////////////////////////////////////////////////////////////
// DIRECTORIES
///////////////////////////////////////////////////////////////////////////////

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var testResultsDir = artifactsDir.Combine("test-results");
var nugetRoot = artifactsDir.Combine("nuget");
var chocolateyRoot = artifactsDir.Combine("chocolatey");
var zipRoot = artifactsDir.Combine("zip");

var dirSuffix = platform + "/" + configuration;
var dirSuffixSkia = (isAnyCPU ? "x86" : platform) + "/" + configuration;

Func<IFileSystemInfo, bool> ExcludeSkia = i => {
    return !(i.Path.FullPath.IndexOf("Skia", StringComparison.OrdinalIgnoreCase) >= 0);
};

Func<string, DirectoryPathCollection> GetSkiaDirectories = pattern => {
    return GetDirectories(pattern) - GetDirectories(pattern, ExcludeSkia);
};

var buildDirs = 
    GetDirectories("./src/**/bin/" + dirSuffix, ExcludeSkia) + 
    GetSkiaDirectories("./src/**/bin/" + dirSuffixSkia) +
    GetDirectories("./src/**/obj/" + dirSuffix, ExcludeSkia) + 
    GetSkiaDirectories("./src/**/obj/" + dirSuffixSkia) + 
    GetDirectories("./dependencies/**/bin/" + dirSuffix, ExcludeSkia) + 
    GetSkiaDirectories("./dependencies/**/bin/" + dirSuffixSkia) + 
    GetDirectories("./dependencies/**/obj/" + dirSuffix, ExcludeSkia) + 
    GetSkiaDirectories("./dependencies/**/obj/" + dirSuffixSkia) + 
    GetDirectories("./tests/**/bin/" + dirSuffix, ExcludeSkia) + 
    GetSkiaDirectories("./tests/**/bin/" + dirSuffixSkia) + 
    GetDirectories("./testssrc/**/obj/" + dirSuffix, ExcludeSkia) + 
    GetSkiaDirectories("./testssrc/**/obj/" + dirSuffixSkia);

///////////////////////////////////////////////////////////////////////////////
// ZIP
///////////////////////////////////////////////////////////////////////////////

var zipSuffix = platform + "-" + configuration + "-" + version + ".zip";
var zipSuffixSkia = (isAnyCPU ? "x86" : platform) + "-" + configuration + "-" + version + ".zip";

var zipSource_Cairo = (DirectoryPath)Directory("./src/Core2D.Avalonia.Cairo/bin/" + dirSuffix);
var zipTarget_Cairo = zipRoot.CombineWithFilePath("Core2D.Avalonia.Cairo." + zipSuffix);

var zipSource_Direct2D = (DirectoryPath)Directory("./src/Core2D.Avalonia.Direct2D/bin/" + dirSuffix);
var zipTarget_Direct2D = zipRoot.CombineWithFilePath("Core2D.Avalonia.Direct2D-" + zipSuffix);

var zipSource_Skia = (DirectoryPath)Directory("./src/Core2D.Avalonia.Skia/bin/" + dirSuffixSkia);
var zipTarget_Skia = zipRoot.CombineWithFilePath("Core2D.Avalonia.Skia-" + zipSuffixSkia);

var zipSource_Wpf = (DirectoryPath)Directory("./src/Core2D.Wpf/bin/" + dirSuffix);
var zipTarget_Wpf = zipRoot.CombineWithFilePath("Core2D.Wpf-" + zipSuffix);

///////////////////////////////////////////////////////////////////////////////
// NUGET NUSPECS
///////////////////////////////////////////////////////////////////////////////

var SystemCollectionsImmutableVersion = "1.2.0";
var NewtonsoftJsonVersion = "9.0.1";
var PortableXamlVersion = "0.14.0";
var CsvHelperVersion = "2.16.0.0";
var AvaloniaVersion = "0.4.1-build1860-alpha";
var AvaloniaXamlBehaviorsVersion = "0.4.1-build245-alpha";
var AvaloniaControlsPanAndZoomVersion = "0.4.1-build33-alpha";
var SkiaSharpVersion = "1.53.0";

var SetNuGetNuspecCommonProperties = new Action<NuGetPackSettings> ((nuspec) => {
    nuspec.Version = version;
    nuspec.Authors = new [] { "wieslaw.soltes" };
    nuspec.Owners = new [] { "wieslaw.soltes" };
    nuspec.LicenseUrl = new Uri("http://opensource.org/licenses/MIT");
    nuspec.ProjectUrl = new Uri("https://github.com/Core2D/Core2D/");
    nuspec.RequireLicenseAcceptance = false;
    nuspec.Symbols = false;
    nuspec.NoPackageAnalysis = true;
    nuspec.Description = "A multi-platform data driven 2D diagram editor.";
    nuspec.Copyright = "Copyright 2016";
    nuspec.Tags = new [] { "Core2D", "Diagram", "Editor", "2D", "Graphics", "Drawing", "Data", "Managed", "C#" };
});

var nuspecNuGetSettingsCore = new []
{
    ///////////////////////////////////////////////////////////////////////////////
    // src: Core2D
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Core2D.dll", Target = "lib/portable-windows8+net45" }
        },
        BasePath = Directory("./src/Core2D/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // src: Core2D.Avalonia
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Avalonia",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.Avalonia", Version = version },
            new NuSpecDependency() { Id = "Core2D.Serializer.Newtonsoft", Version = version },
            new NuSpecDependency() { Id = "Core2D.Serializer.Xaml", Version = version },
            new NuSpecDependency() { Id = "Core2D.TextFieldReader.CsvHelper", Version = version },
            new NuSpecDependency() { Id = "Core2D.TextFieldWriter.CsvHelper", Version = version },
            new NuSpecDependency() { Id = "Avalonia", Version = AvaloniaVersion },
            new NuSpecDependency() { Id = "Avalonia.Xaml.Behaviors", Version = AvaloniaXamlBehaviorsVersion },
            new NuSpecDependency() { Id = "Avalonia.Controls.PanAndZoom", Version = AvaloniaControlsPanAndZoomVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Core2D.Avalonia.dll", Target = "lib/portable-windows8+net45" }
        },
        BasePath = Directory("./src/Core2D.Avalonia/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Avalonia")
    }
};

var nuspecNuGetSettingsDependencies = new []
{
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileSystem.DotNetFx
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileSystem.DotNetFx",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "Core2D", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileSystem.DotNetFx.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileSystem.DotNetFx/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.FileSystem.DotNetFx")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileWriter.Dxf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileWriter.Dxf",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.Dxf", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileWriter.Dxf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileWriter.Dxf/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.FileWriter.Dxf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileWriter.Emf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileWriter.Emf",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.WinForms", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileWriter.Emf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileWriter.Emf/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.FileWriter.Emf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileWriter.Pdf_core
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileWriter.PdfCore",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.PdfSharpCore", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileWriter.Pdf-core.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileWriter.Pdf-core/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.FileWriter.PdfCore")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileWriter.Pdf_wpf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileWriter.PdfWpf",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.PdfSharpWpf", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileWriter.Pdf-wpf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileWriter.Pdf-wpf./bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.FileWriter.PdfWpf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileWriter.Vdx
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileWriter.Vdx",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.Vdx", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileWriter.Vdx.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileWriter.Vdx/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.FileWriter.Vdx")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Log.Trace
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Log.Trace",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Log.Trace.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Log.Trace/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Log.Trace")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.Avalonia
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.Avalonia",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Avalonia", Version = AvaloniaVersion },
            new NuSpecDependency() { Id = "Avalonia.Controls.PanAndZoom", Version = AvaloniaControlsPanAndZoomVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.Avalonia.dll", Target = "portable-windows8+net45" }
        },
        BasePath = Directory("./dependencies/Renderer.Avalonia/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.Avalonia")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.Dxf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.Dxf",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.NetDxf", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.Dxf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.Dxf/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.Dxf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.PdfSharp_core
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.PdfSharpCore",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.PdfSharpCore", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.PdfSharp-core.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.PdfSharp-core/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.PdfSharpCore")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.PdfSharp-wpf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.PdfSharpWpf",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.PdfSharpWpf", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.PdfSharp-wpf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.PdfSharp-wpf/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.PdfSharpWpf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.Vdx
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.Vdx",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.VisioAutomation.VDX", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.Vdx.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.Vdx/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.Vdx")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.WinForms
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.WinForms",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.WinForms.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.WinForms/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.WinForms")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.Wpf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.Wpf",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.Wpf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.Wpf/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.Wpf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Serializer.Newtonsoft
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Serializer.Newtonsoft",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Newtonsoft.Json", Version = NewtonsoftJsonVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Serializer.Newtonsoft.dll", Target = "lib/portable-windows8+net45" }
        },
        BasePath = Directory("./dependencies/Serializer.Newtonsoft/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Serializer.Newtonsoft")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Serializer.Xaml
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Serializer.Xaml",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Portable.Xaml", Version = PortableXamlVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Serializer.Xaml.dll", Target = "lib/portable-windows8+net45" }
        },
        BasePath = Directory("./dependencies/Serializer.Xaml/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.Serializer.Xaml")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: TextFieldReader.CsvHelper
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.TextFieldReader.CsvHelper",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "CsvHelper", Version = CsvHelperVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "TextFieldReader.CsvHelper.dll", Target = "lib/portable-windows8+net45" }
        },
        BasePath = Directory("./dependencies/TextFieldReader.CsvHelper/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.TextFieldReader.CsvHelper")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: TextFieldWriter.CsvHelper
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.TextFieldWriter.CsvHelper",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "CsvHelper", Version = CsvHelperVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "TextFieldWriter.CsvHelper.dll", Target = "lib/portable-windows8+net45" }
        },
        BasePath = Directory("./dependencies/TextFieldWriter.CsvHelper/bin/" + dirSuffix),
        OutputDirectory = nugetRoot.Combine("Core2D.TextFieldWriter.CsvHelper")
    }
};

var nuspecNuGetSettingsDependenciesModules = new []
{
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: PdfSharpCore
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.PdfSharpCore",
        Files = new []
        {
            new NuSpecContent { Source = "PdfSharp.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/PDFsharp/src/PdfSharp/bin/" + configuration),
        OutputDirectory = nugetRoot.Combine("Core2D.PdfSharpCore")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: PdfSharp-wpf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.PdfSharpWpf",
        Files = new []
        {
            new NuSpecContent { Source = "PdfSharp-wpf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/PDFsharp/src/PdfSharp-wpf/bin/" + configuration),
        OutputDirectory = nugetRoot.Combine("Core2D.PdfSharpWpf")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: VisioAutomation.VDX
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.VisioAutomation.VDX",
        Files = new []
        {
            new NuSpecContent { Source = "VisioAutomation.VDX.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/VisioAutomation.VDX/VisioAutomation.VDX/bin/" + configuration),
        OutputDirectory = nugetRoot.Combine("Core2D.VisioAutomation.VDX")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: netDxf
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.NetDxf",
        Files = new []
        {
            new NuSpecContent { Source = "netDxf.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/netDxf/netDxf/bin/" + configuration),
        OutputDirectory = nugetRoot.Combine("Core2D.NetDxf")
    }
};

var nuspecNuGetSettingsDependenciesSkia = new []
{
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: FileWriter.PdfSkiaSharp
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.FileWriter.PdfSkiaSharp",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "Core2D.Renderer.SkiaSharp", Version = version }
        },
        Files = new []
        {
            new NuSpecContent { Source = "FileWriter.PdfSkiaSharp.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/FileWriter.PdfSkiaSharp/bin/" + dirSuffixSkia),
        OutputDirectory = nugetRoot.Combine("Core2D.FileWriter.PdfSkiaSharp")
    },
    ///////////////////////////////////////////////////////////////////////////////
    // dependencies: Renderer.SkiaSharp
    ///////////////////////////////////////////////////////////////////////////////
    new NuGetPackSettings()
    {
        Id = "Core2D.Renderer.SkiaSharp",
        Dependencies = new []
        {
            new NuSpecDependency() { Id = "System.Collections.Immutable", Version = SystemCollectionsImmutableVersion },
            new NuSpecDependency() { Id = "Core2D", Version = version },
            new NuSpecDependency() { Id = "SkiaSharp", Version = SkiaSharpVersion }
        },
        Files = new []
        {
            new NuSpecContent { Source = "Renderer.SkiaSharp.dll", Target = "lib/net45" }
        },
        BasePath = Directory("./dependencies/Renderer.SkiaSharp/bin/" + dirSuffixSkia),
        OutputDirectory = nugetRoot.Combine("Core2D.Renderer.SkiaSharp")
    }
};

var nuspecNuGetSettings = new List<NuGetPackSettings>();

nuspecNuGetSettings.AddRange(nuspecNuGetSettingsCore);
nuspecNuGetSettings.AddRange(nuspecNuGetSettingsDependencies);
nuspecNuGetSettings.AddRange(nuspecNuGetSettingsDependenciesModules);
nuspecNuGetSettings.AddRange(nuspecNuGetSettingsDependenciesSkia);

nuspecNuGetSettings.ForEach((nuspec) => SetNuGetNuspecCommonProperties(nuspec));

var nugetPackages = nuspecNuGetSettings.Select(nuspec => {
    return nuspec.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Id, ".", nuspec.Version, ".nupkg"));
}).ToArray();

var nupkgNuGetDirs = nuspecNuGetSettings.Select(nuspec => nuspec.OutputDirectory);

///////////////////////////////////////////////////////////////////////////////
// CHOCOLATEY NUSPECS
///////////////////////////////////////////////////////////////////////////////

var SetChocolateyNuspecCommonProperties = new Action<ChocolateyPackSettings> ((nuspec) => {
    nuspec.Version = version;
    nuspec.Authors = new [] { "wieslaw.soltes" };
    nuspec.Owners = new [] { "wieslaw.soltes" };
    nuspec.LicenseUrl = new Uri("http://opensource.org/licenses/MIT");
    nuspec.ProjectUrl = new Uri("https://github.com/Core2D/Core2D/");
    nuspec.PackageSourceUrl = new Uri("https://github.com/Core2D/Core2D/");
    nuspec.ProjectSourceUrl = new Uri("https://github.com/Core2D/Core2D/");
    nuspec.BugTrackerUrl = new Uri("https://github.com/Core2D/Core2D/issues/");
    nuspec.DocsUrl = new Uri("http://core2d.github.io/");
    nuspec.RequireLicenseAcceptance = false;
    nuspec.Description = "A multi-platform data driven 2D diagram editor.";
    nuspec.Copyright = "Copyright 2016";
    nuspec.Tags = new [] { "Core2D", "Diagram", "Editor", "2D", "Graphics", "Drawing", "Data" };
});

Func<DirectoryPath, ChocolateyNuSpecContent[]> GetChocolateyNuSpecContent = path => {
    var files = GetFiles(path.FullPath + "/*.dll") + GetFiles(path.FullPath + "/*.exe");
    return files.Select(file => new ChocolateyNuSpecContent { Source = file.FullPath, Target = "bin" }).ToArray();
};

var nuspecChocolateySettings = new Dictionary<ChocolateyPackSettings, DirectoryPath>()
{
    ///////////////////////////////////////////////////////////////////////////////
    // src: Core2D.Avalonia.Cairo
    ///////////////////////////////////////////////////////////////////////////////
    {
        new ChocolateyPackSettings
        {
            Id = "Core2D.Avalonia.Cairo",
            Title = "Core2D (Avalonia/Cairo)",
            OutputDirectory = chocolateyRoot.Combine("Core2D.Avalonia.Cairo")
        },
        zipSource_Cairo
    },
    ///////////////////////////////////////////////////////////////////////////////
    // src: Core2D.Avalonia.Direct2D
    ///////////////////////////////////////////////////////////////////////////////
    {
        new ChocolateyPackSettings
        {
            Id = "Core2D.Avalonia.Direct2D",
            Title = "Core2D (Avalonia/Direct2D)",
            OutputDirectory = chocolateyRoot.Combine("Core2D.Avalonia.Direct2D")
        },
        zipSource_Direct2D
    },
    ///////////////////////////////////////////////////////////////////////////////
    // src: Core2D.Avalonia.Skia
    ///////////////////////////////////////////////////////////////////////////////
    {
        new ChocolateyPackSettings
        {
            Id = "Core2D.Avalonia.Skia",
            Title = "Core2D (Avalonia/Skia)",
            OutputDirectory = chocolateyRoot.Combine("Core2D.Avalonia.Skia")
        },
        zipSource_Skia
    },
    ///////////////////////////////////////////////////////////////////////////////
    // src: Core2D.Wpf
    ///////////////////////////////////////////////////////////////////////////////
    {
        new ChocolateyPackSettings
        {
            Id = "Core2D.Wpf",
            Title = "Core2D (WPF)",
            OutputDirectory = chocolateyRoot.Combine("Core2D.Wpf")
        },
        zipSource_Wpf
    }
}
.ToList();

nuspecChocolateySettings.ForEach((nuspec) => SetChocolateyNuspecCommonProperties(nuspec.Key));

var chocolateyPackages = nuspecChocolateySettings.Select(nuspec => {
    return nuspec.Key.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Key.Id, ".", nuspec.Key.Version, ".nupkg"));
}).ToArray();

var nupkgChocolateyDirs = nuspecChocolateySettings.Select(nuspec => nuspec.Key.OutputDirectory);

///////////////////////////////////////////////////////////////////////////////
// INFORMATION
///////////////////////////////////////////////////////////////////////////////

Information("Building version {0} of Core2D ({1}, {2}, {3}) using version {4} of Cake.", 
    version,
    platform,
    configuration,
    target,
    typeof(ICakeContext).Assembly.GetName().Version.ToString());

if (isRunningOnAppVeyor)
{
    Information("Repository Name: " + BuildSystem.AppVeyor.Environment.Repository.Name);
    Information("Repository Branch: " + BuildSystem.AppVeyor.Environment.Repository.Branch);
}

Information("Target: " + target);
Information("Platform: " + platform);
Information("Configuration: " + configuration);
Information("IsLocalBuild: " + isLocalBuild);
Information("IsRunningOnUnix: " + isRunningOnUnix);
Information("IsRunningOnWindows: " + isRunningOnWindows);
Information("IsRunningOnAppVeyor: " + isRunningOnAppVeyor);
Information("IsPullRequest: " + isPullRequest);
Information("IsMainRepo: " + isMainRepo);
Information("IsMasterBranch: " + isMasterBranch);
Information("IsTagged: " + isTagged);
Information("IsReleasable: " + isReleasable);
Information("IsMyGetRelease: " + isMyGetRelease);
Information("IsNuGetRelease: " + isNuGetRelease);

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories(buildDirs);
    CleanDirectory(artifactsDir);
    CleanDirectory(testResultsDir);
    CleanDirectory(zipRoot);
    CleanDirectory(nugetRoot);
    CleanDirectories(nupkgNuGetDirs);
    CleanDirectory(chocolateyRoot);
    CleanDirectories(nupkgChocolateyDirs);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    var maxRetryCount = 5;
    var toolTimeout = 1d;
    Policy
        .Handle<Exception>()
        .Retry(maxRetryCount, (exception, retryCount, context) => {
            if (retryCount == maxRetryCount)
            {
                throw exception;
            }
            else
            {
                Verbose("{0}", exception);
                toolTimeout+=0.5;
            }})
        .Execute(()=> {
            if(isRunningOnWindows)
            {
                NuGetRestore(msBuildSolution, new NuGetRestoreSettings {
                    ToolTimeout = TimeSpan.FromMinutes(toolTimeout)
                });
            }
            else
            {
                NuGetRestore(xBuildSolution, new NuGetRestoreSettings {
                    ToolTimeout = TimeSpan.FromMinutes(toolTimeout)
                });
            }
        });
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(isRunningOnWindows)
    {
        MSBuild(msBuildSolution, settings => {
        settings.SetConfiguration(configuration);
            settings.WithProperty("Platform", platform);
            settings.SetVerbosity(Verbosity.Minimal);
        });
    }
    else
    {
        XBuild(xBuildSolution, settings => {
            settings.SetConfiguration(configuration);
            settings.WithProperty("Platform", platform);
            settings.SetVerbosity(Verbosity.Minimal);
        });
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    string pattern = "./tests/**/bin/" + platform + "/" + configuration + "/*.UnitTests.dll";
    if (platform == "x86")
    {
        XUnit2(pattern, new XUnit2Settings { 
            ToolPath = "./tools/xunit.runner.console/tools/xunit.console.x86.exe",
            OutputDirectory = testResultsDir,
            XmlReportV1 = true,
            NoAppDomain = true
        });
    }
    else
    {
        XUnit2(pattern, new XUnit2Settings { 
            ToolPath = "./tools/xunit.runner.console/tools/xunit.console.exe",
            OutputDirectory = testResultsDir,
            XmlReportV1 = true,
            NoAppDomain = true
        });
    }
});

Task("Zip-Files")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    Zip(zipSource_Cairo, 
        zipTarget_Cairo, 
        GetFiles(zipSource_Cairo.FullPath + "/*.dll") + 
        GetFiles(zipSource_Cairo.FullPath + "/*.exe"));

    if (isRunningOnWindows)
    {
        Zip(zipSource_Direct2D, 
            zipTarget_Direct2D, 
            GetFiles(zipSource_Direct2D.FullPath + "/*.dll") + 
            GetFiles(zipSource_Direct2D.FullPath + "/*.exe"));

        Zip(zipSource_Skia, 
            zipTarget_Skia, 
            GetFiles(zipSource_Skia.FullPath + "/*.dll") + 
            GetFiles(zipSource_Skia.FullPath + "/*.exe"));

        Zip(zipSource_Wpf, 
            zipTarget_Wpf, 
            GetFiles(zipSource_Wpf.FullPath + "/*.dll") + 
            GetFiles(zipSource_Wpf.FullPath + "/*.exe"));
    }
});

Task("Create-NuGet-Packages")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    foreach(var nuspec in nuspecNuGetSettings)
    {
        NuGetPack(nuspec);
    }
});

Task("Create-Chocolatey-Packages")
    .IsDependentOn("Run-Unit-Tests")
    .WithCriteria(() => isRunningOnWindows)
    .Does(() =>
{
    foreach(var nuspec in nuspecChocolateySettings)
    {
        nuspec.Key.Files = GetChocolateyNuSpecContent(nuspec.Value);
        ChocolateyPack(nuspec.Key);
    }
});

Task("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Zip-Files")
    .IsDependentOn("Create-NuGet-Packages")
    .IsDependentOn("Create-Chocolatey-Packages")
    .WithCriteria(() => isRunningOnAppVeyor)
    .Does(() =>
{
    foreach(var zip in GetFiles(zipRoot + "/*"))
    {
        AppVeyor.UploadArtifact(zip);
    }

    foreach(var nupkg in nugetPackages)
    {
        AppVeyor.UploadArtifact(nupkg.FullPath);
    }

    foreach(var nupkg in chocolateyPackages)
    {
        AppVeyor.UploadArtifact(nupkg.FullPath);
    }
});

Task("Publish-MyGet")
    .IsDependentOn("Create-NuGet-Packages")
    .WithCriteria(() => !isLocalBuild)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)
    .WithCriteria(() => isMyGetRelease)
    .Does(() =>
{
    var apiKey = EnvironmentVariable("MYGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API key.");
    }

    var apiUrl = EnvironmentVariable("MYGET_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API url.");
    }

    foreach(var nupkg in nugetPackages)
    {
        NuGetPush(nupkg, new NuGetPushSettings {
            Source = apiUrl,
            ApiKey = apiKey
        });
    }
})
.OnError(exception =>
{
    Information("Publish-MyGet Task failed, but continuing with next Task...");
});

Task("Publish-NuGet")
    .IsDependentOn("Create-NuGet-Packages")
    .WithCriteria(() => !isLocalBuild)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)
    .WithCriteria(() => isNuGetRelease)
    .Does(() =>
{
    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve NuGet API key.");
    }

    var apiUrl = EnvironmentVariable("NUGET_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve NuGet API url.");
    }

    foreach(var nupkg in nugetPackages)
    {
        NuGetPush(nupkg, new NuGetPushSettings {
            ApiKey = apiKey,
            Source = apiUrl
        });
    }
})
.OnError(exception =>
{
    Information("Publish-NuGet Task failed, but continuing with next Task...");
});

Task("Publish-Chocolatey")
    .IsDependentOn("Create-Chocolatey-Packages")
    .WithCriteria(() => !isLocalBuild)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)
    .WithCriteria(() => isNuGetRelease)
    .Does(() =>
{
    var apiKey = EnvironmentVariable("CHOCOLATEY_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve Chocolatey API key.");
    }

    var apiUrl = EnvironmentVariable("CHOCOLATEY_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve Chocolatey API url.");
    }

    foreach(var nupkg in chocolateyPackages)
    {
        ChocolateyPush(nupkg, new ChocolateyPushSettings {
            ApiKey = apiKey,
            Source = apiUrl
        });
    }
})
.OnError(exception =>
{
    Information("Publish-Chocolatey Task failed, but continuing with next Task...");
});

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Package")
  .IsDependentOn("Zip-Files")
  .IsDependentOn("Create-NuGet-Packages")
  .IsDependentOn("Create-Chocolatey-Packages");

Task("Default")
  .IsDependentOn("Package");

Task("AppVeyor")
  .IsDependentOn("Upload-AppVeyor-Artifacts")
  .IsDependentOn("Publish-MyGet")
  .IsDependentOn("Publish-NuGet")
  .IsDependentOn("Publish-Chocolatey");

Task("Travis")
  .IsDependentOn("Run-Unit-Tests");

///////////////////////////////////////////////////////////////////////////////
// EXECUTE
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
