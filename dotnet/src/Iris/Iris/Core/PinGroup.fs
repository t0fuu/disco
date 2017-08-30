namespace rec Iris.Core

// * Imports

#if FABLE_COMPILER

open Fable.Core
open Fable.Import
open Iris.Core.FlatBuffers
open Iris.Web.Core.FlatBufferTypes

#else

open System.IO
open FlatBuffers
open Iris.Serialization

#endif

open Path

// * PinGroupYaml

#if !FABLE_COMPILER && !IRIS_NODES

open SharpYaml.Serialization

type PinGroupYaml() =
  [<DefaultValue>] val mutable Id: string
  [<DefaultValue>] val mutable Name: string
  [<DefaultValue>] val mutable Path: string
  [<DefaultValue>] val mutable Client: string
  [<DefaultValue>] val mutable Pins: PinYaml array

  static member From (group: PinGroup) =
    let yml = PinGroupYaml()
    yml.Id <- string group.Id
    yml.Name <- unwrap group.Name
    yml.Client <- string group.Client
    yml.Path <- Option.defaultValue null (Option.map unwrap group.Path)
    yml.Pins <- group.Pins |> Map.toArray |> Array.map (snd >> Yaml.toYaml)
    yml

  member yml.ToPinGroup() =
    either {
      let! pins =
        Array.fold
          (fun (m: Either<IrisError,Map<Id,Pin>>) pinyml -> either {
            let! pins = m
            let! (pin : Pin) = Yaml.fromYaml pinyml
            return Map.add pin.Id pin pins
          })
          (Right Map.empty)
          yml.Pins

      let path =
        if isNull yml.Path
        then None
        else Some (filepath yml.Path)

      return { Id = Id yml.Id
               Name = name yml.Name
               Path = path
               Client = Id yml.Client
               Pins = pins }
    }

#endif

// * PinGroup

//  ____  _        ____
// |  _ \(_)_ __  / ___|_ __ ___  _   _ _ __
// | |_) | | '_ \| |  _| '__/ _ \| | | | '_ \
// |  __/| | | | | |_| | | | (_) | |_| | |_) |
// |_|   |_|_| |_|\____|_|  \___/ \__,_| .__/
//                                     |_|

