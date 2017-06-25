namespace Iris.MockClient

open Argu
open Iris.Core
open System
open System.IO
open System.Text
open System.Net
open System.Net.Sockets
open System.Text.RegularExpressions
open Iris.Raft
open Iris.Client
open Iris.Net

[<AutoOpen>]
module Main =


  //   ____ _ _  ___        _   _
  //  / ___| (_)/ _ \ _ __ | |_(_) ___  _ __  ___
  // | |   | | | | | | '_ \| __| |/ _ \| '_ \/ __|
  // | |___| | | |_| | |_) | |_| | (_) | | | \__ \
  //  \____|_|_|\___/| .__/ \__|_|\___/|_| |_|___/
  //                 |_|

  type CliOptions =
    | [<AltCommandLine("-v")>] Verbose
    | [<AltCommandLine("-f")>] File of string
    | [<AltCommandLine("-n")>] Name of string
    | [<AltCommandLine("-h")>] Host of string
    | [<AltCommandLine("-p")>] Port of uint16
    | [<AltCommandLine("-b")>] Bind of string

    interface IArgParserTemplate with
      member self.Usage =
        match self with
        | Verbose -> "be more verbose"
        | File _  -> "specify a commands file to read on startup (optional)"
        | Name _  -> "specify the iris clients' name (optional)"
        | Host _  -> "specify the iris services' host to connect to (optional)"
        | Port _  -> "specify the iris services' port to connect on (optional)"
        | Bind _  -> "specify the iris clients' address to bind to"

  [<Literal>]
  let private help = @"
 ___      _      ____ _ _            _
|_ _|_ __(_)___ / ___| (_) ___ _ __ | |_
 | || '__| / __| |   | | |/ _ \ '_ \| __|
 | || |  | \__ \ |___| | |  __/ | | | |_
|___|_|  |_|___/\____|_|_|\___|_| |_|\__| © Nsynk, 2017

