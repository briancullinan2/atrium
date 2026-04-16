import * as webllm from "https://esm.run/@mlc-ai/web-llm";

export async function createEngine(modelId, dotNetHelper) {
    const engine = await webllm.CreateMLCEngine(modelId, {
        initProgressCallback: (p) => {
            // Sends progress back to C#
            dotNetHelper.invokeMethodAsync("OnProgress", p.progress, p.text);
        }
    });
    return engine;
}

// Flattened invocations
export async function chatCompletion(engine, request) {
    return await engine.chat.completions.create(request);
}

export async function getStats(engine) {
    return await engine.runtimeStats();
}

export async function interrupt(engine) {
    await engine.interruptGenerate();
}

export async function unload(engine) {
    await engine.unload();
}

