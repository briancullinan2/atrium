
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


async function shouldStartInWebAssembly(url) {
  const response = await fetch(url);
  
  // Wait a micro-task to ensure the performance entry is populated
  await new Promise(resolve => setTimeout(resolve, 0));

  const entry = performance.getEntriesByName(new URL(url, window.location.origin).href).pop();

  if (entry) {
    if (entry.workerStart > 0) {
      return true;
    } else if (entry.transferSize === 0) {
      //console.log("Source: Browser HTTP Cache (Disk/Memory)");
    } else {
      //console.log("Source: Network");
    }
  }
  
  return false;
}




var startParameters = null;
window.startBlazor = function (type = "server") {



    if(typeof(type) != "string") {
        if(!navigator.onLine) {
             return startBlazor("webassembly");
        }

        return shouldStartInWebAssembly('/version.json')
            .catch(function (response) { return response; })
            .then(function (response) {
                try {

                    if(response) {
                         return startBlazor("webassembly");
                    }
                    else 
                    {
                         return startBlazor("server");
                    }

                }
                catch (e) {
                    debugger;
                }
            });
    }


    var allParameters = [
        ...Blazor.parse(document)];
    if (startParameters == null)
        startParameters = allParameters[0]

    var geminiSaidICouldnt = {
           

        //server: startParameters,
            
        auto: {
            type: type == "webassembly" ? "webassembly" : "server",
            prerenderId: startParameters?.predrenderId,
            key: {
                locationHash: startParameters?.key?.locationHash,
                formattedKey: '',
            },
            sequence: startParameters?.sequence,
            descriptor: startParameters?.descriptor,
            assembly: "FlashCard",
            typeName: "FlashCard.Routes",
            parameterDefinitions: "[]",
            parameterValues: "[]",
            start: startParameters?.start,
            end: startParameters?.end,
            uniqueId: 0,
        },
        
    };


    var blazorConfig = {

        dotnet: "/_framework/dotnet.js",

        preferMine: true,
        
        ssr: { disableStreamingContent: true },


        geminiSaidICouldnt: {
            server: [...Blazor.parse(document, { geminiSaidICouldnt })][0]
        },



        circuit: {
            // LogLevel: 0 (Trace), 1 (Debug), 2 (Information), etc.
            logLevel: 1,

            // Configuration for the reconnection logic
            reconnectionHandler: {
                onConnectionDown: (options, error) => dotnetHelper.invokeMethodAsync('OnReconnected', error),
                onConnectionUp: () => dotnetHelper.invokeMethodAsync('OnReconnected', "hide")
            },



            webAssembly: {
                // If you want to load custom DLLs or change the environment 
                // without relying on the HTML comments:
                environment: "Development",
                loadBootResource: function(type, name, defaultUri, integrity) {
                    // Manual intervention on the file loading
                    return defaultUri;
                }
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
    };


    try {
        Blazor.start(blazorConfig);
    } catch (e) {
        debugger;
        console.log(e)
        if (typeof (type) != "string") {
             startBlazor("webassembly");
        }
    }
}

window.manageServiceWorker = manageServiceWorker;

async function manageServiceWorker() {
    if (!('serviceWorker' in navigator)) return;

    // 1. Get the Server's Truth first (our Token/Version)
    let serverUpdate = null;
    try {
        const vRes = await fetch('/version.json', { cache: 'no-store' });
        const handshake = await vRes.json();
        serverUpdate = handshake[1]; 
    } catch (e) {
        console.warn("Could not reach server for version check. Proceeding with caution.");
    }

    const registration = await navigator.serviceWorker.getRegistration();
    
    if (registration && registration.active) {
        let swVersion = null;
        let isCheckDone = false;
        const messageChannel = new MessageChannel();

        messageChannel.port1.onmessage = (event) => {
            if (event.data?.type === 'VERSION_REPORT') {
                isCheckDone = true;
                swVersion = JSON.parse(new TextDecoder('utf-8').decode(event.data.version))[1];
            }
        };

        // Ping the worker
        registration.active.postMessage({ type: 'GET_VERSION' }, [messageChannel.port2]);

        // 2. The "Dumb" Poll: Wait for response or 10s timeout
        const startTime = Date.now();
        await new Promise(resolve => {
            const checkInterval = setInterval(() => {
                const elapsed = Date.now() - startTime;
                if (isCheckDone || elapsed > 10000) {
                    clearInterval(checkInterval);
                    if (elapsed > 10000) console.warn("SW version check timed out.");
                    resolve();
                }
            }, 100); // Check every 100ms
        });

        // 3. Compare and Nuke if mismatched
        // We only unregister if we successfully got both versions and they differ
        if (serverUpdate && swVersion && serverUpdate !== swVersion) {
            console.warn(`Version Mismatch! Server: ${serverUpdate}, SW: ${swVersion}. Unregistering...`);
            
            let isDeregistered = false;

            const messageChannel2 = new MessageChannel();
            messageChannel2.port1.onmessage = (event) => {
                if (event.data?.type === 'DEREGISTERED') {
                
                    isDeregistered = true;
                }
            };

            // Ping the worker
            debugger;
            const registration2 = await navigator.serviceWorker.getRegistration();
            registration2.active.postMessage({ type: 'DEREGISTER' }, [messageChannel2.port2]);


            // 2. The "Dumb" Poll: Wait for response or 10s timeout
            const startTime = Date.now();
            await new Promise(resolve => {
                const checkInterval = setInterval(() => {
                    const elapsed = Date.now() - startTime;
                    if (isDeregistered || elapsed > 10000) {
                        clearInterval(checkInterval);
                        if (elapsed > 10000) console.warn("SW deregister timed out.");
                        resolve();
                    }
                }, 100); // Check every 100ms
            });
        }
    }

    if (!serverUpdate
        || (registration && registration.active)) {
        return; // don't register unless we have a valid version from server
    }

    // 4. Always try the registration (this either updates the existing or starts fresh)
    const swUrl = '/service-worker.published.js?t=' + Date.now();
    navigator.serviceWorker.register(swUrl)
        .then(reg => {
            console.info('Service Worker registered successfully:', reg.scope);
        })
        .catch(err => {
            console.error('Service Worker registration failed:', err);
        });
}

