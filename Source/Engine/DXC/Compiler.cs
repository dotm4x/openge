using System.Runtime.InteropServices;
using System.Text;
using OpenGE.Graphics.Resources;

namespace OpenGE.DXC;

/// <summary>
/// Compiles HLSL shader source code into SPIR-V.
/// </summary>
/// <remarks>
/// Requires the native DirectX Shader Compiler library.
/// Compilation does not require an active graphics context.
/// </remarks>
public class DxcShaderCompiler : ShaderCompiler
{
    private static readonly Guid CompilerClass = new("73E22D93-E6CE-47F3-B5BF-F0664F39C1B0");

    private static readonly Guid CompilerInterface = new("228B4687-5A6A-4730-900C-9702B2203F54");

    private static readonly Guid ResultInterface = new("58346CDA-DDE7-4497-9461-6F87AF5E0659");

    private static readonly Guid UtilitiesClass = new("6245D6AF-66E0-48FD-80B4-4D271796748C");

    private static readonly Guid UtilitiesInterface = new("4605C4CB-2019-492A-ADA4-65F20BB7D67F");

    private static readonly Encoding ArgumentEncoding = OperatingSystem.IsWindows()
        ? Encoding.Unicode
        : new UTF32Encoding(
            bigEndian: !BitConverter.IsLittleEndian,
            byteOrderMark: false,
            throwOnInvalidCharacters: true
        );

    private readonly string? _libraryPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="DxcShaderCompiler"/> class.
    /// </summary>
    /// <param name="libraryPath">
    /// The optional explicit path to the native DXC library.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The supplied library path is empty.
    /// </exception>
    public DxcShaderCompiler(string? libraryPath = null)
    {
        if (libraryPath is null)
            return;

        if (string.IsNullOrWhiteSpace(libraryPath))
            throw new ArgumentException(
                "The native library path cannot be empty.",
                nameof(libraryPath)
            );

        _libraryPath = Path.GetFullPath(libraryPath);
    }

    /// <inheritdoc />
    public override bool Supports(ShaderLanguage language, ShaderStage stage, ShaderFormat format)
    {
        return language == ShaderLanguage.Hlsl
            && format == ShaderFormat.SpirV
            && stage is ShaderStage.Vertex or ShaderStage.Fragment or ShaderStage.Compute;
    }

