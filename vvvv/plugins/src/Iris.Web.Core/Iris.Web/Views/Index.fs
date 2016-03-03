namespace Iris.Web.Views

open System
open System.IO

module Index =
  let listPlugins basepath =
    Directory.GetFiles(basepath, "*.js")
    |> Array.toSeq
    |> Seq.map (Path.GetFileName)
    |> Seq.filter(fun item -> not (item = "iris.js"))
    |> Seq.map (fun f -> sprintf @"<script src=""%s""></script>" ("js/" + f))
    |> String.concat Environment.NewLine

  let compileIndex pth =
    listPlugins pth
    |> sprintf
       @"
       <!doctype html>
       <html>
         <head>
           <title>Iris</title>
           <meta charset=""utf-8"">
         </head>
         <body>
           <script src=""dependencies/virtual-dom/dist/virtual-dom.js""></script>
           <script src=""dependencies/rxjs/dist/rx.all.js""></script>
           <script src=""dependencies/jquery/dist/jquery.js""></script>
           <script src=""dependencies/routie/dist/routie.js""></script>
           <script src=""dependencies/fabric.js/dist/fabric.js""></script>
           %s
           <script src=""Iris.Web.js""></script>
         </body>
       </html>
       "