Usage:

  ----------------------------------------
  | Quick Start:                         |
  ----------------------------------------

  // add a pin of <type> with <name> (where <name> will also serve the pin's Id)

  add <type> <name>

  // update a pin's value or properties (enum)

  update <name> <attr> (<value> / 1*<property>)

  // remove a pin from the built-in patch

  remove <name>

  // show a pins value and attributes

  show <name>

  // list all patches and their pins

  listPins|lp

  // help

  help|h

  ----------------------------------------
  | Grammar (see [1] for info):          |
  ----------------------------------------

  <type> :=
    ""toggle""    /
    ""bang""      /
    ""string""    /
    ""multiline"" /
    ""file""      /
    ""dir""       /
    ""url""       /
    ""ip""        /
    ""float""     /
    ""double""    /
    ""bytes""     /
    ""color""     /
    ""enum""

  <name> := 1*ALPHA

  <attr> := ""value"" / ""properties""

  <bool> := ""true"" / ""false""

  <string> := DQUOTE 1*CHAR DQUOTE

  <frac> := 1*DIGIT ""."" 1*DIGIT

  <bytes> := 1*CHAR

  <color> := 1*3DIGIT SP 1*3DIGIT SP 1*3DIGIT

  <value> :=
    1*<bool>   /
    1*<string> /
    1*<frac>   /
    1*<bytes>  /
    1*<color>

  <property> := 1*ALPHA ""="" 1*ALPHA

  ----------------------------------------
  | Examples                             |
  ----------------------------------------

  // add a new toggle with name and id ""my-toggle""

  add toggle my-toggle

  // update its slices

  update my-toggle value true false

  // remove the pin

  remove my-toggle

  // add a new enum pin

  add enum my-enum

  // add some properties

  update my-enum properties one=eins two=zwei three=drei

  // set its slices

  update my-enum value zwei eins drei

  // add a color pin

  add color my-color

  // set its first slice to white, and the second to black with full alpha

  update my-color value ""255 255 255 255"" ""255 255 255 255""

  // add and set a string pin

  add string my-string
  update my-string value ""yo this is a string with spaces"" ""ok, this is cool""

  ----------------------------------------
  | Links                                |
  ----------------------------------------

  [1] https://en.wikipedia.org/wiki/Augmented_Backus%E2%80%93Naur_form
  "

  let private patchid = Id.Create()

  let private nextPort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let port = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    port

  let private (|Exit|_|) str =
    match str with
    | "exit" | "quit" -> Some ()
    | _ -> None

  let private (|Help|_|) str =
    match str with
    | "help" | "h" -> Some help
    | _ -> None

  let private restOf (attr: string) (str: string) =
    if attr = str then
      ""
    else
      str.Substring(attr.Length + 1, str.Length - attr.Length - 1)

  let private parseAttr (attr: string) (str: string) =
    match str.StartsWith(attr) with
    | true -> restOf attr str |> Some
    | false -> None

  let private (|Add|_|) (str: string) =
    parseAttr "add" str

  let private (|Update|_|) (str: string) =
    parseAttr "update" str

  let private (|Remove|_|) (str: string) =
    parseAttr "remove" str

  let private (|Show|_|) (str: string) =
    parseAttr "show" str

  let private (|Toggle|_|) (str: string) =
    parseAttr "toggle" str

  let private (|Bang|_|) (str: string) =
    parseAttr "bang" str

  let private (|String|_|) (str: string) =
    parseAttr "string" str

  let private (|Multiline|_|) (str: string) =
    parseAttr "multiline" str

  let private (|File|_|) (str: string) =
    parseAttr "file" str

  let private (|Dir|_|) (str: string) =
    parseAttr "dir" str

  let private (|Url|_|) (str: string) =
    parseAttr "url" str

  let private (|IP|_|) (str: string) =
    parseAttr "ip" str

  let private (|Float|_|) (str: string) =
    parseAttr "float" str

  let private (|Double|_|) (str: string) =
    parseAttr "double" str

  let private (|Bytes|_|) (str: string) =
    parseAttr "bytes" str

  let private (|Color|_|) (str: string) =
    parseAttr "color" str

  let private (|Enum|_|) (str: string) =
    parseAttr "enum" str

  let private (|AddPin|_|) (str: string) =
    match str with
    | Add rest ->
      match rest with
      | Toggle name ->
        Pin.toggle (Id name) name patchid  [| |]  [| false |]
        |> Some

      | Bang name ->
        Pin.bang (Id name) name patchid [| |] [| false |]
        |> Some

      | String name ->
        Pin.string (Id name) name patchid [| |] [| "" |]
        |> Some

      | Multiline name ->
        Pin.multiLine (Id name) name patchid [| |] [| "" |]
        |> Some

      | File name ->
        Pin.fileName (Id name) name patchid [| |] [| "" |]
        |> Some

      | Dir name ->
        Pin.directory (Id name) name patchid [| |] [| "" |]
        |> Some

      | Url name ->
        Pin.url (Id name) name patchid [| |] [| "" |]
        |> Some

      | IP name ->
        Pin.ip (Id name) name patchid [| |] [| "" |]
        |> Some

      | Float name ->
        Pin.number (Id name) name patchid [| |] [| 0.0 |]
        |> Some

      | Bytes name ->
        Pin.bytes (Id name) name patchid [| |] [| [| |] |]
        |> Some

      | Color name ->
        let color = RGBA { Red = 0uy; Green = 0uy; Blue = 0uy; Alpha = 0uy }
        Pin.color (Id name) name patchid [| |] [| color |]
        |> Some

      | Enum name ->
        let prop = { Key = ""; Value = "" }
        Pin.enum (Id name) name patchid [| |] [| prop |] [| prop |]
        |> Some
      | _ -> None
    | _ -> None

  let private (|Value|_|) (str: string) =
    parseAttr "value" str

  let private (|Properties|_|) (str: string) =
    parseAttr "properties" str

  let private (|ListPins|_|) (str: string) =
    match str with
    | "listpins" | "lp" -> Some ()
    | _ -> None

  let private listPins (client: IApiClient) =
    Map.iter
      (fun _ (patch: PinGroup) ->
        printfn "Patch: %A" (string patch.Id)
        Map.iter
          (fun _ (pin: Pin) ->
            printfn "    id: %s name: %s type: %s" (string pin.Id) pin.Name pin.Type)
          patch.Pins)
      client.State.PinGroups
    printfn ""

  let private parseLine (line: string) : Pin option =
    match line with
    | AddPin pin -> Some pin
    | _ -> None

  let private tryLoad (path: FilePath) =
    let lines = try File.readLines path with | _ -> [| |]
    Array.fold
      (fun (pins: Map<Id,Pin>) line ->
        match parseLine line with
        | Some pin -> Map.add pin.Id pin pins
        | _ -> pins)
      Map.empty
      lines

  let private popWord (str: string) =
    let spc = str.IndexOf(' ')
    let id = str.Substring(0, spc)
    let rest = str.Substring(spc + 1, str.Length - spc - 1)
    id, rest

  let private parseColor (str: string) =
    match str.Split(' ') with
    | [| r; g; b; a; |] ->
      try
        RGBA { Red = uint8 r; Green = uint8 g; Blue = uint8 b; Alpha = uint8 a }
      with
        | _ ->
          Console.Error.WriteLine("Wrong format {0}. See help", str)
          RGBA { Red = 0uy; Green = 0uy; Blue = 0uy; Alpha = 0uy }
    | _ ->
      Console.Error.WriteLine("Wrong format {0}. See help", str)
      RGBA { Red = 0uy; Green = 0uy; Blue = 0uy; Alpha = 0uy }

  let private parseProperties (str: string) =
    let pattern = @"(\w+)=(\w+)"
    let matches = Regex.Matches(str, pattern)

    if matches.Count > 0 then
      let out = Array.zeroCreate matches.Count
      let mutable i = 0
      for m in matches do
        out.[i] <- { Key = m.Groups.[1].Value; Value = m.Groups.[2].Value }
        i <- i + 1
      out
    else
      [| { Key = ""; Value = "" } |]

  let private parseStringValues (str: string) =
    let pat = @"\""(.*?)\"""
    let matches = Regex.Matches(str, pat)

    let out : string array =
      Array.zeroCreate matches.Count

    Seq.iteri
      (fun i _ ->
        let m = matches.[i]
        out.[i] <- m.Groups.[1].Value)
      out

    out

  let inline private parseSimple (f: string -> ^a) (str: string) : ^a array =
    let cmd, rest = popWord str
    let split = rest.Trim().Split(' ')
    let out : ^a array = Array.zeroCreate (Array.length split)

    Array.iteri
      (fun i input ->
        let value = f input
        out.[i] <- value)
      split

    out

  let private parseBoolValues (str: string) : bool array =
    let parse input = try Boolean.Parse input with | _ -> false
    parseSimple parse str
    |> Array.mapi (fun i bool -> bool)

//  let private parseIntValues (str: string) : IntPinD array =
//    let parse input = try int input with | _ -> 0
//    parseSimple parse str
//    |> Array.mapi (fun i num -> { Index = uint32 i; Value = num })
//
//  let private parseFloatValues (str: string) : FloatSliceD array =
//    let parse input = try float input with | _ -> 0.0
//    parseSimple parse str
//    |> Array.mapi (fun i num -> { Index = uint32 i; Value = num })

  let private parseDoubleValues (str: string) : double array =
    let parse input = try double input with | _ -> 0.0
    parseSimple parse str

  let private parseByteValues (str: string) : byte array array =
    parseStringValues str
    |> Array.map Encoding.UTF8.GetBytes

  let private parseColorValues (str: string) : ColorSpace array =
    parseStringValues str
    |> Array.map parseColor

  let private parseEnumValues (props: Property array) (str: string) : Property array =
    str.Split(' ')
    |> Array.mapi
      (fun i input ->
        let prop =
          match Array.tryFind (fun (prop:Property) -> prop.Value = input) props with
          | Some property -> property
          | _ -> { Key = ""; Value = "" }
        prop)

  [<Literal>]
  let private PS1 = "λ: "

  let private addPin (client: IApiClient) (pin: Pin) =
    client.AddPin pin

  let private updateSlices (client: IApiClient) (slices: Slices) =
    client.UpdateSlices slices

  let private updatePin (client: IApiClient) (pin: Pin) =
    client.UpdatePin pin

  let private removePin (client: IApiClient) (pin: Pin) =
    client.RemovePin pin

  let private getPin (client: IApiClient) (id: string)  =
    State.tryFindPin (Id (id.Trim())) client.State

  let private showPin (pin: Pin)  =
    printfn ""
    printfn "%A" pin
    printfn ""

  let private loop (client: IApiClient) (initial: Map<Id,Pin>) (patch:PinGroup) =
    let mutable run = true

    client.AddPinGroup patch

    Map.iter (fun _ (pin: Pin) -> addPin client pin) initial

    while run do
      Console.Write(PS1)
      match Console.ReadLine() with
      //  _____      _ _
      // | ____|_  _(_) |_
      // |  _| \ \/ / | __|
      // | |___ >  <| | |_
      // |_____/_/\_\_|\__|

      | Exit _ ->
        printfn "Bye."
        run <- false

      //  _   _      _
      // | | | | ___| |_ __
      // | |_| |/ _ \ | '_ \
      // |  _  |  __/ | |_) |
      // |_| |_|\___|_| .__/
      //              |_|

      | Help txt -> printfn "%s" txt

      //     _       _     _
      //    / \   __| | __| |
      //   / _ \ / _` |/ _` |
      //  / ___ \ (_| | (_| |
      // /_/   \_\__,_|\__,_|

      | AddPin pin -> addPin client pin

      //   ____      _
      //  / ___| ___| |_
      // | |  _ / _ \ __|
      // | |_| |  __/ |_
      //  \____|\___|\__|

      | Show id ->
        getPin client id
        |> Option.map showPin
        |> ignore

      //  _   _           _       _
      // | | | |_ __   __| | __ _| |_ ___
      // | | | | '_ \ / _` |/ _` | __/ _ \
      // | |_| | |_) | (_| | (_| | ||  __/
      //  \___/| .__/ \__,_|\__,_|\__\___|
      //       |_|

      | Update rest ->
        let id, rest = popWord rest

        match rest with
        // __     __    _
        // \ \   / /_ _| |_   _  ___
        //  \ \ / / _` | | | | |/ _ \
        //   \ V / (_| | | |_| |  __/
        //    \_/ \__,_|_|\__,_|\___|

        | Value value ->
          match getPin client id with
          | Some (StringPin data) ->
            let slices = StringSlices(data.Id, parseStringValues rest)
            updateSlices client slices

//          | Some (IntPin data) ->
//            let slices = IntSlices(data.Id, parseIntValues rest)
//            updateSlices client slices

//          | Some (FloatPin data) ->
//            let slices = FloatSlices(data.Id, parseFloatValues rest)
//            updateSlices client slices

          | Some (NumberPin data) ->
            let slices = NumberSlices(data.Id, parseDoubleValues rest)
            updateSlices client slices

          | Some (BoolPin data) ->
            let slices = BoolSlices(data.Id, parseBoolValues rest)
            updateSlices client slices

          | Some (BytePin data) ->
            let slices = ByteSlices(data.Id, parseByteValues rest)
            updateSlices client slices

          | Some (EnumPin data) ->
            let slices = EnumSlices(data.Id, parseEnumValues data.Properties rest)
            updateSlices client slices

          | Some (ColorPin data) ->
            let slices = ColorSlices(data.Id, parseColorValues rest)
            updateSlices client slices

          | _ -> printfn "could not find pin with id %A" id

        //  ____                            _
        // |  _ \ _ __ ___  _ __   ___ _ __| |_ _   _
        // | |_) | '__/ _ \| '_ \ / _ \ '__| __| | | |
        // |  __/| | | (_) | |_) |  __/ |  | |_| |_| |
        // |_|   |_|  \___/| .__/ \___|_|   \__|\__, |
        //                 |_|                  |___/

        | Properties value ->
          match getPin client id with
          | Some (EnumPin data) ->
            let properties = parseProperties value
            let updated = EnumPin { data with Properties = properties }
            updatePin client updated
          | _ -> ()
        | other -> printfn "error: unknown subcommand %A" other

      //  ____
      // |  _ \ ___ _ __ ___   _____   _____
      // | |_) / _ \ '_ ` _ \ / _ \ \ / / _ \
      // |  _ <  __/ | | | | | (_) \ V /  __/
      // |_| \_\___|_| |_| |_|\___/ \_/ \___|

      | Remove id ->
        getPin client id
        |> Option.map (removePin client)
        |> ignore

      //  _     _     _
      // | |   (_)___| |_
      // | |   | / __| __|
      // | |___| \__ \ |_
      // |_____|_|___/\__|

      | ListPins ->
        listPins client

      | str ->
        printfn "unknown command: %A" str

  [<EntryPoint>]
  let main args =
    let parser = ArgumentParser.Create<CliOptions>(helpTextMessage = help)
    let parsed = parser.Parse args

    let id = Id.Create()

    Logger.initialize id

    let result =
      either {
        let server =
          { Port =
              if parsed.Contains <@ Port @>
              then port (parsed.GetResult <@ Port @>)
              else port Constants.DEFAULT_API_PORT
            IpAddress =
              if parsed.Contains <@ Host @>
              then IPv4Address (parsed.GetResult <@ Host @>)
              else IPv4Address "127.0.0.1" }

        let client =
          { Id = Id.Create()
            Name =
              if parsed.Contains <@ Name @>
              then parsed.GetResult <@ Name @>
              else "<empty>"
            Role = Role.Renderer
            Status = ServiceStatus.Starting
            IpAddress =
              match parsed.Contains <@ Bind @> with
              | true  -> IPv4Address (parsed.GetResult <@ Bind @>)
              | false -> IPv4Address "127.0.0.1"
            Port = nextPort() |> uint16 |> port }

        let client = ApiClient.create server client
        do! client.Start()
        return client
      }

    match result with
    | Right client ->
      let patch : PinGroup =
        { Id = patchid
          Name = name "MockClient Patch"
          Client = id
          Pins = Map.empty }

      let loaded =
        if parsed.Contains <@ File @> then
          parsed.GetResult <@ File @>
          |> filepath
          |> tryLoad
        else
          Map.empty

      let level =
        if parsed.Contains <@ Verbose @> then
          LogLevel.Debug
        else
          LogLevel.Info

      use obs = Logger.subscribe (Logger.stdoutWith level)

      loop client loaded patch
      dispose client
      exit 0
    | Left error ->
      Console.Error.WriteLine("Encountered error starting client: {0}", Error.toMessage error)
      Console.Error.WriteLine("Aborting.")
      error
      |> Error.toExitCode
      |> exit
