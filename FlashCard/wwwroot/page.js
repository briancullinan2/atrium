
export function setSessionCookie(name, value, days) {
    const date = new Date()
    date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000))
    const expires = "expires=" + date.toUTCString()
    document.cookie = name + "=" + value + "" + expires + "path=/"
}


let currentAnimation = null
const loadedModules = {} // Simple cache to avoid re-imports
const scrollRegistry = new Map()
const trackedScrollIds = new Map()


export function subscribePageEvents(dotnetHelper) {
    // 1. Unified Window Events
    window.addEventListener('resize', () => {
        dotnetHelper.invokeMethodAsync('OnResized', window.innerWidth, window.innerHeight, window.innerWidth < 768)
    })

    document.addEventListener("visibilitychange", () => {
        dotnetHelper.invokeMethodAsync('OnVisibility', document.visibilityState)
    })

    window.onfocus = () => dotnetHelper.invokeMethodAsync('OnFocused', true)
    window.onblur = () => dotnetHelper.invokeMethodAsync('OnFocused', false)

    // 2. Global Button Interceptor (Custom Events)
    // Buttons just need to dispatch: window.dispatchEvent(new CustomEvent('app-retry'))
    window.addEventListener('app-disconnect', () => dotnetHelper.invokeMethodAsync('OnReconnected', 'disconnect'))
    window.addEventListener('app-start', () => dotnetHelper.invokeMethodAsync('OnReconnected', 'start'))
    window.addEventListener('app-reconnect', () => dotnetHelper.invokeMethodAsync('OnReconnected', 'reconnect'))
    window.addEventListener('app-resume', () => dotnetHelper.invokeMethodAsync('OnReconnected', 'resume'))
    window.addEventListener('app-pause', () => dotnetHelper.invokeMethodAsync('OnReconnected', 'pause'))

    window.addEventListener('app-disconnect', () => Blazor.disconnect());
    window.addEventListener('app-reconnect', () => Blazor.reconnect());
    window.addEventListener('app-pause', () => Blazor.pauseCircuit());
    window.addEventListener('app-resume', () => Blazor.resumeCircuit());
    window.addEventListener('app-start', () => startBlazor());

    window.addEventListener("components-reconnect-state-changed", (e) => {
        dotnetHelper.invokeMethodAsync('OnReconnected', e.detail)
    })

    window.addEventListener('app-stop', () => dotnetHelper.invokeMethodAsync('OnStopped'))

    window.addEventListener('scroll', (event) => {
        const target = event.target

        // Ensure the target is an element (not the document) and has an ID we are tracking
        if (target instanceof HTMLElement && target.id && trackedScrollIds.has(target.id)) {
            const id = target.id

            // Calculate if the specific element is at the bottom
            const isBottom = target.scrollHeight - target.scrollTop <= target.clientHeight + 5

            // Only notify C# if the state actually changed
            if (trackedScrollIds.get(id) !== isBottom) {
                trackedScrollIds.set(id, isBottom)
                dotnetHelper.invokeMethodAsync('OnScrolled', id, isBottom)
            }
        }
    }, true)


    window.getThemeColors = getThemeColors
    window.normalizeColor = normalizeColor
}


export function dispatchEvent(eventName, detail) { 
    const event = new CustomEvent(eventName, {
        detail: detail,
        bubbles: true,
        cancelable: true
    });

    window.dispatchEvent(event);
}



export function subscribeScroll(id) {
    const el = document.getElementById(id)
    if (el) {
        // Initial state check
        const initialBottom = el.scrollHeight - el.scrollTop <= el.clientHeight + 5
        trackedScrollIds.set(id, initialBottom)
        return initialBottom
    }
    return false
}

export async function scrollToBottom(id, smooth = false) {
    const el = document.getElementById(id)
    if (el) el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' })
}


export async function getScrollStates(ids) {
    const results = {}
    const targets = ids || Array.from(scrollRegistry.keys())
    targets.forEach(id => {
        results[id] = scrollUtils.isAtBottom(id)
    })
    return results
}


export async function isAtBottom(id) {
    const el = document.getElementById(id)
    if (!el) return false
    // Threshold of 5px to account for sub-pixel rendering issues
    return el.scrollHeight - el.scrollTop <= el.clientHeight + 5
}

export async function scrollSlightly(id, amount = 10)  {
    const el = document.getElementById(id)
    if (el) el.scrollTop += amount
}




// Basic Measurements
export async function getLineHeight(el) { return window.getComputedStyle(el || document.body).lineHeight }

export async function getLineHeightInt(el) {
    const style = window.getComputedStyle(el || document.body)
    const lh = style.lineHeight
    if (lh === 'normal') return Math.round(parseFloat(style.fontSize) * 1.2)
    return Math.round(parseFloat(lh))
}


// 5. Background Display Launcher (The Module Manager)
export async function initBackground(mode, canvasId) {
    // Dispose previous if it exists
    if (currentAnimation) {
        try { await currentAnimation.dispose() } catch (e) { console.warn(e) }
        currentAnimation = null
    }

    if (mode === "none") return null

    // Use the cache or import
    const path = `/_content/FlashCard/${mode.toLowerCase()}.js`
    if (!loadedModules[path]) {
        loadedModules[path] = await import(path)
    }

    const module = loadedModules[path]
    // Every module exports 'init' or 'initLichtenberg'
    currentAnimation = await module.initLichtenberg(canvasId)
    return currentAnimation
}


export function getThemeColors() {
    const sensor = document.getElementById('theme-sensor')
    if (!sensor) return {
        stone: 'rgba(63, 98, 18, 0.5)',
        nature: '#10b981',
        electric: '#67e8f9',
        gold: '#fbbf24'
    }
    const getStyle = (cls) => window.getComputedStyle(sensor.querySelector(cls)).color
    return {
        stone: getStyle('.sensor-stone'),
        main: getStyle('.sensor-main'),
        nature: getStyle('.sensor-nature'),
        electric: getStyle('.sensor-electric'),
        gold: getStyle('.sensor-gold')
    }
}


export function normalizeColor(colorStr) {
    // style will always be "rgb(r, g, b)" or "rgba(r, g, b, a)"
    const matches = colorStr.match(/\d+/g)
    return matches ? matches.slice(0, 3) : [0, 0, 0]
}


export function restoreState() {
    return Array.from(document.getElementsByTagName('input')).reduce((acc, input) => {
        let key = input.id || input.name || 'unnamed'
        if (key.substring(0, 6) == 'state_')
            acc[key] = input.value
        return acc
    }, {})
}


export function startBlazor() {
    Blazor.start({
        circuit: {
            // LogLevel: 0 (Trace), 1 (Debug), 2 (Information), etc.
            logLevel: 1,

            // Configuration for the reconnection logic
            reconnectionHandler: {
                onConnectionDown: (options, error) => dotnetHelper.invokeMethodAsync('OnReconnected', error),
                onConnectionUp: () => dotnetHelper.invokeMethodAsync('OnReconnected', "hide")
            },

            // Adjusting the internal circuit behavior
            configureSignalR: function(builder) {
                const afToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    
                builder.withUrl("_blazor", {
                    headers: { "X-XSRF-TOKEN": afToken }, // Standard header name
                    skipNegotiation: true,
                    transport: 1 
                });
            }
        }
    });
}


