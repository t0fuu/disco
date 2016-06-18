// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization
{

using System;
using FlatBuffers;

public sealed class ShortSlice : Table {
  public static ShortSlice GetRootAsShortSlice(ByteBuffer _bb) { return GetRootAsShortSlice(_bb, new ShortSlice()); }
  public static ShortSlice GetRootAsShortSlice(ByteBuffer _bb, ShortSlice obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public ShortSlice __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public byte Index { get { int o = __offset(4); return o != 0 ? bb.Get(o + bb_pos) : (byte)0; } }
  public short Value { get { int o = __offset(6); return o != 0 ? bb.GetShort(o + bb_pos) : (short)0; } }

  public static Offset<ShortSlice> CreateShortSlice(FlatBufferBuilder builder,
      byte Index = 0,
      short Value = 0) {
    builder.StartObject(2);
    ShortSlice.AddValue(builder, Value);
    ShortSlice.AddIndex(builder, Index);
    return ShortSlice.EndShortSlice(builder);
  }

  public static void StartShortSlice(FlatBufferBuilder builder) { builder.StartObject(2); }
  public static void AddIndex(FlatBufferBuilder builder, byte Index) { builder.AddByte(0, Index, 0); }
  public static void AddValue(FlatBufferBuilder builder, short Value) { builder.AddShort(1, Value, 0); }
  public static Offset<ShortSlice> EndShortSlice(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<ShortSlice>(o);
  }
};


}
