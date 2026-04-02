'use strict';
console.log('Service Worker registered');

// Import the Blazor-generated assets manifest
self.importScripts('./service-worker-assets.js');
self.importScripts('./service-worker-static.js');

var DB_STORE_NAME = 'FileCache';
var DB_NAME = 'AtriumOffline';
var DB_VERSION = 21;
var open = null;
const FS_DIR = 16384; 

// Blazor Filter Settings
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/service-worker-assets\.js$|^service-worker/];

/* --- Your IDB Interface Functions --- */
async function openDatabase() {
    
    if (open && open.readyState != 'done') {
        var checkInterval, count = 0;
        await new Promise(resolve => {
            checkInterval = setInterval(() => {
                if ((open != null && open.readyState == 'done') || count === 1000) {
                    clearInterval(checkInterval);
                    resolve();
                } else count++;
            }, 100);
        });
    }
    if (open && open.readyState == 'done') return Promise.resolve(open.result);
    open = indexedDB.open(DB_NAME, DB_VERSION);
    return await new Promise(function (resolve) {
        open.onsuccess = function() {
            if (!open.result || typeof open.result.transaction !== 'function') return resolve(null);
            resolve(open.result);
        };
        open.onupgradeneeded = function (evt) {
            var fileStore = open.result.createObjectStore(DB_STORE_NAME);
            if (!fileStore.indexNames.contains('timestamp')) {
                fileStore.createIndex('timestamp', 'timestamp', { unique: false });
            }
        };
        open.onerror = function (error) { console.error(error); resolve(error); };
    });
}

async function readFile(key) {
    var db = await openDatabase();
    if (!db) return;
    var transaction = db.transaction([DB_STORE_NAME], 'readwrite');
    var objStore = transaction.objectStore(DB_STORE_NAME);
    return await new Promise(function (resolve) {
        let tranCursor = objStore.get(key);
        tranCursor.onsuccess = function() { 
            resolve(tranCursor.result); 
        };
        tranCursor.onerror = function (error) { resolve(null); };
        transaction.commit();
    });
}

async function writeStore(value, key) {
    var db = await openDatabase();
    if (!db) return;
    var transaction = db.transaction([DB_STORE_NAME], 'readwrite');
    var objStore = transaction.objectStore(DB_STORE_NAME);
    return await new Promise(function (resolve) {
        let storeValue = objStore.put(value, key);
        transaction.oncomplete = function () { resolve(); };
        storeValue.onerror = function (error) { resolve(error); };
        transaction.commit();
    });
}

async function mkdirp(path) {
    var segments = path.split(/\/|\\/gi);
    for (var i = 3; i <= segments.length; i++) {
        var dir = '/' + segments.slice(0, i).join('/');
        var obj = { timestamp: new Date(), mode: FS_DIR };
        await writeStore(obj, dir);
    }
}




