// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization.Raft
{

using System;
using FlatBuffers;

public sealed class UpdateSessionFB : Table {
  public static UpdateSessionFB GetRootAsUpdateSessionFB(ByteBuffer _bb) { return GetRootAsUpdateSessionFB(_bb, new UpdateSessionFB()); }
  public static UpdateSessionFB GetRootAsUpdateSessionFB(ByteBuffer _bb, UpdateSessionFB obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public UpdateSessionFB __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public SessionFB Session { get { return GetSession(new SessionFB()); } }
  public SessionFB GetSession(SessionFB obj) { int o = __offset(4); return o != 0 ? obj.__init(__indirect(o + bb_pos), bb) : null; }

  public static Offset<UpdateSessionFB> CreateUpdateSessionFB(FlatBufferBuilder builder,
      Offset<SessionFB> SessionOffset = default(Offset<SessionFB>)) {
    builder.StartObject(1);
    UpdateSessionFB.AddSession(builder, SessionOffset);
    return UpdateSessionFB.EndUpdateSessionFB(builder);
  }

  public static void StartUpdateSessionFB(FlatBufferBuilder builder) { builder.StartObject(1); }
  public static void AddSession(FlatBufferBuilder builder, Offset<SessionFB> SessionOffset) { builder.AddOffset(0, SessionOffset.Value, 0); }
  public static Offset<UpdateSessionFB> EndUpdateSessionFB(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<UpdateSessionFB>(o);
  }
};


}