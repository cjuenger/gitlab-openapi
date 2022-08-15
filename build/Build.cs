using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.Npm;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Generate);

    [Parameter("NuGet API Key for io.juenger.GitLabClient Package", Name = "NUGET_API_KEY_GITLABCLIENT")]
    readonly string NuGetApiKey;
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [GitVersion] 
    readonly GitVersion GitVersion;
    
    [PathExecutable("openapi-generator-cli")]
    readonly Tool OpenApiGeneratorCli;

    static AbsolutePath OutputDirectory => RootDirectory / "generated";
    static AbsolutePath PackageOutputDirectory => RootDirectory / "packages";
    
    const string NuGetSource = "https://api.nuget.org/v3/index.json";
    
    Target Clean => _ => _
        .Before(Setup)
        .Executes(() =>
        {
            EnsureCleanDirectory(OutputDirectory);
            EnsureCleanDirectory(PackageOutputDirectory);
        });

    Target Setup => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            NpmTasks.Npm("install @openapitools/openapi-generator-cli -g");
        });

    Target Generate => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            // NpmTasks.Npm("exec @openapitools/openapi-generator-cli generate -g csharp-netcore " +
            //              "--additional-properties=targetFramework=net5.0 " +
            //              "--additional-properties=packageName=IO.Juenger.GitLabClient " +
            //              "-i ./openapi.yaml " +
            //              $"-o {OutputDirectory}");
            
            OpenApiGeneratorCli("generate -g csharp-netcore " +
                                "--additional-properties=targetFramework=net5.0 " +
                                "--additional-properties=packageName=IO.Juenger.GitLabClient " +
                                "-i ./openapi.yaml " +
                                $"-o {OutputDirectory}");
        });

     Target Pack => _ => _
        .DependsOn(Generate)
        .Executes(() =>
        {
            var solution = ProjectModelTasks.ParseSolution(OutputDirectory + "Io.Juenger.GitLabClient.sln");
            
            var packableProjects = solution
                .AllProjects
                .Where(project => project.GetProperty<bool>("IsPackable")) ?? Enumerable.Empty<Project>();

            foreach (var project in packableProjects)
            {
                Serilog.Log.Information("Packaging project '{ProjectName}'...", project.Name);
                
                DotNetPack(settings => settings
                    .SetProject(project)
                    .SetDescription(
                        "This library provides client to GitLab's web API.\n" +
                        "It is generated from an OpenAPI specification by using the tool openapi-generator-cli.")
                    .SetAuthors("Christian Jünger")
                    .SetProperty("PackageLicenseExpression", "MIT")
                    .SetRepositoryUrl("https://github.com/cjuenger/gitlab-openapi")
                    .SetOutputDirectory(PackageOutputDirectory)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .EnableNoRestore()
                    .SetVersion(GitVersion?.NuGetVersionV2)
                    .SetAssemblyVersion(GitVersion?.AssemblySemVer)
                    .SetFileVersion(GitVersion?.AssemblySemFileVer)
                    .SetInformationalVersion(GitVersion?.InformationalVersion));
            }
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey))
        .Executes(() =>
        {
            var packageFiles = PackageOutputDirectory.GlobFiles("*.nupkg");

            foreach (var packageFile in packageFiles)
            {
                Serilog.Log.Information("Pushing '{PackageName}'...", packageFile.Name);

                DotNetNuGetPush(settings => settings
                    .SetApiKey(NuGetApiKey)
                    .SetSymbolApiKey(NuGetApiKey)
                    .SetTargetPath(packageFile)
                    .SetSource(NuGetSource)
                    .SetSymbolSource(NuGetSource));
            }
        });
}
