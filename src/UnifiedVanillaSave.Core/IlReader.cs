using System.Reflection;
using System.Reflection.Emit;

namespace UnifiedVanillaSave.Core;

public static class IlReader
{
    public sealed record Instruction(int Offset, OpCode Code, object? Operand);
    private static readonly Dictionary<ushort, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => unchecked((ushort)o.Value));

    public static IEnumerable<Instruction> Read(byte[] bytes)
    {
        var position = 0;
        while (position < bytes.Length)
        {
            var offset = position;
            ushort code = bytes[position++];
            if (code == 0xFE) { Need(bytes, position, 1); code = (ushort)(0xFE00 | bytes[position++]); }
            if (!Codes.TryGetValue(code, out var opcode)) throw new BadImageFormatException($"Unknown IL opcode at {offset:X4}");
            object? operand = null;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineI:
                    Need(bytes, position, 1); operand = (sbyte)bytes[position++]; break;
                case OperandType.ShortInlineVar:
                    Need(bytes, position, 1); operand = bytes[position++]; break;
                case OperandType.InlineVar:
                    Need(bytes, position, 2); operand = BitConverter.ToUInt16(bytes, position); position += 2; break;
                case OperandType.ShortInlineBrTarget:
                    Need(bytes, position, 1); operand = "IL_" + (position + 1 + (sbyte)bytes[position]).ToString("X4"); position++; break;
                case OperandType.InlineBrTarget:
                    Need(bytes, position, 4); operand = "IL_" + (position + 4 + BitConverter.ToInt32(bytes, position)).ToString("X4"); position += 4; break;
                case OperandType.InlineI8:
                    Need(bytes, position, 8); operand = BitConverter.ToInt64(bytes, position); position += 8; break;
                case OperandType.ShortInlineR:
                    Need(bytes, position, 4); operand = BitConverter.ToSingle(bytes, position); position += 4; break;
                case OperandType.InlineR:
                    Need(bytes, position, 8); operand = BitConverter.ToDouble(bytes, position); position += 8; break;
                case OperandType.InlineSwitch:
                    Need(bytes, position, 4); var count = BitConverter.ToInt32(bytes, position); position += 4;
                    if (count < 0 || count > (bytes.Length - position) / 4) throw new BadImageFormatException("Invalid IL switch");
                    var end = position + count * 4;
                    var labels = new string[count];
                    for (var i = 0; i < count; i++) { labels[i] = "IL_" + (end + BitConverter.ToInt32(bytes, position)).ToString("X4"); position += 4; }
                    operand = string.Join(", ", labels); break;
                default:
                    Need(bytes, position, 4); operand = BitConverter.ToInt32(bytes, position); position += 4; break;
            }
            yield return new Instruction(offset, opcode, operand);
        }
    }

    private static void Need(byte[] bytes, int position, int count)
    {
        if (position > bytes.Length - count) throw new BadImageFormatException("Truncated IL operand");
    }
}
