#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

Target.initEnvironment ()

let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let wcatCliPath = Path.getFullName "./src/wcat"
let pywcatPath = Path.getFullName "./src/Python/pywcat"
let clientDeployPath = Path.combine clientPath "deploy"
let deployDir = Path.getFullName "./deploy"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

let nodeTool = platformTool "node" "node.exe"
let yarnTool = platformTool "yarn" "yarn.cmd"
let gitTool = platformTool "git" "git.exe"

let runTool cmd args workingDir =
    let arguments = args |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "opening browser failed"
    |> Proc.run
    |> ignore


Target.create "Clean" (fun _ ->
    [ deployDir
      clientDeployPath ]
    |> Shell.cleanDirs
)

Target.create "InstallClient" (fun _ ->
    printfn "Node version:"
    runTool nodeTool [ "--version" ] __SOURCE_DIRECTORY__
    printfn "Yarn version:"
    runTool yarnTool [ "--version" ] __SOURCE_DIRECTORY__
    runTool yarnTool [ "install"; "--frozen-lockfile" ] __SOURCE_DIRECTORY__
)

Target.create "ReplaceVersions" (fun _ ->
    Shell.regexReplaceInFileWithEncoding
        "let app = \".+\""
       ("let app = \"" + release.NugetVersion + "\"")
        System.Text.Encoding.UTF8
        (Path.combine clientPath "Version.fs")

    Shell.regexReplaceInFileWithEncoding
        "var version = \".+\""
       ("var version = \"" + release.NugetVersion + "\"")
        System.Text.Encoding.UTF8
        (Path.combine wcatCliPath "version.go")

    Shell.regexReplaceInFileWithEncoding
        "__version__ = \".+\""
       ("__version__ = \"" + release.NugetVersion + "\"")
        System.Text.Encoding.UTF8
        (Path.combine pywcatPath "__init__.py")
)

Target.create "Build" (fun _ ->
    runDotNet "build" serverPath
    runTool yarnTool [ "webpack-cli"; "-p" ] __SOURCE_DIRECTORY__
)

Target.create "Run" (fun _ ->
    let goTool = platformTool "go" "go.exe"

    let server = async {
        runDotNet "watch run" serverPath
    }
    let client = async {
        runTool yarnTool [ "webpack-dev-server" ] __SOURCE_DIRECTORY__
    }
    let browser = async {
        do! Async.Sleep 5000
        openBrowser "http://localhost:8084"
    }
    let gowatcher = async {
        //install package and watch for changes
        let goInstall _ =
            try
                runTool goTool [ "install" ] wcatCliPath
                Trace.tracefn "Successfully ran go install"
            with err ->
                Trace.traceErrorfn "Failed to run go install: %s" err.Message

        !! (wcatCliPath @@ "**/*.go")
        |> ChangeWatcher.run goInstall
        |> ignore

        goInstall ()
    }

    let vsCodeSession = Environment.hasEnvironVar "vsCodeSession"
    let safeClientOnly = Environment.hasEnvironVar "safeClientOnly"
    let skipWcatCLI = Environment.hasEnvironVar "skipWcatCLI"

    let tasks =
        [ if not safeClientOnly then yield server
          if not safeClientOnly && not skipWcatCLI then yield gowatcher
          yield client
          if not vsCodeSession then yield browser ]

    tasks
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)

let gitCommitRelease message =
    runTool gitTool [
        "add"
        "RELEASE_NOTES.md"
        "src/Client/Version.fs"
        "src/wcat/version.go"
        "src/Python/pywcat/__init__.py"
        ] __SOURCE_DIRECTORY__
    Trace.tracefn "Git added release files"
    runTool gitTool [ "commit"; "-m"; message ] __SOURCE_DIRECTORY__
    Trace.tracefn "Created git commit: %s" message

let gitTag tag =
    let args = [ "tag"; "-a"; tag; "-m"; tag ]
    runTool gitTool args __SOURCE_DIRECTORY__
    Trace.tracefn "Created git tag: %s" tag

let gitPush _ =
    use trace = Trace.traceTask "git push" "Push commit and tag with git"
    let args = [ "push"; "--follow-tags" ]
    runTool gitTool args __SOURCE_DIRECTORY__
    trace.MarkSuccess()

Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"
    let clientDir = Path.combine deployDir "Client"
    let publicDir = Path.combine clientDir "public"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath

    Shell.copyDir publicDir clientDeployPath FileFilter.allFiles
)

Target.create "Release" (fun _ ->
    let tag = sprintf "v%s" release.NugetVersion
    let commitMessage = sprintf "Release %s" release.NugetVersion

    gitCommitRelease commitMessage
    gitTag tag

    gitPush()
)


open Fake.Core.TargetOperators

"Clean"
    ==> "InstallClient"
    ==> "ReplaceVersions"
    ==> "Build"
    ==> "Bundle"


"ReplaceVersions"
    ==> "Release"

"Clean"
    ==> "InstallClient"
    ==> "Run"

Target.runOrDefaultWithArguments "Build"