using System;
using System.Linq;
using System.Reflection;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

using System.Runtime.InteropServices.JavaScript;

public partial class QuakeEngine
{
    // Directly import the JS function that initializes the WASM
    [JSImport("initWasm", "QuakeModule")]
    internal static partial Task InitializeWasm(string path);

    // Export a C# method so the WASM can call it for "Sys_Milliseconds"
    [JSExport]
    public static int GetMilliseconds() =>
        (int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static async Task Start()
    {
        await InitializeWasm("quake3e.wasm");
        Console.WriteLine("WASM Loaded and Linked!");
    }
}
