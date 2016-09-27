// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization.Raft
{

using System;
using FlatBuffers;

public struct FloatSliceFB : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static FloatSliceFB GetRootAsFloatSliceFB(ByteBuffer _bb) { return GetRootAsFloatSliceFB(_bb, new FloatSliceFB()); }
  public static FloatSliceFB GetRootAsFloatSliceFB(ByteBuffer _bb, FloatSliceFB obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p.bb_pos = _i; __p.bb = _bb; }
  public FloatSliceFB __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  public uint Index { get { int o = __p.__offset(4); return o != 0 ? __p.bb.GetUint(o + __p.bb_pos) : (uint)0; } }
  public float Value { get { int o = __p.__offset(6); return o != 0 ? __p.bb.GetFloat(o + __p.bb_pos) : (float)0.0f; } }

  public static Offset<FloatSliceFB> CreateFloatSliceFB(FlatBufferBuilder builder,
      uint Index = 0,
      float Value = 0.0f) {
    builder.StartObject(2);
    FloatSliceFB.AddValue(builder, Value);
    FloatSliceFB.AddIndex(builder, Index);
    return FloatSliceFB.EndFloatSliceFB(builder);
  }

  public static void StartFloatSliceFB(FlatBufferBuilder builder) { builder.StartObject(2); }
  public static void AddIndex(FlatBufferBuilder builder, uint Index) { builder.AddUint(0, Index, 0); }
  public static void AddValue(FlatBufferBuilder builder, float Value) { builder.AddFloat(1, Value, 0.0f); }
  public static Offset<FloatSliceFB> EndFloatSliceFB(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    return new Offset<FloatSliceFB>(o);
  }
};


}
