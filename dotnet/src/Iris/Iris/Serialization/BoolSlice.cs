// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization
{

using System;
using FlatBuffers;

public sealed class BoolSlice : Table {
  public static BoolSlice GetRootAsBoolSlice(ByteBuffer _bb) { return GetRootAsBoolSlice(_bb, new BoolSlice()); }
  public static BoolSlice GetRootAsBoolSlice(ByteBuffer _bb, BoolSlice obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public BoolSlice __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public byte Index { get { int o = __offset(4); return o != 0 ? bb.Get(o + bb_pos) : (byte)0; } }
  public bool Value { get { int o = __offset(6); return o != 0 ? 0!=bb.Get(o + bb_pos) : (bool)false; } }

  public static Offset<BoolSlice> CreateBoolSlice(FlatBufferBuilder builder,
      byte Index = 0,
      bool Value = false) {
    builder.StartObject(2);
    BoolSlice.AddValue(builder, Value);
    BoolSlice.AddIndex(builder, Index);
    return BoolSlice.EndBoolSlice(builder);
  }

  public static void StartBoolSlice(FlatBufferBuilder builder) { builder.StartObject(2); }
  public static void AddIndex(FlatBufferBuilder builder, byte Index) { builder.AddByte(0, Index, 0); }
  public static void AddValue(FlatBufferBuilder builder, bool Value) { builder.AddBool(1, Value, false); }
  public static Offset<BoolSlice> EndBoolSlice(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<BoolSlice>(o);
  }
};


}
