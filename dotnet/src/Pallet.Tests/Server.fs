namespace Pallet.Tests

open System
open System.Net
open Fuchu
open Fuchu.Test
open Pallet.Core

[<AutoOpen>]
module Server =
  ////////////////////////////////////////
  //  ____                              //
  // / ___|  ___ _ ____   _____ _ __    //
  // \___ \ / _ \ '__\ \ / / _ \ '__|   //
  //  ___) |  __/ |   \ V /  __/ |      //
  // |____/ \___|_|    \_/ \___|_|      //
  ////////////////////////////////////////

  let server_voted_for_records_who_we_voted_for =
    testCase "Raft server voted for records who we voted for" <| fun _ ->
      let id1 = RaftId.Create()
      raft {
         do! expectM  "Should one node" 1UL numNodes
         do! addNodeM (Node.create id1 ())
         do! expectM  "Should two nodes" 2UL numNodes

         let! node = getNodeM id1
         do! voteFor node

         do! expectM "Should have voted for last id" id1 (votedFor >> Option.get)
      }
      |> runWithDefaults
      |> noError

  let server_idx_starts_at_one =
    testCase "Raft server index should start at 1" <| fun _ ->
      raft {
         do! expectM "Should have default idx" 0UL currentIndex
         do! createEntryM () >>= ignoreM
         do! expectM "Should have current idx" 1UL currentIndex
         do! createEntryM () >>= ignoreM
         do! expectM "Should have current idx" 2UL currentIndex
         do! createEntryM () >>= ignoreM
         do! expectM "Should have current idx" 3UL currentIndex
      }
      |> runWithDefaults
      |> noError

  let server_currentterm_defaults_to_zero =
    testCase "Raft server current Term should default to zero" <| fun _ ->
      raft {
        do! expectM "Should be Zero" 0UL currentTerm
      }
      |> runWithDefaults
      |> noError

  let server_set_currentterm_sets_term =
    testCase "Raft server set term sets term" <| fun _ ->
      raft {
        do! setTermM 5UL
        do! expectM "Should be correct term" 5UL currentTerm
      }
      |> runWithDefaults
      |> noError

  let server_voting_results_in_voting =
    testCase "Raft server voting should set voted for" <| fun _ ->
      let node1 = Node.create (RaftId.Create()) ()
      let node2 = Node.create (RaftId.Create()) ()

      raft {
        // add node and vote for it
        do! addNodeM node1
        do! voteFor (Some node1)
        do! expectM "should be correct id" node1.Id (votedFor >> Option.get)
        do! addNodeM node2
        do! voteFor (Some node2)
        do! expectM "should be correct id" node2.Id (votedFor >> Option.get)
      }
      |> runWithDefaults
      |> noError

  let server_add_node_makes_non_voting_node_voting =
    testCase "Raft add node now makes non-voting node voting" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      raft {
        do! addNonVotingNodeM node
        let! peer = getNodeM node.Id
        expect "Non-voting node should not be voting" false Node.isVoting (Option.get peer)
        do! addNodeM node
        let! peer = getNodeM node.Id
        expect "Node should be voting" true Node.isVoting (Option.get peer)
        do! expectM "Should have two nodes (incl. self)" 2UL numNodes
      }
      |> runWithDefaults
      |> noError

  let server_remove_node =
    testCase "Raft remove node should set correct node count" <| fun _ ->
      let node1 = Node.create (RaftId.Create()) ()
      let node2 = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM node1
        do! expectM "Should have Node count of two" 2UL numNodes
        do! addNodeM node2
        do! expectM "Should have Node count of three" 3UL numNodes
        do! removeNodeM node1
        do! expectM "Should have Node count of two" 2UL numNodes
        do! removeNodeM node2
        do! expectM "Should have Node count of one" 1UL numNodes
      }
      |> runWithDefaults
      |> noError

  let server_election_start_increments_term =
    testCase "Raft election increments current term" <| fun _ ->
      raft {
        do! setTermM 2UL
        do! startElection ()
        do! expectM "Raft should have correct term" 3UL currentTerm
      }
      |> runWithDefaults
      |> noError


  let server_set_state =
    testCase "Raft set state should set supplied state" <| fun _ ->
      raft {
        do! setStateM Leader
        do! expectM "Raft should be leader now" Leader state
      }
      |> runWithDefaults
      |> noError

  let server_starts_as_follower =
    testCase "Raft starts as follower" <| fun _ ->
      raft {
        do! expectM "Raft state should be Follower" Follower state
      }
      |> runWithDefaults
      |> noError

  let server_starts_with_election_timeout_of_1000m =
    testCase "Raft should start with election timeout of 1000ms" <| fun _ ->
      raft {
        do! expectM "Raft election timeout should be 1000ms" 1000UL electionTimeout
      }
      |> runWithDefaults
      |> noError

  let server_starts_with_request_timeout_of_200ms =
    testCase "Raft should start with request timeout of 200ms" <| fun _ ->
      raft {
        do! expectM "Raft request timeout should be 200ms" 200UL requestTimeout
      }
      |> runWithDefaults
      |> noError

  let server_append_entry_is_retrievable =
    testCase "Raft should be able to retrieve entry and data by index" <| fun _ ->
      let msg1 = "default state"
      let msg2 = "add some state"
      let msg3 = "add some more state"

      let init = Raft.create (Node.create (RaftId.Create()) "one")
      let cbs = mk_cbs (ref "hi") :> IRaftCallbacks<_,_>

      raft {
        do! setStateM Candidate
        do! setTermM 5UL

        do! createEntryM msg2 >>= ignoreM
        let! entry = getEntryAtM 1UL
        match Option.get entry with
          | LogEntry(_,_,_,data,_) ->
            Assert.Equal("Should have correct contents", msg2, data)
          | _ -> failwith "Should be a Log"

        do! createEntryM msg3 >>= ignoreM
        let! entry = getEntryAtM 2UL
        match Option.get entry with
          | LogEntry(_,_,_,data,_) ->
            Assert.Equal("Should have correct contents", msg3, data)
          | _ -> failwith "Should be a Log"
      }
      |> runWithRaft init cbs
      |> noError

  let server_wont_apply_entry_if_we_dont_have_entry_to_apply =
    testCase "Raft won't apply entry if we don't have entry to apply" <| fun _ ->
      raft {
        do! setCommitIndexM 0UL
        do! setLastAppliedIdxM 0UL
        do! applyEntries ()
        do! expectM "Last applied index should be zero" 0UL lastAppliedIdx
        do! expectM "Last commit index should be zero"  0UL commitIndex
      }
      |> runWithDefaults
      |> noError

  let server_wont_apply_entry_if_there_isnt_a_majority =
    testCase "Raft won't apply a change if the is not a majority" <| fun _ ->
      let nodes = // create 5 nodes
        Array.map (fun n -> Node.create (RaftId.Create()) ()) [|1UL..5UL|]

      raft {
        do! setCommitIndexM 0UL
        do! setLastAppliedIdxM 0UL
        do! addNodesM nodes
        do! applyEntries ()
        do! expectM "Should not have incremented last applied index" 0UL lastAppliedIdx
        do! expectM "Should not have incremented commit index" 0UL commitIndex
        do! createEntryM () >>= ignoreM
        do! applyEntries () >>= ignoreM
        do! expectM "fhould not have incremented last applied index" 0UL lastAppliedIdx
        do! expectM "Should not have incremented commit index" 0UL commitIndex
      }
      |> runWithDefaults
      |> noError


  let server_increment_lastApplied_when_lastApplied_lt_commitidx =
    testCase "Raft increment lastApplied when lastApplied lt commitidx" <| fun _ ->
      raft {
        do! setStateM Follower
        do! setTermM 1UL
        do! setLastAppliedIdxM 0UL
        do! createEntryM () >>= ignoreM
        do! setCommitIndexM 1UL
        do! periodic 1UL
        do! expectM "1) Last applied index should be one" 1UL lastAppliedIdx
      }
      |> runWithDefaults
      |> noError

  let server_apply_entry_increments_last_applied_idx =
    testCase "Raft applyEntry increments LastAppliedIndex" <| fun _ ->
      raft {
        do! setLastAppliedIdxM 0UL
        do! createEntryM () >>= ignoreM
        do! setCommitIndexM 1UL
        do! applyEntries ()
        do! expectM "2) Last applied index should be one" 1UL lastAppliedIdx
      }
      |> runWithDefaults
      |> noError

  let server_periodic_elapses_election_timeout =
    testCase "Raft Periodic elapses election timeout" <| fun _ ->
      raft {
        do! setElectionTimeoutM 1000UL
        do! expectM "Timeout elapsed should be zero" 0UL timeoutElapsed
        do! periodic 0UL
        do! expectM "Timeout elapsed should be zero" 0UL timeoutElapsed
        do! periodic 100UL
        do! expectM "Timeout elapsed should be 100" 100UL timeoutElapsed
      }
      |> runWithDefaults
      |> noError

  let server_election_timeout_does_no_promote_us_to_leader_if_there_is_only_1_node =
    testCase "Election timeout does not promote us to leader if there is only 1 node" <| fun _ ->
      raft {
        do! addNodeM (Node.create (RaftId.Create()) ())
        do! setElectionTimeoutM 1000UL
        do! periodic 1001UL
        do! expectM "Should not be Leader" false isLeader
      }
      |> runWithDefaults
      |> noError

  let server_recv_entry_auto_commits_if_we_are_the_only_node =
    testCase "Receive entry auto-commits if we are the only node" <| fun _ ->
      let entry = LogEntry(RaftId.Create(),0UL,0UL,(),None)
      raft {
        do! setElectionTimeoutM 1000UL
        do! becomeLeader ()
        do! expectM "Should have commit idx 0UL" 0UL commitIndex

        let! result = receiveEntry entry

        do! expectM "Should have log count 1UL" 1UL numLogs
        do! expectM "Should have commit idx 1UL" 1UL commitIndex
      }
      |> runWithDefaults
      |> noError

  let server_recv_entry_fails_if_there_is_already_a_voting_change =
    testCase "Receive entry fails if there is already a voting change" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()
      let mklog term =
        JointConsensus(RaftId.Create(), 1UL, term, [| NodeAdded(node) |] , None)

      raft {
        do! setElectionTimeoutM 1000UL
        do! becomeLeader ()
        do! expectM "Should have commit idx of zero" 0UL commitIndex

        let! term = currentTermM ()
        let! result = receiveEntry (mklog term)

        do! expectM "Should have log count of one" 1UL numLogs

        let! term = currentTermM ()
        return! receiveEntry (mklog term)
      }
      |> runWithDefaults
      |> expectError UnexpectedVotingChange

  let server_recv_entry_adds_missing_node_on_addnode =
    testCase "recv entry adds missing node on addnode" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()
      let mklog term =
        JointConsensus(RaftId.Create(), 1UL, term, [| NodeAdded(node) |] , None)

      raft {
        do! setElectionTimeoutM 1000UL
        do! becomeLeader ()
        do! expectM "Should have commit idx of zero" 0UL commitIndex
        do! expectM "Should have node count of one" 1UL numNodes

        let! term = currentTermM ()
        let! result = receiveEntry (mklog term)

        do! expectM "Should have node count of two" 2UL numNodes
      }
      |> runWithDefaults
      |> noError

  let server_recv_entry_added_node_should_be_nonvoting =
    testCase "recv entry adds missing node on addnode" <| fun _ ->
      let nid = RaftId.Create()
      let node = Node.create nid ()
      let mklog term =
        JointConsensus(RaftId.Create(), 1UL, term, [| NodeAdded(node) |] , None)

      raft {
        do! setElectionTimeoutM 1000UL
        do! becomeLeader ()
        do! expectM "Should have commit idx of zero" 0UL commitIndex
        do! expectM "Should have node count of one" 1UL numNodes

        let! term = currentTermM ()
        let! result = receiveEntry (mklog term)

        do! expectM "Should be non-voting node for start" false (getNode nid >> Option.get >> Node.isVoting)
      }
      |> runWithDefaults
      |> noError

  let server_recv_entry_removes_node_on_removenode =
    testCase "recv entry removes node on removenode" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()
      let mklog term =
        JointConsensus(RaftId.Create(), 1UL, term, [| NodeRemoved node |] , None)

      raft {
        do! setElectionTimeoutM 1000UL
        do! becomeLeader ()
        do! addNodeM node
        do! expectM "Should have node count of two" 2UL numNodes

        let! term = currentTermM ()
        let! result = receiveEntry (mklog term)

        do! expectM "Should have node count of one" 1UL numNodes
      }
      |> runWithDefaults
      |> noError

  let server_added_node_should_become_voting_once_it_caught_up =
    testCase "recv entry adds missing node on addnode" <| fun _ ->
      let nid2 = RaftId.Create()
      let node = Node.create nid2 ()
      let mklog term =
        JointConsensus(RaftId.Create(), 1UL, term, [| NodeAdded(node) |] , None)

      let state = Raft.create (Node.create (RaftId.Create()) ())
      let count = ref 0
      let cbs = { mk_cbs (ref ()) with
                    HasSufficientLogs = fun _ -> count := 1 + !count }
                  :> IRaftCallbacks<_,_>
      raft {
        do! setElectionTimeoutM 1000UL
        do! becomeLeader ()
        do! expectM "Should have commit idx of zero" 0UL commitIndex
        do! expectM "Should have node count of one" 1UL numNodes

        let! term = currentTermM ()

        let! one = receiveEntry (Log.make term ())
        let! two = receiveEntry (Log.make term ())

        let! r1 = responseCommitted one
        let! r2 = responseCommitted two

        expect "'r1' should be committed" true id r1
        expect "'r2' should be committed" true id r2

        let! result = receiveEntry (mklog term)
        let! r3 = responseCommitted result

        expect "'r3' should not be committed" false id r3

        let! three = receiveEntry (Log.make term ())
        let! four  = receiveEntry (Log.make term ())

        let! r4 = responseCommitted three
        let! r5 = responseCommitted four

        expect "'r4' should not be committed" false id r4
        expect "'r5' should not be committed" false id r5

        do! expectM "Should be non-voting node for start" false (getNode nid2 >> Option.get >> Node.isVoting)
        do! expectM "Should be in joining state for start" Joining (getNode nid2 >> Option.get >> Node.getState)

        let response = { Term = 0UL; Success = true; CurrentIndex = 5UL; FirstIndex = 1UL }
        do! receiveAppendEntriesResponse nid2 response

        let! r6 = responseCommitted result
        let! r7 = responseCommitted three
        let! r8 = responseCommitted four

        expect "'r6' should be committed" true id r6
        expect "'r7' should be committed" true id r7
        expect "'r8' should be committed" true id r8

        expect "should have called the 'hassufficientlogs' callback" 1 id !count

        do! expectM "Should be voting node now" true (getNode nid2 >> Option.get >> Node.isVoting)
        do! expectM "Should be in running state now" Running (getNode nid2 >> Option.get >> Node.getState)
      }
      |> runWithRaft state cbs
      |> noError

  let server_cfg_sets_num_nodes =
    testCase "Configuration sets the number of nodes counter" <| fun _ ->
      let count = 12UL

      let flip f b a = f b a
      let nodes =
        List.map (fun n -> Node.create (RaftId.Create()) ()) [1UL..count]

      raft {
        for node in nodes do
          do! addNodeM node
        do! expectM "Should have 13 nodes now" 13UL numNodes
      }
      |> runWithDefaults
      |> noError

  let server_votes_are_majority_is_true =
    testCase "Vote are majority is majority" <| fun _ ->
      majority 3UL 1UL
      |> expect "1) Should not be a majority" false id

      majority 3UL 2UL
      |> expect "2) Should be a majority" true id

      majority 5UL 2UL
      |> expect "3) Should not be a majority" false id

      majority 5UL 3UL
      |> expect "4) Should be a majority" true id

      majority 1UL 2UL
      |> expect "5) Should not be a majority" false id

      majority 4UL 2UL
      |> expect "6) Should not be a majority" false id

  let recv_requestvote_response_dont_increase_votes_for_me_when_not_granted =
    testCase "Receive vote response does not increase votes for me when not granted" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM node
        do! setTermM 1UL
        do! setStateM Candidate
        do! expectM "Votes for me should be zero" 0UL numVotesForMe

        let! term = currentTermM ()
        let response = { Term = term; Granted = false; Reason = Some NoError }
        let! result = receiveVoteResponse node.Id response
        do! expectM "Votes for me should be zero" 0UL numVotesForMe
      }
      |> runWithDefaults
      |> noError

  let recv_requestvote_response_dont_increase_votes_for_me_when_term_is_not_equal =
    testCase "Recv requestvote response does not increase votes for me when term is not equal" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM node
        do! setTermM 3UL
        do! setStateM Candidate
        do! expectM "Should have zero votes for me" 0UL numVotesForMe

        let response = { Term = 2UL; Granted = true; Reason = None }
        return! receiveVoteResponse node.Id response
      }
      |> runWithDefaults
      |> expectError VoteTermMismatch

  let recv_requestvote_response_increase_votes_for_me =
    testCase "Recv requestvote response increase votes for me" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()
      raft {
        do! addNodeM node
        do! setTermM 1UL
        do! expectM "Should have zero votes for me" 0UL numVotesForMe
        do! becomeCandidate ()
        do! receiveVoteResponse node.Id { Term = 2UL; Granted = true; Reason = None }
        do! expectM "Should have two votes for me" 2UL numVotesForMe
      }
      |> runWithDefaults
      |> noError

  let recv_requestvote_response_must_be_candidate_to_receive =
    testCase "recv requestvote response must be candidate to receive" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM node
        do! setTermM 1UL
        let response = { Term = 1UL; Granted = true; Reason = None }
        do! receiveVoteResponse node.Id response
      }
      |> runWithDefaults
      |> expectError NotCandidate

  let recv_requestvote_fails_if_term_less_than_current_term =
    testCase "recv requestvote fails if term less than current term" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM node
        do! setTermM 3UL
        do! becomeCandidate ()
        let! response = receiveVoteResponse node.Id { Term = 3UL; Granted = true; Reason = None }
        do! expectM "Should have term 4" 4UL currentTerm
      }
      |> runWithDefaults
      |> expectError VoteTermMismatch

  ////////////////////////////////////////////////////////////////////////////////////
  //  ____  _                 _     _  ____                 _ __     __    _        //
  // / ___|| |__   ___  _   _| | __| |/ ___|_ __ __ _ _ __ | |\ \   / /__ | |_ ___  //
  // \___ \| '_ \ / _ \| | | | |/ _` | |  _| '__/ _` | '_ \| __\ \ / / _ \| __/ _ \ //
  //  ___) | | | | (_) | |_| | | (_| | |_| | | | (_| | | | | |_ \ V / (_) | ||  __/ //
  // |____/|_| |_|\___/ \__,_|_|\__,_|\____|_|  \__,_|_| |_|\__| \_/ \___/ \__\___| //
  ////////////////////////////////////////////////////////////////////////////////////

  let shouldgrantvote_vote_term_too_small =
    testCase "grantVote should be false when vote term too small" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      let vote =
        { Term = 1UL
        ; Candidate = node
        ; LastLogIndex = 1UL
        ; LastLogTerm = 1UL
        }

      raft {
        do! setTermM 2UL
        let! (res,_) = shouldGrantVote vote
        expect "Should not grant vote" false id res
      }
      |> runWithDefaults
      |> noError


  let shouldgrantvote_alredy_voted =
    testCase "grantVote should be false when already voted" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      let vote =
        { Term = 2UL
        ; Candidate = node
        ; LastLogIndex = 1UL
        ; LastLogTerm = 1UL
        }

      raft {
        do! setTermM 2UL
        do! voteForMyself ()
        let! (res,_) = shouldGrantVote vote
        expect "Should not grant vote" false id res
      }
      |> runWithDefaults
      |> noError

  let shouldgrantvote_log_empty =
    testCase "grantVote should be true when log is empty" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      let vote =
        { Term = 1UL
        ; Candidate = node
        ; LastLogIndex = 1UL
        ; LastLogTerm = 1UL
        }

      raft {
        do! addNodeM node
        do! setTermM 1UL
        do! voteFor None
        do! expectM "Should have currentIndex zero" 0UL currentIndex
        do! expectM "Should have voted for nobody" None votedFor
        let! (res,_) = shouldGrantVote vote
        expect "Should grant vote" true id res
      }
      |> runWithDefaults
      |> noError

  let shouldgrantvote_raft_log_term_smaller_vote_logterm =
    testCase "grantVote should be true if last raft log term is smaller than vote last log term " <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      let vote =
        { Term = 2UL
        ; Candidate = node
        ; LastLogIndex = 1UL
        ; LastLogTerm = 2UL
        }

      raft {
        do! addNodeM node
        do! setTermM 1UL
        do! voteFor None
        do! expectM "Should have currentIndex zero" 0UL currentIndex
        do! createEntryM () >>= ignoreM
        do! createEntryM () >>= ignoreM
        do! expectM "Should have currentIndex one" 2UL currentIndex
        let! (res,_) = shouldGrantVote vote
        expect "Should grant vote" true id res
      }
      |> runWithDefaults
      |> noError

  let shouldgrantvote_raft_last_log_valid =
    testCase "grantVote should be true if last raft log is valid" <| fun _ ->
      let node = Node.create (RaftId.Create()) ()

      let vote =
        { Term = 2UL
        ; Candidate = node
        ; LastLogIndex = 3UL
        ; LastLogTerm = 2UL
        }

      raft {
        do! addNodeM node
        do! setTermM 2UL
        do! voteFor None
        do! expectM "Should have currentIndex zero" 0UL currentIndex
        do! createEntryM () >>= ignoreM
        do! createEntryM () >>= ignoreM
        do! expectM "Should have currentIndex one" 2UL currentIndex
        let! (res,_) = shouldGrantVote vote
        expect "Should grant vote" true id res
      }
      |> runWithDefaults
      |> noError

  let leader_recv_requestvote_does_not_step_down =
    testCase "leader recv requestvote does not step down" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM peer
        do! setTermM 1UL
        do! voteForMyself ()
        do! becomeLeader ()
        do! expectM "Should be leader" Leader state
        let request =
          { Term = 1UL
          ; Candidate = peer
          ; LastLogIndex = 1UL
          ; LastLogTerm = 1UL
          }
        let! resp = receiveVoteRequest peer.Id request
        do! expectM "Should be leader" Leader state
      }
      |> runWithDefaults
      |> noError


  let recv_requestvote_reply_true_if_term_greater_than_or_equal_to_current_term =
    testCase "recv requestvote reply true if term greater than or equal to current term" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM peer
        do! setTermM 1UL
        let request =
          { Term = 2UL
          ; Candidate = peer
          ; LastLogIndex = 1UL
          ; LastLogTerm = 1UL
          }
        let! resp = receiveVoteRequest peer.Id request
        expect "Should be granted" true Vote.granted resp
      }
      |> runWithDefaults
      |> noError

  let recv_requestvote_reset_timeout =
    testCase "recv requestvote reset timeout" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM peer
        do! setTermM 1UL
        do! setElectionTimeoutM 1000UL
        do! periodic 900UL
        let request =
          { Term = 2UL
          ; Candidate = peer
          ; LastLogIndex = 1UL
          ; LastLogTerm = 1UL
          }
        let! resp = receiveVoteRequest peer.Id request
        expect "Vote should be granted" true Vote.granted resp
        do! expectM "Timeout Elapsed should be reset" 0UL timeoutElapsed
      }
      |> runWithDefaults
      |> noError

  let recv_requestvote_candidate_step_down_if_term_is_higher_than_current_term =
    testCase "recv requestvote candidate step down if term is higher than current term" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()

      raft {
        do! addNodeM peer
        do! becomeCandidate ()
        do! setTermM 1UL
        do! expectM "Should have voted for myself" true votedForMyself
        do! expectM "Should have term 1" 1UL currentTerm
        let request =
          { Term = 2UL
          ; Candidate = peer
          ; LastLogIndex = 1UL
          ; LastLogTerm = 1UL
          }
        let! resp = receiveVoteRequest peer.Id request
        do! expectM "Should now be Follower" Follower state
        do! expectM "Should have term 2" 2UL currentTerm
        do! expectM "Should have voted for peer" peer.Id (votedFor >> Option.get)
      }
      |> runWithDefaults
      |> noError

  let recv_requestvote_add_unknown_candidate =
    testCase "recv_requestvote_adds_candidate" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let other = Node.create (RaftId.Create())()

      raft {
        do! addNodeM peer
        do! becomeCandidate ()
        do! setTermM 1UL
        do! expectM "Should have voted for myself" true votedForMyself
        let request =
          { Term = 2UL
          ; Candidate = other
          ; LastLogIndex = 1UL
          ; LastLogTerm = 1UL
          }
        let! resp = receiveVoteRequest other.Id request
        do! expectM "Should have added node" None (getNode other.Id)
        expect "Should not have granted vote" false Vote.granted resp
      }
      |> runWithDefaults
      |> noError

  let recv_requestvote_dont_grant_vote_if_we_didnt_vote_for_this_candidate =
    testCase "recv_requestvote_dont_grant_vote_if_we_didnt_vote_for_this_candidate" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) ()
      let peer2 = Node.create (RaftId.Create()) ()
      let request =
        { Term = 1UL
        ; Candidate = peer1
        ; LastLogIndex = 1UL
        ; LastLogTerm = 1UL
        }

      raft {
        do! addNodesM [| peer1; peer2 |]
        do! setTermM 1UL
        do! voteForMyself ()
        do! setTermM 1UL
        do! expectM "Should have voted for myself" true votedForMyself
        do! expectM "Should have 3 nodes" 3UL numNodes

        let! raft' = get
        let req1 = { request with Candidate = raft'.Node }

        let! result = receiveVoteRequest peer2.Id req1
        expect "Should not have granted vote" false Vote.granted result
      }
      |> runWithDefaults
      |> noError

  let follower_becomes_follower_is_follower =
    testCase "follower becomes follower is follower" <| fun _ ->
      raft {
        do! becomeLeader ()
        do! expectM "Should be leader now" Leader state
        do! becomeFollower ()
        do! expectM "Should be follower now" Follower state
      }
      |> runWithDefaults
      |> noError

  let follower_becomes_follower_does_not_clear_voted_for =
    testCase "follower becomes follower does not clear voted for" <| fun _ ->
      raft {
        do! voteForMyself ()
        do! expectM "Should have voted for myself" true votedForMyself
        do! becomeFollower ()
        do! expectM "Should have voted for myself" true votedForMyself
      }
      |> runWithDefaults
      |> noError

  let candidate_becomes_candidate_is_candidate =
    testCase "candidate becomes candidate is candidate" <| fun _ ->
      raft {
        do! becomeCandidate ()
        do! expectM "Should be Candidate" true isCandidate
      }
      |> runWithDefaults
      |> noError

  let candidate_election_timeout_and_no_leader_results_in_new_election =
    testCase "candidate election timeout and no leader results in new election"  <| fun _ ->
      // When the election timeout is reached and we didn't get enougth votes to
      // become leader yet, periodic is expected to re-start the elections (and
      // thereby increasing the term again).
      let peer = Node.create (RaftId.Create()) ()
      raft {
        do! addNodeM peer
        do! setElectionTimeoutM 1000UL
        do! expectM "Should be at term zero" 0UL currentTerm
        do! becomeCandidate ()
        do! expectM "Should be at term one" 1UL currentTerm
        do! periodic 1001UL
        do! expectM "Should be at term two" 2UL currentTerm
      }
      |> runWithDefaults
      |> noError


  let follower_becomes_candidate_when_election_timeout_occurs =
    testCase "follower becomes candidate when election timeout occurs" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()

      raft {
        do! setElectionTimeoutM 1000UL
        do! addNodeM peer
        do! periodic 1001UL
        do! expectM "Should be candidate now" Candidate state
      }
      |> runWithDefaults
      |> noError


  let follower_dont_grant_vote_if_candidate_has_a_less_complete_log =
    testCase "follower dont grant vote if candidate has a less complete log" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let log1 = LogEntry(RaftId.Create(), 0UL, 1UL, (), None)
      let log2 = LogEntry(RaftId.Create(), 0UL, 2UL, (), None)

      raft {
        do! addPeerM peer
        do! setTermM 1UL
        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM

        let! raft' = get
        let vote : VoteRequest<_> =
          { Term = 1UL
          ; Candidate = raft'.Node
          ; LastLogIndex = 1UL
          ; LastLogTerm = 1UL
          }

        let! resp = receiveVoteRequest peer.Id vote
        expect "Should have failed" false id resp.Granted

        do! setTermM 2UL

        let! resp = receiveVoteRequest peer.Id { vote with Term = 2UL; LastLogTerm = 3UL; }
        expect "Should be granted" true Vote.granted resp
      }
      |> runWithDefaults
      |> noError

  let follower_becoming_candidate_increments_current_term =
    testCase "follower becoming candidate increments current term" <| fun _ ->
      raft {
        do! expectM "Should have term 0" 0UL currentTerm
        do! becomeCandidate ()
        do! expectM "Should have term 1" 1UL currentTerm
      }
      |> runWithDefaults
      |> noError

  let follower_becoming_candidate_votes_for_self =
    testCase "follower becoming candidate votes for self" <| fun _ ->
      raft {
        let peer = Node.create (RaftId.Create()) ()
        let! raft' = get
        do! addNodeM peer
        do! expectM "Should have no VotedFor" None votedFor
        do! becomeCandidate ()
        do! expectM "Should have voted for myself" (Some raft'.Node.Id) votedFor
        do! expectM "Should have one vote for me" 1UL numVotesForMe
      }
      |> runWithDefaults
      |> noError

  let follower_becoming_candidate_resets_election_timeout =
    testCase "follower becoming candidate resets election timeout" <| fun _ ->
      raft {
        do! setElectionTimeoutM 1000UL
        do! expectM "Should have zero elapsed timout" 0UL timeoutElapsed
        do! periodic 900UL
        do! expectM "Should have 900 elapsed timout" 900UL timeoutElapsed
        do! becomeCandidate ()
        do! expectM "Should have timeout elapsed below 1000" true (timeoutElapsed >> ((>) 1000UL))
      }
      |> runWithDefaults
      |> noError

  let follower_becoming_candidate_requests_votes_from_other_servers =
    testCase "follower becoming candidate requests votes from other servers" <| fun _ ->
      let peer0 = Node.create (RaftId.Create()) ()
      let peer1 = Node.create (RaftId.Create()) ()
      let peer2 = Node.create (RaftId.Create()) ()

      let raft' : Raft<unit,unit> = create peer0
      let mutable i = 0
      let cbs =
        { mk_cbs (ref ()) with SendRequestVote = fun _ _ -> i <- i + 1 }
        :> IRaftCallbacks<_,_>

      raft {
        do! addNodeM peer1
        do! addNodeM peer2
        do! setTermM 2UL
        do! becomeCandidate ()
        expect "Should have two vote requests" 2 id i
      }
      |> runWithRaft raft' cbs
      |> noError


  let candidate_receives_majority_of_votes_becomes_leader =
    testCase "candidate receives majority of votes becomes leader" <| fun _ ->
      let self  = Node.create (RaftId.Create()) ()
      let peer1 = Node.create (RaftId.Create()) ()
      let peer2 = Node.create (RaftId.Create()) ()
      let peer3 = Node.create (RaftId.Create()) ()
      let peer4 = Node.create (RaftId.Create()) ()

      raft {
        do! addPeersM [| peer1; peer2; peer3; peer4 |]
        do! expectM "Should have 5 nodes" 5UL numNodes
        do! becomeCandidate ()
        do! receiveVoteResponse peer1.Id { Term = 1UL; Granted = true; Reason = None }
        do! receiveVoteResponse peer2.Id { Term = 1UL; Granted = true; Reason = None }
        do! expectM "Should be leader" true isLeader
      }
      |> runWithDefaults
      |> noError

  let candidate_will_not_respond_to_voterequest_if_it_has_already_voted =
    testCase "candidate will not respond to voterequest if it has already voted" <| fun _ ->
      raft {
        let! raft' = get
        let peer = Node.create (RaftId.Create()) ()
        let vote : VoteRequest<unit> =
          { Term = 0UL                // term must be equal or lower that raft's
          ; Candidate = raft'.Node   // term for this to work
          ; LastLogIndex = 0UL
          ; LastLogTerm = 0UL
          }
        do! addPeerM peer
        do! voteFor (Some raft'.Node)
        let! resp = receiveVoteRequest peer.Id vote
        expect "Should have failed" true Vote.declined resp
      }
      |> runWithDefaults
      |> noError

  let candidate_requestvote_includes_logidx =
    testCase "candidate requestvote includes logidx" <| fun _ ->
      let self = Node.create (RaftId.Create()) "peer0"
      let raft' : Raft<string,string> = create self
      let sender = Sender.create
      let response = { Term = 5UL; Granted = true; Reason = None }
      let cbs =
        { mk_cbs (ref "yep") with SendRequestVote = senderRequestVote sender }
        :> IRaftCallbacks<_,_>

      raft {
        let peer1 = Node.create (RaftId.Create()) "peer1"
        let peer2 = Node.create (RaftId.Create()) "peer2"

        let log =
          LogEntry(RaftId.Create(),0UL, 3UL,  "three",
            Some <| LogEntry(RaftId.Create(),0UL, 1UL,  "two",
              Some <| LogEntry(RaftId.Create(),0UL, 1UL,  "one", None)))

        do! addPeersM [| peer1; peer2 |]
        do! setStateM Candidate
        do! setTermM 5UL
        do! appendEntryM log >>= ignoreM

        do! sendVoteRequest peer1
        do! receiveVoteResponse peer1.Id response

        let vote = List.head (!sender.Outbox) |> getVote

        expect "should have last log index be 3" 3UL Vote.lastLogIndex vote
        expect "should have last term be 5" 5UL Vote.term vote
        expect "should have last log term be 3" 3UL Vote.lastLogTerm vote
        expect "should have candidate id be me" self Vote.candidate vote
      }
      |> runWithRaft raft' cbs
      |> noError

  let candidate_recv_requestvote_response_becomes_follower_if_current_term_is_less_than_term =
    testCase "candidate recv requestvote response becomes follower if current term is less than term" <| fun _ ->
      raft {
        let peer = Node.create (RaftId.Create()) ()
        let response = { Term = 2UL ; Granted = false; Reason = None }
        do! addPeerM peer
        do! setTermM 1UL
        do! setStateM Candidate
        do! voteFor None
        do! expectM "Should not be follower" false isFollower
        do! expectM "Should not *have* a leader" None currentLeader
        do! expectM "Should have term 1" 1UL currentTerm
        do! receiveVoteResponse peer.Id response
        do! expectM "Should be Follower" Follower state
        do! expectM "Should have term 2" 2UL currentTerm
        do! expectM "Should have voted for nobody" None votedFor
      }
      |> runWithDefaults
      |> noError


  let candidate_recv_appendentries_frm_leader_results_in_follower =
    testCase "candidate recv appendentries frm leader results in follower" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let ae : AppendEntries<_,_> =
        { Term = 1UL
        ; PrevLogIdx = 0UL
        ; PrevLogTerm = 0UL
        ; LeaderCommit = 0UL
        ; Entries = None
        }

      raft {
        do! addPeerM peer
        do! setStateM Candidate
        do! voteFor None
        do! expectM "Should not be follower" false isFollower
        do! expectM "Should have no leader" None currentLeader
        do! expectM "Should have term 0UL" 0UL currentTerm
        let! resp = receiveAppendEntries (Some peer.Id) ae
        do! expectM "Should be follower" Follower state
        do! expectM "Should have peer as leader" (Some peer.Id) currentLeader
        do! expectM "Should have term 1" 1UL currentTerm
        do! expectM "Should have voted for noone" None votedFor
      }
      |> runWithDefaults
      |> noError

  let candidate_recv_appendentries_from_same_term_results_in_step_down =
    testCase "candidate recv appendentries from same term results in step down" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let ae : AppendEntries<_,_> =
        { Term = 2UL
        ; PrevLogIdx = 1UL
        ; PrevLogTerm = 1UL
        ; LeaderCommit = 0UL
        ; Entries = None
        }

      raft {
        do! addPeerM peer
        do! setTermM 2UL
        do! setStateM Candidate
        do! expectM "Should not be follower" false isFollower
        let! resp = receiveAppendEntries (Some peer.Id) ae
        do! expectM "Should not be candidate anymore" false isCandidate
      }
      |> runWithDefaults
      |> noError


  let leader_becomes_leader_is_leader =
    testCase "leader becomes leader is leader" <| fun _ ->
      raft {
        do! becomeLeader ()
        do! expectM "Should be leader" Leader state
      }
      |> runWithDefaults
      |> noError


  let leader_becomes_leader_does_not_clear_voted_for =
    testCase "leader becomes leader does not clear voted for" <| fun _ ->
      raft {
        let! raft' = get
        do! voteForMyself ()
        do! expectM "Should have voted for myself" (Some raft'.Node.Id) votedFor
        do! becomeLeader ()
        do! expectM "Should still have votedFor" (Some raft'.Node.Id) votedFor
      }
      |> runWithDefaults
      |> noError


  let leader_when_becomes_leader_all_nodes_have_nextidx_equal_to_lastlog_idx_plus_1 =
    testCase "leader when becomes leader all nodes have nextidx equal to lastlog idx plus 1" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) ()
      let peer2 = Node.create (RaftId.Create()) ()

      raft {
        do! addPeerM peer1
        do! addPeerM peer2
        do! setStateM Candidate
        do! becomeLeader ()
        let! raft' = get
        let cidx = currentIndex raft' + 1UL

        for peer in raft'.Peers do
          if peer.Value.Id <> raft'.Node.Id then
            expect "Should have correct nextIndex" cidx id peer.Value.NextIndex
      }
      |> runWithDefaults
      |> noError


  let leader_when_it_becomes_a_leader_sends_empty_appendentries =
    testCase "leader when it becomes a leader sends empty appendentries" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) "peer1"
      let peer2 = Node.create (RaftId.Create()) "peer2"

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "yep") with SendAppendEntries = senderAppendEntries sender}
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer1
        do! addPeerM peer2
        do! setStateM Candidate
        do! becomeLeader ()
        expect "Should have two messages" 2 List.length (!sender.Outbox)
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_responds_to_entry_msg_when_entry_is_committed =
    testCase "leader responds to entry msg when entry is committed" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let log = LogEntry(RaftId.Create(),0UL,0UL,(),None)

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! expectM "Should have log count 0UL" 0UL numLogs
        let! resp = receiveEntry log
        do! expectM "Should have log count 1UL" 1UL numLogs
        do! applyEntries ()
        let response = { Term = 0UL; Success = true; CurrentIndex = 1UL; FirstIndex = 1UL }
        do! receiveAppendEntriesResponse peer.Id response
        let! committed = responseCommitted resp
        expect "Should be committed" true id committed
      }
      |> runWithDefaults
      |> noError


  let non_leader_recv_entry_msg_fails =
    testCase "non leader recv entry msg fails" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let log = LogEntry(RaftId.Create(),0UL,0UL,(),None)

      raft {
        do! addNodeM peer
        do! setStateM Follower
        let! resp = receiveEntry log
        return "never reached"
      }
      |> runWithDefaults
      |> expectError NotLeader

  let leader_sends_appendentries_with_NextIdx_when_PrevIdx_gt_NextIdx =
    testCase "leader sends appendentries with NextIdx when PrevIdx gt NextIdx" <| fun _ ->
      let peer = { Node.create (RaftId.Create()) "peer" with NextIndex = 4UL }
      let raft' : Raft<string,string> = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let log = LogEntry(RaftId.Create(),0UL, 1UL,  "one", None)
      let cbs =
        { mk_cbs (ref "yep") with SendAppendEntries = senderAppendEntries sender}
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! sendAllAppendEntriesM ()
        expect "Should have one message in cue" 1 List.length (!sender.Outbox)
      }
      |> runWithRaft raft' cbs
      |> noError


  let leader_sends_appendentries_with_leader_commit =
    testCase "leader sends appendentries with leader commit" <| fun _ ->
      let peer = { Node.create (RaftId.Create()) "peer" with NextIndex = 4UL }
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "yep") with SendAppendEntries = senderAppendEntries sender}
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer
        do! setStateM Leader

        for n in 0 .. 9 do
          let l = LogEntry(RaftId.Create(), 0UL, 1UL, string n, None)
          do! appendEntryM l >>= ignoreM

        do! setCommitIndexM 10UL
        do! sendAllAppendEntriesM ()

        (!sender.Outbox)
        |> List.head
        |> getAppendEntries
        |> expect "Should have leader commit 10UL" 10UL (fun ae -> ae.LeaderCommit)
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_sends_appendentries_with_prevLogIdx =
    testCase "leader sends appendentries with prevLogIdx" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "yep") with SendAppendEntries = senderAppendEntries sender}
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! sendAppendEntry peer

        (!sender.Outbox)
        |> List.head
        |> getAppendEntries
        |> expect "Should have PrevLogIndex 0" 0UL (fun ae -> ae.PrevLogIdx)

        let log = LogEntry(RaftId.Create(),0UL,2UL,"yeah",None)

        do! appendEntryM log >>= ignoreM
        do! setNextIndexM peer.Id 1UL

        let! peer = getNodeM peer.Id >>= (Option.get >> returnM)
        do! sendAppendEntry peer

        (!sender.Outbox)
        |> List.head
        |> getAppendEntries
        |> assume "Should have PrevLogIdx 0" 0UL (fun ae -> ae.PrevLogIdx)
        |> assume "Should have one entry" 1UL (fun ae -> ae.Entries |> Option.get |> Log.depth )
        |> assume "Should have entry with correct id" (Log.id log) (fun ae -> ae.Entries |> Option.get |> Log.id)
        |> expect "Should have entry with term" 2UL (fun ae -> ae.Entries |> Option.get |> Log.entryTerm)

        sender.Outbox := List.empty // reset outbox

        do! setNextIndexM peer.Id 2UL
        let! peer = getNodeM peer.Id >>= (Option.get >> returnM)
        do! sendAppendEntry peer

        (!sender.Outbox)
        |> List.head
        |> getAppendEntries
        |> expect "Should have PrevLogIdx 1" 1UL (fun ae -> ae.PrevLogIdx)
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_sends_appendentries_when_node_has_next_idx_of_0 =
    testCase "leader sends appendentries when node has next idx of 0" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "hey") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! sendAppendEntry peer

        (!sender.Outbox)
        |> List.head
        |> getAppendEntries
        |> expect "Should have PrevLogIdx 0" 0UL (fun ae -> ae.PrevLogIdx)

        sender.Outbox := List.empty // reset outbox

        let log = LogEntry(RaftId.Create(),0UL,1UL,"Hm ja", None)

        do! setNextIndexM peer.Id 1UL
        do! appendEntryM log >>= ignoreM
        do! sendAppendEntry peer

        (!sender.Outbox)
        |> List.head
        |> getAppendEntries
        |> expect "Should have PrevLogIdx 0" 0UL (fun ae -> ae.PrevLogIdx)
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_retries_appendentries_with_decremented_NextIdx_log_inconsistency =
    testCase "leader retries appendentries with decremented NextIdx log inconsistency" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "ohai") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! sendAppendEntry peer

        (!sender.Outbox)
        |> expect "Should have a message" 1 List.length
      }
      |> runWithRaft raft' cbs
      |> noError


  let leader_append_entry_to_log_increases_idxno =
    testCase "leader append entry to log increases idxno" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "other"
      let log = LogEntry(RaftId.Create(),0UL,1UL,"entry",None)
      let raft' = defaultServer "local"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "no!") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! expectM "Should have zero logs" 0UL numLogs
        let! resp = receiveEntry log
        do! expectM "Should have on log" 1UL numLogs
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_recv_appendentries_response_increase_commit_idx_when_majority_have_entry_and_atleast_one_newer_entry =
    testCase "leader recv appendentries response increase commit idx when majority have entry and atleast one newer entry" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) "peer 1"
      let peer2 = Node.create (RaftId.Create()) "peer 2"
      let peer3 = Node.create (RaftId.Create()) "peer 3"
      let peer4 = Node.create (RaftId.Create()) "peer 4"

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "yep") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log1 = LogEntry(RaftId.Create(),0UL,1UL,"one",None)
      let log2 = LogEntry(RaftId.Create(),0UL,1UL,"two",None)
      let log3 = LogEntry(RaftId.Create(),0UL,1UL,"three",None)

      let response =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 3UL
        ; FirstIndex = 1UL
        }

      raft {
        do! addNodesM [| peer1; peer2; peer3; peer4; |]
        do! setStateM Leader
        do! setTermM 1UL
        do! setCommitIndexM 0UL
        do! setLastAppliedIdxM 0UL
        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM
        do! appendEntryM log3 >>= ignoreM

        do! sendAppendEntry peer1
        do! sendAppendEntry peer2

        do! receiveAppendEntriesResponse peer1.Id response
        // first response, no majority yet, will not set commit idx
        do! expectM "Should have commit index 0" 0UL commitIndex

        do! receiveAppendEntriesResponse peer2.Id response
        //  leader will now have majority followers who have appended this log
        do! expectM "Should have commit index 3" 3UL commitIndex

        do! expectM "Should have last applied index 0" 0UL lastAppliedIdx
        do! periodic 1UL
        // should have now applied all committed ertries
        do! expectM "Should have last applied index 3" 3UL lastAppliedIdx
      }
      |> runWithRaft raft' cbs
      |> noError


  let leader_recv_appendentries_response_duplicate_does_not_decrement_match_idx =
    testCase "leader recv appendentries response duplicate does not decrement match idx" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) "peer 1"
      let peer2 = Node.create (RaftId.Create()) "peer 2"

      let response =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 1UL
        ; FirstIndex = 1UL
        }

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs = mk_cbs (ref "awyea") :> IRaftCallbacks<_,_>

      let log1 = LogEntry(RaftId.Create(),0UL,1UL,"one",None)
      let log2 = LogEntry(RaftId.Create(),0UL,1UL,"two",None)
      let log3 = LogEntry(RaftId.Create(),0UL,1UL,"three",None)

      raft {
        do! addNodesM [| peer1; peer2; |]
        do! setStateM Leader
        do! setTermM 1UL
        do! setCommitIndexM 0UL
        do! setLastAppliedIdxM 0UL
        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM
        do! appendEntryM log3 >>= ignoreM
        do! sendAllAppendEntriesM ()
        do! receiveAppendEntriesResponse peer1.Id response
        do! receiveAppendEntriesResponse peer2.Id response
        do! expectM "Should have matchIdx 1" 1UL (getNode peer1.Id >> Option.get >> Node.getMatchIndex)
        do! receiveAppendEntriesResponse peer1.Id response
        do! expectM "Should still have matchIdx 1" 1UL (getNode peer1.Id >> Option.get >> Node.getMatchIndex)
      }
      |> runWithRaft raft' cbs
      |> expectError StaleResponse

  let leader_recv_appendentries_response_do_not_increase_commit_idx_because_of_old_terms_with_majority =
    testCase "leader recv appendentries response do not increase commit idx because of old terms with majority" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) "peer 1"
      let peer2 = Node.create (RaftId.Create()) "peer 2"
      let peer3 = Node.create (RaftId.Create()) "peer 3"
      let peer4 = Node.create (RaftId.Create()) "peer 4"

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "hell no") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log1 = LogEntry(RaftId.Create(),0UL,1UL,"one",None)
      let log2 = LogEntry(RaftId.Create(),0UL,1UL,"two",None)
      let log3 = LogEntry(RaftId.Create(),0UL,2UL,"three",None)

      let response =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 1UL
        ; FirstIndex = 1UL
        }

      raft {
        do! addNodesM [| peer1; peer2; peer3; peer4 |]
        do! setStateM Leader
        do! setTermM 2UL
        do! setCommitIndexM 0UL
        do! setLastAppliedIdxM 0UL
        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM
        do! appendEntryM log3 >>= ignoreM

        do! sendAppendEntry peer1
        do! sendAppendEntry peer2

        do! receiveAppendEntriesResponse peer1.Id response
        do! expectM "Should have commit index 0" 0UL commitIndex

        do! receiveAppendEntriesResponse peer2.Id response
        do! expectM "Should have commit index 0" 0UL commitIndex

        do! periodic 1UL
        do! expectM "Should have lastAppliedIndex 0" 0UL lastAppliedIdx

        do! sendAppendEntry peer1
        do! sendAppendEntry peer2

        do! receiveAppendEntriesResponse peer1.Id { response with CurrentIndex = 2UL; FirstIndex = 2UL }
        do! expectM "Should have commit index 0" 0UL commitIndex

        do! receiveAppendEntriesResponse peer2.Id { response with CurrentIndex = 2UL; FirstIndex = 2UL }
        do! expectM "Should have commit index 0" 0UL commitIndex

        do! periodic 1UL
        do! expectM "Should have lastAppliedIndex 0" 0UL lastAppliedIdx

        do! sendAppendEntry peer1
        do! sendAppendEntry peer2

        do! receiveAppendEntriesResponse peer1.Id { response with Term = 2UL; CurrentIndex = 3UL; FirstIndex = 3UL }
        do! expectM "Should have commit index 0" 0UL commitIndex

        do! receiveAppendEntriesResponse peer2.Id { response with Term = 2UL; CurrentIndex = 3UL; FirstIndex = 3UL }
        do! expectM "Should have commit index 3" 3UL commitIndex

        do! periodic 1UL
        do! expectM "Should have lastAppliedIndex 1" 1UL lastAppliedIdx

        do! periodic 1UL
        do! expectM "Should have lastAppliedIndex 2" 2UL lastAppliedIdx

        do! periodic 1UL
        do! expectM "Should have lastAppliedIndex 3" 3UL lastAppliedIdx
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_recv_appendentries_response_jumps_to_lower_next_idx =
    testCase "leader recv appendentries response jumps to lower next idx" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer 1"

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "meow") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log1 = LogEntry(RaftId.Create(),0UL,1UL,"one",None)
      let log2 = LogEntry(RaftId.Create(),0UL,2UL,"two",None)
      let log3 = LogEntry(RaftId.Create(),0UL,3UL,"three",None)
      let log4 = LogEntry(RaftId.Create(),0UL,4UL,"four",None)

      let response =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 1UL
        ; FirstIndex = 1UL
        }

      raft {
        do! addNodeM peer
        do! setStateM Leader
        do! setTermM 2UL
        do! setCommitIndexM 0UL
        do! setLastAppliedIdxM 0UL
        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM
        do! appendEntryM log3 >>= ignoreM
        do! appendEntryM log4 >>= ignoreM
        do! becomeLeader ()

        do! expectM "Should have nextIdx 5" 5UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! expectM "Should have a msg 1" 1 (fun _ -> List.length (!sender.Outbox))

        sender.Inbox  := List.empty
        sender.Outbox := List.empty

        // need to get an up-to-date version of the peer, because its nextIdx
        // will have been bumped when becoming leader!
        let! peer = getNodeM peer.Id >>= (Option.get >> returnM)

        do! sendAppendEntry peer

        !sender.Outbox
        |> List.head
        |> getAppendEntries
        |> assume "Should have prevLogIdx 4" 4UL AppendRequest.prevLogIndex
        |> expect "Should have prevLogTerm 4" 4UL AppendRequest.prevLogTerm

        sender.Inbox  := List.empty
        sender.Outbox := List.empty

        do! receiveAppendEntriesResponse peer.Id { response with Term = 2UL; Success = false; CurrentIndex = 1UL }
        do! expectM "Should have nextIdx 2" 2UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! expectM "Should have 2 msgs" 2 (fun _ -> List.length !sender.Outbox)

        !sender.Outbox
        |> List.head
        |> getAppendEntries
        |> assume "Should have prevLogIdx 1" 1UL AppendRequest.prevLogIndex
        |> expect "Should have prevLogTerm 1" 1UL AppendRequest.prevLogTerm
      }
      |> runWithRaft raft' cbs
      |> noError


  let leader_recv_appendentries_response_decrements_to_lower_next_idx =
    testCase "leader recv appendentries response decrements to lower next idx" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer 1"

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "fuck off") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log1 = LogEntry(RaftId.Create(),0UL,1UL,"one",None)
      let log2 = LogEntry(RaftId.Create(),0UL,2UL,"two",None)
      let log3 = LogEntry(RaftId.Create(),0UL,3UL,"three",None)
      let log4 = LogEntry(RaftId.Create(),0UL,4UL,"four",None)

      let response =
        { Term = 2UL
        ; Success = false
        ; CurrentIndex = 4UL
        ; FirstIndex = 0UL
        }

      raft {
        do! addNodeM peer
        do! setTermM 2UL
        do! setCommitIndexM 0UL

        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM
        do! appendEntryM log3 >>= ignoreM
        do! appendEntryM log4 >>= ignoreM

        do! becomeLeader ()
        do! expectM "Should have correct nextIdx" 5UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! expectM "Should have a message in outbox" 1 (fun _ -> List.length !sender.Outbox)

        // need to get updated peer, because nextIdx will be bumped when
        // becoming leader!
        let! peer = getNodeM peer.Id >>= (Option.get >> returnM)

        do! sendAppendEntry peer

        do! expectM "Should have 2 msgs" 2 (fun _ -> List.length !sender.Outbox)

        !sender.Outbox
        |> List.head
        |> getAppendEntries
        |> assume "Should have prevLogTerm 4" 4UL AppendRequest.prevLogTerm
        |> expect "Should have prevLogIdx 4" 4UL AppendRequest.prevLogIndex

        do! receiveAppendEntriesResponse peer.Id response
        do! expectM "Should have nextIdx 4" 4UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! expectM "Should have 3 msgs" 3 (fun _ -> List.length !sender.Outbox)

        !sender.Outbox
        |> List.head
        |> getAppendEntries
        |> assume "Should have prevLogTerm 3" 3UL AppendRequest.prevLogTerm
        |> expect "Should have prevLogIdx 3" 3UL AppendRequest.prevLogIndex

        do! receiveAppendEntriesResponse peer.Id response
        do! expectM "Should have correct nextIdx" 3UL (getNode peer.Id >> Option.get >> Node.getNextIndex)

        do! expectM "Should have 4 msgs" 4 (fun _ -> List.length !sender.Outbox)

        !sender.Outbox
        |> List.head
        |> getAppendEntries
        |> assume "Should have prevLogTerm 2" 2UL AppendRequest.prevLogTerm
        |> expect "Should have prevLogIdx 2" 2UL AppendRequest.prevLogIndex
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_recv_appendentries_response_retry_only_if_leader =
    testCase "leader recv appendentries response retry only if leader" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) "peer 1"
      let peer2 = Node.create (RaftId.Create()) "peer 2"

      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "well") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log = LogEntry(RaftId.Create(),0UL,1UL,"one",None)

      let response =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 1UL
        ; FirstIndex = 1UL
        }

      raft {
        do! addNodesM [| peer1; peer2 |]
        do! setTermM 1UL
        do! setCommitIndexM 0UL
        do! setStateM Leader
        do! setLastAppliedIdxM 0UL

        do! appendEntryM log >>= ignoreM

        do! sendAppendEntry peer1
        do! sendAppendEntry peer2

        do! expectM "Should have 2 msgs" 2 (fun _ -> List.length !sender.Outbox)
        do! becomeFollower ()
        do! receiveAppendEntriesResponse peer1.Id response
      }
      |> runWithRaft raft' cbs
      |> expectError NotLeader

  let leader_recv_entry_resets_election_timeout =
    testCase "leader recv entry resets election timeout" <| fun _ ->
      let log = LogEntry(RaftId.Create(), 0UL, 1UL,  (), None)
      raft {
        do! setElectionTimeoutM 1000UL
        do! setStateM Leader
        do! periodic 900UL
        let! response = receiveEntry log
        do! expectM "Should have reset timeout elapsed" 0UL timeoutElapsed
      }
      |> runWithDefaults
      |> noError

  let leader_recv_entry_is_committed_returns_0_if_not_committed =
    testCase "leader recv entry is committed returns 0 if not committed" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let log = LogEntry(RaftId.Create(), 0UL, 1UL,  (), None)

      raft {
        do! addPeerM peer
        do! setStateM Leader

        do! setCommitIndexM 0UL
        let! response = receiveEntry log
        let! committed = responseCommitted response
        expect "Should not have committed" false id committed

        do! setCommitIndexM 1UL
        let! response = receiveEntry log
        let! committed = responseCommitted response
        expect "Should have committed" true id committed
      }
      |> runWithDefaults
      |> noError

  let leader_recv_entry_is_committed_returns_neg_1_if_invalidated =
    testCase "leader recv entry is committed returns neg 1 if invalidated" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let log = Log.make 1UL ()

      let ae =
        { LeaderCommit = 1UL
        ; Term = 2UL
        ; PrevLogIdx = 0UL
        ; PrevLogTerm = 0UL
        ; Entries = Log.make 2UL () |> Some
        }

      raft {
        do! addNodeM peer
        do! setStateM Leader
        do! setCommitIndexM 0UL
        do! setTermM 1UL

        do! expectM "Should have current idx 0UL" 0UL currentIndex

        let! response = receiveEntry log
        let! committed = responseCommitted response

        expect "Should not have committed entry" false id committed
        expect "Should have term 1UL" 1UL Entry.term response
        expect "Should have index 1UL" 1UL Entry.index response

        do! expectM "(1) Should have current idx 1UL" 1UL currentIndex
        do! expectM "Should have commit idx 0UL" 0UL commitIndex

        let! resp = receiveAppendEntries (Some peer.Id) ae

        expect "Should have succeeded" true AppendRequest.succeeded resp

        do! expectM "(2) Should have current idx 1" 1UL currentIndex
        do! expectM "Should have commit idx 1" 1UL commitIndex

        return! responseCommitted response
      }
      |> runWithDefaults
      |> expectError EntryInvalidated


  let leader_recv_entry_does_not_send_new_appendentries_to_slow_nodes =
    testCase "leader recv entry does not send new appendentries to slow nodes" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer 1"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "yikes") with
            SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log = Log.make 1UL "hello"

      raft {
        do! addNodeM peer
        do! setStateM Leader
        do! setTermM 1UL
        do! setCommitIndexM 0UL
        do! setNextIndexM peer.Id 1UL
        do! appendEntryM log >>= ignoreM
        let! response = receiveEntry log

        !sender.Outbox
        |> expect "Should have no msg" 0 List.length
      }
      |> runWithRaft raft' cbs
      |> noError


  let leader_recv_appendentries_response_failure_does_not_set_node_nextid_to_0 =
    testCase "leader recv appendentries response failure does not set node nextid to 0" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer 1"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "get me out of here") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let log = Log.make 1UL "hello"
      let resp =
        { Term = 1UL
        ; Success = false
        ; CurrentIndex = 0UL
        ; FirstIndex = 0UL
        }

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! setTermM 1UL
        do! setCommitIndexM 0UL
        do! appendEntryM log >>= ignoreM

        do! sendAppendEntry peer

        do! receiveAppendEntriesResponse peer.Id resp
        do! expectM "Should have nextIdx Works 1" 1UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! receiveAppendEntriesResponse peer.Id resp
        do! expectM "Should have nextIdx Dont work 1" 1UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_recv_appendentries_response_increment_idx_of_node =
    testCase "leader recv appendentries response increment idx of node" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer 1"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "please") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let resp =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 0UL
        ; FirstIndex = 0UL
        }

      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! setTermM 1UL
        do! expectM "Should have nextIdx 1" 1UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! receiveAppendEntriesResponse peer.Id resp
        do! expectM "Should have nextIdx 1" 1UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
      }
      |> runWithRaft raft' cbs
      |> noError


  let leader_recv_appendentries_response_drop_message_if_term_is_old =
    testCase "leader recv appendentries response drop message if term is old" <| fun _ ->
      let peer = Node.create (RaftId.Create()) "peer 1"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>
      let cbs =
        { mk_cbs (ref "make it stop") with SendAppendEntries = senderAppendEntries sender }
        :> IRaftCallbacks<_,_>

      let resp =
        { Term = 1UL
        ; Success = true
        ; CurrentIndex = 1UL
        ; FirstIndex = 1UL
        }
      raft {
        do! addPeerM peer
        do! setStateM Leader
        do! setTermM 2UL
        do! expectM "Should have nextIdx 1" 1UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
        do! receiveAppendEntriesResponse peer.Id resp
        do! expectM "Should have nextIdx 1" 1UL (getNode peer.Id >> Option.get >> Node.getNextIndex)
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_recv_appendentries_steps_down_if_newer =
    testCase "leader recv appendentries steps down if newer" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let ae =
        { Term = 6UL
        ; PrevLogIdx = 6UL
        ; PrevLogTerm = 5UL
        ; LeaderCommit = 0UL
        ; Entries = None
        }
      raft {
        let! raft' = get
        let nid = Some raft'.Node.Id
        do! addNodeM peer
        do! setStateM Leader
        do! setTermM 5UL
        do! expectM "Should be leader" true isLeader
        do! expectM "Should be leader" true (currentLeader >> ((=) nid))
        let! response = receiveAppendEntries (Some peer.Id) ae
        do! expectM "Should be follower" true isFollower
        do! expectM "Should follow peer" true (currentLeader >> ((=) nid))
      }
      |> runWithDefaults
      |> noError

  let leader_recv_appendentries_steps_down_if_newer_term =
    testCase "leader recv appendentries steps down if newer term" <| fun _ ->
      let peer = Node.create (RaftId.Create()) ()
      let resp =
        { Term = 6UL
        ; PrevLogIdx = 5UL
        ; PrevLogTerm = 5UL
        ; LeaderCommit = 0UL
        ; Entries = None
        }
      raft {
        do! addNodeM peer
        do! setStateM Leader
        do! setTermM 5UL
        let! response = receiveAppendEntries (Some peer.Id) resp
        do! expectM "Should be follower" true isFollower
      }
      |> runWithDefaults
      |> noError

  let leader_sends_empty_appendentries_every_request_timeout =
    testCase "leader sends empty appendentries every request timeout" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) "peer 1"
      let peer2 = Node.create (RaftId.Create()) "peer 2"
      let raft' = defaultServer "localhost"
      let sender = Sender.create<_,_>

      let response = ref { Term = 0UL
                         ; Success = true
                         ; CurrentIndex = 1UL
                         ; FirstIndex = 1UL
                         }

      let vote = { Term = 0UL; Granted = true; Reason = None }

      let cbs =
        { mk_cbs (ref "dreadful stuff") with
            SendAppendEntries = senderAppendEntries sender
            SendRequestVote = senderRequestVote sender }
        :> IRaftCallbacks<_,_>

      raft {
        do! addNodesM [| peer1; peer2 |]
        do! setElectionTimeoutM 1000UL
        do! setRequestTimeoutM 500UL
        do! expectM "Should have timout elapsed 0" 0UL timeoutElapsed

        do! setStateM Candidate
        do! becomeLeader ()

        !sender.Outbox
        |> expect "Should have 2 messages " 2 List.length

        // update CurrentIndex to latest nodeIdx to prevent StaleResponse error
        let! node1 = getNodeM peer1.Id

        do! receiveAppendEntriesResponse (node1 |> Option.get |> Node.getId) { !response with CurrentIndex = Option.get node1 |> Node.getNextIndex |> ((+) 1UL) }

        do! periodic 501UL

        !sender.Outbox
        |> expect "Should have 4 messages" 4 List.length // because 2 peers
      }
      |> runWithRaft raft' cbs
      |> noError

  let leader_recv_requestvote_responds_without_granting =
    testCase "leader recv requestvote responds without granting" <| fun _ ->
      let peer1 = Node.create (RaftId.Create()) ()
      let peer2 = Node.create (RaftId.Create()) ()
      let sender = Sender.create<_,_>
      let resp = { Term = 1UL; Granted = true; Reason = None }

      let vote =
        { Term = 1UL
        ; Candidate = peer2
        ; LastLogIndex = 0UL
        ; LastLogTerm = 0UL }

      raft {
        do! addNodesM [| peer1; peer2 |]
        do! setElectionTimeoutM 1000UL
        do! setRequestTimeoutM 500UL
        do! expectM "Should have timout elapsed 0" 0UL timeoutElapsed
        do! startElection ()
        do! receiveVoteResponse peer1.Id resp
        do! expectM "Should be leader" Leader state
        let! resp = receiveVoteRequest peer2.Id vote
        expect "Should have declined vote" true Vote.declined resp
      }
      |> runWithDefaults
      |> noError


  let leader_recv_requestvote_responds_with_granting_if_term_is_higher =
    testCase "leader recv requestvote responds with granting if term is higher" <| fun _ ->

      let peer1 = Node.create (RaftId.Create()) ()
      let peer2 = Node.create (RaftId.Create()) ()
      let sender = Sender.create<_,_>
      let resp = { Term = 1UL; Granted = true; Reason = None }

      let vote =
        { Term = 2UL
        ; Candidate = peer2
        ; LastLogIndex = 0UL
        ; LastLogTerm = 0UL }

      raft {
        do! addNodesM [| peer1; peer2 |]
        do! setElectionTimeoutM 1000UL
        do! setRequestTimeoutM 500UL
        do! expectM "Should have timout elapsed 0" 0UL timeoutElapsed

        do! startElection ()
        do! receiveVoteResponse peer1.Id resp
        do! expectM "Should be Leader" true isLeader
        let! resp = receiveVoteRequest peer2.Id vote
        do! expectM "Should be Follower" true isFollower
      }
      |> runWithDefaults
      |> noError

  let server_recv_appendentry_executes_all_cfg_changes =
    testCase "recv appendentry executes all cfg changes" <| fun _ ->
      let node1 = Node.create (RaftId.Create()) ()
      let node2 = Node.create (RaftId.Create()) ()

      let log =
        JointConsensus(RaftId.Create(), 3UL, 0UL, [| NodeAdded node2 |],
                Some <| JointConsensus(RaftId.Create(), 2UL, 0UL, [| NodeRemoved node1 |],
                           Some <| JointConsensus(RaftId.Create(), 1UL, 0UL, [| NodeAdded node1 |], None)))

      let getstuff r = Map.toList r.Peers |> List.map (snd >> Node.getId)

      raft {
        do! becomeLeader ()
        do! appendEntryM log >>= ignoreM
        let! me = selfM()

        do! expectM "Should have 2 nodes" 2UL numNodes
        do! expectM "Should have correct nodes" [me.Id; node2.Id] getstuff
      }
      |> runWithDefaults
      |> noError


  let server_should_not_request_vote_from_failed_nodes =
    testCase "should not request vote from failed nodes" <| fun _ ->
      let node1 =   Node.create (RaftId.Create()) ()
      let node2 =   Node.create (RaftId.Create()) ()
      let node3 =   Node.create (RaftId.Create()) ()
      let node4 = { Node.create (RaftId.Create()) () with State = Failed }

      let mutable i = 0

      let raft' = Raft.create node1
      let cbs =
        { mk_cbs (ref "oh no get lost") with SendRequestVote = fun _ _ -> i <- i + 1 }
        :> IRaftCallbacks<_,_>

      raft {
        do! addPeersM [| node2; node3; node4 |]
        do! setElectionTimeoutM 1000UL
        do! periodic 1001UL
        expect "Should have sent 2 requests" 2 id i
      }
      |> runWithRaft raft' cbs
      |> noError




  let server_should_not_consider_failed_nodes_when_deciding_vote_outcome =
    testCase "should not consider failed nodes when deciding vote outcome" <| fun _ ->
      let node1 =   Node.create (RaftId.Create()) ()
      let node2 =   Node.create (RaftId.Create()) ()
      let node3 = { Node.create (RaftId.Create()) () with State = Failed }
      let node4 = { Node.create (RaftId.Create()) () with State = Failed }

      let resp = { Term = 1UL; Granted = true; Reason = None }

      raft {
        do! addPeersM [| node1; node2; node3; node4 |]
        do! setElectionTimeoutM 1000UL
        do! periodic 1001UL
        do! receiveVoteResponse node1.Id resp
        do! expectM "Should be leader now" Leader state
      }
      |> runWithDefaults
      |> noError


  let server_periodic_should_trigger_snapshotting =
    testCase "periodic should trigger snapshotting when MaxLogDepth is reached" <| fun _ ->
      raft {
        let! state = get
        for n in 0UL .. state.MaxLogDepth do
          do! appendEntryM (Log.make state.CurrentTerm (string n)) >>= ignoreM

        do! setLeaderM (Some state.Node.Id)
        do! expectM "Should have correct number of entries" 41UL numLogs
        do! periodic 10UL
        do! expectM "Should have correct number of entries" 1UL numLogs
      }
      |> runWithData (ref "fucking hell")
      |> noError

  let server_should_apply_each_log_when_receiving_a_snapshot =
    testCase "should apply each log when receiving a snapshot" <| fun _ ->
      let idx = 9UL
      let term = 1UL
      let count = ref 0

      let init = defaultServer "holy crap"
      let cbs =
        { mk_cbs (ref "yep") with ApplyLog = fun _ -> count := !count + 1 }
        :> IRaftCallbacks<_,_>

      let nodes =
        [| "one"; "two"; "three" |]
        |> Array.mapi (fun i s -> Node.create (RaftId.Create()) s)

      let is: InstallSnapshot<_,_> =
        { Term = term
        ; LeaderId = RaftId.Create()
        ; LastTerm = term
        ; LastIndex = idx
        ; Data = Snapshot(RaftId.Create(), idx, term, idx, term, nodes, "state")
        }

      raft {
        do! setTermM term
        let! response = receiveInstallSnapshot is
        do! expectM "Should have correct number of nodes" 4UL numNodes // including our own node
        do! expectM "Should have correct number of log entries" 1UL numLogs
        expect "Should have called ApplyLog once" 1 id !count
      }
      |> runWithRaft init cbs
      |> noError

  let server_should_merge_snaphot_and_existing_log_when_receiving_a_snapshot =
    testCase "should merge snaphot and existing log when receiving a snapshot" <| fun _ ->
      let idx = 9UL
      let num = 5UL
      let term = 1UL
      let count = ref 0

      let init = defaultServer "holy crap"
      let cbs =
        { mk_cbs (ref "yep") with
            ApplyLog = fun l ->
              count := !count + 1
          }
        :> IRaftCallbacks<_,_>

      let nodes =
        [| "one"; "two"; "three" |]
        |> Array.mapi (fun i s -> Node.create (RaftId.Create()) s)

      let is: InstallSnapshot<_,_> =
        { Term = term
        ; LeaderId = RaftId.Create()
        ; LastTerm = term
        ; LastIndex = idx
        ; Data = Snapshot(RaftId.Create(), idx, term, idx, term, nodes, "state")
        }

      raft {
        do! setTermM term
        for n in 0UL .. (idx + num) do
          do! appendEntryM (Log.make term (string n)) >>= ignoreM

        do! applyEntries ()

        let! response = receiveInstallSnapshot is

        do! expectM "Should have correct number of nodes" 4UL numNodes // including our own node
        do! expectM "Should have correct number of log entries" 7UL numLogs
        expect "Should have called ApplyLog once" 7 id !count
      }
      |> runWithRaft init cbs
      |> noError


  let server_should_fire_node_callbacks_on_config_change =
    testCase "should fire node callbacks on config change" <| fun _ ->
      let count = ref 0

      let init = defaultServer "holy crap"

      let cb _ l =
        count := !count + 1

      let cbs =
        { mk_cbs (ref "yep") with
            NodeAdded   = cb "added"
            NodeRemoved = cb "removed"
        } :> IRaftCallbacks<_,_>

      raft {
        let node = Node.create (RaftId.Create()) "one"

        do! setStateM Leader

        do! appendEntryM (JointConsensus(RaftId.Create(), 0UL, 0UL, [| NodeAdded(node)|] ,None)) >>= ignoreM
        do! setCommitIndexM 1UL
        do! applyEntries ()

        expect "Should have count 1" 1 id !count

        do! appendEntryM (JointConsensus(RaftId.Create(), 0UL, 0UL, [| NodeRemoved node |] ,None)) >>= ignoreM
        do! setCommitIndexM 3UL
        do! applyEntries ()

        expect "Should have count 2" 2 id !count
      }
      |> runWithRaft init cbs
      |> noError

  let server_should_call_persist_callback_for_each_appended_log =
    testCase "should call persist callback for each appended log" <| fun _ ->
      let count = ref List.empty

      let init = defaultServer "holy crap"

      let cb l = count := Log.id l :: !count

      let cbs =
        { mk_cbs (ref "yep") with
            PersistLog = cb
        } :> IRaftCallbacks<_,_>

      raft {
        let log1 = Log.make 0UL "one"
        let log2 = Log.make 0UL "two"
        let log3 = Log.make 0UL "three"

        let ids =
          [ log3; log2; log1; ]
          |> List.map Log.id

        do! setStateM Leader

        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2  >>= ignoreM
        do! appendEntryM log3  >>= ignoreM

        expect "should have correct ids" ids id !count
      }
      |> runWithRaft init cbs
      |> noError

  let server_should_call_delete_callback_for_each_deleted_log =
    testCase "should call delete callback for each deleted log" <| fun _ ->
      let log1 = Log.make 0UL "one"
      let log2 = Log.make 0UL "two"
      let log3 = Log.make 0UL "three"

      let count = ref [ log3; log2; log1; ]

      let init = defaultServer "holy crap"

      let cb l =
        let fltr l r = Log.id l <> Log.id r
        in count := List.filter (fltr l) !count

      let cbs =
        { mk_cbs (ref "yep") with
            DeleteLog = cb
        } :> IRaftCallbacks<_,_>

      raft {
        do! setStateM Leader

        do! appendEntryM log1 >>= ignoreM
        do! appendEntryM log2 >>= ignoreM
        do! appendEntryM log3 >>= ignoreM

        do! removeEntryM 3UL
        do! expectM "Should have only 2 entries" 2UL numLogs

        do! removeEntryM 2UL
        do! expectM "Should have only 1 entry" 1UL numLogs

        do! removeEntryM 1UL
        do! expectM "Should have zero entries" 0UL numLogs

        expect "should have deleted all logs" List.empty id !count
      }
      |> runWithRaft init cbs
      |> noError

  let server_should_use_old_and_new_config_during_intermittend_elections =
    testCase "should use old and new config during intermittend elections" <| fun _ ->

      let n = 10UL                       // we want ten nodes overall

      let nodes =
        [| for n in 1UL .. (n - 1UL) do      // subtract one for the implicitly
            let nid = RaftId.Create()
            yield (nid, Node.create nid ()) |] // create node in the Raft state

      let vote = { Granted = true; Term = 0UL; Reason = None }

      raft {
        let! self = getSelfM ()

        do! setPeersM (nodes |> Map.ofArray)
        do! becomeCandidate ()          // increases term!

        do! expectM "Should have be candidate" Candidate Raft.state
        do! expectM "Should have $n nodes" n numNodes

        //       _           _   _               _
        //   ___| | ___  ___| |_(_) ___  _ __   / |
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \  | |
        // |  __/ |  __/ (__| |_| | (_) | | | | | |
        //  \___|_|\___|\___|\__|_|\___/|_| |_| |_|
        //
        // with the full cluster of 10 nodes in total
        let! term = currentTermM ()

        do! expectM "Should use the regular configuration" false inJointConsensus

        // we need only 5 votes coming in (plus our own) to make a majority
        for nid in 1UL .. (n / 2UL) do
          do! receiveVoteResponse (fst nodes.[int nid]) { vote with Term = term }

        do! expectM "Should be leader in base configuration" Leader Raft.state

        //                                         __ _
        //  _ __   _____      __   ___ ___  _ __  / _(_) __ _
        // | '_ \ / _ \ \ /\ / /  / __/ _ \| '_ \| |_| |/ _` |
        // | | | |  __/\ V  V /  | (_| (_) | | | |  _| | (_| |
        // |_| |_|\___| \_/\_/    \___\___/|_| |_|_| |_|\__, |
        //                                              |___/

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration *without* the last 5 nodes
        // with node id's 5 - 9
        let entry =
          nodes
          |> Array.map snd
          |> Array.filter (fun node -> uint64 <| Array.IndexOf(nodes, node) < (n / 2UL))
          |> Array.append [| self |]
          |> Log.mkConfigChange 1UL peers

        let! response = receiveEntry entry

        do! expectM "Should still have correct node count for new configuration" (n / 2UL) numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" n numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)

        //       _           _   _               ____
        //   ___| | ___  ___| |_(_) ___  _ __   |___ \
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \    __) |
        // |  __/ |  __/ (__| |_| | (_) | | | |  / __/
        //  \___|_|\___|\___|\__|_|\___/|_| |_| |_____|
        //
        // now in joint consensus state, with 2 configurations (old and new)
        do! becomeCandidate ()

        let! term = currentTermM ()

        // testing with the new configuration (the nodes with the lower id values)
        // We only need the votes from 2 more nodes out of the old configuration
        // to form a majority.
        for nid in 1UL .. ((n / 2UL) / 2UL) do
          do! receiveVoteResponse (fst nodes.[int nid]) { vote with Term = term }

        do! expectM "Should be leader in joint consensus with votes from the new configuration" Leader Raft.state

        //       _           _   _               _____
        //   ___| | ___  ___| |_(_) ___  _ __   |___ /
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \    |_ \
        // |  __/ |  __/ (__| |_| | (_) | | | |  ___) |
        //  \___|_|\___|\___|\__|_|\___/|_| |_| |____/
        //
        // still in joint consensus state
        do! becomeCandidate ()

        let! term = currentTermM ()

        // testing with the old configuration (the nodes with the higher id
        // values that have been removed with the joint consensus entry)
        for nid in (n / 2UL) .. (n - 1UL) do
          do! receiveVoteResponse (fst nodes.[int nid]) { vote with Term = term }

        do! expectM "Should be leader in joint consensus with votes from the old configuration" Leader Raft.state

        //                                  __ _                       _   _
        //  _ __ ___        ___ ___  _ __  / _(_) __ _ _   _ _ __ __ _| |_(_) ___  _ __
        // | '__/ _ \_____ / __/ _ \| '_ \| |_| |/ _` | | | | '__/ _` | __| |/ _ \| '_ \
        // | | |  __/_____| (_| (_) | | | |  _| | (_| | |_| | | | (_| | |_| | (_) | | | |
        // |_|  \___|      \___\___/|_| |_|_| |_|\__, |\__,_|_|  \__,_|\__|_|\___/|_| |_|
        // is now complete!                      |___/

        let! term = currentTermM ()

        let entry = Log.mkConfig term Array.empty

        let! response = receiveEntry entry

        do! expectM "Should only have half the nodes" (n / 2UL) numNodes
        do! expectM "Should have None as ConfigChange" None lastConfigChange

        //       _           _   _               _  _
        //   ___| | ___  ___| |_(_) ___  _ __   | || |
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \  | || |_
        // |  __/ |  __/ (__| |_| | (_) | | | | |__   _|
        //  \___|_|\___|\___|\__|_|\___/|_| |_|    |_|
        //
        // with the new configuration only (should not work with nodes in old config anymore)

        do! becomeCandidate ()
        let! term = currentTermM ()

        for nid in 1UL .. ((n / 2UL) / 2UL) do
          do! receiveVoteResponse (fst nodes.[int nid]) { vote with Term = term }

        do! expectM "Should be leader in election with regular configuration" Leader Raft.state

        //            _     _                   _
        //   __ _  __| | __| |  _ __   ___   __| | ___  ___
        //  / _` |/ _` |/ _` | | '_ \ / _ \ / _` |/ _ \/ __|
        // | (_| | (_| | (_| | | | | | (_) | (_| |  __/\__ \
        //  \__,_|\__,_|\__,_| |_| |_|\___/ \__,_|\___||___/

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration with 5 new nodes
        let entry =
          nodes
          |> Array.map snd
          |> Array.append [| self |]
          |> Log.mkConfigChange 1UL peers

        let! response = receiveEntry entry

        do! expectM "Should still have correct node count for new configuration" n numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" (n / 2UL) numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)

        //       _           _   _               ____
        //   ___| | ___  ___| |_(_) ___  _ __   | ___|
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \  |___ \
        // |  __/ |  __/ (__| |_| | (_) | | | |  ___) |
        //  \___|_|\___|\___|\__|_|\___/|_| |_| |____/

        do! becomeCandidate ()
        let! term = currentTermM ()

        // should become candidate with the old configuration of 5 nodes only
        for nid in 1UL .. ((n / 2UL) / 2UL) do
          do! receiveVoteResponse (fst nodes.[int nid]) { vote with Term = term }

        do! expectM "Should be leader in election in joint consensus with old configuration" Leader Raft.state

        //       _           _   _                __
        //   ___| | ___  ___| |_(_) ___  _ __    / /_
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \  | '_ \
        // |  __/ |  __/ (__| |_| | (_) | | | | | (_) |
        //  \___|_|\___|\___|\__|_|\___/|_| |_|  \___/

        do! becomeCandidate ()
        let! term = currentTermM ()

        // should become candidate with the new configuration of 10 nodes also
        for id in (n / 2UL) .. (n - 2UL) do
          let nid = fst nodes.[int id]
          let! result = getNodeM nid
          match result with
            | Some node ->
              // the nodes are not able to vote at first, because they will need
              // to be up to date to do that
              do! updateNodeM { node with State = Running; Voting = true }
              do! receiveVoteResponse nid { vote with Term = term }
            | _ -> failwith "Node not found. :("

        do! expectM "Should be leader in election in joint consensus with new configuration" Leader Raft.state

        //                                  __ _                       _   _
        //  _ __ ___        ___ ___  _ __  / _(_) __ _ _   _ _ __ __ _| |_(_) ___  _ __
        // | '__/ _ \_____ / __/ _ \| '_ \| |_| |/ _` | | | | '__/ _` | __| |/ _ \| '_ \
        // | | |  __/_____| (_| (_) | | | |  _| | (_| | |_| | | | (_| | |_| | (_) | | | |
        // |_|  \___|      \___\___/|_| |_|_| |_|\__, |\__,_|_|  \__,_|\__|_|\___/|_| |_|
        // is now complete.                      |___/

        let! term = currentTermM ()

        let entry = Log.mkConfig term Array.empty

        let! response = receiveEntry entry

        do! expectM "Should have all the nodes" n numNodes
        do! expectM "Should have None as ConfigChange" None lastConfigChange

      }
      |> runWithDefaults
      |> noError

  let server_should_revert_to_follower_state_on_config_change_removal =
    testCase "should revert to follower state on config change removal" <| fun _ ->

      let n = 10UL                       // we want ten nodes overall

      let nodes =
        [| for n in 1UL .. (n - 1UL) do      // subtract one for the implicitly
            let nid = RaftId.Create()
            yield (nid, Node.create nid ()) |] // create node in the Raft state

      let vote = { Granted = true; Term = 0UL; Reason = None }

      raft {
        let! self = getSelfM ()

        do! setPeersM (nodes |> Map.ofArray)
        do! becomeCandidate ()          // increases term!

        do! expectM "Should have be candidate" Candidate Raft.state
        do! expectM "Should have $n nodes" n numNodes

        //       _           _   _               _
        //   ___| | ___  ___| |_(_) ___  _ __   / |
        //  / _ \ |/ _ \/ __| __| |/ _ \| '_ \  | |
        // |  __/ |  __/ (__| |_| | (_) | | | | | |
        //  \___|_|\___|\___|\__|_|\___/|_| |_| |_|
        //
        // with the full cluster of 10 nodes in total
        let! term = currentTermM ()

        do! expectM "Should use the regular configuration" false inJointConsensus

        // we need only 5 votes coming in (plus our own) to make a majority
        for nid in 1UL .. (n / 2UL) do
          do! receiveVoteResponse (fst nodes.[int nid]) { vote with Term = term }

        do! expectM "Should be leader in base configuration" Leader Raft.state

        //                                         __ _
        //  _ __   _____      __   ___ ___  _ __  / _(_) __ _
        // | '_ \ / _ \ \ /\ / /  / __/ _ \| '_ \| |_| |/ _` |
        // | | | |  __/\ V  V /  | (_| (_) | | | |  _| | (_| |
        // |_| |_|\___| \_/\_/    \___\___/|_| |_|_| |_|\__, |
        //                                              |___/
        let! term = currentTermM ()

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration *without* the last 5 nodes
        // with node id's 5 - 9
        let entry =
          nodes
          |> Array.map snd
          |> Array.filter (fun node -> uint64 <| Array.IndexOf(nodes,node) >= (uint64 n / 2UL))
          |> Log.mkConfigChange term peers

        let! response = receiveEntry entry
        let! my = selfM()

        do! expectM "Should still have correct node count for new configuration" (n / 2UL) numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" n numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)

        do! expectM "Should be found in joint consensus configuration myself" true (getNode my.Id >> Option.isSome)

        //                                  __ _                       _   _
        //  _ __ ___        ___ ___  _ __  / _(_) __ _ _   _ _ __ __ _| |_(_) ___  _ __
        // | '__/ _ \_____ / __/ _ \| '_ \| |_| |/ _` | | | | '__/ _` | __| |/ _ \| '_ \
        // | | |  __/_____| (_| (_) | | | |  _| | (_| | |_| | | | (_| | |_| | (_) | | | |
        // |_|  \___|      \___\___/|_| |_|_| |_|\__, |\__,_|_|  \__,_|\__|_|\___/|_| |_|
        // is now complete!                      |___/

        let! term = currentTermM ()
        let entry = Log.mkConfig term Array.empty
        let! response = receiveEntry entry
        let! my = selfM()

        do! expectM "Should only have half the nodes" (n / 2UL) numNodes
        do! expectM "Should have None as ConfigChange" None lastConfigChange
        do! expectM "Should be able to find myself" false (getNode my.Id >> Option.isSome)
        do! expectM "Should still be leader" Leader Raft.state

        let! result = responseCommitted response
        expect "Should not be committed yet" false id result

        let! idx = currentIndexM ()

        let aer = { Term         = term
                    Success      = true
                    CurrentIndex = idx
                    FirstIndex   = 1UL }

        for nid in (n / 2UL) .. (n - 1UL) do
          do! receiveAppendEntriesResponse (fst nodes.[int nid]) aer

        let! result = responseCommitted response
        expect "Should be committed now" true id result

        do! periodic 1001UL
        do! expectM "Should be follower now" Follower Raft.state
      }
      |> runWithDefaults
      |> noError

  let server_should_send_appendentries_to_all_servers_in_joint_consensus =
    testCase "should send appendentries to all servers in joint consensus" <| fun _ ->
      let count = ref 0
      let init = Raft.create (Node.create (RaftId.Create()) ())
      let cbs = { mk_cbs (ref ()) with
                    SendAppendEntries = fun _ _ -> count := 1 + !count }
                :> IRaftCallbacks<_,_>

      let n = 10UL                       // we want ten nodes overall

      let nodes =
        [| for n in 1UL .. (n - 1UL) do      // subtract one for the implicitly
            let nid = RaftId.Create()
            yield (nid, Node.create nid ()) |] // create node in the Raft state
        |> Map.ofArray

      raft {
        let! self = getSelfM ()
        do! becomeLeader ()             // increases term!
        do! expectM "Should have be Leader" Leader Raft.state

        //                                         __ _
        //  _ __   _____      __   ___ ___  _ __  / _(_) __ _
        // | '_ \ / _ \ \ /\ / /  / __/ _ \| '_ \| |_| |/ _` |
        // | | | |  __/\ V  V /  | (_| (_) | | | |  _| | (_| |
        // |_| |_|\___| \_/\_/    \___\___/|_| |_|_| |_|\__, |
        //                                              |___/  adding a ton of nodes

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration *without* the last 5 nodes
        // with node id's 5 - 9
        let entry =
          Map.toArray nodes
          |> Array.map snd
          |> Array.append [| self |]
          |> Log.mkConfigChange 1UL peers

        let! response = receiveEntry entry

        do! expectM "Should still have correct node count for new configuration" n numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" 1UL numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)
        do! expectM "Should be in joint consensus configuration" true inJointConsensus

        let! term = currentTermM ()
        let! _ = receiveEntry (Log.make term ())

        expect "Count should be n" ((n - 1UL) * 2UL) uint64 !count
      }
      |> runWithRaft init cbs
      |> noError

  let server_should_send_requestvote_to_all_servers_in_joint_consensus =
    testCase "should send appendentries to all servers in joint consensus" <| fun _ ->
      let count = ref 0
      let init = Raft.create (Node.create (RaftId.Create()) ())
      let cbs = { mk_cbs (ref ()) with
                    SendRequestVote = fun _ _ -> count := 1 + !count }
                :> IRaftCallbacks<_,_>

      let n = 10UL                       // we want ten nodes overall

      let nodes =
        [| for n in 1UL .. (n - 1UL) do      // subtract one for the implicitly
            let nid = RaftId.Create()
            yield (nid, Node.create nid ()) |] // create node in the Raft state

      raft {
        let! self = getSelfM ()

        do! becomeLeader ()             // increases term!
        do! expectM "Should have be Leader" Leader Raft.state

        //                                         __ _
        //  _ __   _____      __   ___ ___  _ __  / _(_) __ _
        // | '_ \ / _ \ \ /\ / /  / __/ _ \| '_ \| |_| |/ _` |
        // | | | |  __/\ V  V /  | (_| (_) | | | |  _| | (_| |
        // |_| |_|\___| \_/\_/    \___\___/|_| |_|_| |_|\__, |
        //                                              |___/  adding a ton of nodes

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration *without* the last 5 nodes
        // with node id's 5 - 9
        let entry =
          nodes
          |> Array.map snd
          |> Array.append [| self |]
          |> Log.mkConfigChange 1UL peers

        let! response = receiveEntry entry

        do! expectM "Should still have correct node count for new configuration" n numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" 1UL numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)
        do! expectM "Should be in joint consensus configuration" true inJointConsensus

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        for peer in peers do
          do! updateNodeM { peer with State = Running; Voting = true }

        do! startElection ()

        expect "Count should be n" (n - 1UL) uint64 !count
      }
      |> runWithRaft init cbs
      |> noError

  let server_should_use_old_and_new_config_during_intermittend_appendentries =
    testCase "should use old and new config during intermittend appendentries" <| fun _ ->
      let count = ref 0
      let init = Raft.create (Node.create (RaftId.Create()) ())
      let cbs = { mk_cbs (ref()) with
                   SendAppendEntries = fun _ _ -> count := 1 + !count }
                :> IRaftCallbacks<_,_>

      let n = 10UL                       // we want ten nodes overall

      let nodes =
        [| for n in 1UL .. (n - 1UL) do      // subtract one for the implicitly
            let nid = RaftId.Create()
            yield (nid, Node.create nid ()) |] // create node in the Raft state

      raft {
        let! self = getSelfM ()

        do! setPeersM (nodes |> Map.ofArray)
        do! becomeLeader ()          // increases term!

        do! expectM "Should have be Leader" Leader Raft.state
        do! expectM "Should have $n nodes" n numNodes

        //                                         __ _
        //  _ __   _____      __   ___ ___  _ __  / _(_) __ _
        // | '_ \ / _ \ \ /\ / /  / __/ _ \| '_ \| |_| |/ _` |
        // | | | |  __/\ V  V /  | (_| (_) | | | |  _| | (_| |
        // |_| |_|\___| \_/\_/    \___\___/|_| |_|_| |_|\__, |
        //                                              |___/

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration *without* the last 5 nodes
        // with node id's 5 - 9
        let entry =
          nodes
          |> Array.map snd
          |> Array.filter (fun node -> uint64 <| Array.IndexOf(nodes, node) < (n / 2UL))
          |> Array.append [| self |]
          |> Log.mkConfigChange 1UL peers

        let! response = receiveEntry entry

        do! expectM "Should still have correct node count for new configuration" (n / 2UL) numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" n numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)
        do! expectM "Should be in joint consensus configuration" true inJointConsensus

        expect "Count should be correct" ((n - 1UL) * 2UL) uint64 !count

        //                    _                    _        _             _
        //   __ _ _ __  _ __ | |_   _    ___ _ __ | |_ _ __(_) ___  ___  / |
        //  / _` | '_ \| '_ \| | | | |  / _ \ '_ \| __| '__| |/ _ \/ __| | |
        // | (_| | |_) | |_) | | |_| | |  __/ | | | |_| |  | |  __/\__ \ | |
        //  \__,_| .__/| .__/|_|\__, |  \___|_| |_|\__|_|  |_|\___||___/ |_|
        //       |_|   |_|      |___/  in a joint consensus configuration

        let! committed = responseCommitted response
        expect "should not have been committed" false id committed

        let! term = currentTermM ()
        let! idx = currentIndexM ()
        let aer = { Success      = true
                    Term         = term
                    CurrentIndex = idx
                    FirstIndex   = 1UL }

        for nid in (n / 2UL) .. (n - 1UL) do
          do! receiveAppendEntriesResponse (fst nodes.[int nid]) aer

        let! committed = responseCommitted response
        expect "should be committed" true id committed

        //                                  __ _                       _   _
        //  _ __ ___        ___ ___  _ __  / _(_) __ _ _   _ _ __ __ _| |_(_) ___  _ __
        // | '__/ _ \_____ / __/ _ \| '_ \| |_| |/ _` | | | | '__/ _` | __| |/ _ \| '_ \
        // | | |  __/_____| (_| (_) | | | |  _| | (_| | |_| | | | (_| | |_| | (_) | | | |
        // |_|  \___|      \___\___/|_| |_|_| |_|\__, |\__,_|_|  \__,_|\__|_|\___/|_| |_|
        // is now complete!                      |___/

        let! term = currentTermM ()
        let entry = Log.mkConfig term Array.empty
        let! response = receiveEntry entry

        do! expectM "Should only have half the nodes" (n / 2UL) numNodes
        do! expectM "Should have None as ConfigChange" None lastConfigChange

        //            _     _                   _
        //   __ _  __| | __| |  _ __   ___   __| | ___  ___
        //  / _` |/ _` |/ _` | | '_ \ / _ \ / _` |/ _ \/ __|
        // | (_| | (_| | (_| | | | | | (_) | (_| |  __/\__ \
        //  \__,_|\__,_|\__,_| |_| |_|\___/ \__,_|\___||___/

        let! peers = getNodesM () >>= (Map.toArray >> Array.map snd >> returnM)

        // we establish a new cluster configuration with 5 new nodes
        let entry =
          nodes
          |> Array.map snd
          |> Array.append [| self |]
          |> Log.mkConfigChange 1UL peers

        let! response = receiveEntry entry

        do! expectM "Should still have correct node count for new configuration" n numPeers
        do! expectM "Should still have correct logical node count" n numLogicalPeers
        do! expectM "Should still have correct node count for old configuration" (n / 2UL) numOldPeers
        do! expectM "Should have JointConsensus entry as ConfigChange" (Log.id entry) (lastConfigChange >> Option.get >> Log.id)

        //                    _                    _        _             ____
        //   __ _ _ __  _ __ | |_   _    ___ _ __ | |_ _ __(_) ___  ___  |___ \
        //  / _` | '_ \| '_ \| | | | |  / _ \ '_ \| __| '__| |/ _ \/ __|   __) |
        // | (_| | |_) | |_) | | |_| | |  __/ | | | |_| |  | |  __/\__ \  / __/
        //  \__,_| .__/| .__/|_|\__, |  \___|_| |_|\__|_|  |_|\___||___/ |_____|
        //       |_|   |_|      |___/

        let! result = responseCommitted response
        expect "Should not be committed" false id result

        let! term = currentTermM ()
        let! idx = currentIndexM ()
        let aer = { Success      = true
                    Term         = term
                    CurrentIndex = idx
                    FirstIndex   = 1UL }

        for nid in 1UL .. (n / 2UL) do
          do! receiveAppendEntriesResponse (fst nodes.[int nid]) aer

        let! result = responseCommitted response
        expect "Should be committed" true id result

      }
      |> runWithRaft init cbs
      |> noError

  let should_call_node_updated_callback_on_node_udpated =
    testCase "call node updated callback on node udpated" <| fun _ ->
      let count = ref 0
      let init = Raft.create (Node.create (RaftId.Create()) ())
      let cbs = { mk_cbs (ref ()) with
                    NodeUpdated = fun _ -> count := 1 + !count }
                :> IRaftCallbacks<_,_>

      raft {
        let node = Node.create (RaftId.Create()) ()
        do! addNodeM node
        do! updateNodeM { node with State = Joining }
        do! updateNodeM { node with State = Running }
        do! updateNodeM { node with State = Failed }

        expect "Should have called once" 3 id !count
      }
      |> runWithRaft init cbs
      |> noError

  let should_call_state_changed_callback_on_state_change =
    testCase "call state changed callback on state change" <| fun _ ->
      let count = ref 0
      let init = Raft.create (Node.create (RaftId.Create()) ())
      let cbs = { mk_cbs (ref ()) with
                    StateChanged = fun _ _ -> count := 1 + !count }
                :> IRaftCallbacks<_,_>

      raft {
        do! becomeCandidate ()
        do! becomeLeader ()
        do! becomeFollower ()
        expect "Should have called once" 3 id !count
      }
      |> runWithRaft init cbs
      |> noError