    /// <inheritdoc />
    protected override ShaderBinary OnCompile(ShaderSource source, ShaderFormat format)
    {
        var profile = source.Stage switch
        {
            ShaderStage.Vertex => "vs_6_0",
            ShaderStage.Fragment => "ps_6_0",
            ShaderStage.Compute => "cs_6_0",
            _ => throw new NotSupportedException(
                $"Shader stage '{source.Stage}' is not supported."
            ),
        };

        ValidateArgument(source.EntryPoint, nameof(source.EntryPoint));

        var sourcePath = source.Path is null ? null : Path.GetFullPath(source.Path);

        if (sourcePath is not null)
            ValidateArgument(sourcePath, nameof(source.Path));

        var arguments = new List<string>();

        if (sourcePath is not null)
            arguments.Add(sourcePath);

        arguments.AddRange([
            "-E",
            source.EntryPoint,
            "-T",
            profile,
            "-spirv",
            "-fspv-target-env=vulkan1.1",
            "-fvk-use-dx-layout",
            "-HV",
            "2018",
            "-O3",
        ]);

        if (sourcePath is not null)
        {
            var directory = Path.GetDirectoryName(sourcePath);

            if (!string.IsNullOrEmpty(directory))
            {
                arguments.Add("-I");
                arguments.Add(directory);
            }
        }

        var library = IntPtr.Zero;
        var compiler = IntPtr.Zero;
        var utilities = IntPtr.Zero;
        var includeHandler = IntPtr.Zero;
        var result = IntPtr.Zero;
        var errors = IntPtr.Zero;
        var output = IntPtr.Zero;
        var sourcePointer = IntPtr.Zero;
        var argumentTable = IntPtr.Zero;

        var argumentPointers = new List<IntPtr>(arguments.Count);

        try
        {
            library = LoadLibrary();

            var createInstance = Marshal.GetDelegateForFunctionPointer<CreateInstance>(
                NativeLibrary.GetExport(library, "DxcCreateInstance")
            );

            CheckResult(
                createInstance(in CompilerClass, in CompilerInterface, out compiler),
                "create the DXC compiler"
            );

            CheckResult(
                createInstance(in UtilitiesClass, in UtilitiesInterface, out utilities),
                "create DXC utilities"
            );

            // IDxcUtils::CreateDefaultIncludeHandler
            CheckResult(
                Method<CreateIncludeHandler>(utilities, 9)(utilities, out includeHandler),
                "create the DXC include handler"
            );

            var sourceBytes = Encoding.UTF8.GetBytes(source.Code);

            sourcePointer = Marshal.AllocHGlobal(sourceBytes.Length);
            Marshal.Copy(sourceBytes, 0, sourcePointer, sourceBytes.Length);

            var buffer = new SourceBuffer
            {
                Pointer = sourcePointer,
                Size = (nuint)sourceBytes.Length,
                Encoding = 65001,
            };

            argumentTable = Marshal.AllocHGlobal(checked(arguments.Count * IntPtr.Size));

            for (var index = 0; index < arguments.Count; index++)
            {
                var bytes = ArgumentEncoding.GetBytes(arguments[index] + "\0");
                var pointer = Marshal.AllocHGlobal(bytes.Length);

                argumentPointers.Add(pointer);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);

                Marshal.WriteIntPtr(argumentTable, index * IntPtr.Size, pointer);
            }

            // IDxcCompiler3::Compile
            CheckResult(
                Method<CompileNative>(compiler, 3)(
                    compiler,
                    in buffer,
                    argumentTable,
                    (uint)arguments.Count,
                    includeHandler,
                    in ResultInterface,
                    out result
                ),
                "invoke DXC compilation"
            );

            if (result == IntPtr.Zero)
                throw new InvalidDataException("DXC returned no compilation result.");

            // IDxcOperationResult::GetStatus
            CheckResult(
                Method<GetStatus>(result, 3)(result, out var status),
                "retrieve the compilation status"
            );

            // IDxcOperationResult::GetErrorBuffer
            var errorStatus = Method<GetBlob>(result, 5)(result, out errors);

            var diagnostics =
                errorStatus >= 0 && errors != IntPtr.Zero
                    ? Encoding.UTF8.GetString(ReadBlob(errors)).TrimEnd('\0')
                    : string.Empty;

            if (status < 0)
            {
                var message =
                    $"Failed to compile shader '{source.Path ?? "<memory>"}' "
                    + $"with profile '{profile}' "
                    + $"(HRESULT 0x{status:X8}).";

                if (!string.IsNullOrWhiteSpace(diagnostics))
                    message += Environment.NewLine + diagnostics;

                throw new InvalidDataException(message);
            }

            // IDxcOperationResult::GetResult
            CheckResult(
                Method<GetBlob>(result, 4)(result, out output),
                "retrieve the compiled shader"
            );

            if (output == IntPtr.Zero)
                throw new InvalidDataException("DXC returned no compiled shader code.");

            var bytecode = ReadBlob(output);

            if (!HasSpirVHeader(bytecode))
                throw new InvalidDataException(
                    "DXC returned code without a recognized SPIR-V header."
                );

            return new ShaderBinary(bytecode, source.Stage, format, source.EntryPoint);
        }
        finally
        {
            ReleaseObject(output);
            ReleaseObject(errors);
            ReleaseObject(result);
            ReleaseObject(includeHandler);
            ReleaseObject(utilities);
            ReleaseObject(compiler);

            foreach (var pointer in argumentPointers)
                Marshal.FreeHGlobal(pointer);

            if (argumentTable != IntPtr.Zero)
                Marshal.FreeHGlobal(argumentTable);

            if (sourcePointer != IntPtr.Zero)
                Marshal.FreeHGlobal(sourcePointer);

            if (library != IntPtr.Zero)
                NativeLibrary.Free(library);
        }
    }

    private IntPtr LoadLibrary()
    {
        if (_libraryPath is not null)
            return NativeLibrary.Load(_libraryPath);

        var platform =
            OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsMacOS() ? "osx"
            : throw new PlatformNotSupportedException(
                "DXC library discovery is not supported on this platform."
            );

        var name = platform switch
        {
            "win" => "dxcompiler.dll",
            "linux" => "libdxcompiler.so",
            _ => "libdxcompiler.dylib",
        };

        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        var portableIdentifier = $"{platform}-{architecture}";

        string[] directories =
        [
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(DxcShaderCompiler).Assembly.Location)
                ?? AppContext.BaseDirectory,
        ];

        var candidates = new List<string>();

        foreach (var directory in directories)
        {
            candidates.Add(Path.Combine(directory, name));

            candidates.Add(Path.Combine(directory, "runtimes", portableIdentifier, "native", name));

            candidates.Add(
                Path.Combine(
                    directory,
                    "runtimes",
                    RuntimeInformation.RuntimeIdentifier,
                    "native",
                    name
                )
            );
        }

        foreach (var candidate in candidates.Distinct())
        {
            if (File.Exists(candidate))
                return NativeLibrary.Load(candidate);
        }

        try
        {
            return NativeLibrary.Load(name, typeof(DxcShaderCompiler).Assembly, null);
        }
        catch (DllNotFoundException exception)
        {
            throw new DllNotFoundException(
                $"Could not load '{name}'. Deploy the native DXC library "
                    + $"for '{portableIdentifier}' or supply its explicit path."
                    + Environment.NewLine
                    + "Searched:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, candidates.Distinct()),
                exception
            );
        }
    }

    private static void ValidateArgument(string value, string name)
    {
        if (value.Contains('\0'))
            throw new ArgumentException("DXC arguments cannot contain null characters.", name);
    }

    private static void CheckResult(int result, string operation)
    {
        if (result < 0)
            throw new InvalidOperationException(
                $"Failed to {operation} (HRESULT 0x{result:X8}).",
                Marshal.GetExceptionForHR(result)
            );
    }

    private static bool HasSpirVHeader(byte[] code)
    {
        return code.Length >= 20
            && code.Length % 4 == 0
            && (
                (code[0] == 0x03 && code[1] == 0x02 && code[2] == 0x23 && code[3] == 0x07)
                || (code[0] == 0x07 && code[1] == 0x23 && code[2] == 0x02 && code[3] == 0x03)
            );
    }

    private static byte[] ReadBlob(IntPtr blob)
    {
        var pointer = Method<GetBufferPointer>(blob, 3)(blob);
        var size = checked((int)Method<GetBufferSize>(blob, 4)(blob));

        if (size > 0 && pointer == IntPtr.Zero)
            throw new InvalidDataException("DXC returned an invalid native buffer.");

        var bytes = new byte[size];

        if (size > 0)
            Marshal.Copy(pointer, bytes, 0, size);

        return bytes;
    }

    private static T Method<T>(IntPtr instance, int slot)
        where T : Delegate
    {
        var table = Marshal.ReadIntPtr(instance);
        var pointer = Marshal.ReadIntPtr(table, slot * IntPtr.Size);

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private static void ReleaseObject(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
            Method<Release>(instance, 2)(instance);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SourceBuffer
    {
        public IntPtr Pointer;
        public nuint Size;
        public uint Encoding;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int CreateInstance(in Guid classId, in Guid interfaceId, out IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int CompileNative(
        IntPtr instance,
        in SourceBuffer source,
        IntPtr arguments,
        uint argumentCount,
        IntPtr includeHandler,
        in Guid interfaceId,
        out IntPtr result
    );

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int CreateIncludeHandler(IntPtr instance, out IntPtr includeHandler);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetStatus(IntPtr instance, out int status);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetBlob(IntPtr instance, out IntPtr blob);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetBufferPointer(IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nuint GetBufferSize(IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint Release(IntPtr instance);
}
