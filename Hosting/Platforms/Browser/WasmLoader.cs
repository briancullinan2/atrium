using System.Runtime.InteropServices.JavaScript;

namespace Hosting.Platforms.Browser;

public partial class QuakeEngine
{
#if BROWSER
    // Directly import the JS function that initializes the WASM
    [JSImport("initWasm", "QuakeModule")]
    internal static partial Task InitializeWasm(string path);
#else
    static partial void InitializeWasm(string path);
#endif

    // Export a C# method so the WASM can call it for "Sys_Milliseconds"
#if BROWSER
    [JSExport]
#else
    [JSInvokable]
#endif
    public static int GetMilliseconds() =>
        (int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static async Task Start()
    {
#if BROWSER
        await InitializeWasm("quake3e.wasm");
#else
        InitializeWasm("quake3e.wasm");
#endif
        Console.WriteLine("WASM Loaded and Linked!");
    }
}
