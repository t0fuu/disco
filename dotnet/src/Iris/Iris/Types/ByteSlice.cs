// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Types
{

using System;
using FlatBuffers;

public sealed class ByteSlice : Table {
  public static ByteSlice GetRootAsByteSlice(ByteBuffer _bb) { return GetRootAsByteSlice(_bb, new ByteSlice()); }
  public static ByteSlice GetRootAsByteSlice(ByteBuffer _bb, ByteSlice obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public ByteSlice __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public byte Index { get { int o = __offset(4); return o != 0 ? bb.Get(o + bb_pos) : (byte)0; } }
  public sbyte Value { get { int o = __offset(6); return o != 0 ? bb.GetSbyte(o + bb_pos) : (sbyte)0; } }

  public static Offset<ByteSlice> CreateByteSlice(FlatBufferBuilder builder,
      byte Index = 0,
      sbyte Value = 0) {
    builder.StartObject(2);
    ByteSlice.AddValue(builder, Value);
    ByteSlice.AddIndex(builder, Index);
    return ByteSlice.EndByteSlice(builder);
  }

  public static void StartByteSlice(FlatBufferBuilder builder) { builder.StartObject(2); }
  public static void AddIndex(FlatBufferBuilder builder, byte Index) { builder.AddByte(0, Index, 0); }
  public static void AddValue(FlatBufferBuilder builder, sbyte Value) { builder.AddSbyte(1, Value, 0); }
  public static Offset<ByteSlice> EndByteSlice(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<ByteSlice>(o);
  }
};


}
