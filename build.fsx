#r "packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open Fake.Git

open System
open System.Collections.Generic
open System.IO

let commitHash = Information.getCurrentHash()
let configuration = getBuildParamOrDefault "configuration" "Debug"
let projectJsonFiles = !! "src/*/project.json" -- "src/*Web*/project.json"

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin" ++ "src/*/obj" ++ "test/*/bin" ++ "test/*/obj" ++ "build" ++ "deploy"
        |> CleanDirs
)

Target "GenerateAssemblyInfo" (fun _ ->
    CreateCSharpAssemblyInfo "./src/AssemblyInfo.cs"
        [
            (Attribute.CLSCompliant(true))
            (Attribute.ComVisible(false))
            (Attribute.Metadata("githash", commitHash))]
)

Target "Build" (fun _ ->

    let restore f = DotNetCli.Restore (fun p ->
                { p with
                    AdditionalArgs = [f] })

    projectJsonFiles
        |> Seq.iter restore

    projectJsonFiles
        |> DotNetCli.Build
            (fun p ->
                { p with
                    Configuration = configuration })
)

Target "BuildSolutions" (fun _ ->

    let setParams defaults =
            { defaults with
                Verbosity = Some(Quiet)
                Targets = ["Build"]
                Properties =
                    [
                        "Optimize", "True"
                        "DebugSymbols", "True"
                        "Configuration", configuration
                    ]
            }
    build setParams "./Quartz.sln"
        |> DoNothing

    build setParams "./Quartz-DotNetCore.sln"
        |> DoNothing
)

Target "Pack" (fun _ ->
    !! "src/Quartz/project.json"
        |> DotNetCli.Pack
            (fun p ->
                { p with
                    Configuration = "Release"
                })

    !! "src/*/bin/**/*.nupkg"
        |> Copy "artifacts"
)

Target "Test" (fun _ ->
    !! "src/Quartz.Tests.Unit/project.json"
        |>  DotNetCli.Test
            (fun p ->
                    { p with
                        Configuration = configuration
                        AdditionalArgs = ["--where \"cat != database && cat != fragile\""] })
)

"Clean"
  ==> "GenerateAssemblyInfo"
  ==> "Build"
  =?> ("BuildSolutions", hasBuildParam "buildSolutions")
  ==> "Test"
  ==> "Pack"

RunTargetOrDefault "Test"
