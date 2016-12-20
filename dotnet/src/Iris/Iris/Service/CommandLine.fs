namespace Iris.Service

// * CommandLine
module CommandLine =

  let private tag (str: string) = sprintf "CommandLine.%s" str

  // ** Imports
  open Argu
  open Iris.Core
  open Iris.Raft
  open Iris.Service.Persistence
  open Iris.Service.Iris
  open Iris.Service.Raft
  open FSharpx.Functional
  open System
  open System.IO
  open System.Linq
  open System.Text
  open System.Text.RegularExpressions

  // ** Command Line Argument Parser

  //     _
  //    / \   _ __ __ _ ___
  //   / _ \ | '__/ _` / __|
  //  / ___ \| | | (_| \__ \
  // /_/   \_\_|  \__, |___/
  //              |___/

  type SubCommand =
    | Help
    | Setup
    | Create
    | Start
    | Reset
    | Dump
    | Add_User
    | Add_Member

    static member Doc
      with get () =
        @"
 ____                                        _        _   _
|  _ \  ___   ___ _   _ _ __ ___   ___ _ __ | |_ __ _| |_(_) ___  _ __
| | | |/ _ \ / __| | | | '_ ` _ \ / _ \ '_ \| __/ _` | __| |/ _ \| '_ \
| |_| | (_) | (__| |_| | | | | | |  __/ | | | || (_| | |_| | (_) | | | |
|____/ \___/ \___|\__,_|_| |_| |_|\___|_| |_|\__\__,_|\__|_|\___/|_| |_|

----------------------------------------------------------------------
| create                                                             |
----------------------------------------------------------------------

  Create a new project in the directory specified by

  --dir=/path/to/parent/dir/project-name

  The target directory name is initially used as the project name.

  You must also specify all ports with their respective flags:

  --raft=<uint16> : Port of underlying Raft service
  --git=<uint16>  : Port of `git daemon` service
  --ws=<uint16>   : Port of WebSocket service
  --web=<uint16>  : Port of Http service

  Additionally, you also need to specify the address which all
  services should bind to, using

  --bind=192.168.2.x : Address to bind services to

  Beware that service discovery will not work on loopback interfaces!

----------------------------------------------------------------------
| setup                                                              |
----------------------------------------------------------------------

  Create a new Machine-level configuration file. This sets the current
  machine's global identifier and also specifies the workspace
  directory used by Iris to scan for projects.

  You can specify the parent directory to create configuration in by
  using the dir flag:

  --dir=/path/to/parent/dir : Base directory for the new config file

----------------------------------------------------------------------
| start                                                              |
----------------------------------------------------------------------

  Start the Iris daemon with the project specified. You must specify
  the project to start with using

  --dir=/path/to/myproject : Base directory containing `project.yml`

  Additionally, you can use the following two flags to enter
  interactive mode, and/or prevent the http server from being started.

  -i        : Enter interactive mode
  --no-http : Disable the Http server

----------------------------------------------------------------------
| add-user                                                           |
----------------------------------------------------------------------

  Add a new user to the project. Requires you to specify the project
  directory

  --dir=/path/to/project : Base path containing `project.yml`

----------------------------------------------------------------------
| add-member                                                         |
----------------------------------------------------------------------

  Add a new cluster member to the project. Requires you to specify the
  project directory

  --dir=/path/to/project : Base path containing `project.yml`

----------------------------------------------------------------------
| reset                                                              |
----------------------------------------------------------------------

  Reset a project. This is an internal command and might disappear in
  the future.

----------------------------------------------------------------------
| dump                                                               |
----------------------------------------------------------------------

  Dump the current state of the project. Requires you to specify the
  project directory

  --dir=/path/to/project : Base path containing `project.yml`

----------------------------------------------------------------------
| help                                                               |
----------------------------------------------------------------------

  Show this help message.
"

  type CLIArguments =
    | [<Mandatory;MainCommand;CliPosition(CliPosition.First)>] Cmd of SubCommand

    | [<AltCommandLine("-i")>]        Interactive

    | [<EqualsAssignment>] Http         of string
    | [<EqualsAssignment>] Bind         of string
    | [<EqualsAssignment>] Raft         of uint16
    | [<EqualsAssignment>] Web          of uint16
    | [<EqualsAssignment>] Git          of uint16
    | [<EqualsAssignment>] Ws           of uint16
    | [<EqualsAssignment>] Dir          of string

    interface IArgParserTemplate with
      member self.Usage =
        match self with
          | Interactive -> "Start daemon in interactive mode"
          | Http    _   -> "Base path of http server (if `--http=false` http server won't be started)"
          | Dir     _   -> "Project directory to place the config & database in"
          | Bind    _   -> "Specify a valid IP address."
          | Web     _   -> "Http server port."
          | Git     _   -> "Git server port."
          | Ws      _   -> "WebSocket port."
          | Raft    _   -> "Raft server port."
          | Cmd     _   -> "Either one of setup, create, start, reset, user or dump."

  let parser = ArgumentParser.Create<CLIArguments>()

  // ** validateOptions

  let validateOptions (opts: ParseResults<CLIArguments>) =
    let ensureDir result =
      if opts.Contains <@ Dir @> |> not then
        "Missing Startup-Dir"
        |> Error.asOther (tag "validateOptions")
        |> Error.exitWith
      result

    match opts.GetResult <@ Cmd @> with
    | Start | Reset | Dump | Add_User | Add_Member -> ensureDir ()
    | _ -> ()

    if opts.GetResult <@ Cmd @> = Create then
      let dir  = opts.Contains <@ Dir @>
      let bind = opts.Contains <@ Bind @>
      let web  = opts.Contains <@ Web @>
      let raft = opts.Contains <@ Raft @>
      let git  = opts.Contains <@ Git @>
      let ws   = opts.Contains <@ Ws @>

      if not (bind && web && raft && ws) then
        printfn "Error: when creating a new configuration you must specify the following options:"
        if not dir  then printfn "    --dir=<directory>"
        if not bind then printfn "    --bind=<binding address>"
        if not web  then printfn "    --web=<web interface port>"
        if not git  then printfn "    --git=<git server port>"
        if not raft then printfn "    --raft=<raft port>"
        if not ws   then printfn "    --ws=<ws port>"
        "CLI options parse error"
        |> Error.asOther (tag "validateOptions")
        |> Error.exitWith

  // ** Utilities

  let private withTrim (token: string) (str: string) =
    let trimmed = String.trim str
    if trimmed.StartsWith(token) then
      let substr = trimmed.Substring(token.Length)
      Some <| String.trim substr
    else None

  let private withEmpty (token: string) (str: string) =
    if String.trim str = token
    then Some ()
    else None

  let private handleError (error: IrisError) =
    printfn "Encountered error during ForceTimeout operation: %A" error

  let parseHostString (str: string) =
    let trimmed = str.Trim().Split(' ')
    match trimmed with
      | [| id; hostname; hostspec |] ->
        if hostspec.StartsWith("tcp://") then
          match hostspec.Substring(6).Split(':') with
            | [| addr; port |] -> Some (uint32 id, hostname, addr, int port)
            | _ -> None
        else None
      | _ -> None

  // ** tryAppendEntry

  let tryAppendEntry (ctx: IIrisServer) str =
    warn "CLI AppendEntry currently not supported"

  // ** timeoutRaft

  let tryForceElection (ctx: IIrisServer) =
    either {
      do! ctx.ForceElection()
    }
    |> Either.mapError handleError
    |> ignore

  // ** Command Parsers

  let (|Exit|_|)     str = withEmpty "exit" str
  let (|Quit|_|)     str = withEmpty "quit" str
  let (|Status|_|)   str = withEmpty "status" str
  let (|Periodic|_|) str = withEmpty "step" str
  let (|Timeout|_|)  str = withEmpty "timeout" str
  let (|Leave|_|)    str = withEmpty "leave" str

  let (|Append|_|)  str = withTrim "append" str
  let (|Join|_|)    str = withTrim "join" str
  let (|AddMember|_|) str = withTrim "addmem" str
  let (|RmMember|_|)  str = withTrim "rmmem" str

  let (|Interval|_|) (str: string) =
    let trimmed = str.Trim()
    match trimmed.Split(' ') with
    | [| "interval"; x |] ->
      try
        uint8 x |> Some
      with
        | _ -> None
    | _ -> None

  let (|LogLevel|_|) (str: string) =
    let parsed = str.Trim().Split(' ')
    match parsed with
      | [| "log"; "debug" |] -> Some "debug"
      | [| "log"; "info" |]  -> Some "info"
      | [| "log"; "warn" |]  -> Some "warn"
      | [| "log"; "err" |]   -> Some "err"
      | _                  -> None

  let (|JoinParams|_|) (str: string) =
    let pattern = "(?<ip>[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):(?<port>[0-9]{1,5})"
    let m = Regex.Match(str, pattern)
    if m.Success then
      match IpAddress.TryParse m.Groups.[1].Value, UInt16.TryParse m.Groups.[2].Value with
      | Right ip, (true, port) -> Some (ip, port)
      | _ -> None
    else None

  let (|AddMemberParams|_|) (str: string) =
    let pattern =
      [| "id:(?<id>.*)"
      ; "hn:(?<hn>.*)"
      ; "ip:(?<ip>[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})"
      ; "port:(?<port>[0-9]{1,5})"
      ; "web:(?<web>[0-9]{1,5})"
      ; "ws:(?<ws>[0-9]{1,5})"
      ; "git:(?<git>[0-9]{1,5})" |]
      |> String.join " "
    let m = Regex.Match(str, pattern)
    if m.Success then
      let id = Id m.Groups.[1].Value
      let hn =  m.Groups.[2].Value
      let ip = IpAddress.TryParse m.Groups.[3].Value
      let port = UInt16.TryParse m.Groups.[4].Value
      let web = UInt16.TryParse m.Groups.[5].Value
      let ws = UInt16.TryParse m.Groups.[6].Value
      let git = UInt16.TryParse m.Groups.[7].Value
      match ip, port, web, ws, git with
      | Right ip, (true,port), (true,web), (true,ws), (true,git) ->
        { Member.create id with
            HostName = hn
            IpAddr   = ip
            Port     = port
            WebPort  = web
            WsPort   = ws
            GitPort  = git }
        |> Some
      | _ -> None
    else None

  // ** trySetLogLevel

  let trySetLogLevel (context: IIrisServer) (str: string) =
    either {
      let! config = context.Config
      let updated = { config.Raft with LogLevel = LogLevel.Parse str }
      do! context.SetConfig (Config.updateEngine updated config)
    }
    |> Either.mapError handleError
    |> ignore

  // ** trySetInterval

  let trySetInterval (context: IIrisServer) i =
    either {
      let! config = context.Config
      let updated = { config.Raft with PeriodicInterval = i }
      do! context.SetConfig (Config.updateEngine updated config)
    }
    |> Either.mapError handleError
    |> ignore

  // ** tryJoinCluster

  let tryJoinCluster (context: IIrisServer) (hst: string) =
    match hst with
      | JoinParams (ip, port) ->
        either {
          do! context.JoinCluster ip port
        }
        |> Either.mapError handleError
        |> ignore
      | _ ->
        sprintf "parameters %A could not be parsed" hst
        |> Error.asOther (tag "tryJoinCluster")
        |> handleError

  // ** tryLeaveCluster

  let tryLeaveCluster (context: IIrisServer) =
    either {
      do! context.LeaveCluster()
    }
    |> Either.mapError handleError
    |> ignore

  // ** tryAddMember

  let tryAddMember (context: IIrisServer) (hst: string) =
    match hst with
      | AddMemberParams mem ->
        either {
          let! appended = context.AddMember mem
          printfn "Added mem: %A in entry %A" id (string appended.Id)
          return ()
        }
        |> Either.mapError handleError
        |> ignore
      | _ ->
        sprintf "parameters %A could not be parsed" hst
        |> Error.asOther (tag "tryAddMember")
        |> handleError

  // ** tryRmMember

  let tryRmMember (context: IIrisServer) (hst: string) =
    either {
      let! appended = context.RmMember (Id (String.trim hst))
      printfn "Removed mem: %A in entry %A" id (string appended.Id)
      return ()
    }
    |> Either.mapError handleError
    |> ignore

  // ** tryPeriodic

  let tryPeriodic (context: IIrisServer) =
    either {
      do! context.Periodic()
    }
    |> Either.mapError handleError
    |> ignore

  // ** tryGetStatus

  let tryGetStatus (context: IIrisServer) =
    either {
      let! status = context.Status
      printfn "IrisService Status: %A" status
      return ()
    }
    |> Either.mapError handleError
    |> ignore

  // ** consoleLoop

  //  _
  // | |    ___   ___  _ __
  // | |   / _ \ / _ \| '_ \
  // | |__| (_) | (_) | |_) |
  // |_____\___/ \___/| .__/ s
  //                  |_|

  let registerExitHandlers (context: IIrisServer) =
    Console.CancelKeyPress.Add (fun _ ->
      printfn "Disposing context..."
      dispose context
      exit 0)
    System.AppDomain.CurrentDomain.ProcessExit.Add (fun _ -> dispose context)
    System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ -> dispose context)

  let interactiveLoop (context: IIrisServer) : unit =
    printfn "Welcome to the Raft REPL. Type help to see all commands."
    let kont = ref true
    let rec proc kontinue =
      printf "λ "
      let input = Console.ReadLine()
      match input with
        | LogLevel opt -> trySetLogLevel   context opt
        | Interval   i -> trySetInterval   context i
        | Exit         -> dispose          context; kontinue := false
        | Periodic     -> tryPeriodic      context
        | Append ety   -> tryAppendEntry   context ety
        | Join hst     -> tryJoinCluster   context hst
        | Leave        -> tryLeaveCluster  context
        | AddMember hst  -> tryAddMember       context hst
        | RmMember hst   -> tryRmMember        context hst
        | Timeout      -> tryForceElection context
        | Status       -> tryGetStatus     context
        | _            -> printfn "unknown command"
      if !kontinue then
        proc kontinue
    proc kont

  let silentLoop () =
    let kont = ref true
    let rec proc kontinue =
      Console.ReadLine() |> ignore
      if !kontinue then
        proc kontinue
    proc kont

  // ** ensureMachineConfig

  let ensureMachineConfig () =
    if not (File.Exists MachineConfig.defaultPath) then
      MachineConfig.create ()
      |> MachineConfig.save None
      |> Error.orExit id

  // ** buildMember

  //  _   _           _
  // | \ | | ___   __| | ___
  // |  \| |/ _ \ / _` |/ _ \
  // | |\  | (_) | (_| |  __/
  // |_| \_|\___/ \__,_|\___|

  let buildMember (parsed: ParseResults<CLIArguments>) (id: Id) =
    { Member.create(id) with
        IpAddr  = parsed.GetResult <@ Bind @> |> IpAddress.Parse
        GitPort = parsed.GetResult <@ Git  @>
        WsPort  = parsed.GetResult <@ Ws   @>
        WebPort = parsed.GetResult <@ Web  @>
        Port    = parsed.GetResult <@ Raft @> }

  // ** startService

  //  ____  _             _
  // / ___|| |_ __ _ _ __| |_
  // \___ \| __/ _` | '__| __|
  //  ___) | || (_| | |  | |_
  // |____/ \__\__,_|_|   \__|

  let startService (web: string option) (interactive: bool) (projectdir: FilePath) : Either<IrisError, unit> =
    ensureMachineConfig ()

    let projFile = Path.GetFullPath(projectdir) </> PROJECT_FILENAME + ASSET_EXTENSION

    if File.Exists projFile |> not then
      sprintf "Project Not Found: %s" projectdir
      |> Error.asOther "startService"
      |> Either.fail
    else
      either {
        let! machine = MachineConfig.load None
        let! server = IrisService.create machine web
        use _ = Logger.subscribe Logger.stdout

        registerExitHandlers server

        do! server.Load projFile

        let result =
          if interactive then
            interactiveLoop server
          else
            silentLoop ()

        dispose server

        return result
      }

  // ** createProject

  //   ____                _
  //  / ___|_ __ ___  __ _| |_ ___
  // | |   | '__/ _ \/ _` | __/ _ \
  // | |___| | |  __/ (_| | ||  __/
  //  \____|_|  \___|\__,_|\__\___|

  /// ## buildProject
  ///
  /// Create a new IrisProject data structure with given parameters.
  ///
  /// ### Signature:
  /// - name: Name of the Project
  /// - path: destination path of the Project
  /// - raftDir: Raft data directory
  /// - mem: self Member (built from Member Id env var)
  ///
  /// Returns: IrisProject
  let buildProject (machine: IrisMachine)
                   (name: string)
                   (path: FilePath)
                   (raftDir: FilePath)
                   (mem: RaftMember) =
    either {
      let! project = Project.create path name machine

      let updated =
        project
        |> Project.updateDataDir raftDir
        |> Project.addMember mem

      let! _ = Asset.saveWithCommit path User.Admin.Signature updated

      printfn "project: %A" project.Name
      printfn "created in: %s" project.Path

      return updated
    }

  /// ## initializeRaft
  ///
  /// Given the user (usually the admin user) and Project value, initialize the Raft intermediate
  /// state in the data directory and commit the result to git.
  ///
  /// ### Signature:
  /// - user: User to commit as
  /// - project: IrisProject to initialize
  ///
  /// Returns: unit
  let initializeRaft (project: IrisProject) = either {
      let! raft = createRaft project.Config
      let! _ = saveRaft project.Config raft
      return ()
    }

  /// ## createProject
  ///
  /// Create a new project given the passed command line options.
  ///
  /// ### Signature:
  /// - parsed: ParseResult<CLIArguments>
  ///
  /// Returns: unit
  let createProject (parsed: ParseResults<CLIArguments>) = either {
      ensureMachineConfig ()

      let! machine = MachineConfig.load None

      let dir =
        let path = parsed.GetResult <@ Dir @>
        if Path.IsPathRooted path then
          path
        else
          Path.GetFullPath path

      let name = Path.GetFileName dir

      let raftDir = dir </> RAFT_DIRECTORY

      do! match Directory.Exists dir with
          | true  ->
            match Directory.EnumerateFileSystemEntries(dir).Count() = 0 with
            | true  -> Either.nothing
            | false ->
              printf "%A not empty. I clean first? y/n" dir
              let input = Console.ReadKey()
              match input.Key with
                | ConsoleKey.Y -> rmDir dir
                | _            -> OK |> Either.fail
          | false -> Either.nothing

      do! mkDir dir
      do! mkDir raftDir

      let mem = buildMember parsed machine.MachineId

      let! project = buildProject machine name dir raftDir mem

      do! initializeRaft project
    }

  // ** resetProject

  //  ____                _
  // |  _ \ ___  ___  ___| |_
  // | |_) / _ \/ __|/ _ \ __|
  // |  _ <  __/\__ \  __/ |_
  // |_| \_\___||___/\___|\__|

  /// ## resetProject
  ///
  /// Reset a Project at given path to initial state.
  ///
  /// ### Signature:
  /// - datadir: FilePath to Project directory
  ///
  /// Returns: unit
  let resetProject (datadir: FilePath) = either {
      let path = datadir </> PROJECT_FILENAME + ASSET_EXTENSION
      let raftDir = datadir </> RAFT_DIRECTORY

      let! machine = MachineConfig.load None
      let! project = Asset.loadWithMachine path machine

      do! match Directory.Exists raftDir with
          | true  -> rmDir raftDir
          | false -> Either.nothing

      do! mkDir raftDir

      let! raft = createRaft project.Config
      let! _ = saveRaft project.Config raft
      return ()
    }

  // ** dumpDataDir

  //  ____
  // |  _ \ _   _ _ __ ___  _ __
  // | | | | | | | '_ ` _ \| '_ \
  // | |_| | |_| | | | | | | |_) |
  // |____/ \__,_|_| |_| |_| .__/
  //                       |_|

  let dumpDataDir (datadir: FilePath) =
    implement "dumpDataDir"

  // ** setup

  let setup (location: FilePath option) =
    let create (path: FilePath) =
      try
        if File.Exists path then
          printfn "Machine configuration already present. Contents:"
          File.ReadAllText path
          |> String.indent 4
          |> printfn "%s"
          printf "Should I overwrite this? (y/n) "
          let key = Console.ReadKey()
          printfn "\nAnswer: %c" key.KeyChar
          match key.KeyChar with
          | 'y' | 'Y' ->
            MachineConfig.create ()
            |> MachineConfig.save (Some path)
            |> Error.orExit id
            printfn "Created Machine new config in %A" path
            |> Either.succeed
          | _ ->
            printfn "Not making any changes. Bye."
            |> Either.succeed
        else
          MachineConfig.create ()
          |> MachineConfig.save (Some path)
          |> Error.orExit id
          printfn "Created Machine new config in %A:" path
          |> Either.succeed
      with
        | exn ->
          exn.Message
          |> Error.asOther (tag "setup")
          |> Either.fail

    match location with
    | Some path ->
      path </> MACHINECONFIG_NAME + ASSET_EXTENSION
      |> create
    | None ->
      create MachineConfig.defaultPath

  // ** help

  [<Literal>]
  let private header = @"   *   .  *.  .
 *  ___ ____.*___ ____  .     * .
