// wwwroot/js/dbStore.js

const DB_VERSION = 1 // Increment this when you add new C# Entities!
const DB_NAME = "AtriumCache" + DB_VERSION

export async function getDB(dbName = null, dbVersion = null) {
    return new Promise((rs, rj) => {
        const req = indexedDB.open(dbName || DB_NAME, dbVersion || DB_VERSION)
        req.onsuccess = () => rs(req.result)
        req.onerror = () => rj(req.error)
        // Note: setupStore handles the onupgradeneeded logic
        return [dbName || DB_NAME, dbVersion || DB_VERSION]
    })
}

export async function deleteOldDatabase(dbName = null) {
    return new Promise((rs) => {
        const req = indexedDB.deleteDatabase(dbName || DB_NAME)
        req.onsuccess = () => rs(true)
        req.onerror = () => rs(false) // Silent fail is usually fine for cleanup
    })
}


export async function getDatabaseMetadata() {
    // Returns a list of { name, version } objects
    // Note: this may not be supported in very old WebViews, 
    // but works in modern MAUI (Edge/WebView2/WebKit)
    if (!indexedDB.databases) {
        return [];
    }
    const dbs = await indexedDB.databases();
    return dbs.map(db => ({ key: db.name, value: db.version }));
}


export async function needsInstall(dbName, expectedStores) {
    return new Promise((resolve) => {
        const request = indexedDB.open(dbName || DB_NAME, DB_VERSION);

        request.onsuccess = (event) => {
            const db = event.target.result;
            const existingStores = Array.from(db.objectStoreNames);

            // Identify which expected stores are missing
            const missingStores = expectedStores.filter(s => !existingStores.includes(s.key)).map(s => s.key);

            db.close();
            resolve({
                item1: dbName,
                item2: db.version,
                item3: missingStores.length > 0,
                item4: missingStores
            });
        };

        request.onerror = () => {
            // If we can't even open it, mark as corrupted
            resolve({ item1: dbName || DB_NAME, item2: DB_VERSION, item3: true, item4: expectedStores.map(s => s.key) });
        };
    });
}


export async function setupDatabase(dbName, stores) {
    var created = false;
    var error = null;
    return new Promise((rs, rj) => {
        const request = indexedDB.open(dbName || DB_NAME, DB_VERSION)
        
        request.onupgradeneeded = (event) => {
            const db = event.target.result

            try {

                for (var key in stores) {
                    var storeName = stores[key].key
                    var keyPath = stores[key].value.item1
                    var columnNames = stores[key].value.item2

                    if (!keyPath || keyPath.length == 0) throw new Error('Keypath invalid for: ' + JSON.stringify(stores[key]))

                    // If the store already exists, delete it so we can refresh the indexes
                    if (db.objectStoreNames.contains(storeName)) {
                        continue;
                    }

                    const store = db.createObjectStore(storeName, { keyPath: keyPath })

                    columnNames.forEach(col => {
                        if (col.key !== keyPath) {
                            store.createIndex(col.key, col.value, { unique: false })
                        }
                    })

                }

                created = true;

            } catch (ex) { error = ('' + ex) + ' on ' + JSON.stringify(stores) }
        }
        request.onsuccess = () => rs({ item1: created, item2: error ? ('' + error) : (created ? "upgraded" : "finished") })
        request.onerror = () => rj(request.error)
        request.onblocked = () => rj("Database upgrade blocked. Close other tabs.")
    })
}


export async function putRecord(storeName, record) {
    const db = await getDB()
    const tx = db.transaction(storeName, 'readwrite')
    const store = tx.objectStore(storeName)
    return new Promise((rs, rj) => {
        const req = store.put(record) 
        req.onsuccess = () => rs(req.result) 
        req.onerror = () => rj(req.error)
    })
}

export async function getRecord(storeName, key) {
    const db = await getDB()
    const tx = db.transaction(storeName, 'readonly')
    const store = tx.objectStore(storeName)
    return new Promise((rs, rj) => {
        const req = store.get(key)
        req.onsuccess = () => rs(req.result)
        req.onerror = () => rj(req.error)
    })
}



export async function queryIndex(storeName, indexName, exactIndex = null, lower = null, upper = null, getAll = true) {
    const db = await getDB()
    const tx = db.transaction(storeName, 'readonly')
    const store = tx.objectStore(storeName)
    const index = store.index(indexName)
    
    var range = null;
    if (exactIndex !== null) {
        range = IDBKeyRange.only(exactIndex);
    } else if (upper !== null && lower !== null) {
        range = IDBKeyRange.bound(lower, upper);
    } else if (upper !== null) {
        range = IDBKeyRange.upperBound(upper);
    } else if (lower !== null) {
        range = IDBKeyRange.lowerBound(lower);
    } else if (getAll) {
        range = null; // Get all records
    }


    return new Promise((rs, rj) => {
        const req = getAll == null ? index.getAll(range) : index.get(range)
        req.onsuccess = () => rs(req.result)
        req.onerror = () => rj(req.error)
    })
}



export async function deleteRecord(storeName, key) {
    const db = await getDB();
    const tx = db.transaction(storeName, 'readwrite');
    const store = tx.objectStore(storeName);
    return new Promise((rs, rj) => {
        const req = store.delete(key);
        req.onsuccess = () => rs(true);
        req.onerror = () => rj(req.error);
    });
}
