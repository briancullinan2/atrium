#if !BROWSER
using Wasmtime;
#endif

namespace Hosting.Platforms.Windows;


public partial class QuakeModule : IDisposable
{
#if !BROWSER
    private readonly Engine _engine = new();
    private readonly Linker _linker;
    private readonly Store _store;
    private readonly Wasmtime.Module _module;
    private readonly Instance _instance;
    private readonly Memory _memory;

    public QuakeModule(string wasmPath)
    {
        _store = new Store(_engine);
        _module = Wasmtime.Module.FromFile(_engine, wasmPath);
        _linker = new Linker(_engine);

        // 1. Initialize Memory
        _memory = new Memory(_store, minimum: 3200, maximum: 32000);
        _linker.Define("env", "memory", _memory);

        // 2. Bind host functions (Add your GL/SND/FS calls here)
        BindHostFunctions();

        _instance = _linker.Instantiate(_store, _module);
    }

    private void BindHostFunctions()
    {
        // Instead of a structure, just flat defines for the 'env' namespace
        _linker.Define("env", "Sys_Milliseconds", Function.FromCallback(_store, () =>
            (int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        _linker.Define("env", "Z_Malloc", Function.FromCallback(_store, (int size) =>
            CallExport<int>("malloc", size)));

        // Add a stub for anything the linker complains is missing
        _linker.Define("wasi_snapshot_preview1", "proc_exit",
            Function.FromCallback(_store, (int code) => Environment.Exit(code)));
    }

    public void Start(string[] args)
    {
        var startFunc = _instance.GetFunction("_start");
        if (startFunc == null) return;

        // Convert C# strings to WASM memory pointers for char** argv
        int argc = args.Length;
        int argv = WriteStringsToMemory(args);

        startFunc.Invoke(argc, argv);
    }

    // Helper to invoke WASM exports from C#
    public T CallExport<T>(string name, params object[] args) =>
        (T)_instance.GetFunction(name)?.Invoke([.. args.Select(ValueBox.AsBox)])!;

    private int WriteStringsToMemory(string[] args)
    {
        // This is a simplified "bump allocator" logic 
        // In a real scenario, use your WASM's malloc
        int currentPtr = 1024; // Start safely past the header
        int[] pointers = new int[args.Length];

        for (int i = 0; i < args.Length; i++)
        {
            pointers[i] = currentPtr;
            byte[] bytes = Encoding.UTF8.GetBytes(args[i] + '\0');

            // Copy the managed byte[] into the WASM memory span.
            // Memory.Write<T> requires an unmanaged T, so we use GetSpan and Span.CopyTo instead.
            var dest = _memory.GetSpan(currentPtr, bytes.Length);
            bytes.AsSpan().CopyTo(dest);

            currentPtr += bytes.Length;
        }

        int argvPtr = currentPtr;
        for (int i = 0; i < pointers.Length; i++)
        {
            _memory.WriteInt32(argvPtr + (i * 4), pointers[i]);
        }

        return argvPtr;
    }

    public void Dispose()
    {
        _store.Dispose();
        _module.Dispose();
        _engine.Dispose();
        GC.SuppressFinalize(this);
    }
#else
    public void Dispose() { 
        GC.SuppressFinalize(this);
    }
#endif
}
