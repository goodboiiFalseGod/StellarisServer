using System.Buffers.Binary;
using System.Text;

namespace Stellaris.Core.Assets;

// Только ограниченный read-only reader material metadata. Геометрию/skin проверяет local client importer.
internal static class GeneratedWorldVisualMeshMetadata
{
    internal sealed record Layer(string Name, IReadOnlyDictionary<string, string> Material);
    private sealed class Node(string name)
    {
        internal string Name { get; } = name;
        internal Dictionary<string, string[]> Strings { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Properties { get; } = new(StringComparer.Ordinal);
        internal List<Node> Children { get; } = [];
    }

    internal static IReadOnlyList<Layer> Read(byte[] bytes)
    {
        if (bytes.Length is < 4 or > 64 * 1024 * 1024 || Encoding.ASCII.GetString(bytes, 0, 4) != "@@b@") { throw new InvalidDataException("PDX signature/limit."); }
        var offset = 4; var root = new Node("$root"); var stack = new List<Node> { root }; var records = 0; int[]? version = null;
        byte Byte() { if (offset >= bytes.Length) throw new InvalidDataException("PDX truncated."); return bytes[offset++]; }
        uint UInt() { if (offset > bytes.Length - 4) throw new InvalidDataException("PDX truncated."); var v = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)); offset += 4; return v; }
        string Text(int count)
        {
            if (count < 0 || offset > bytes.Length - count) throw new InvalidDataException("PDX truncated.");
            var span = bytes.AsSpan(offset, count); if (span.ContainsAnyInRange((byte)128, byte.MaxValue)) throw new InvalidDataException("PDX ASCII only.");
            offset += count; return Encoding.ASCII.GetString(span);
        }
        while (offset < bytes.Length)
        {
            if (++records > 100000) throw new InvalidDataException("PDX record limit.");
            var token = Byte();
            if (token == '[')
            {
                var depth = 1; while (offset < bytes.Length && bytes[offset] == '[') { Byte(); depth++; }
                if (depth > 32 || depth > stack.Count) throw new InvalidDataException("PDX depth.");
                var name = new StringBuilder(); byte b;
                while ((b = Byte()) != 0) { if (b > 127 || name.Length >= 1024) throw new InvalidDataException("PDX node name."); name.Append((char)b); }
                if (name.Length == 0) throw new InvalidDataException("PDX empty node.");
                var node = new Node(name.ToString()); stack.RemoveRange(depth, stack.Count - depth); stack[^1].Children.Add(node); stack.Add(node);
            }
            else if (token == '!')
            {
                var name = Text(Byte()); var kind = Byte(); var count = UInt();
                if (name.Length == 0 || count > 8 * 1024 * 1024 || !stack[^1].Properties.Add(name)) throw new InvalidDataException("PDX property limit/duplicate.");
                if (kind is (byte)'i' or (byte)'f')
                {
                    if ((long)count * 4 > bytes.Length - offset) throw new InvalidDataException("PDX truncated numeric values.");
                    if (kind == 'f') { for (var i = 0; i < count; i++) { if (!float.IsFinite(BitConverter.Int32BitsToSingle((int)UInt()))) throw new InvalidDataException("PDX nonfinite."); } }
                    else if (stack.Count == 1 && name == "pdxasset") { if (count != 2) throw new InvalidDataException("PDX version."); version = [(int)UInt(), (int)UInt()]; }
                    else offset += checked((int)count * 4);
                }
                else if (kind == 's')
                {
                    if (count > 1024) throw new InvalidDataException("PDX string count.");
                    var values = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        var length = UInt(); if (length is < 1 or > 4096) throw new InvalidDataException("PDX string length.");
                        var value = Text((int)length); if (!value.EndsWith('\0') || value[..^1].Contains('\0')) throw new InvalidDataException("PDX null terminator.");
                        values[i] = value[..^1];
                    }
                    stack[^1].Strings.Add(name, values);
                }
                else throw new InvalidDataException("PDX property kind.");
            }
            else throw new InvalidDataException("PDX token.");
        }
        if (version is null || version[0] != 1 || version[1] != 0) throw new InvalidDataException("PDX version.");
        var layers = new List<Layer>();
        void Visit(Node node, string? objectName)
        {
            if (node.Name == "mesh")
            {
                var materialNodes = node.Children.Where(c => c.Name == "material").ToArray();
                if (materialNodes.Length != 1 || objectName is null) throw new InvalidDataException("PDX material/object.");
                var material = materialNodes[0].Strings.ToDictionary(pair => pair.Key, pair => pair.Value.Length == 1 ? pair.Value[0] : throw new InvalidDataException("PDX scalar material."), StringComparer.Ordinal);
                if (!material.TryGetValue("shader", out var shader)) throw new InvalidDataException("PDX missing shader.");
                if (shader != "Collision") layers.Add(new(objectName, material));
            }
            foreach (var child in node.Children) Visit(child, node.Name == "object" ? child.Name : objectName);
        }
        Visit(root, null);
        if (layers.Count == 0) throw new InvalidDataException("PDX missing visible layers.");
        return layers.AsReadOnly();
    }
}
