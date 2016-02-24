namespace Iris.Core

open System
open System.IO
open System.Text.RegularExpressions

module Utils =

  let isLinux : bool =
    int Environment.OSVersion.Platform
    |> fun p ->
      (p = 4) || (p = 6) || (p = 128)

  // __        __         _     ____
  // \ \      / /__  _ __| | __/ ___| _ __   __ _  ___ ___
  //  \ \ /\ / / _ \| '__| |/ /\___ \| '_ \ / _` |/ __/ _ \
  //   \ V  V / (_) | |  |   <  ___) | |_) | (_| | (_|  __/
  //    \_/\_/ \___/|_|  |_|\_\|____/| .__/ \__,_|\___\___|
  //                                 |_|
  // Path:
  //
  // the standard location projects are create/cloned to.
  // Settable it via environment variable.
  let Workspace =
    let wsp = Environment.GetEnvironmentVariable("IRIS_WORKSPACE")
    if isNull wsp
    then
      if isLinux
      then
        let usr = Security.Principal.WindowsIdentity.GetCurrent().Name
        sprintf @"/home/%s/iris" usr
      else @"C:\\Iris\"
    else wsp

  /// Iris File Extension
  let IrisExt = ".iris"

  let workspaceExists () =
    Directory.Exists Workspace

  let createWorkspace () =
    if not <| workspaceExists()
    then Directory.CreateDirectory Workspace
         |> ignore

  let sanitizeName (name : string) =
    let regex = new Regex("(\.|\ |\*|\^)")
    if regex.IsMatch(name)
    then regex.Replace(name, "_")
    else name