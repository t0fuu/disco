// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization
{

using System;
using FlatBuffers;

public sealed class RemoveCue : Table {
  public static RemoveCue GetRootAsRemoveCue(ByteBuffer _bb) { return GetRootAsRemoveCue(_bb, new RemoveCue()); }
  public static RemoveCue GetRootAsRemoveCue(ByteBuffer _bb, RemoveCue obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public RemoveCue __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public Iris.Serialization.Cue Payload { get { return GetPayload(new Iris.Serialization.Cue()); } }
  public Iris.Serialization.Cue GetPayload(Iris.Serialization.Cue obj) { int o = __offset(4); return o != 0 ? obj.__init(__indirect(o + bb_pos), bb) : null; }

  public static Offset<RemoveCue> CreateRemoveCue(FlatBufferBuilder builder,
      Offset<Iris.Serialization.Cue> PayloadOffset = default(Offset<Iris.Serialization.Cue>)) {
    builder.StartObject(1);
    RemoveCue.AddPayload(builder, PayloadOffset);
    return RemoveCue.EndRemoveCue(builder);
  }

  public static void StartRemoveCue(FlatBufferBuilder builder) { builder.StartObject(1); }
  public static void AddPayload(FlatBufferBuilder builder, Offset<Iris.Serialization.Cue> PayloadOffset) { builder.AddOffset(0, PayloadOffset.Value, 0); }
  public static Offset<RemoveCue> EndRemoveCue(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<RemoveCue>(o);
  }
};


}
