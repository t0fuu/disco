// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization.Raft
{

using System;
using FlatBuffers;

public sealed class CueListFB : Table {
  public static CueListFB GetRootAsCueListFB(ByteBuffer _bb) { return GetRootAsCueListFB(_bb, new CueListFB()); }
  public static CueListFB GetRootAsCueListFB(ByteBuffer _bb, CueListFB obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public CueListFB __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public string Id { get { int o = __offset(4); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetIdBytes() { return __vector_as_arraysegment(4); }
  public string Name { get { int o = __offset(6); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetNameBytes() { return __vector_as_arraysegment(6); }
  public CueFB GetCues(int j) { return GetCues(new CueFB(), j); }
  public CueFB GetCues(CueFB obj, int j) { int o = __offset(8); return o != 0 ? obj.__init(__indirect(__vector(o) + j * 4), bb) : null; }
  public int CuesLength { get { int o = __offset(8); return o != 0 ? __vector_len(o) : 0; } }

  public static Offset<CueListFB> CreateCueListFB(FlatBufferBuilder builder,
      StringOffset IdOffset = default(StringOffset),
      StringOffset NameOffset = default(StringOffset),
      VectorOffset CuesOffset = default(VectorOffset)) {
    builder.StartObject(3);
    CueListFB.AddCues(builder, CuesOffset);
    CueListFB.AddName(builder, NameOffset);
    CueListFB.AddId(builder, IdOffset);
    return CueListFB.EndCueListFB(builder);
  }

  public static void StartCueListFB(FlatBufferBuilder builder) { builder.StartObject(3); }
  public static void AddId(FlatBufferBuilder builder, StringOffset IdOffset) { builder.AddOffset(0, IdOffset.Value, 0); }
  public static void AddName(FlatBufferBuilder builder, StringOffset NameOffset) { builder.AddOffset(1, NameOffset.Value, 0); }
  public static void AddCues(FlatBufferBuilder builder, VectorOffset CuesOffset) { builder.AddOffset(2, CuesOffset.Value, 0); }
  public static VectorOffset CreateCuesVector(FlatBufferBuilder builder, Offset<CueFB>[] data) { builder.StartVector(4, data.Length, 4); for (int i = data.Length - 1; i >= 0; i--) builder.AddOffset(data[i].Value); return builder.EndVector(); }
  public static void StartCuesVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(4, numElems, 4); }
  public static Offset<CueListFB> EndCueListFB(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<CueListFB>(o);
  }
};


}