namespace Iris.Service

// * Imports

open Iris.Raft
open Iris.Core
open Iris.Core.Utils
open Iris.Service.Interfaces

open Suave
open Suave.Http
open Suave.Files
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open Suave.Logging
open Suave.Logging.Log
open Suave.CORS
open Suave.Web
open Suave.Git

open System
open System.IO
open System.Text
open System.Threading
open System.Diagnostics

open System.Collections.Concurrent
open Microsoft.FSharp.Control

// * GitServer

module GitServer =

  // ** tag

  let private tag (str: string) = sprintf "GitServer.%s" str

  // ** Listener

  type private Listener = IObservable<GitEvent>

  // ** Subscriptions

  type private Subscriptions = Subscriptions<GitEvent>

  // ** createListener

  let private createListener (subscriptions: Subscriptions) =
    let guid = Guid.NewGuid()
    { new Listener with
        member self.Subscribe(obs) =
          subscriptions.TryAdd(guid,obs) |> ignore

          { new IDisposable with
              member self.Dispose () =
                lock subscriptions <| fun _ ->
                  subscriptions.TryRemove(guid)
                  |> ignore } }

  // ** makeConfig

  let private makeConfig (ip: IpAddress) port (cts: CancellationTokenSource) =
    let addr = ip.toIPAddress()
    { defaultConfig with
        cancellationToken = cts.Token
        bindings = [ HttpBinding.create HTTP addr port ]
        mimeTypesMap = defaultMimeTypesMap }

  // ** create

  let create (mem: RaftMember) (project: IrisProject) =
    let mutable status = ServiceStatus.Stopped
    let cts = new CancellationTokenSource()
    let subscriptions = Subscriptions()

    { new IGitServer with
        member self.Status
          with get () = status

        member self.Subscribe(callback: GitEvent -> unit) =
          let listener = createListener subscriptions
          { new IObserver<GitEvent> with
              member self.OnCompleted() = ()
              member self.OnError(error) = ()
              member self.OnNext(value) = callback value }
          |> listener.Subscribe

        member self.Start () = either {
            do! Network.ensureIpAddress mem.IpAddr
            do! Network.ensureAvailability mem.IpAddr mem.GitPort

            status <- ServiceStatus.Starting
            let config = makeConfig mem.IpAddr (unwrap mem.GitPort) cts

            unwrap project.Path
            |> gitServer (project.Name |> unwrap |> Some)
            |> startWebServerAsync config
            |> (fun (_, server) -> Async.Start(server, cts.Token))

            Thread.Sleep(150)

            Observable.notify subscriptions GitEvent.Started
            status <- ServiceStatus.Running
          }

        member self.Dispose() =
          if not (Service.isDisposed status) then
            try
              cts.Cancel()
              cts.Dispose()
            finally
              status <- ServiceStatus.Disposed
      }
