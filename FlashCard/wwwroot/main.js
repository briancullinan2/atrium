
// wwwroot/js/interconnect.js
window.interconnect = {
    activeReapers: {},

    clear: function(namespace) {
        delete window[namespace];
    },


    register: function(path, dotnetHelper, methodNames = [], servicable) {
        // 1. REAPER CHECK: Skip if already wired to this specific instance
        //if (this.activeReapers[path] && this.activeReapers[path].id === componentId) {
        //    return;
        //}
        if (window.GetService == null && servicable) {
            window.GetService = async s => {
                var path = await dotnetHelper.invokeMethodAsync('GetService', s)
                if (path == null) return null;
                const parts = path.split('.');
                let current = window;
                for (let i = 0; i < parts.length - 1; i++) {
                    current[parts[i]] = current[parts[i]] || {};
                    current = current[parts[i]];
                }
                return current;
            }
        }


        if (this.activeReapers[path]) {
            this.unregister(path);
        }

        // 2. PATH TRAVERSAL
        const parts = path.split('.');
        let current = window;
        for (let i = 0; i < parts.length - 1; i++) {
            current[parts[i]] = current[parts[i]] || {};
            current = current[parts[i]];
        }

        const className = parts[parts.length - 1];

        // 3. THE AUTOCOMPLETE FIX
        // We create a target object that physically holds the keys
        const proxyTarget = {};
        methodNames.forEach(name => {
            proxyTarget[name] = function() { /* Shadow function for Intellisense */ };
        });

        current[className] = new Proxy(proxyTarget, {
            get: (target, prop) => {
                // Return the actual C# invoker
                return (...args) => {
                    const cleanedArgs = args.map(arg => typeof arg === 'undefined' ? null : arg);
                    console.log(`[Interconnect] Calling ${path}.${prop}`, cleanedArgs);
                    return dotnetHelper.invokeMethodAsync('Invoke', prop, cleanedArgs);
                };
            },
            // These two traps are the "Secret Sauce" for DevTools autocomplete
            ownKeys: (target) => {
                return methodNames;
            },
            getOwnPropertyDescriptor: (target, prop) => {
                return {
                    enumerable: true,
                    configurable: true
                };
            }
        });

        console.log(`[Interconnect] Proxy Active: window.${path} (${methodNames.length} methods)`);
        //this.attachReaper(path, componentId);
    },


    /*
    attachReaper: function(path, componentId) {
        const observer = new MutationObserver((mutations) => {
            // "STAY OF EXECUTION": Use a small timeout to ensure the component 
            // isn't just being moved or re-rendered in a batch.
            setTimeout(() => {
                const isStillInDom = document.body.innerHTML.includes(`"id":${componentId}`);
                
                if (!isStillInDom) {
                    console.log(`[Interconnect] Component ${componentId} confirmed dead. Nuking ${path}`);
                    this.unregister(path);
                }
            }, 100); // 100ms is usually enough for the Blazor renderer to finish a batch
        });

        observer.observe(document.body, { childList: true, subtree: true });
        
        // Track it so we can disconnect it later
        this.activeReapers[path] = { id: componentId, obs: observer };
    },
    */

    unregister: function(path) {
        const reaper = this.activeReapers[path];
        if (reaper) {
            reaper.obs.disconnect();
            delete this.activeReapers[path];
        }

        const parts = path.split('.');
        let current = window;
        for (let i = 0; i < parts.length - 1; i++) {
            current = current[parts[i]];
        }
        if (current) {
            console.log(`[Interconnect] C# Reaper nuked ghost: ${path}`);
            delete current[parts[parts.length - 1]];
        }
    }

};

