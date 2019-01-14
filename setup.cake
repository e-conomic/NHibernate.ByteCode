///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var solution = "src/NHibernate.ByteCode.sln";
var AssemblyInfoPath = "src/NHibernate.ByteCode.Castle/AssemblyInfoVersion.cs";

var target = Argument ("target", "Default");
var configuration = Argument ("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task ("Clean")
    .Does (() => {
        CleanDirectories(
            $"./**/bin/"
        );
        CleanDirectories(
            $"./**/obj/"
        );
    });

GitVersion versionInfo = null;
Task ("Version")
    .Does(() => {
        versionInfo = GitVersion(new GitVersionSettings{
            UpdateAssemblyInfo = false,
            UpdateAssemblyInfoFilePath = AssemblyInfoPath,
            OutputType = GitVersionOutput.Json,
            NoFetch = true
        });

        CreateAssemblyInfo(AssemblyInfoPath, new AssemblyInfoSettings {
            InformationalVersion = versionInfo.InformationalVersion,
            FileVersion = versionInfo.AssemblySemFileVer,
            Version = versionInfo.AssemblySemVer
        });
        
        GitVersion(new GitVersionSettings{
            UpdateAssemblyInfo = true,
            UpdateAssemblyInfoFilePath = AssemblyInfoPath,
            OutputType = GitVersionOutput.Json,
            NoFetch = true
        });
    });

Task("Build")
    .IsDependentOn ("Version")
    .Does (() => {
        MSBuild (solution, new MSBuildSettings{
            Configuration = configuration,
            MaxCpuCount = 0,
            Restore = true
        });
    });

Task("Package")
    .IsDependentOn("Build")
    .Does(() => {
        if(versionInfo == null){
            versionInfo = GitVersion(new GitVersionSettings{
                UpdateAssemblyInfo = false,
                UpdateAssemblyInfoFilePath = AssemblyInfoPath,
                OutputType = GitVersionOutput.Json,
                NoFetch = true
            });
        }

        EnsureDirectoryExists("./artifacts");
        CleanDirectories("./artifacts");

        var directories = GetSubDirectories("./src/");

        foreach(var directoryPath in directories){
            FileInfo fi = new FileInfo(directoryPath.ToString());
            
            if (fi.Name.StartsWith("NHibernate.") && !fi.Name.EndsWith(".Tests")){
                Information($"Copying {fi.Name} info artifacts");
                CopyDirectory($"./src/{fi.Name}/bin/", "artifacts");
            }
        }

        var filesToBeDeleted = GetFiles("./artifacts/**/Iesi.Collections.dll");
        DeleteFiles(filesToBeDeleted);
        filesToBeDeleted = GetFiles("./artifacts/**/Castle.Core.dll");
        DeleteFiles(filesToBeDeleted);
        filesToBeDeleted = GetFiles("./artifacts/**/NHibernate.dll");
        DeleteFiles(filesToBeDeleted);

        var nugetPackSettings = new NuGetPackSettings {
            Id                      = "Economic.NHibernate.ByteCode",
            Version                 = versionInfo.NuGetVersionV2,
            Title                   = "Economic.NHibernate.ByteCode",
            Authors                 = new[] {"e-conomic"},
            Owners                  = new[] {"e-conomic"},
            Description             = "Collection of Economic Libraries for consuming micro-services, and other stuff",
            ProjectUrl              = new Uri("https://github.com/e-conomic/NHibernate.ByteCode"),
            Tags                    = new [] {"NHibernate.ByteCode"},
            RequireLicenseAcceptance= false,
            Symbols                 = true,
            NoPackageAnalysis       = true,
            Files                   = new [] {
                new NuSpecContent {Source = "**/*", Target = "lib"}
        },
            Dependencies            = new [] {
                new NuSpecDependency {Id = "Castle.Core", Version="4.3.1", TargetFramework="net461"},
                new NuSpecDependency {Id = "NHibernate", Version="3.3.3.4000", TargetFramework="net461"},
                new NuSpecDependency {Id = "Iesi.Collections", Version="3.3.1.4000", TargetFramework="net461"}
            },
            BasePath                = "./artifacts/",
            OutputDirectory         = "."
        };

        NuGetPack(nugetPackSettings);
    });

Task ("Default")
    .IsDependentOn("Package");

RunTarget (target);