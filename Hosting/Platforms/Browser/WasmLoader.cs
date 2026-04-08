using System.Runtime.InteropServices.JavaScript;

namespace Hosting.Platforms.Browser;

public partial class QuakeEngine
{
    // Directly import the JS function that initializes the WASM
    [JSImport("initWasm", "QuakeModule")]
    internal static partial Task InitializeWasm(string path);

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
        await InitializeWasm("quake3e.wasm");
        Console.WriteLine("WASM Loaded and Linked!");
    }
}
