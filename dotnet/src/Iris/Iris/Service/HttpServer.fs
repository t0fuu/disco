namespace Iris.Service

open Suave
open Suave.Http;
open Suave.Files
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open Suave.Logging
open Suave.Logging.Log
open Suave.Web
open System.Threading
open System.IO
open System.Net
open System.Net.Sockets
open System.Diagnostics
open System.Text.RegularExpressions
open Iris.Core
open Iris.Service.Interfaces

module Http =
  let private tag (str: string) = "HttpServer." + str

  module private Actions =
    open System.Text

    let private getString rawForm =
      System.Text.Encoding.UTF8.GetString(rawForm)

    let respond ctx status (txt: string) =
      let res =
        { ctx.response with
            status = status
            headers = ["Content-Type", "text/plain"]
            content = Encoding.UTF8.GetBytes txt |> Bytes }
      Some { ctx with response = res }

    let getWsport (iris: IIrisServer) (ctx: HttpContext) =
      either {
        let! cfg = iris.Config
        let! mem = Config.selfMember cfg
        return mem.WsPort
      }
      |> Either.unwrap (fun err ->
//        Logger.err config.MachineId (tag "getWsport") (string err)
        0us)
      |> string |> respond ctx HTTP_200.status |> async.Return

    let postIrisCommand (postCmd: CommandAgent) (ctx: HttpContext) = async {
      let! res = ctx.request.rawForm |> getString |> postCmd
      match res with
      | Left err -> return respond ctx HTTP_500.status (string err)
      | Right msg -> return respond ctx HTTP_200.status msg
    }

  let private noCache =
    setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
    >=> setHeader "Need-Help" "k@ioct.it"
    >=> setHeader "Pragma" "no-cache"
    >=> setHeader "Expires" "0"

  let private locate dir str =
    noCache >=> file (dir </> str)

  let getDefaultBasePath() =
  #if INTERACTIVE
    Path.GetFullPath(".") </> "assets" </> "frontend"
  #else
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let dir = Path.GetDirectoryName(asm.Location)
    dir </> "assets"
  #endif

//  let private widgetPath = basePath </> "widgets"
//
//  let private listFiles (path: FilePath) : FileName list =
//    DirectoryInfo(widgetPath).EnumerateFiles()
//    |> Seq.map (fun file -> file.Name)
//    |> Seq.toList
//
//  let private importStmt (name: FileName) =
//    sprintf """<link rel="import" href="widgets/%s" />""" name
//
//  let private indexHtml () =
//    listFiles widgetPath
//    |> List.map importStmt
//    |> List.fold (+) ""
//    |> sprintf "%s"

  // Add more mime-types here if necessary
  // the following are for fonts, source maps etc.
  let private mimeTypes = defaultMimeTypesMap

  // our application only needs to serve files off the disk
  // but we do need to specify what to do in the base case, i.e. "/"
  let private app (iris: IIrisServer) postCommand indexHtml =
    choose [
      Filters.GET >=>
        (choose [
          Filters.path WS_PORT_ENDPOINT >=> Actions.getWsport iris
          Filters.path "/" >=> (Files.file indexHtml)
          Files.browseHome ])
      Filters.POST >=>
        (choose [
          Filters.path COMMAND_ENDPOINT >=> Actions.postIrisCommand postCommand
        ])
      RequestErrors.NOT_FOUND "Page not found."
    ]

  let private mkConfig (config: IrisMachine)
                       (basePath: string)
                       (cts: CancellationTokenSource) :
                       Either<IrisError,SuaveConfig> =
    either {
      try
        let logger =
          let reg = Regex("\{(\w+)(?:\:(.*?))?\}")
          { new Logger with
              member x.log(level: Suave.Logging.LogLevel) (nextLine: Suave.Logging.LogLevel -> Message): Async<unit> = 
                match level with
                | Suave.Logging.LogLevel.Verbose -> ()
                | level ->
                  let line = nextLine level
                  match line.value with
                  | Event template ->
                    reg.Replace(template, fun m ->
                      let value = line.fields.[m.Groups.[1].Value]
                      if m.Groups.Count = 3
                      then System.String.Format("{0:" + m.Groups.[2].Value + "}", value)
                      else string value)
                    |> Logger.debug config.MachineId (tag "logger")
                  | Gauge _ -> ()
                async.Return ()
              member x.logWithAck(arg1: Suave.Logging.LogLevel) (arg2: Suave.Logging.LogLevel -> Message): Async<unit> = 
//                failwith "Not implemented yet"
                async.Return ()
              member x.name: string [] = 
                [|"iris"|] }

        let machine = MachineConfig.get()
        let addr = IPAddress.Parse machine.WebIP
        let port = Sockets.Port.Parse (string machine.WebPort)

        sprintf "Suave Web Server ready to start on: %A:%A" addr port
        |> Logger.info config.MachineId (tag "mkConfig")

        return
          { defaultConfig with
              logger            = logger
              cancellationToken = cts.Token
              homeFolder        = Some basePath
              bindings          = [ HttpBinding.create HTTP addr port ]
              mimeTypesMap      = mimeTypes }
      with
        | exn ->
          return!
            exn.Message
            |> Error.asSocketError (tag "mkConfig")
            |> Error.exitWith
    }

  // ** HttpServer

  [<RequireQualifiedAccess>]
  module HttpServer =

    // *** create

    let create (config: IrisMachine) (iris: IIrisServer) (postCommand: CommandAgent) =
      either {
        let basePath = getDefaultBasePath()
        let cts = new CancellationTokenSource()
        let! webConfig = mkConfig config basePath cts

        return
          { new IHttpServer with
              member self.Start () =
                try
                  let _, server =
                    Path.Combine(basePath, "index.html")
                    |> app iris postCommand
                    |> startWebServerAsync webConfig
                  Async.Start server
                  |> Either.succeed
                with
                  | exn ->
                    exn.Message
                    |> Error.asSocketError (tag "create")
                    |> Either.fail

              member self.Dispose () =
                try
                  cts.Cancel ()
                  cts.Dispose ()
                with
                  | _ -> () }
      }
