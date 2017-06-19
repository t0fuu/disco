﻿namespace Iris.Tests

open System
open System.IO
open System.Linq
open Expecto
open Iris.Core
open Iris.Raft
open LibGit2Sharp

[<AutoOpen>]
module ProjectTests =

  //   _                    _    ______
  //  | |    ___   __ _  __| |  / / ___|  __ ___   _____
  //  | |   / _ \ / _` |/ _` | / /\___ \ / _` \ \ / / _ \
  //  | |__| (_) | (_| | (_| |/ /  ___) | (_| |\ V /  __/
  //  |_____\___/ \__,_|\__,_/_/  |____/ \__,_| \_/ \___|ed
  //
  let loadSaveTest =
    testCase "Save/Load Project should render equal project values" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None

        let path = tmpPath()
        let name = Path.getFileName path |> unwrap

        let! project = Project.create (Project.ofFilePath path) name machine

        let result = Asset.loadWithMachine (Project.toFilePath project.Path) machine

        do! expectE "Projects should be equal" true ((=) project) result
      }
      |> noError

  //  ____  _
  // |  _ \(_)_ __ _   _
  // | | | | | '__| | | |
  // | |_| | | |  | |_| |
  // |____/|_|_|   \__, |
  //               |___/

  let dirtyTest =
    testCase "Project create should render clean repo" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None

        let path = tmpPath()
        let name = Path.getFileName path |> unwrap

        let! project = Project.create (Project.ofFilePath path) name machine
        let! repo = Project.repository project
        let! status = Git.Repo.status repo
        let untracked = status.Untracked.Count()

        expect "Projects should not be dirty" false id status.IsDirty
        expect "Projects should not have untracked files" 0 id untracked
      }
      |> noError

  //  ____       _   _
  // |  _ \ __ _| |_| |__
  // | |_) / _` | __| '_ \
  // |  __/ (_| | |_| | | |
  // |_|   \__,_|\__|_| |_|

  let relpathTest =
    testCase "Project create should only work on absolute paths" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None

        let path = Path.getRandomFileName()

        let result = Project.create (Project.ofFilePath path) (unwrap path) machine

        expect "Create should have failed" false Either.isSuccess result

        return!
          match result with
          | Left (GitError("Git.Repo.stage",_)) -> Right ()
          | Left other  -> Left other
          | Right other -> Left (Other("relpathTest", sprintf "Should have failed: %A" other))
      }
      |> noError

  //    ____          _                  _             _
  //   / ___|   _ ___| |_ ___  _ __ ___ (_)_______  __| |
  //  | |  | | | / __| __/ _ \| '_ ` _ \| |_  / _ \/ _` |
  //  | |__| |_| \__ \ || (_) | | | | | | |/ /  __/ (_| |
  //   \____\__,_|___/\__\___/|_| |_| |_|_/___\___|\__,_| load/saved
  //
  let testCustomizedCfg =
    testCase "Save/Load of Project with customized configs" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None

        let path = tmpPath()
        let fn = Path.getFileName path

        let engineCfg = RaftConfig.Default

        let vvvvCfg =
          { VvvvConfig.Default with
              Executables =
                [| { Executable = filepath "/pth/to/nowhere"
                  ; Version    = version "0.0.0.0.0.0.1"
                  ; Required   = true };
                  { Executable = filepath "/antoher/path"
                  ; Version    = version "1.2.34.4"
                  ; Required   = false } |]
            }

        let display1 =
          { Id        = Id.Create()
          ; Name      = name "Nice Display"
          ; Size      = Rect (1280,1080)
          ; Signals   =
              [| { Size    = Rect       (500,500)
                 ; Position = Coordinate (0,0) };
                 { Size     = Rect       (800,800)
                 ; Position = Coordinate (29, 13) } |]
          ; RegionMap =
            {
              SrcViewportId = Id.Create()
              Regions =
                [| { Id             = Id.Create()
                     Name           = name "A Cool Region"
                     SrcPosition    = Coordinate (0,0)
                     SrcSize        = Rect       (50,50)
                     OutputPosition = Coordinate (50,50)
                     OutputSize     = Rect       (100,100) };
                   { Id             = Id.Create()
                     Name           = name "Another Cool Region"
                     SrcPosition    = Coordinate (8,67)
                     SrcSize        = Rect       (588,5130)
                     OutputPosition = Coordinate (10,5300)
                     OutputSize     = Rect       (800,900) } |]
            }
          }

        let display2 =
          { Id        = Id.Create()
          ; Name      = name "Cool Display"
          ; Size      = Rect (180,12080)
          ; Signals   =
              [| { Size     = Rect (800,200)
                ; Position = Coordinate (3,8) };
                { Size     = Rect (1800,8800)
                ; Position = Coordinate (2900, 130) } |]
          ; RegionMap =
            { SrcViewportId = Id.Create();
              Regions =
                [| { Id             = Id.Create()
                  ; Name           = name "One Region"
                  ; SrcPosition    = Coordinate (0,8)
                  ; SrcSize        = Rect       (50,52)
                  ; OutputPosition = Coordinate (53,50)
                  ; OutputSize     = Rect       (103,800)
                  };
                  { Id             = Id.Create()
                  ; Name           = name "Premium Region"
                  ; SrcPosition    = Coordinate (8333,897)
                  ; SrcSize        = Rect       (83,510)
                  ; OutputPosition = Coordinate (1580,50)
                  ; OutputSize     = Rect       (1800,890)
                  } |]
            }
          }

        let viewPort1 =
          { Id             = Id.Create()
          ; Name           = name "One fine viewport"
          ; Position       = Coordinate (22,22)
          ; Size           = Rect       (666,666)
          ; OutputPosition = Coordinate (0,0)
          ; OutputSize     = Rect       (98327,121)
          ; Overlap        = Rect       (0,0)
          ; Description    = "Its better than bad, its good."
          }

        let viewPort2 =
          { Id             = Id.Create()
          ; Name           = name "Another fine viewport"
          ; Position       = Coordinate (82,2)
          ; Size           = Rect       (466,86)
          ; OutputPosition = Coordinate (12310,80)
          ; OutputSize     = Rect       (98,89121)
          ; Overlap        = Rect       (0,33)
          ; Description    = "Its awesome actually"
          }

        let task1 =
          { Id             = Id.Create()
          ; Description    = "A very important task, indeed."
          ; DisplayId      = Id.Create()
          ; AudioStream    = "hm"
          ; Arguments      = [| ("key", "to you heart") |]
          }

        let task2 =
          { Id             = Id.Create()
          ; Description    = "yay, its another task"
          ; DisplayId      = Id.Create()
          ; AudioStream    = "hoho"
          ; Arguments      = [| ("mykey", "to my heart") |]
          }

        let memA =
          { Member.create (Id.Create()) with
              HostName = name "moomoo"
              IpAddr   = IpAddress.Parse "182.123.18.2"
              State    = Running
              Port     = port 1234us }

        let memB =
          { Member.create (Id.Create()) with
              HostName = name "taataaa"
              IpAddr   = IpAddress.Parse "118.223.8.12"
              State    = Joining
              Port     = port 1234us }

        let groupA: HostGroup =
          { Name    = name "Group A"
          ; Members = [| Id.Create() |]
          }

        let groupB: HostGroup =
          { Name    = name "Group B"
          ; Members = [| Id.Create() |]
          }

        let cluster =
          { Id = Id.Create()
            Name   = name "A mighty cool cluster"
            Members = Map.ofArray [| (memA.Id,memA); (memB.Id,memB) |]
            Groups = [| groupA; groupB |] }

        let! project = Project.create (Project.ofFilePath path) (unwrap fn) machine

        let updated =
          Project.updateConfig
            { project.Config with
                Raft       = engineCfg
                Vvvv       = vvvvCfg
                ViewPorts  = [| viewPort1; viewPort2 |]
                Displays   = [| display1;  display2  |]
                Tasks      = [| task1;     task2     |]
                ActiveSite = Some cluster.Id
                Sites      = [| cluster |] }
            project

        let! commit = Asset.saveWithCommit path User.Admin.Signature updated
        let! loaded = Asset.loadWithMachine path machine

        // the only difference will be the automatically assigned timestamp
        expect "CreatedOn should be structurally equal"  true ((=) loaded.CreatedOn) updated.CreatedOn
        expect "VVVVConfig should be structurally equal" true ((=) loaded.Config.Vvvv) updated.Config.Vvvv
        expect "RaftCofnig should be structurally equal" true ((=) loaded.Config.Raft) updated.Config.Raft
        expect "ViewPorts should be structurally equal"  true ((=) loaded.Config.ViewPorts) updated.Config.ViewPorts
        expect "Timing should be structurally equal"     true ((=) loaded.Config.Timing) updated.Config.Timing
        expect "Displays should be structurally equal"   true ((=) loaded.Config.Displays) updated.Config.Displays
        expect "Tasks should be structurally equal"      true ((=) loaded.Config.Tasks) updated.Config.Tasks
        expect "Sites should be structurally equal"      true ((=) loaded.Config.Sites) updated.Config.Sites
      }
      |> noError

  // Adapted from http://stackoverflow.com/a/648055
  let rec deleteFileSystemInfo (fileSystemInfo: FileSystemInfo) =
    try
        match fileSystemInfo with
        | :? DirectoryInfo as dirInfo ->
            for childInfo in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories) do
                deleteFileSystemInfo childInfo
        | _ -> ()
        fileSystemInfo.Attributes <- FileAttributes.Normal
        fileSystemInfo.Delete()
    with _ -> ()


  //    ____ _ _
  //   / ___(_) |_
  //  | |  _| | __|
  //  | |_| | | |_
  //   \____|_|\__| initialzation
  //
  let saveInitsGit =
    testCase "Saved Project should be a git repository with yaml file." <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None
        let path = tmpPath()
        let name = Path.getFileName path |> unwrap

        let! _ = Project.create (Project.ofFilePath path) name machine

        let loaded = Asset.loadWithMachine path machine

        expect "Projects should be a folder"   true  Directory.exists path
        expect "Projects should be a git repo" true  Directory.exists (path </> filepath ".git")

        let projectFile = path </> filepath (PROJECT_FILENAME + ASSET_EXTENSION)

        expect "Projects should have project yml" true  File.exists projectFile

        let getRepo =
          Project.repository
          >> Either.isSuccess

        do! expectE "Projects should have repo" true getRepo loaded

        let checkDirty (project: IrisProject) =
          project
          |> Project.repository
          |> Either.bind Git.Repo.isDirty
          |> Either.get

        do! expectE "Projects should not be dirty" false checkDirty loaded

        let commitCount (project: IrisProject) =
          project
          |> Project.repository
          |> Either.map Git.Repo.commitCount
          |> Either.get

        do! expectE "Projects should have initial commit" 1  commitCount loaded
      }
      |> noError

  //    ____                          _ _
  //   / ___|___  _ __ ___  _ __ ___ (_) |_ ___
  //  | |   / _ \| '_ ` _ \| '_ ` _ \| | __/ __|
  //  | |__| (_) | | | | | | | | | | | | |_\__ \
  //   \____\___/|_| |_| |_|_| |_| |_|_|\__|___/ per save
  //
  let savesMultipleCommits =
    testCase "Saving project should contain multiple commits" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None

        let path = tmpPath()
        let name = Path.getFileName path |> unwrap

        let author1 = "karsten"

        let! project = Project.create (Project.ofFilePath path) name machine

        let updated = { project with Author = Some author1 }
        let! commit = Asset.saveWithCommit path User.Admin.Signature updated

        let! loaded = Asset.loadWithMachine path machine
        let! repo = Project.repository loaded

        let checkAuthor = (Option.get >> (=)) loaded.Author
        let checkCount = (=) (Git.Repo.commitCount repo)

        expect "Authors should be equal"                true checkAuthor author1
        expect "Project should have one initial commit" true checkCount 2

        let author2 = "ingolf"

        let updated = { updated with Author = Some author2 }
        let! commit2 = Asset.saveWithCommit path User.Admin.Signature updated

        let! loaded = Asset.loadWithMachine path machine

        expect "Authors should be equal"     true ((=) (Option.get loaded.Author)) author2
        expect "Projects should two commits" true ((=) (Git.Repo.commitCount repo)) 3

        let author3 = "eno"

        let updated = { updated with Author = Some author3 }
        let! commit3 = Asset.saveWithCommit path User.Admin.Signature updated

        let! loaded = Asset.loadWithMachine path machine

        expect "Authors should be equal"           true ((=) (Option.get loaded.Author)) author3
        expect "Projects should have four commits" true ((=) (Git.Repo.commitCount repo)) 4
      }
      |> noError

  let upToDatePath =
    testCase "Saving project should always contain an up-to-date path" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None
        let path = tmpPath()
        let name = Path.getFileName path |> unwrap

        let! project = Project.create (Project.ofFilePath path) name machine
        let! (loaded: IrisProject) = Asset.loadWithMachine path machine

        expect "Project should have correct path" path Project.toFilePath loaded.Path

        let newpath = tmpPath()

        FileSystem.moveFile path newpath

        let! (loaded: IrisProject) = Asset.loadWithMachine newpath machine

        expect "Project should have correct path" newpath Project.toFilePath loaded.Path
      }
      |> noError

  let saveAsset =
    testCase "Should save an asset in new commit" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None

        let path = tmpPath()
        let fn = Path.getFileName path |> unwrap

        let! project = Project.create (Project.ofFilePath path) fn machine

        let user =
          { Id = Id.Create()
            UserName = name "krgn"
            FirstName = name "karsten"
            LastName = name "gebbert"
            Email = email "k@lazy.af"
            Password = checksum "1234"
            Salt = checksum "56789"
            Joined = DateTime.Now
            Created = DateTime.Now }

        let! (commit, project) = Project.saveAsset user User.Admin project

        let! (loaded: User) =
          let userpath = Project.toFilePath project.Path </> Asset.path user
          File.readText(userpath)
          |> Yaml.decode

        expect "Should be the same" true ((=) user) loaded
      }
      |> noError

  let createDefaultUser =
    testCase "Should create a default admin user" <| fun _ ->
      either {
        let machine = MachineConfig.create "127.0.0.1" None
        let path = tmpPath()
        let name = Path.getFileName path |> unwrap

        let! project = Project.create (Project.ofFilePath path) name machine

        let! (admin: User) =
          Project.toFilePath project.Path </> Asset.path User.Admin
          |> File.readText
          |> Yaml.decode

        // Don't compare Joined and Created as they may differ a bit
        let isUserAdmin (admin: User) =
          User.Admin.Id               = admin.Id              &&
          User.Admin.UserName         = admin.UserName        &&
          User.Admin.FirstName        = admin.FirstName       &&
          User.Admin.LastName         = admin.LastName        &&
          User.Admin.Email            = admin.Email

        expect "Should have create the admin user" true isUserAdmin admin
      }
      |> noError

  // For tests async stuff:
  //
  // let testTests =
  //   testCase "making a case" <| (timeout 1000
  //     (fun _ ->
  //       Thread.Sleep(900)
  //       failtest "nop"))

  [<Tests>]
  let projectTests =
    testList "Load/Save tests" [
        dirtyTest
        relpathTest
        loadSaveTest
        testCustomizedCfg
        saveInitsGit
        savesMultipleCommits
        upToDatePath
        saveAsset
        createDefaultUser
      ] |> testSequenced
