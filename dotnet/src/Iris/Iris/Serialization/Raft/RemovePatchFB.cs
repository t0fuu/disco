// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization.Raft
{

using System;
using FlatBuffers;

public sealed class RemovePatchFB : Table {
  public static RemovePatchFB GetRootAsRemovePatchFB(ByteBuffer _bb) { return GetRootAsRemovePatchFB(_bb, new RemovePatchFB()); }
  public static RemovePatchFB GetRootAsRemovePatchFB(ByteBuffer _bb, RemovePatchFB obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public RemovePatchFB __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public PatchFB Patch { get { return GetPatch(new PatchFB()); } }
  public PatchFB GetPatch(PatchFB obj) { int o = __offset(4); return o != 0 ? obj.__init(__indirect(o + bb_pos), bb) : null; }

  public static Offset<RemovePatchFB> CreateRemovePatchFB(FlatBufferBuilder builder,
      Offset<PatchFB> PatchOffset = default(Offset<PatchFB>)) {
    builder.StartObject(1);
    RemovePatchFB.AddPatch(builder, PatchOffset);
    return RemovePatchFB.EndRemovePatchFB(builder);
  }

  public static void StartRemovePatchFB(FlatBufferBuilder builder) { builder.StartObject(1); }
  public static void AddPatch(FlatBufferBuilder builder, Offset<PatchFB> PatchOffset) { builder.AddOffset(0, PatchOffset.Value, 0); }
  public static Offset<RemovePatchFB> EndRemovePatchFB(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<RemovePatchFB>(o);
  }
};


}