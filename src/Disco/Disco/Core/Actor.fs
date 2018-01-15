(*
 *  This file is part of Distributed Show Control
 *
 *  Copyright 2015, 2018 by it's authors.
 *  Some rights reserved. See COPYING, AUTHORS.
 *)

namespace Disco.Core

open System
open System.Threading

type IActor<'a> =
  inherit IDisposable
  abstract Post: 'a -> unit
  abstract Start: unit -> unit
  abstract CurrentQueueLength: int

// * Actor

module Actor =

  // ** tag

  let private tag (str: string) = String.format "MailboxProcessor.{0}" str

  // ** warnQueueLength

  let private warnQueueLength t (inbox: IActor<_>) =
    // wa't when 't :> rn if the queue length surpasses threshold
    let count = inbox.CurrentQueueLength
    if count > Constants.QUEUE_LENGTH_THRESHOLD then
      count
      |> String.format "Queue length threshold was reached: {0}"
      |> printfn "WARNING: %s"

  // ** loop

  let private loop<'a> actor (f: IActor<'a> -> 'a -> Async<unit>) (inbox: MailboxProcessor<'a>) =
    let rec _loop () =
      async {
        let! msg = inbox.Receive()
        do! f actor msg
        do warnQueueLength "tag" actor
        return! _loop ()
      }
    _loop ()

  // ** create

  let create<'a> (f: IActor<'a> -> 'a -> Async<unit>) =
    let cts = new CancellationTokenSource()
    let mutable mbp = Unchecked.defaultof<MailboxProcessor<'a>>
    { new IActor<'a> with
      member actor.Start () =
        mbp <- MailboxProcessor.Start(loop<'a> actor f, cts.Token)
        mbp.Error.Add(sprintf "unhandled error on loop: %O" >> printfn "IActor: %s")
      member actor.Post value = try mbp.Post value with _ -> ()
      member actor.CurrentQueueLength = try mbp.CurrentQueueLength with _ -> 0
      member actor.Dispose () = cts.Cancel() }
