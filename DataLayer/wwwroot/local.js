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

export async function setupStore(storeName, keyPath, columnNames) {
    return new Promise((rs, rj) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION)
        
        request.onupgradeneeded = (event) => {
            const db = event.target.result

            // If the store already exists, delete it so we can refresh the indexes
            if (db.objectStoreNames.contains(storeName)) {
                return
            }

            const store = db.createObjectStore(storeName, { keyPath: keyPath })
            
            columnNames.forEach(col => {
                if (col !== keyPath) {
                    store.createIndex(col, col, { unique: false })
                }
            })
        }
        request.onsuccess = () => rs(true)
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



export async function queryIndex(storeName, indexName, lower, upper = null, getAll = true) {
    const db = await getDB()
    const tx = db.transaction(storeName, 'readonly')
    const store = tx.objectStore(storeName)
    const index = store.index(indexName)
    
    const range = (upper !== null) ? IDBKeyRange.bound(lower, upper) : IDBKeyRange.only(lower)

    return new Promise((rs, rj) => {
        const req = getAll ? index.getAll(range) : index.get(range)
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
