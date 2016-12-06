namespace Iris.Service

// * Imports

open System
open System.IO
open Iris.Raft
open Iris.Core
open Iris.Core.Utils
open Iris.Service.Persistence
open Iris.Service.Git
open Iris.Service.WebSockets
open Iris.Service.Raft
open Microsoft.FSharp.Control
open FSharpx.Functional
open LibGit2Sharp

// * IrisService

//  ___      _     ____                  _
// |_ _|_ __(_)___/ ___|  ___ _ ____   _(_) ___ ___
//  | || '__| / __\___ \ / _ \ '__\ \ / / |/ __/ _ \
//  | || |  | \__ \___) |  __/ |   \ V /| | (_|  __/
// |___|_|  |_|___/____/ \___|_|    \_/ |_|\___\___|
//

module Iris =

  // ** tag

  [<Literal>]
  let private tag = "IrisServer"

  // ** keys
  [<Literal>]
  let private GIT_SERVER = "git"

  [<Literal>]
  let private LOG_HANDLER = "log"

  [<Literal>]
  let private RAFT_SERVER = "raft"

  [<Literal>]
  let private WS_SERVER = "ws"

  [<Literal>]
  let private WEB_SERVER = "web"

  let private signature =
    new Signature("Karsten Gebbert", "k@ioctl.it", new DateTimeOffset(DateTime.Now))

  // ** IrisEvent

  [<NoComparison;NoEquality>]
  type IrisEvent =
    | Git    of GitEvent
    | Socket of SocketEvent
    | Raft   of RaftEvent
    | Log    of LogEvent
    | Status of ServiceStatus

  // ** Subscriptions

  /// ## Subscriptions
  ///
  /// Type alias for IObserver subscriptions.
  ///
  type Subscriptions = ResizeArray<IObserver<IrisEvent>>

  // ** disposeAll

  /// ## disposeAll
  ///
  /// Dispose all resource in the passed `seq`.
  ///
  /// ### Signature:
  /// - disposables: IDisposable seq
  ///
  /// Returns: unit
  let private disposeAll (disposables: Map<string,IDisposable>) =
    Map.iter (konst dispose) disposables

  // ** IrisStateData

  /// ## IrisStateData
  ///
  /// Encapsulate all service-internal state to hydrate an `IrisAgent` with. As the actor receives
  /// messages, it uses (and updates) this record and passes it on. For ease of use it implements
  /// the IDisposable interface.
  ///
  /// ### Fields:
  /// - Status: ServiceStatus of currently loaded project
  /// - Store: Store containing all state. This is sent to user via WebSockets on connection.
  /// - Project: IrisProject currently loaded
  /// - GitServer: IGitServer for current project
  /// - RaftServer: IRaftServer for current project
  /// - SocketServer: IWebSocketServer for current project
  /// - Disposables: IDisposable list for Observables and the like
  ///
  [<NoComparison;NoEquality>]
  type private IrisStateData =
    { NodeId        : Id
      Status        : ServiceStatus
      Store         : Store
      Project       : IrisProject
      GitServer     : IGitServer
      RaftServer    : IRaftServer
      HttpServer    : AssetServer
      SocketServer  : IWebSocketServer
      Subscriptions : Subscriptions
      Disposables   : Map<string,IDisposable> }

    interface IDisposable with
      member self.Dispose() =
        disposeAll self.Disposables
        dispose self.GitServer
        dispose self.RaftServer
        dispose self.SocketServer
        dispose self.HttpServer

  // ** Reply

  /// ## Reply
  ///
  /// Type to model synchronous replies from the internal actor.
  ///
  [<RequireQualifiedAccess;NoComparison;NoEquality>]
  type private Reply =
    | Ok
    | State  of IrisStateData
    | Entry  of EntryResponse
    | Config of IrisConfig

  // ** ReplyChan

  /// ## ReplyChan
  ///
  /// Type alias over reply channel for computations on the internal actor that can fail.
  ///
  type private ReplyChan = AsyncReplyChannel<Either<IrisError,Reply>>

  // ** Msg

  /// ## Msg
  ///
  /// Model the actor-internal state machine. Some constructors include a `ReplyChan` for
  /// synchronous request/response style computations.
  ///
  [<RequireQualifiedAccess;NoComparison;NoEquality>]
  type private Msg =
    | Git       of GitEvent
    | Socket    of SocketEvent
    | Raft      of RaftEvent
    | Log       of LogEvent
    | Load      of ReplyChan * FilePath
    | SetConfig of ReplyChan * IrisConfig
    | AddNode   of ReplyChan * RaftNode
    | RmNode    of ReplyChan * Id
    | Join      of ReplyChan * IpAddress  * uint16
    | Leave     of ReplyChan
    | Config    of ReplyChan
    | Unload    of ReplyChan
    | State     of ReplyChan
    | ForceElection
    | Periodic

  // ** IrisAgent

  /// ## IrisAgent
  ///
  /// Type alias for internal state mutation actor.
  ///
  type private IrisAgent = MailboxProcessor<Msg>

  // ** IrisState

  /// ## IrisState
  ///
  /// Encodes the presence or absence of a loaded project. Implements IDisposable for
  /// convenience. This is the type our inner loop function is fed with.
  ///
  /// ### Constructors:
  /// - Idle: no IrisProject is currently loaded (implies ServiceStatus.Stopped)
  /// - Loaded: IrisStateData for loaded IrisProject
  ///
  [<NoComparison;NoEquality>]
  type private IrisState =
    | Idle
    | Loaded of IrisStateData

    interface IDisposable with
      member self.Dispose() =
        match self with
        | Idle -> ()
        | Loaded data -> dispose data

  /// ## withLoaded
  ///
  /// Reach into passed IrisState value and apply either one of the passed functions to the inner
  /// value.
  ///
  /// ### Signature:
  /// - state: IrisState value to reach into
  /// - idle: (unit -> IrisState) function evaluated when the Idle case was hit
  /// - arg: (IrisStateData -> IrisState) function evaluated when Loaded constructor was encoutered.
  ///
  /// Returns: IrisState
  let inline private withLoaded (state: IrisState)
                                (idle: unit -> 'a)
                                (loaded: IrisStateData -> 'a) =
    match state with
    | Idle        -> idle ()
    | Loaded data -> loaded data

  /// ## withState
  ///
  /// If the passed `IrisState` is a loaded project, execute the supplied function against it.
  ///
  /// ### Signature:
  /// - state: IrisState value to check
  /// - cb: IrisStateData -> unit workload
  ///
  /// Returns: unit
  let private withState (state: IrisState) (loaded: IrisStateData -> unit) =
    withLoaded state (konst state) (loaded >> konst state)
    |> ignore

  /// ## notLoaded
  ///
  /// Reply with the most common error.
  ///
  /// ### Signature:
  /// - chan: ReplyChan to reply with
  ///
  /// Returns: unit
  let private notLoaded (chan: ReplyChan) () =
    "No project loaded"
    |> Other
    |> Either.fail
    |> chan.Reply

  // ** withDefaultReply

  let private withDefaultReply (state: IrisState)
                               (chan: ReplyChan)
                               (loaded: IrisStateData -> IrisState) =
    withLoaded state (notLoaded chan >> konst state) loaded

  // ** withoutReply

  let private withoutReply (state: IrisState)
                           (loaded: IrisStateData -> IrisState) =
    withLoaded state (konst state) loaded

  // ** resetState

  /// ## resetState
  ///
  /// Dispose the passed `IrisState` and return `Idle`
  ///
  /// ### Signature:
  /// - state: IrisState to dispose
  ///
  /// Returns: IrisState
  let private resetState (state: IrisState) =
    withLoaded state (konst state) (dispose >> konst Idle)

  // ** IIrisServer

  /// ## IIrisServer
  ///
  /// Interface type to close over internal actors and state.
  ///
  type IIrisServer =
    inherit IDisposable
    abstract Config        : Either<IrisError,IrisConfig>
    abstract Status        : Either<IrisError,ServiceStatus>
    abstract GitServer     : Either<IrisError,IGitServer>
    abstract RaftServer    : Either<IrisError,IRaftServer>
    abstract SocketServer  : Either<IrisError,IWebSocketServer>
    abstract HttpServer    : Either<IrisError,AssetServer>
    abstract SetConfig     : IrisConfig -> Either<IrisError,unit>
    abstract Load          : FilePath   -> Either<IrisError,unit>
    abstract Periodic      : unit       -> Either<IrisError,unit>
    abstract ForceElection : unit       -> Either<IrisError,unit>
    abstract LeaveCluster  : unit       -> Either<IrisError,unit>
    abstract RmNode        : Id         -> Either<IrisError,EntryResponse>
    abstract AddNode       : RaftNode   -> Either<IrisError,EntryResponse>
    abstract JoinCluster   : IpAddress  -> uint16 -> Either<IrisError,unit>
    abstract Subscribe     : (IrisEvent -> unit) -> IDisposable

  // ** triggerOnNext

  let private triggerOnNext (subscriptions: Subscriptions) (ev: IrisEvent) =
    for subscription in subscriptions do
      subscription.OnNext ev

  // ** triggerWithState

  let private triggerWithState (state: IrisState) (ev: IrisEvent) =
    match state with
    | Loaded data -> triggerOnNext data.Subscriptions ev
    | _ -> ()

  // ** broadcastMsg

  let private broadcastMsg (state: IrisStateData) (cmd: StateMachine) =
    state.SocketServer.Broadcast cmd
    |> ignore

  // ** sendMsg

  let private sendMsg (state: IrisStateData) (id: Id) (cmd: StateMachine) =
    state.SocketServer.Send id cmd
    |> ignore

  // ** appendCmd

  let private appendCmd (state: IrisStateData) (cmd: StateMachine) =
    state.RaftServer.Append(cmd)

  // ** onOpen

  // __        __   _    ____             _        _
  // \ \      / /__| |__/ ___|  ___   ___| | _____| |_ ___
  //  \ \ /\ / / _ \ '_ \___ \ / _ \ / __| |/ / _ \ __/ __|
  //   \ V  V /  __/ |_) |__) | (_) | (__|   <  __/ |_\__ \
  //    \_/\_/ \___|_.__/____/ \___/ \___|_|\_\___|\__|___/

  /// ## OnOpen
  ///
  /// Register a callback with the WebSocket server that is run when new browser session has
  /// contacted this IrisSerivce. First, we send a `DataSnapshot` to the client to initialize it
  /// with the current state. Then, we append the newly created Session value to the Raft log to
  /// replicate it throughout the cluster.

  let private onOpen (state: IrisState) (session: Id) =
    withState state <| fun data ->
      sendMsg data session (DataSnapshot data.Store.State)

    // FIXME: need to check this bit for proper session handling
    // match appendCmd state (AddSession session) with
    // | Right entry ->
    //   entry
    //   |> Reply.Entry
    //   |> Either.succeed
    //   |> chan.Reply
    // | Left error ->
    //   error
    //   |> Either.fail
    //   |> chan.Reply

  // ** onClose

  /// ## OnClose
  ///
  /// Register a callback to be run when a browser as exited a session in an orderly fashion. The
  /// session is removed from the global state by appending a `RemoveSession`
  let private onClose (state: IrisState) (id: Id) =
    withState state <| fun data ->
      match Map.tryFind id data.Store.State.Sessions with
      | Some session ->
        match appendCmd data (RemoveSession session) with
        | Right _ -> ()
        | Left error  ->
          error
          |> string
          |> Logger.err data.NodeId tag
      | _ -> ()

  // ** onError

  /// ## OnError
  ///
  /// Register a callback to be run if the client connection unexpectectly fails. In that case the
  /// Session is retrieved and removed from global state.
  let private onError (state: IrisState) (sessionid: Id) (err: Exception) =
    withState state <| fun data ->
      match Map.tryFind sessionid data.Store.State.Sessions with
      | Some session ->
        match appendCmd data (RemoveSession session) with
        | Right _ -> ()
        | Left error ->
          error
          |> string
          |> Logger.err data.NodeId tag
      | _ -> ()


  // ** onMessage

  /// ## OnMessage
  ///
  /// Register a handler to process messages coming from the browser client. The current handling
  /// mechanism is that incoming message get appended to the `Raft` log immediately, and a log
  /// message is sent back to the client. Once the new command has been replicated throughout the
  /// system, it will be applied to the server-side global state, then pushed over the socket to
  /// be applied to all client-side global state atoms.
  let private onMessage (state: IrisState) (id: Id) (cmd: StateMachine) =
    withState state <| fun data ->
      match appendCmd data cmd with
      | Right _ -> ()
      | Left error ->
        error
        |> string
        |> Logger.err data.NodeId tag

  // ** handleSocketEvent

  let private handleSocketEvent (state: IrisState) (ev: SocketEvent) =
    match ev with
    | OnOpen id         -> onOpen    state id
    | OnClose id        -> onClose   state id
    | OnMessage (id,sm) -> onMessage state id sm
    | OnError (id,err)  -> onError   state id err
    state

  // ** onConfigured

  //  ____        __ _
  // |  _ \ __ _ / _| |_
  // | |_) / _` | |_| __|
  // |  _ < (_| |  _| |_
  // |_| \_\__,_|_|  \__|

  /// ## OnConfigured
  ///
  /// Register a callback to run when a new cluster configuration has been committed, and the
  /// joint-consensus mode has been concluded.
  let private onConfigured (state: IrisState) (nodes: RaftNode array) =
    withoutReply state <| fun data ->
      either {
        let! nodeid = data.RaftServer.NodeId
        nodes
        |> Array.map (Node.getId >> string)
        |> Array.fold (fun s id -> sprintf "%s %s" s  id) "New Configuration with: "
        |> Logger.debug nodeid tag
      }
      |> konst state

  // ** onNodeAdded

  /// ## OnNodeAdded
  ///
  /// Register a callback to be run when the user has added a new node to the `Raft` cluster. This
  /// commences the joint-consensus mode until the new node has been caught up and is ready be a
  /// full member of the cluster.

  let private onNodeAdded (state: IrisState) (node: RaftNode) =
    withoutReply state <| fun data ->
      let cmd = AddNode node
      data.Store.Dispatch cmd
      broadcastMsg data cmd
      state

  // ** onNodeUpdated

  /// ## OnNodeUpdated
  ///
  /// Register a callback to be called when a cluster node's properties such as e.g. its node
  /// state.

  let private onNodeUpdated (state: IrisState) (node: RaftNode) =
    withoutReply state <| fun data ->
      let cmd = UpdateNode node
      data.Store.Dispatch cmd
      broadcastMsg data cmd
      state

  // ** onNodeRemoved

  /// ## OnNodeRemoved
  ///
  /// Register a callback to be run when a node was removed from the cluster, resulting into
  /// the cluster entering into joint-consensus mode until the node was successfully removed.

  let private onNodeRemoved (state: IrisState) (node: RaftNode) =
    withoutReply state <| fun data ->
      let cmd = RemoveNode node
      data.Store.Dispatch cmd
      broadcastMsg data cmd
      state

  // ** onApplyLog

  /// ## onApplyLog
  ///
  /// Register a callback to be run when an appended entry is considered safely appended to a
  /// majority of servers logs. The entry then is regarded as applied.
  ///
  /// In this callback implementation we essentially do 3 things:
  ///
  ///   - the state machine command is applied to the store, potentially altering its state
  ///   - the state machine command is broadcast to all clients
  ///   - the state machine command is persisted to disk (potentially recorded in a git commit)

  let private onApplyLog (state: IrisState) (sm: StateMachine) =
    withoutReply state <| fun data ->
      data.Store.Dispatch sm
      broadcastMsg data sm

      if RaftServer.isLeader data.RaftServer then
        match persistEntry data.Project sm with
        | Right (info, commit, updated) ->
          Loaded { data with Project = updated }
        | Left error -> state
      else
        match data.RaftServer.State with
        | Right state ->
          let node =
            state.Raft
            |> Raft.currentLeader
            |> Option.bind (flip Raft.getNode state.Raft)

          match node with
          | Some leader ->
            match updateRepo data.Project leader with
            | Right () -> ()
            | Left error ->
              error
              |> string
              |> Logger.err data.NodeId tag
          | None -> ()
        | Left error ->
          error
          |> string
          |> Logger.err data.NodeId tag
        Loaded data

  // ** onStateChanged

  let private onStateChanged (state: IrisState)
                             (oldstate: RaftState)
                             (newstate: RaftState) =
    withState state <| fun data ->
      sprintf "Raft state changed from %A to %A" oldstate newstate
      |> Logger.debug data.NodeId tag
    state

  // ** onCreateSnapshot

  let private onCreateSnapshot (state: IrisState) =
    withState state <| fun data ->
      "CreateSnapshot requested"
      |> Logger.debug data.NodeId tag
    state

  // ** handleRaftEvent

  let private handleRaftEvent (state: IrisState) (ev: RaftEvent) =
    match ev with
    | ApplyLog sm             -> onApplyLog       state sm
    | NodeAdded node          -> onNodeAdded      state node
    | NodeRemoved node        -> onNodeRemoved    state node
    | NodeUpdated node        -> onNodeUpdated    state node
    | Configured nodes        -> onConfigured     state nodes
    | CreateSnapshot str      -> onCreateSnapshot state
    | StateChanged (ost, nst) -> onStateChanged   state ost nst

  // ** forwardLogEvents

  let private forwardLogEvents (agent: IrisAgent) (log: LogEvent) =
    agent.Post(Msg.Log log)

  // ** forwardRaftEvents

  let private forwardRaftEvents (agent: IrisAgent) (ev: RaftEvent) =
    agent.Post(Msg.Raft ev)

  // ** forwardGitEvents

  let private forwardGitEvents (agent: IrisAgent) (ev: GitEvent) =
    agent.Post(Msg.Git ev)

  // ** forwardSocketEvents

  let private forwardSocketEvents (agent: IrisAgent) (ev: SocketEvent) =
    agent.Post(Msg.Socket ev)

  //   ____ _ _
  //  / ___(_) |_
  // | |  _| | __|
  // | |_| | | |_
  //  \____|_|\__|

  // ** restartGitServer

  let private restartGitServer (data: IrisStateData) (agent: IrisAgent) =
    data.Disposables
    |> Map.tryFind GIT_SERVER
    |> Option.map dispose
    |> ignore

    dispose data.GitServer

    let result =
      either {
        let! node = data.RaftServer.Node
        let! gitserver = GitServer.create node data.Project.Path
        let disposable =
          forwardGitEvents agent
          |> gitserver.Subscribe
        match gitserver.Start() with
        | Right () ->
          return { data with
                     GitServer = gitserver
                     Disposables = Map.add GIT_SERVER disposable data.Disposables }
        | Left error ->
          dispose disposable
          dispose gitserver
          return! Either.fail error
      }

    match result with
    | Right newdata -> Loaded newdata
    | Left error ->
      error
      |> string
      |> Logger.err data.NodeId tag
      Loaded data

  // ** handleGitEvent

  let private handleGitEvent (state: IrisState) (agent: IrisAgent) (ev: GitEvent) =
    withoutReply state <| fun data ->
      triggerOnNext data.Subscriptions (IrisEvent.Git ev)
      match ev with
      | Started pid ->
        "Git daemon started"
        |> Logger.debug data.NodeId tag
        state

      | Exited pid ->
        "Git daemon exited. Attempting to restart."
        |> Logger.debug data.NodeId tag
        restartGitServer data agent

      | Pull (_, addr, port) ->
        sprintf "Client %s:%d pulled updates from me" addr port
        |> Logger.debug data.NodeId tag
        state
  //  _                    _
  // | |    ___   __ _  __| |
  // | |   / _ \ / _` |/ _` |
  // | |__| (_) | (_| | (_| |
  // |_____\___/ \__,_|\__,_|

  // ** loadProject

  let private loadProject (state: IrisState) (path: FilePath) (subscriptions: Subscriptions) =
    either {
      dispose state

      let! project = Project.load path

      // FIXME: load the actual state from disk
      let! node = Config.selfNode project.Config

      let  httpserver = new AssetServer(project.Config)
      let! raftserver = RaftServer.create ()
      let! wsserver   = SocketServer.create node
      let! gitserver  = GitServer.create node path

      return
        Loaded { NodeId       = node.Id
                 Status       = ServiceStatus.Starting
                 Store        = new Store(State.Empty)
                 Project      = project
                 GitServer    = gitserver
                 RaftServer   = raftserver
                 HttpServer   = httpserver
                 SocketServer = wsserver
                 Subscriptions= subscriptions
                 Disposables  = Map.empty }
    }

  // ** start

  let private start (state: IrisState) (agent: IrisAgent) =
    withLoaded state (konst (Right state)) <| fun data ->
      let disposables =
        [ (LOG_HANDLER, forwardLogEvents agent |> Logger.subscribe)
          (RAFT_SERVER, forwardRaftEvents agent |> data.RaftServer.Subscribe)
          (WS_SERVER, forwardSocketEvents agent |> data.SocketServer.Subscribe)
          (GIT_SERVER, forwardGitEvents agent |> data.GitServer.Subscribe) ]
        |> Map.ofList

      let result =
        either {
          do! data.RaftServer.Load(data.Project.Config)
          do! data.SocketServer.Start()
          do! data.GitServer.Start()
          do! data.HttpServer.Start()
        }

      match result with
      | Right _ ->
        Loaded { data with
                  Status = ServiceStatus.Running
                  Disposables = disposables }
        |> Either.succeed
      | Left error ->
        disposeAll disposables
        dispose data.SocketServer
        dispose data.RaftServer
        dispose data.GitServer
        dispose data.HttpServer
        Either.fail error


  // ** handleLoad

  let private handleLoad (state: IrisState)
                         (chan: ReplyChan)
                         (path: FilePath)
                         (subscriptions: Subscriptions)
                         (inbox: IrisAgent) =
    match loadProject state path subscriptions with
    | Right nextstate ->
      match start nextstate inbox with
      | Right finalstate ->
        // notify
        ServiceStatus.Running
        |> Status
        |> triggerOnNext subscriptions
        // reply
        Reply.Ok
        |> Either.succeed
        |> chan.Reply
        finalstate
      | Left error ->
        // notify
        ServiceStatus.Failed error
        |> Status
        |> triggerOnNext subscriptions
        // reply
        error
        |> Either.fail
        |> chan.Reply
        Idle
    | Left error ->
      ServiceStatus.Failed error
      |> Status
      |> triggerWithState state
      error
      |> Either.fail
      |> chan.Reply
      Idle

  //  _
  // | |    ___   __ _
  // | |   / _ \ / _` |
  // | |__| (_) | (_| |
  // |_____\___/ \__, |
  //             |___/

  // ** handleLogEvent

  let private handleLogEvent (state: IrisState) (log: LogEvent) =
    withState state <| fun data ->
      broadcastMsg data (LogMsg log)
    state

  // ** handleUnload

  let private handleUnload (state: IrisState) (chan: ReplyChan) =
    triggerWithState state (Status ServiceStatus.Stopped)
    dispose state
    Reply.Ok
    |> Either.succeed
    |> chan.Reply
    Idle

  // ** handleConfig

  let private handleConfig (state: IrisState) (chan: ReplyChan) =
    withDefaultReply state chan <| fun data ->
      data.Project.Config
      |> Reply.Config
      |> Either.succeed
      |> chan.Reply
      state

  // ** handleSetConfig

  let private handleSetConfig (state: IrisState) (chan: ReplyChan) (config: IrisConfig) =
    withDefaultReply state chan <| fun data ->
      Reply.Ok
      |> Either.succeed
      |> chan.Reply
      Loaded { data with Project = Project.updateConfig config data.Project }

  // ** handleForceElection

  let private handleForceElection (state: IrisState) =
    withoutReply state <| fun data ->
      match data.RaftServer.ForceElection () with
      | Left error ->
        error
        |> string
        |> Logger.err data.NodeId tag
      | other -> ignore other
      state

  // ** handlePeriodic

  let private handlePeriodic (state: IrisState) =
    withoutReply state <| fun data ->
      match data.RaftServer.Periodic() with
      | Left error ->
        error
        |> string
        |> Logger.err data.NodeId tag
      | other -> ignore other
      state

  // ** handleJoin

  let private handleJoin (state: IrisState) (chan: ReplyChan) (ip: IpAddress) (port: uint16) =
    withDefaultReply state chan <| fun data ->
      match data.RaftServer.JoinCluster ip port with
      | Right () ->
        Reply.Ok
        |> Either.succeed
        |> chan.Reply
      | Left error ->
        error
        |> Either.fail
        |> chan.Reply
      state

  // ** handleLeave

  let private handleLeave (state: IrisState) (chan: ReplyChan) =
    withDefaultReply state chan <| fun data ->
      match data.RaftServer.LeaveCluster () with
      | Right () ->
        Reply.Ok
        |> Either.succeed
        |> chan.Reply
      | Left error ->
        error
        |> Either.fail
        |> chan.Reply
      state

  // ** handleAddNode

  let private handleAddNode (state: IrisState) (chan: ReplyChan) (node: RaftNode) =
    withDefaultReply state chan <| fun data ->
      match data.RaftServer.AddNode node with
      | Right entry ->
        Reply.Entry entry
        |> Either.succeed
        |> chan.Reply
      | Left error ->
        error
        |> Either.fail
        |> chan.Reply
      state

  // ** handleRmNode

  let private handleRmNode (state: IrisState) (chan: ReplyChan) (id: Id) =
    withDefaultReply state chan <| fun data ->
      match data.RaftServer.RmNode id  with
      | Right entry ->
        Reply.Entry entry
        |> Either.succeed
        |> chan.Reply
      | Left error ->
        error
        |> Either.fail
        |> chan.Reply
      state

  // ** handleState

  let private handleState (state: IrisState) (chan: ReplyChan) =
    withDefaultReply state chan <| fun data ->
      Reply.State data
      |> Either.succeed
      |> chan.Reply
      state

  // ** loop

  let private loop (initial: IrisState) (subscriptions: Subscriptions) (inbox: IrisAgent) =
    let rec act (state: IrisState) =
      async {
        let! msg = inbox.Receive()
        let newstate =
          match msg with
          | Msg.Load (chan,path)     -> handleLoad          state chan  path subscriptions  inbox
          | Msg.Unload chan          -> handleUnload        state chan
          | Msg.Config chan          -> handleConfig        state chan
          | Msg.SetConfig (chan,cnf) -> handleSetConfig     state chan  cnf
          | Msg.Git    ev            -> handleGitEvent      state inbox ev
          | Msg.Socket ev            -> handleSocketEvent   state       ev
          | Msg.Raft   ev            -> handleRaftEvent     state       ev
          | Msg.Log   log            -> handleLogEvent      state       log
          | Msg.ForceElection        -> handleForceElection state
          | Msg.Periodic             -> handlePeriodic      state
          | Msg.Join (chan,ip,port)  -> handleJoin          state chan  ip port
          | Msg.Leave  chan          -> handleLeave         state chan
          | Msg.AddNode (chan,node)  -> handleAddNode       state chan  node
          | Msg.RmNode (chan,id)     -> handleRmNode        state chan  id
          | Msg.State chan           -> handleState         state chan
        return! act newstate
      }

    act initial

  // ** IrisService

  [<RequireQualifiedAccess>]
  module IrisService =

    let create () =
      let subscriptions = new Subscriptions()
      let agent = new IrisAgent(loop Idle subscriptions)

      let listener =
        { new IObservable<IrisEvent> with
            member self. Subscribe(obs) =
              lock subscriptions <| fun _ ->
                subscriptions.Add obs
              { new IDisposable with
                  member self.Dispose () =
                    lock subscriptions <| fun _ ->
                      subscriptions.Remove obs
                      |> ignore } }
      agent.Start()

      Either.succeed
        { new IIrisServer with
            member self.Config
              with get () =
                match agent.PostAndReply(fun chan -> Msg.Config chan) with
                | Right (Reply.Config config) -> Right config
                | Right  other                -> Left (Other "Unexpected response from IrisAgent")
                | Left   error                -> Left error

            member self.SetConfig (config: IrisConfig) =
              match agent.PostAndReply(fun chan -> Msg.SetConfig(chan,config)) with
              | Right Reply.Ok -> Right ()
              | Right other    -> Left (Other "Unexpected response from IrisAgent")
              | Left  error    -> Left error

            member self.Status
              with get () =
                match agent.PostAndReply(fun chan -> Msg.State chan) with
                | Right (Reply.State state) -> Right state.Status
                | Right other               -> Left (Other "Unexpected response from IrisAgent")
                | Left error                -> Left error

            member self.Load(path: FilePath) =
              match agent.PostAndReply(fun chan -> Msg.Load(chan,path)) with
              | Right Reply.Ok -> Right ()
              | Right other    -> Left (Other "Unexpectted response from IrisAgent")
              | Left error     -> Left error

            member self.ForceElection () =
              agent.Post(Msg.ForceElection)
              |> Either.succeed

            member self.Periodic () =
              agent.Post(Msg.Periodic)
              |> Either.succeed

            member self.LeaveCluster () =
              match agent.PostAndReply(fun chan -> Msg.Leave chan) with
              | Right Reply.Ok -> Right ()
              | Right other    -> Left (Other "Unexpectted response from IrisAgent")
              | Left error     -> Left error

            member self.JoinCluster ip port =
              match agent.PostAndReply(fun chan -> Msg.Join(chan,ip, port)) with
              | Right Reply.Ok -> Right ()
              | Right other    -> Left (Other "Unexpectted response from IrisAgent")
              | Left error     -> Left error

            member self.AddNode node =
              match agent.PostAndReply(fun chan -> Msg.AddNode(chan,node)) with
              | Right (Reply.Entry entry) -> Right entry
              | Right other               -> Left (Other "Unexpectted response from IrisAgent")
              | Left error                -> Left error

            member self.RmNode id =
              match agent.PostAndReply(fun chan -> Msg.RmNode(chan,id)) with
              | Right (Reply.Entry entry) -> Right entry
              | Right other               -> Left (Other "Unexpectted response from IrisAgent")
              | Left error                -> Left error

            member self.GitServer
              with get () =
                match agent.PostAndReply(fun chan -> Msg.State chan) with
                | Right (Reply.State state) -> Right state.GitServer
                | Right other               -> Left (Other "Unexpectted response from IrisAgent")
                | Left error                -> Left error

            member self.RaftServer
              with get () =
                match agent.PostAndReply(fun chan -> Msg.State chan) with
                | Right (Reply.State state) -> Right state.RaftServer
                | Right other               -> Left (Other "Unexpectted response from IrisAgent")
                | Left error                -> Left error

            member self.SocketServer
              with get () =
                match agent.PostAndReply(fun chan -> Msg.State chan) with
                | Right (Reply.State state) -> Right state.SocketServer
                | Right other               -> Left (Other "Unexpectted response from IrisAgent")
                | Left error                -> Left error

            member self.HttpServer
              with get () =
                match agent.PostAndReply(fun chan -> Msg.State chan) with
                | Right (Reply.State state) -> Right state.HttpServer
                | Right other               -> Left (Other "Unexpectted response from IrisAgent")
                | Left error                -> Left error

            member self.Subscribe(callback: IrisEvent -> unit) =
              { new IObserver<IrisEvent> with
                  member self.OnCompleted() = ()
                  member self.OnError(error) = ()
                  member self.OnNext(value) = callback value }
              |> listener.Subscribe

            member self.Dispose() =
              triggerOnNext subscriptions (Status ServiceStatus.Stopping)
              agent.PostAndReply(fun chan -> Msg.Unload chan)
              |> ignore
              dispose agent
          }
