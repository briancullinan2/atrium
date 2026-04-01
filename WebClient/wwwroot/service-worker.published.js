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
        tranCursor.onsuccess = function () { resolve(tranCursor.result); };
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
    const timeoutSignal = AbortSignal.timeout(5000);

    try {
        var response = await fetch(url, { 
            credentials: 'omit', 
            mode: 'no-cors',
            signal: timeoutSignal
        });

        // Handle Blazor's opaque responses. 
        // If it's NOT opaque and NOT ok (e.g., 404/500), we bail.
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

        return response;

    } catch (err) {
        // Specifically catch the timeout so you can log it distinctly from a 404
        if (err.name === 'TimeoutError' || err.name === 'AbortError') {
            console.warn(`Asset download timed out for: ${key} (${url})`);
        } else {
            console.error(`Failed to fetch/store asset: ${key}`, err);
        }
        
        // Re-throw the error so your Promise.all in the 'install' 
        // listener properly fails and doesn't mark the install as 'complete'.
        throw err;
    }
}




/* --- Blazor Template Events with your Logic --- */

self.addEventListener('install', event => {
    console.info('Service worker: Install (Form-fitted IDB)');
   
    event.waitUntil(installAssets().then(() => self.skipWaiting()))
});


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
            return fetchAsset(asset.url, localName);
        });
    }))
}



self.addEventListener('activate', event => {
    event.waitUntil(openDatabase().then(db => {
        open = null;
        db.close();
        return self.clients.claim();
    }));
});


function clearLocalDatabase(dbName) {
    return new Promise((rs) => {
        const req = indexedDB.deleteDatabase(dbName || DB_NAME)
        req.onsuccess = () => rs(true)
        req.onerror = () => rs(false) // Silent fail is usually fine for cleanup
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
var localVersion = null

async function checkStatus() {
    const now = Date.now();
    if (now - lastConnectivityCheck < 60000) return;
    lastConnectivityCheck = now;

    try {
        const response = await fetch('/version.json', { cache: 'no-store' });
        if (!response.ok) throw new Error('Version check failed');
        
        const [appStart, latestFileTime] = await response.json();

        // 1. Initial load: Get current local version if we don't have it
        if (!localVersion) {
            const versionFile = await readFile('/base/version.json');
            localVersion = versionFile ? versionFile.contents : null;
        }

        // 2. Comparison Logic
        if (localVersion && latestFileTime > localVersion) {
            console.info('New version detected. Cleaning up...');
            
            // Wipe the database (assuming a utility exists to clear your IDB)
            await clearLocalDatabase(); 
            
            // Save the new version timestamp so we don't loop
            localVersion = latestFileTime;
            
            // 3. Trigger Service Worker Update
            // This tells the browser to check the SW script for changes immediately
            await self.registration.update();

            await installAssets();
        } else if (!localVersion) {
            // First run: record the version
            localVersion = latestFileTime;
        }

    } catch (err) {
        console.warn('Connectivity/Version check failed:', err);
    }
}

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    const url = new URL(event.request.url);
    const isNavigation = event.request.mode === 'navigate';
    let assetUrl = getManifestMatch(url.pathname);

    if (!assetUrl && isNavigation) {
        assetUrl = 'index.html';
    }

    if (assetUrl) {
        const localName = '/base/' + assetUrl.replace(/^\/?assets\/|^\//ig, '');
        const contentType = getMimeType(assetUrl);

        event.respondWith((async () => {
            // 1. Run the heartbeat check (it only pings if the timer expired)
            checkStatus();

            // 2. If we think we are online, always try the Network first
            if (!isOffline) {
                try {
                    // This lets the browser handle ETag/304/Disk Cache automatically
                    const response = await fetch(event.request);
                    if (response.ok) return response;
                } catch (e) {
                    // Network failed unexpectedly (tunnel dropped, etc.)
                    isOffline = true; 
                }
            }

            // 3. Fallback: Only read from IndexedDB/Local if offline or network failed
            const files = await readFile(localName);
            if (files && files.contents) {
                return new Response(files.contents, {
                    headers: { 'Content-Type': contentType }
                });
            }

            // 4. Final fallback if even the local DB fails
            return fetch(event.request); 
        })());
    }
});

function getManifestMatch(path) {
    const relativePath = path.startsWith('/') ? path.substring(1) : path;
    const match = self.assetsManifest.assets.find(a => a.url === relativePath);
    return match ? match.url : null;
}

function getMimeType(url) {
    if (url.endsWith('.wasm')) return 'application/wasm';
    if (url.endsWith('.js')) return 'application/javascript';
    if (url.endsWith('.json')) return 'application/json';
    if (url.endsWith('.html')) return 'text/html';
    if (url.endsWith('.css')) return 'text/css';
    return 'application/octet-stream';
}