* .|_ _|  _ \|_ _/ ___|*    .
  * | || |_) || |\___ \  .*  *
    | ||  _ < | | ___) |     .
.* |___|_| \_\___|____/. Automation Framework Daemon © Nsynk GmbH, 2016
*        .*           .* .
 "

  let help () =
    parser.PrintUsage(header, "iris.exe", true)
    |> flip (printfn "%s\n%s") SubCommand.Doc
    |> Either.succeed

  // ** addUser

  let private readPass (field: string) =
    let mutable pass = ""
    while String.length pass = 0 do
      printf "%s: " field
      let mutable last = Unchecked.defaultof<ConsoleKeyInfo>
      while last.Key <> ConsoleKey.Enter do
        last <- Console.ReadKey(true)
        if last.Key <> ConsoleKey.Backspace && last.Key <> ConsoleKey.Enter then
          pass <- sprintf "%s%c" pass last.KeyChar
          Console.Write("*")
        else
          if last.Key = ConsoleKey.Backspace && String.length pass > 0 then
            pass <- String.subString 0 (String.length pass - 1) pass
            Console.Write("\b \b")
      printf "%s" Environment.NewLine
    pass

  let private readString (field: string) =
    let mutable str = ""
    while String.length str = 0 do
      printf "%s: " field
      str <-
        Console.ReadLine()
        |> String.trim
    str

  let private readPort (field: string) =
    let mutable port = 0us
    while port = 0us do
      let str = readString field
      match UInt16.TryParse(str) with
      | (true, parsed) -> port <- parsed
      | _ -> ()
    port

  let private readIP (field: string) =
    let mutable ip = None
    while Option.isNone ip do
      let str = readString field
      match IpAddress.TryParse str with
      | Right parsed -> ip <- Some parsed
      | Left error -> printfn "%A" error
    Option.get ip

  let private readID (field: string) =
    let mutable id = None
    while Option.isNone id do
      let str = readString field
      match Id.TryParse str with
      | Some parsed -> id <- Some parsed
      | None -> ()
    Option.get id

  let private readEmail (field: string) =
    let pattern = "^.*@.*\..*"
    let mutable email = ""
    while String.length email = 0 do
      let str = readString field
      let m = Regex.Match(str, pattern)
      if m.Success then
        email <- str
    email

  let addUser (datadir: FilePath) =
    either {
      let path = datadir </> PROJECT_FILENAME + ASSET_EXTENSION
      let! machine = MachineConfig.load None
      let! project = Asset.loadWithMachine path machine

      let username  = readString "UserName"
      let firstname = readString "First Name"
      let lastname  = readString "Last Name"
      let email     = readEmail  "Email"
      let password1 = readPass   "Enter Password"
      let password2 = readPass   "Re-Enter Password"

      if password1 = password2 then
        let hash, salt = Crypto.hash password1
        let user =
          { Id        = Id.Create()
            UserName  = username
            FirstName = firstname
            LastName  = lastname
            Email     = email
            Password  = hash
            Salt      = salt
            Joined    = DateTime.Now
            Created   = DateTime.Now }
        let! _ = Project.saveAsset user User.Admin project
        return ()
      else
        return!
          "Passwords do not match. Try again Sam."
          |> Error.asOther (tag "addUser")
          |> Either.fail
    }

  let addMember (datadir: FilePath) =
    either {
      let path = datadir </> PROJECT_FILENAME + ASSET_EXTENSION
      let! machine = MachineConfig.load None
      let! project = Asset.loadWithMachine path machine

      let id   = readID     "Member ID"
      let hn   = readString "Host Name"
      let ip   = readIP     "IP Address"
      let raft = readPort   "Raft Port"
      let web  = readPort   "Web Port"
      let ws   = readPort   "Sockets Port"
      let git  = readPort   "Git Port"

      let mem =
        { Member.create id with
            HostName = hn
            IpAddr   = ip
            Port     = raft
            WebPort  = web
            WsPort   = ws
            GitPort  = git }

      let! _ =
        Asset.saveWithCommit
          datadir
          User.Admin.Signature
          (Project.addMember mem project)

      printfn "successfully added new member %A" mem.HostName
    }