type PinGroup =
  { Id: Id
    Name: Name
    Client: Id
    Path: FilePath option
    Pins: Map<Id,Pin> }

  // ** ToYamlObject

  // __   __              _
  // \ \ / /_ _ _ __ ___ | |
  //  \ V / _` | '_ ` _ \| |
  //   | | (_| | | | | | | |
  //   |_|\__,_|_| |_| |_|_|

  #if !FABLE_COMPILER && !IRIS_NODES

  member group.ToYamlObject () = PinGroupYaml.From(group)

  // ** ToYaml

  member self.ToYaml (serializer: Serializer) =
    self
    |> Yaml.toYaml
    |> serializer.Serialize

  // ** FromYamlObject

  static member FromYamlObject (yml: PinGroupYaml) = yml.ToPinGroup()

  // ** FromYaml

  static member FromYaml (str: string) : Either<IrisError,PinGroup> =
    let serializer = Serializer()
    let yml = serializer.Deserialize<PinGroupYaml>(str)
    Yaml.fromYaml yml

  #endif

  // ** FromFB

  //  ____  _
  // | __ )(_)_ __   __ _ _ __ _   _
  // |  _ \| | '_ \ / _` | '__| | | |
  // | |_) | | | | | (_| | |  | |_| |
  // |____/|_|_| |_|\__,_|_|   \__, |
  //                           |___/

  static member FromFB (fb: PinGroupFB) =
    either {
      let! pins =
        let arr = Array.zeroCreate fb.PinsLength
        Array.fold
          (fun (m: Either<IrisError,int * Map<Id,Pin>>) _ -> either {
              let! (i, pins) = m

              #if FABLE_COMPILER
              let! pin = i |> fb.Pins |> Pin.FromFB
              #else
              let! pin =
                let nullable = fb.Pins(i)
                if nullable.HasValue then
                  nullable.Value
                  |> Pin.FromFB
                else
                  "Could not parse empty PinFB"
                  |> Error.asParseError "PinGroup.FromFB"
                  |> Either.fail
              #endif

              return (i + 1, Map.add pin.Id pin pins)
            })
          (Right (0, Map.empty))
          arr
        |> Either.map snd

      let path =
        if isNull fb.Path
        then None
        else Some (filepath fb.Path)

      return { Id = Id fb.Id
               Name = name fb.Name
               Path = path
               Client = Id fb.Client
               Pins = pins }
    }

  // ** ToOffset

  member self.ToOffset(builder: FlatBufferBuilder) : Offset<PinGroupFB> =
    let id = string self.Id |> builder.CreateString
    let name = self.Name |> unwrap |> Option.mapNull builder.CreateString
    let path = self.Path |> Option.map (unwrap >> builder.CreateString)
    let client = self.Client |> string |> builder.CreateString
    let pinoffsets =
      self.Pins
      |> Map.toArray
      |> Array.map (fun (_,pin: Pin) -> pin.ToOffset(builder))

    let pins = PinGroupFB.CreatePinsVector(builder, pinoffsets)
    PinGroupFB.StartPinGroupFB(builder)
    PinGroupFB.AddId(builder, id)
    Option.iter (fun value -> PinGroupFB.AddName(builder,value)) name
    Option.iter (fun value -> PinGroupFB.AddPath(builder,value)) path
    PinGroupFB.AddClient(builder, client)
    PinGroupFB.AddPins(builder, pins)
    PinGroupFB.EndPinGroupFB(builder)

  // ** ToBytes

  member self.ToBytes() : byte[] = Binary.buildBuffer self

  // ** FromBytes

  static member FromBytes (bytes: byte[]) : Either<IrisError,PinGroup> =
    Binary.createBuffer bytes
    |> PinGroupFB.GetRootAsPinGroupFB
    |> PinGroup.FromFB

  // ** Load

  //  _                    _
  // | |    ___   __ _  __| |
  // | |   / _ \ / _` |/ _` |
  // | |__| (_) | (_| | (_| |
  // |_____\___/ \__,_|\__,_|

  #if !FABLE_COMPILER && !IRIS_NODES

  static member Load(path: FilePath) : Either<IrisError, PinGroup> =
    IrisData.load path

  // ** LoadAll

  static member LoadAll(basePath: FilePath) : Either<IrisError, PinGroup array> =
    basePath </> filepath Constants.PINGROUP_DIR
    |> IrisData.loadAll

  // ** Save

  //  ____
  // / ___|  __ ___   _____
  // \___ \ / _` \ \ / / _ \
  //  ___) | (_| |\ V /  __/
  // |____/ \__,_| \_/ \___|

  member group.Save (basePath: FilePath) =
    PinGroup.save basePath group

  // ** Persisted

  member group.Persisted
    with get () = PinGroup.persisted group

  // ** IsSaved

  member group.Exists (basePath: FilePath) =
    basePath </> PinGroup.assetPath group
    |> File.exists

  #endif

  // ** AssetPath

  //     _                 _   ____       _   _
  //    / \   ___ ___  ___| |_|  _ \ __ _| |_| |__
  //   / _ \ / __/ __|/ _ \ __| |_) / _` | __| '_ \
  //  / ___ \\__ \__ \  __/ |_|  __/ (_| | |_| | | |
  // /_/   \_\___/___/\___|\__|_|   \__,_|\__|_| |_|

  member pingroup.AssetPath
    with get () = PinGroup.assetPath pingroup

// * PinGroup module

