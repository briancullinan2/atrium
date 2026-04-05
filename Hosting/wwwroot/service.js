// serviceWorkerBridge.js
let dotNetHelper = null;

export async function init(helper) {
    dotNetHelper = helper;
    if ('serviceWorker' in navigator) {
        // Listen for generic messages (not sent via MessageChannel)
        navigator.serviceWorker.addEventListener('message', (event) => {
            dotNetHelper.invokeMethodAsync('ReceiveInternal', event.data);
        });
    }
}

export async function getStatus() {
    if (!('serviceWorker' in navigator)) return { supported: false };
    const reg = await navigator.serviceWorker.getRegistration();
    return {
        supported: true,
        isActive: !!reg?.active,
        isWaiting: !!reg?.waiting,
        isInstalling: !!reg?.installing,
        scope: reg?.scope || null,
        state: reg?.active?.state || 'none'
    };
}

export async function register(url) {
    try {
        const reg = await navigator.serviceWorker.register(url + '?t=' + Date.now());
        return { success: true, scope: reg.scope };
    } catch (e) {
        return { success: false, error: e.message };
    }
}

export async function unregister() {
    const reg = await navigator.serviceWorker.getRegistration();
    if (!reg) return false;
    return await reg.unregister();
}

/**
 * Sends a message and awaits a specific response via MessageChannel.
 * This handles your 'Dumb Poll' logic natively in JS.
 */
export async function postMessageWithResponse(message, timeoutMs = 10000) {
    const reg = await navigator.serviceWorker.getRegistration();
    if (!reg?.active) throw new Error("No active service worker");

    return new Promise((resolve, reject) => {
        const channel = new MessageChannel();
        const timeout = setTimeout(() => {
            channel.port1.onmessage = null;
            reject(new Error("Service Worker response timeout"));
        }, timeoutMs);

        channel.port1.onmessage = (event) => {
            clearTimeout(timeout);
            resolve(event.data);
        };

        reg.active.postMessage(message, [channel.port2]);
    });
}

export async function update() {
    const reg = await navigator.serviceWorker.getRegistration();
    if (reg) await reg.update();
}

