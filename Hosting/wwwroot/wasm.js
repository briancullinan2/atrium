// WasmHost.js
export class WasmHost {
    constructor(config = {}) {
        this.memory = new WebAssembly.Memory({
            initial: config.initialPages || 3200,
            maximum: config.maxPages || 32000
        });

        this.table = new WebAssembly.Table({
            initial: 2000,
            element: 'anyfunc'
        });

        this.instance = null;
        this.exports = null;

        // Minimal Import Object
        this.importObject = {
            env: {
                memory: this.memory,
                __indirect_function_table: this.table,
                ...this.createStandardEnv(), // Logic for printf, malloc, etc.
                ...config.customImports
            },
            wasi_snapshot_preview1: this.createWasiShim()
        };
    }

    async load(wasmPath) {
        const response = await fetch(wasmPath);
        const { instance } = await WebAssembly.instantiateStreaming(response, this.importObject);
        this.instance = instance;
        this.exports = instance.exports;
        return this.exports;
    }

    createStandardEnv() {
        return {
            // Emscripten/C standard libs often expect these
            emscripten_memcpy_big: (dest, src, num) => {
                new Uint8Array(this.memory.buffer).set(
                    new Uint8Array(this.memory.buffer, src, num), dest
                );
            },
            // Add your GL/SYS/SND calls here directly
            Sys_Milliseconds: () => performance.now(),
            Z_Malloc: (size) => this.exports.malloc(size), // if exported
        };
    }

    createWasiShim() {
        // Essential to prevent "Missing Import" errors
        return {
            proc_exit: (code) => console.log(`Process exited with code ${code}`),
            fd_write: (fd, iovs, iovs_len, nwritten) => {
                // Simplified logging logic for stdout/stderr
                return 0;
            }
        };
    }

    // Helper to read strings from WASM memory
    readString(ptr) {
        const bytes = new Uint8Array(this.memory.buffer, ptr);
        let len = 0;
        while (bytes[len] !== 0) len++;
        return new TextDecoder().decode(bytes.slice(0, len));
    }
}

// QuakeModule.js
import { dotnet } from './_framework/dotnet.js'

export async function initWasm(wasmPath) {
    const { getAssemblyExports } = await dotnet.create();
    const exports = await getAssemblyExports("YourAssemblyName.dll");

    const importObject = {
        env: {
            // Map the WASM's import directly to the C# export
            Sys_Milliseconds: exports.QuakeEngine.GetMilliseconds,

            // Standard direct memory access
            memory: new WebAssembly.Memory({ initial: 3200 }),

            // Your custom GL/SND functions
            GL_BindTexture: (target, id) => { /* direct call */ }
        }
    };

    const { instance } = await WebAssembly.instantiateStreaming(fetch(wasmPath), importObject);
    instance.exports._start();
}