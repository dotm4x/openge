using System.Buffers.Binary;
using System.Text;
using OpenGE.Graphics.Resources;

namespace OpenGE.SDL3.Rendering;

internal readonly record struct SDL3ShaderLayout(uint Samplers, uint UniformBuffers);

// Inspects trusted compiler output for the limited Render2DCommand shader ABI.
// Requires one entry point and four float4 MVP rows in the vertex uniform.
internal static class SDL3SpirV
{
    private static NotSupportedException Unsupported(string message) => new(message);

    public static SDL3ShaderLayout Inspect(Shader shader)
    {
        if (shader.Format != ShaderFormat.SpirV)
            throw Unsupported("Only SPIR-V shaders are supported.");
        var bytes = shader.Code.Span;
        if (bytes.Length < 20 || bytes.Length % 4 != 0)
            throw Unsupported("The shader does not contain a complete SPIR-V module.");
        var words = new uint[bytes.Length / 4];
        for (var i = 0; i < words.Length; i++)
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 4, 4));
        if (words[0] != 0x07230203)
            throw Unsupported("The SPIR-V magic number is invalid.");
        var model = shader.Stage switch
        {
            ShaderStage.Vertex => 0u,
            ShaderStage.Fragment => 4u,
            _ => throw Unsupported("The shader stage is unsupported."),
        };

        var types = new Dictionary<uint, TypeInfo>();
        var decorations = new Dictionary<(uint Id, uint Kind), uint[]>();
        var offsets = new Dictionary<(uint Id, uint Member), uint>();
        var variables = new List<(uint Type, uint Id, uint Storage)>();
        var entryPoints = 0;

        for (var offset = 5; offset < words.Length; )
        {
            var length = checked((int)(words[offset] >> 16));
            var opcode = words[offset] & 0xffff;
            if (length < 1 || length > words.Length - offset)
                throw Unsupported("A SPIR-V instruction is truncated.");
            var args = words.AsSpan(offset + 1, length - 1);
            switch (opcode)
            {
                case 15: // OpEntryPoint
                    if (args.Length < 3)
                        throw Unsupported("Invalid SPIR-V entry point.");
                    entryPoints++;
                    var nameBytes = new List<byte>();
                    for (var index = 2; index < args.Length; index++)
                    {
                        var value = args[index];
                        for (var shift = 0; shift < 32; shift += 8)
                        {
                            var b = (byte)(value >> shift);
                            if (b == 0)
                                goto EndName;
                            nameBytes.Add(b);
                        }
                    }
                    throw Unsupported("Unterminated entry point name.");
                    EndName:
                    if (
                        args[0] != model
                        || Encoding.UTF8.GetString(nameBytes.ToArray()) != shader.EntryPoint
                    )
                        throw Unsupported(
                            "The SPIR-V entry point or stage does not match the Shader resource."
                        );
                    break;
                case >= 19 and <= 33: // OpType*
                    if (args.Length > 0)
                        types[args[0]] = new TypeInfo(opcode, args[1..].ToArray());
                    break;
                case 59: // OpVariable
                    if (args.Length < 3)
                        throw Unsupported("Invalid SPIR-V variable.");
                    variables.Add((args[0], args[1], args[2]));
                    break;
                case 71: // OpDecorate
                    if (args.Length < 2)
                        throw Unsupported("Invalid SPIR-V decoration.");
                    decorations[(args[0], args[1])] = args[2..].ToArray();
                    break;
                case 72: // OpMemberDecorate (Offset)
                    if (args.Length >= 4 && args[2] == 35)
                        offsets[(args[0], args[1])] = args[3];
                    break;
                case 73 or 74 or 75: // Decoration groups require full reflection.
                    throw Unsupported(
                        "SPIR-V decoration groups are not supported by this ABI reader."
                    );
            }
            offset += length;
        }
        if (entryPoints != 1)
            throw Unsupported("Each Shader must contain exactly one entry point.");

        TypeInfo Type(uint id) =>
            types.TryGetValue(id, out var type)
                ? type
                : throw Unsupported($"SPIR-V type {id} is missing.");
        uint Decoration(uint id, uint kind) =>
            decorations.TryGetValue((id, kind), out var value) && value.Length == 1
                ? value[0]
                : throw Unsupported($"SPIR-V resource {id} is missing decoration {kind}.");
        bool FloatVector(uint id, uint count)
        {
            var vector = Type(id);
            if (vector.Opcode != 23 || vector.Args.Length != 2 || vector.Args[1] != count)
                return false;
            var scalar = Type(vector.Args[0]);
            return scalar.Opcode == 22 && scalar.Args.Length == 1 && scalar.Args[0] == 32;
        }

        var samplers = new SortedSet<uint>();
        var uniforms = new SortedSet<uint>();
        var samplerSet = model == 0 ? 0u : 2u;

        foreach (var variable in variables)
        {
            var pointer = Type(variable.Type);
            if (pointer.Opcode != 32 || pointer.Args.Length != 2)
                throw Unsupported("A shader variable does not use a supported pointer type.");
            var typeId = pointer.Args[1];
            var type = Type(typeId);

            if (variable.Storage == 1 && model == 0) // Vertex inputs.
            {
                if (decorations.ContainsKey((variable.Id, 11))) // BuiltIn
                    continue;
                var location = Decoration(variable.Id, 30);
                if (location > 2 || !FloatVector(typeId, location == 2 ? 4u : 2u))
                    throw Unsupported(
                        "Vertex inputs must use float2/float2/float4 at locations 0/1/2."
                    );
            }
            else if (variable.Storage == 0) // UniformConstant: combined sampler2D only.
            {
                if (type.Opcode != 27 || type.Args.Length != 1)
                    throw Unsupported(
                        "Use combined image samplers. In HLSL, annotate both Texture2D and SamplerState with vk::combinedImageSampler and the same vk::binding."
                    );
                var image = Type(type.Args[0]);
                if (
                    image.Opcode != 25
                    || image.Args.Length < 7
                    || image.Args[1] != 1
                    || image.Args[2] == 1
                    || image.Args[3] != 0
                    || image.Args[4] != 0
                    || image.Args[5] != 1
                )
                    throw Unsupported(
                        "Only non-arrayed, non-depth, non-multisampled sampled Texture2D resources are supported."
                    );
                var scalar = Type(image.Args[0]);
                if (scalar.Opcode != 22 || scalar.Args[0] != 32)
                    throw Unsupported("Sampled textures must return 32-bit floats.");
                if (
                    Decoration(variable.Id, 34) != samplerSet
                    || !samplers.Add(Decoration(variable.Id, 33))
                )
                    throw Unsupported(
                        $"Sampler bindings must be unique and use descriptor set {samplerSet}."
                    );
            }
            else if (variable.Storage == 2) // Uniform block.
            {
                if (
                    model != 0
                    || Decoration(variable.Id, 34) != 1
                    || Decoration(variable.Id, 33) != 0
                )
                    throw Unsupported(
                        "Render2D supplies only vertex uniform slot 0 (set 1, binding 0)."
                    );
                if (
                    type.Opcode != 30
                    || type.Args.Length != 4
                    || !decorations.ContainsKey((typeId, 2))
                )
                    throw Unsupported(
                        "The vertex uniform block must contain four float4 MVP rows."
                    );
                for (uint member = 0; member < 4; member++)
                    if (
                        !FloatVector(type.Args[member], 4)
                        || !offsets.TryGetValue((typeId, member), out var at)
                        || at != member * 16
                    )
                        throw Unsupported(
                            "MVP rows must be float4 fields at byte offsets 0, 16, 32 and 48."
                        );
                if (!uniforms.Add(0))
                    throw Unsupported("Duplicate vertex uniform block.");
            }
            else if (variable.Storage is 9 or 12 or 11 or 5349)
                throw Unsupported(
                    "Push constants, storage resources and physical pointers are not supported by Render2DCommand."
                );
        }
        uint expected = 0;
        foreach (var binding in samplers)
            if (binding != expected++)
                throw Unsupported("Texture/sampler bindings must start at zero and have no gaps.");
        if (samplers.Count > 16)
            throw Unsupported("This backend supports at most 16 sampler bindings per stage.");
        if (model == 0 && uniforms.Count != 1)
            throw Unsupported(
                "A Render2D vertex shader must declare the MVP uniform at set 1, binding 0."
            );
        return new SDL3ShaderLayout((uint)samplers.Count, (uint)uniforms.Count);
    }

    private readonly record struct TypeInfo(uint Opcode, uint[] Args);
}