async function fetchAsset(url, key) {
    // 10s timeout for assets; enough for a moderate DLL but short enough 
    // to keep the app from feeling hung during a "reinstall" phase.
    const timeoutSignal = AbortSignal.timeout(10000);

    var response;
    try {
        response = typeof(url) == 'string' ? await fetch(url, {
            cache: 'no-store',
            credentials: 'omit', 
            mode: 'no-cors',
            signal: timeoutSignal
        }) : await fetch(url);

        // Handle Blazor's opaque responses. 
        // If it's NOT opaque and NOT ok (e.g., 404/500), we bail.
        
        if (response.redirected || response.type == 'opaqueredirect') {
            if(!url.includes('index.html'))
                console.warn('asset file was redirected: ' + url);
            const newHeaders = new Headers(response.headers);
            newHeaders.set('X-Service-Worker-Handled', 'true');
            newHeaders.set('Location', response.url);
            
            isOffline = false;

            return new Response(null, {
                status: 302,
                statusText: response.statusText,
                url: response.url,
                headers: newHeaders
            })
        }

        if (!response.ok && response.type !== 'opaque') {
            throw new Error(`Request for ${key} failed with status: ${response.status}`);
        }


        // We clone the response because .arrayBuffer() consumes the body,
        // and we still need to return the original response to the caller.
        var content = await response.clone().arrayBuffer();
        
        var localKey = '/base/' + key.replace(/^\/?base\/|^\/?assets\/|^\//ig, '');
        var dirPath = 'base/' + key.replace(/^\/?base\/|^\/?assets\/|^\/|\/[^\/]*$/ig, '');
        
        await mkdirp(dirPath);
        await writeStore({
            timestamp: new Date(),
            mode: 33206,
            contents: new Uint8Array(content)
        }, localKey);

        const newHeaders = new Headers(response.headers);
        newHeaders.set('X-Service-Worker-Handled', 'true');
        
        isOffline = false;

        return new Response(response.body, {
            status: response.status,
            statusText: response.statusText,
            headers: newHeaders
        })

    } catch (err) {

        if (!navigator.onLine || err instanceof TypeError)
        {
            isOffline = true;
            debugger;
        }

        // Specifically catch the timeout so you can log it distinctly from a 404
        if (err.name === 'TimeoutError' || err.name === 'AbortError') {
            
        }
        
        // Re-throw the error so your Promise.all in the 'install' 
        // listener properly fails and doesn't mark the install as 'complete'.
        throw err;
    }
}




/* --- Blazor Template Events with your Logic --- */

self.addEventListener('install', event => {
    console.info('Service worker: Install (Form-fitted IDB)');
    var fetchPromise = fetch('/version.json?t=' + Date.now(), { cache: 'no-store' })
        .then(async function(response) {
            if(response.ok)
            {
                await installAssets();
                self.skipWaiting()
            }
        });

    event.waitUntil(fetchPromise)
});


var assetLookup = null;


async function installAssets() {
     const assets = (self?.assetsManifest?.assets || [])
                    .concat((self?.assetsManifestStatic?.assets || []))
                    .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
                    .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)));

    return await Promise.all(assets.map(asset => {
        var localName = '/base/' + asset.url.replace(/^\/?assets\//ig, '');
        return readFile(localName).then(files => {
            // YOUR LOGIC: If it exists in IDB, do nothing. Otherwise, fetch.
            if (files && files.contents) return; 
            try {
                return fetchAsset(asset.url, localName).catch(e => {
                    console.warn("Offline asset failed: " + asset.url);
                });
            } catch (e) {
                console.warn("Offline asset failed: " + asset.url);
            }
        });
    })).then(() => {
    
        assetLookup = new Map();

        [...self.assetsManifest.assets, ...self.assetsManifestStatic.assets].forEach(a => {
            assetLookup.set(a.url, a.url); 
        });

    })
}


self.addEventListener('activate', event => {
    event.waitUntil(openDatabase().then(db => {
        open = null;
        db.close();
        //if (self.registration.navigationPreload) {
        //    self.registration.navigationPreload.enable();
        //}
        setTimeout(installAssets, 100);
        return self.clients.claim();
    }));
});


async function clearLocalDatabase(dbName) {
    return new Promise((rs) => {
        const req = indexedDB.deleteDatabase(dbName || DB_NAME)
        req.onsuccess = () => rs(true)
        req.onerror = () => rs(false)
        req.onblocked = () => rs(true) 

    })
}


// Global state for the singleton worker
let lastConnectivityCheck = 0;
let isOffline = false;
let connectivityLog = [];

/**
    * Connectivity Heartbeat
    * Pings a small resource to verify live status once per minute.
    */
var localVersion = null;
if(self.registration.active != null)
{
    const scriptUrl = self.registration.active.scriptURL;
    console.log('Whats wrong with the fucking URL: ', self.registration.active);
    const url = new URL(scriptUrl);
    const timestampStr = url.searchParams.get('t');
    if (timestampStr) {
        localVersion = parseInt(timestampStr, 10);
        //const date = new Date(timestamp);
    }
}

var needsRefresh = false

async function checkStatus() {
    const now = Date.now();
    if (now - lastConnectivityCheck < 10000) return;
    lastConnectivityCheck = now;

    try {
        if (!localVersion) {
            const versionFile = await readFile('/base/version.json');
            localVersion = versionFile ? JSON.parse(new TextDecoder('utf-8').decode(versionFile.contents))[1] : null;
        }
    } catch (e) {}

    var response;
    try {
        response = await fetchAsset('/version.json?t=' + Date.now(), 'version.json');
        if (!response.ok) throw new Error('Version check failed');
        
        const [appStart, latestFileTime] = await response.json();

        // 2. Comparison Logic
        if (localVersion && latestFileTime > localVersion) {
            console.warn('New version detected. Cleaning up...');
            
            // Wipe the database (assuming a utility exists to clear your IDB)
            await clearLocalDatabase(); 
            
            await self.registration.unregister();

            needsRefresh = true;
        } else if (!localVersion) {
        }

        isOffline = false;

    } catch (err) {
        console.warn('Connectivity/Version check failed:', err);
        // Network failed unexpectedly (tunnel dropped, etc.)
        if (!navigator.onLine || err instanceof TypeError) {
            isOffline = true; 
            debugger;
        }
    }
}

