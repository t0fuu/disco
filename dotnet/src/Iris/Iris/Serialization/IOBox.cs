// automatically generated by the FlatBuffers compiler, do not modify

namespace Iris.Serialization
{

using System;
using FlatBuffers;

public sealed class IOBox : Table {
  public static IOBox GetRootAsIOBox(ByteBuffer _bb) { return GetRootAsIOBox(_bb, new IOBox()); }
  public static IOBox GetRootAsIOBox(ByteBuffer _bb, IOBox obj) { return (obj.__init(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public IOBox __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; return this; }

  public string Id { get { int o = __offset(4); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetIdBytes() { return __vector_as_arraysegment(4); }
  public string Name { get { int o = __offset(6); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetNameBytes() { return __vector_as_arraysegment(6); }
  public PinType Type { get { int o = __offset(8); return o != 0 ? (PinType)bb.GetShort(o + bb_pos) : PinType.Value; } }
  public string Patch { get { int o = __offset(10); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetPatchBytes() { return __vector_as_arraysegment(10); }
  public string GetTag(int j) { int o = __offset(12); return o != 0 ? __string(__vector(o) + j * 4) : null; }
  public int TagLength { get { int o = __offset(12); return o != 0 ? __vector_len(o) : 0; } }
  public Behavior Behavior { get { int o = __offset(14); return o != 0 ? (Behavior)bb.GetShort(o + bb_pos) : Behavior.Slider; } }
  public short VecSize { get { int o = __offset(16); return o != 0 ? bb.GetShort(o + bb_pos) : (short)0; } }
  public int Min { get { int o = __offset(18); return o != 0 ? bb.GetInt(o + bb_pos) : (int)0; } }
  public int Max { get { int o = __offset(20); return o != 0 ? bb.GetInt(o + bb_pos) : (int)0; } }
  public string Unit { get { int o = __offset(22); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetUnitBytes() { return __vector_as_arraysegment(22); }
  public int Precision { get { int o = __offset(24); return o != 0 ? bb.GetInt(o + bb_pos) : (int)0; } }
  public ValType ValueType { get { int o = __offset(26); return o != 0 ? (ValType)bb.GetShort(o + bb_pos) : ValType.Real; } }
  public StringType StringType { get { int o = __offset(28); return o != 0 ? (StringType)bb.GetShort(o + bb_pos) : StringType.Simple; } }
  public string FileMask { get { int o = __offset(30); return o != 0 ? __string(o + bb_pos) : null; } }
  public ArraySegment<byte>? GetFileMaskBytes() { return __vector_as_arraysegment(30); }
  public uint MaxChars { get { int o = __offset(32); return o != 0 ? bb.GetUint(o + bb_pos) : (uint)0; } }
  public string GetProperties(int j) { int o = __offset(34); return o != 0 ? __string(__vector(o) + j * 4) : null; }
  public int PropertiesLength { get { int o = __offset(34); return o != 0 ? __vector_len(o) : 0; } }
  public Iris.Serialization.Slice GetSlices(int j) { return GetSlices(new Iris.Serialization.Slice(), j); }
  public Iris.Serialization.Slice GetSlices(Iris.Serialization.Slice obj, int j) { int o = __offset(36); return o != 0 ? obj.__init(__indirect(__vector(o) + j * 4), bb) : null; }
  public int SlicesLength { get { int o = __offset(36); return o != 0 ? __vector_len(o) : 0; } }

  public static Offset<IOBox> CreateIOBox(FlatBufferBuilder builder,
      StringOffset IdOffset = default(StringOffset),
      StringOffset NameOffset = default(StringOffset),
      PinType Type = PinType.Value,
      StringOffset PatchOffset = default(StringOffset),
      VectorOffset TagOffset = default(VectorOffset),
      Behavior Behavior = Behavior.Slider,
      short VecSize = 0,
      int Min = 0,
      int Max = 0,
      StringOffset UnitOffset = default(StringOffset),
      int Precision = 0,
      ValType ValueType = ValType.Real,
      StringType StringType = StringType.Simple,
      StringOffset FileMaskOffset = default(StringOffset),
      uint MaxChars = 0,
      VectorOffset PropertiesOffset = default(VectorOffset),
      VectorOffset SlicesOffset = default(VectorOffset)) {
    builder.StartObject(17);
    IOBox.AddSlices(builder, SlicesOffset);
    IOBox.AddProperties(builder, PropertiesOffset);
    IOBox.AddMaxChars(builder, MaxChars);
    IOBox.AddFileMask(builder, FileMaskOffset);
    IOBox.AddPrecision(builder, Precision);
    IOBox.AddUnit(builder, UnitOffset);
    IOBox.AddMax(builder, Max);
    IOBox.AddMin(builder, Min);
    IOBox.AddTag(builder, TagOffset);
    IOBox.AddPatch(builder, PatchOffset);
    IOBox.AddName(builder, NameOffset);
    IOBox.AddId(builder, IdOffset);
    IOBox.AddStringType(builder, StringType);
    IOBox.AddValueType(builder, ValueType);
    IOBox.AddVecSize(builder, VecSize);
    IOBox.AddBehavior(builder, Behavior);
    IOBox.AddType(builder, Type);
    return IOBox.EndIOBox(builder);
  }

  public static void StartIOBox(FlatBufferBuilder builder) { builder.StartObject(17); }
  public static void AddId(FlatBufferBuilder builder, StringOffset IdOffset) { builder.AddOffset(0, IdOffset.Value, 0); }
  public static void AddName(FlatBufferBuilder builder, StringOffset NameOffset) { builder.AddOffset(1, NameOffset.Value, 0); }
  public static void AddType(FlatBufferBuilder builder, PinType Type) { builder.AddShort(2, (short)Type, 0); }
  public static void AddPatch(FlatBufferBuilder builder, StringOffset PatchOffset) { builder.AddOffset(3, PatchOffset.Value, 0); }
  public static void AddTag(FlatBufferBuilder builder, VectorOffset TagOffset) { builder.AddOffset(4, TagOffset.Value, 0); }
  public static VectorOffset CreateTagVector(FlatBufferBuilder builder, StringOffset[] data) { builder.StartVector(4, data.Length, 4); for (int i = data.Length - 1; i >= 0; i--) builder.AddOffset(data[i].Value); return builder.EndVector(); }
  public static void StartTagVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(4, numElems, 4); }
  public static void AddBehavior(FlatBufferBuilder builder, Behavior Behavior) { builder.AddShort(5, (short)Behavior, 0); }
  public static void AddVecSize(FlatBufferBuilder builder, short VecSize) { builder.AddShort(6, VecSize, 0); }
  public static void AddMin(FlatBufferBuilder builder, int Min) { builder.AddInt(7, Min, 0); }
  public static void AddMax(FlatBufferBuilder builder, int Max) { builder.AddInt(8, Max, 0); }
  public static void AddUnit(FlatBufferBuilder builder, StringOffset UnitOffset) { builder.AddOffset(9, UnitOffset.Value, 0); }
  public static void AddPrecision(FlatBufferBuilder builder, int Precision) { builder.AddInt(10, Precision, 0); }
  public static void AddValueType(FlatBufferBuilder builder, ValType ValueType) { builder.AddShort(11, (short)ValueType, 0); }
  public static void AddStringType(FlatBufferBuilder builder, StringType StringType) { builder.AddShort(12, (short)StringType, 0); }
  public static void AddFileMask(FlatBufferBuilder builder, StringOffset FileMaskOffset) { builder.AddOffset(13, FileMaskOffset.Value, 0); }
  public static void AddMaxChars(FlatBufferBuilder builder, uint MaxChars) { builder.AddUint(14, MaxChars, 0); }
  public static void AddProperties(FlatBufferBuilder builder, VectorOffset PropertiesOffset) { builder.AddOffset(15, PropertiesOffset.Value, 0); }
  public static VectorOffset CreatePropertiesVector(FlatBufferBuilder builder, StringOffset[] data) { builder.StartVector(4, data.Length, 4); for (int i = data.Length - 1; i >= 0; i--) builder.AddOffset(data[i].Value); return builder.EndVector(); }
  public static void StartPropertiesVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(4, numElems, 4); }
  public static void AddSlices(FlatBufferBuilder builder, VectorOffset SlicesOffset) { builder.AddOffset(16, SlicesOffset.Value, 0); }
  public static VectorOffset CreateSlicesVector(FlatBufferBuilder builder, Offset<Iris.Serialization.Slice>[] data) { builder.StartVector(4, data.Length, 4); for (int i = data.Length - 1; i >= 0; i--) builder.AddOffset(data[i].Value); return builder.EndVector(); }
  public static void StartSlicesVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(4, numElems, 4); }
  public static Offset<IOBox> EndIOBox(FlatBufferBuilder builder) {
    int o = builder.EndObject();
    builder.Required(o, 4);  // Id
    builder.Required(o, 6);  // Name
    return new Offset<IOBox>(o);
  }
  public static void FinishIOBoxBuffer(FlatBufferBuilder builder, Offset<IOBox> offset) { builder.Finish(offset.Value); }
};


}
