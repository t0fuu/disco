// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization.Raft
{

using System;
using FlatBuffers;

public struct VoteRequestFB : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static VoteRequestFB GetRootAsVoteRequestFB(ByteBuffer _bb) { return GetRootAsVoteRequestFB(_bb, new VoteRequestFB()); }
  public static VoteRequestFB GetRootAsVoteRequestFB(ByteBuffer _bb, VoteRequestFB obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p.bb_pos = _i; __p.bb = _bb; }
  public VoteRequestFB __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  public uint Term { get { int o = __p.__offset(4); return o != 0 ? __p.bb.GetUint(o + __p.bb_pos) : (uint)0; } }
  public NodeFB? Candidate { get { int o = __p.__offset(6); return o != 0 ? (NodeFB?)(new NodeFB()).__assign(__p.__indirect(o + __p.bb_pos), __p.bb) : null; } }
  public uint LastLogIndex { get { int o = __p.__offset(8); return o != 0 ? __p.bb.GetUint(o + __p.bb_pos) : (uint)0; } }
  public uint LastLogTerm { get { int o = __p.__offset(10); return o != 0 ? __p.bb.GetUint(o + __p.bb_pos) : (uint)0; } }

  public static Offset<VoteRequestFB> CreateVoteRequestFB(FlatBufferBuilder builder,
      uint Term = 0,
      Offset<NodeFB> CandidateOffset = default(Offset<NodeFB>),
      uint LastLogIndex = 0,
      uint LastLogTerm = 0) {
    builder.StartObject(4);
    VoteRequestFB.AddLastLogTerm(builder, LastLogTerm);
    VoteRequestFB.AddLastLogIndex(builder, LastLogIndex);
    VoteRequestFB.AddCandidate(builder, CandidateOffset);
    VoteRequestFB.AddTerm(builder, Term);
    return VoteRequestFB.EndVoteRequestFB(builder);
  }

  public static void StartVoteRequestFB(FlatBufferBuilder builder) { builder.StartObject(4); }
  public static void AddTerm(FlatBufferBuilder builder, uint Term) { builder.AddUint(0, Term, 0); }
  public static void AddCandidate(FlatBufferBuilder builder, Offset<NodeFB> CandidateOffset) { builder.AddOffset(1, CandidateOffset.Value, 0); }
  public static void AddLastLogIndex(FlatBufferBuilder builder, uint LastLogIndex) { builder.AddUint(2, LastLogIndex, 0); }
  public static void AddLastLogTerm(FlatBufferBuilder builder, uint LastLogTerm) { builder.AddUint(3, LastLogTerm, 0); }
  public static Offset<VoteRequestFB> EndVoteRequestFB(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<VoteRequestFB>(o);
  }
};


}