self.addEventListener('message', async (event) => {
    if (event.data && event.data.type === 'DEREGISTER') {
        
        await self.registration.unregister();

        needsRefresh = true;

        event.ports[0].postMessage({
            type: 'DEREGISTERED',
            version: localVersion
        });
    }
    else if (event.data && event.data.type === 'GET_VERSION') {
        // Fallback to reading from IDB if the memory variable is empty
        if (!localVersion) {
            const versionFile = await readFile('/base/version.json');
            localVersion = versionFile ? versionFile.contents : 'unknown';
        }

        // Reply to the specific port that asked
        event.ports[0].postMessage({
            type: 'VERSION_REPORT',
            version: localVersion
        });

        checkStatus();
    }
});

var ignoreUrlParametersMatching = [/^utm_|^t=/]

var stripIgnoredUrlParameters = function(originalUrl, ignoreUrlParametersMatching) {
    var url = new URL(originalUrl)
    // Remove the hash; see https://github.com/GoogleChrome/sw-precache/issues/290
    url.hash = ''
    url.search = url.search.slice(1) // Exclude initial '?'
        .split('&') // Split into an array of 'key=value' strings
        .map(function(kv) {
            return kv.split('='); // Split each 'key=value' string into a [key, value] array
        })
        .filter(function(kv) {
            return ignoreUrlParametersMatching.every(function(ignoredRegex) {
                return !ignoredRegex.test(kv[0]); // Return true iff the key doesn't match any of the regexes.
            })
        })
        .map(function(kv) {
            return kv.join('='); // Join each [key, value] array into a 'key=value' string
        })
        .join('&'); // Join the array of 'key=value' strings into a string with '&' in between each

    return url.pathname.replace(/^\//ig, '') + (url.search ? ('?' + url.search) : '')
}

function manufactureRefreshResponse() {
    const html = `
    <!DOCTYPE html>
    <html>
        <head><title>Updating...</title></head>
        <body>
            <script>
                // Force a reload from the server
                window.location = '/' + windows.location.pathname + "?t=" + Date.now();
            </script>
        </body>
    </html>
`;

    return new Response(html, {
        headers: { 
            'Content-Type': 'text/html',
            'X-Service-Worker-Handled': 'true',
            // Ensure the browser doesn't cache this "Updating" bridge
            'Cache-Control': 'no-store, no-cache, must-revalidate, max-age=0'
        }
    });
}


self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    
    // allow version to pass through so client can
    //   start in "server" mode, index.html will 
    //   cause a checkStatus(), and version.json wont
    if (!isOffline && event.request.url.includes('version.json')) {
        // browser will handle request naturally
        return;
    }

    //if (event.preloadResponse
    //    && event.request.mode === 'navigate') return event.respondWith(event.preloadResponse);

    var url = stripIgnoredUrlParameters(event.request.url, ignoreUrlParametersMatching)
    const isNavigation = event.request.mode === 'navigate';
    let assetUrl = assetLookup != null ? assetLookup.get(url) : null;

    if (!assetUrl && isNavigation) {
        assetUrl = 'index.html';
    }

    /*
    if (needsRefresh && isNavigation) {
        console.log('Serving refresh page for navigation to trigger update');

        debugger;

        self.registration.unregister();

        return manufactureRefreshResponse();
    }
    */

    if (!assetUrl) {
        return; // let browser handle it
    }
    
    const localName = '/base/' + assetUrl.replace(/^\/?assets\/|^\//ig, '');
    const contentType = getMimeType(assetUrl);

    event.respondWith((async () => {
        // 1. Run the heartbeat check (it only pings if the timer expired)
        checkStatus();
            

        // 2. If we think we are online, always try the Network first
        if (assetUrl) {
            try {
                // This lets the browser handle ETag/304/Disk Cache automatically
                const response = await fetchAsset(isOffline ? assetUrl : event.request, assetUrl);
                if (response.ok) return response;
            } catch (e) {
            }
        }

        // 3. Fallback: Only read from IndexedDB/Local if offline or network failed
        if (assetUrl) {
            const files = await readFile(localName);
            if (files && files.contents) {
                const newHeaders = new Headers();
                newHeaders.set('Content-Type', contentType);
                newHeaders.set('X-Service-Worker-Handled', 'true');

                return new Response(files.contents, {
                    status: 200,
                    statusText: 'OK',
                    headers: newHeaders
                });
            }
        }

        // 4. Final fallback if even the local DB fails
        return fetch(event.request); 
    })());
});


function getMimeType(url) {
    if (url.endsWith('.wasm')) return 'application/wasm';
    if (url.endsWith('.js')) return 'application/javascript';
    if (url.endsWith('.json')) return 'application/json';
    if (url.endsWith('.html')) return 'text/html';
    if (url.endsWith('.css')) return 'text/css';
    return 'application/octet-stream';
}