module PinGroup =

  // ** persisted

  let persisted (group: PinGroup) =
    group.Pins
    |> Map.filter (fun _ (pin: Pin) -> pin.Persisted)
    |> Map.isEmpty
    |> not

  // ** persistedPins

  let persistedPins (group: PinGroup) =
    Map.filter (fun _ (pin: Pin) -> pin.Persisted) group.Pins

  // ** volatilePins

  let volatilePins (group: PinGroup) =
    Map.filter (fun _ (pin: Pin) -> not pin.Persisted) group.Pins

  // ** removeVolatile

  let removeVolatile (group: PinGroup) =
    { group with Pins = persistedPins group }

  // ** save

  #if !FABLE_COMPILER && !IRIS_NODES

  let save basePath (group: PinGroup) =
    if persisted group then
      group
      |> removeVolatile
      |> IrisData.save basePath
    else Either.succeed ()

  #endif

  // ** assetPath

  let assetPath (group: PinGroup) =
    let fn = (string group.Id |> String.sanitize) + ASSET_EXTENSION
    let path = (string group.Client) <.> fn
    filepath PINGROUP_DIR </> path

  // ** hasPin

  let hasPin (group : PinGroup) (id: Id) : bool =
    Map.containsKey id group.Pins

  // ** findPin

  let findPin (group: PinGroup) (id: Id) =
    Map.find id group.Pins

  // ** tryFindPin

  let tryFindPin (group: PinGroup) (id: Id) =
    Map.tryFind id group.Pins

  // ** addPin

  let addPin (group : PinGroup) (pin : Pin) : PinGroup =
    if hasPin group pin.Id
    then   group
    else { group with Pins = Map.add pin.Id pin group.Pins }

  // ** updatePin

  let updatePin (group : PinGroup) (pin : Pin) : PinGroup =
    if hasPin group pin.Id
    then { group with Pins = Map.add pin.Id pin group.Pins }
    else   group

  // ** updateSlices

  let updateSlices (group : PinGroup) (slices: Slices) : PinGroup =
    match Map.tryFind slices.Id group.Pins with
    | Some pin -> { group with Pins = Map.add pin.Id (Pin.setSlices slices pin) group.Pins }
    | None -> group

  // ** processSlices

  let processSlices (group: PinGroup) (slices: Map<Id,Slices>) : PinGroup =
    let mapper _ (pin: Pin) =
      match Map.tryFind pin.Id slices with
      | Some slices -> Pin.setSlices slices pin
      | None -> pin
    { group with Pins = Map.map mapper group.Pins }

  // ** removePin

  //                                    ____  _
  //  _ __ ___ _ __ ___   _____   _____|  _ \(_)_ __
  // | '__/ _ \ '_ ` _ \ / _ \ \ / / _ \ |_) | | '_ \
  // | | |  __/ | | | | | (_) \ V /  __/  __/| | | | |
  // |_|  \___|_| |_| |_|\___/ \_/ \___|_|   |_|_| |_|

  let removePin (group : PinGroup) (pin : Pin) : PinGroup =
    { group with Pins = Map.remove pin.Id group.Pins }

  // ** setPinsOffline

  let setPinsOffline (group: PinGroup) =
    { group with Pins = Map.map (fun _ pin -> Pin.setOnline false pin) group.Pins }

// * Map module

module Map =

  //  _              _____ _           _ ____  _
  // | |_ _ __ _   _|  ___(_)_ __   __| |  _ \(_)_ __
  // | __| '__| | | | |_  | | '_ \ / _` | |_) | | '_ \
  // | |_| |  | |_| |  _| | | | | | (_| |  __/| | | | |
  //  \__|_|   \__, |_|   |_|_| |_|\__,_|_|   |_|_| |_|
  //           |___/

  let tryFindPin (id : Id) (groups : Map<Id, PinGroup>) : Pin option =
    let folder (m : Pin option) _ (group: PinGroup) =
      match m with
        | Some _ as res -> res
        |      _        -> Map.tryFind id group.Pins
    Map.fold folder None groups

  //                  _        _           ____  _
  //   ___ ___  _ __ | |_ __ _(_)_ __  ___|  _ \(_)_ __
  //  / __/ _ \| '_ \| __/ _` | | '_ \/ __| |_) | | '_ \
  // | (_| (_) | | | | || (_| | | | | \__ \  __/| | | | |
  //  \___\___/|_| |_|\__\__,_|_|_| |_|___/_|   |_|_| |_|

  let containsPin (id: Id) (groups : Map<Id,PinGroup>) : bool =
    let folder m _ group =
      if m then m else PinGroup.hasPin group id || m
    Map.fold folder false groups
