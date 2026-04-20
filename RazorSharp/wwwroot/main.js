
// wwwroot/js/interconnect.js



async function shouldStartInWebAssembly(url) {
  const response = await fetch(url);
  
  // Wait a micro-task to ensure the performance entry is populated
  await new Promise(resolve => setTimeout(resolve, 0));

  const entry = performance.getEntriesByName(new URL(url, window.location.origin).href).pop();
  if (response.headers.get('X-Service-Worker-Handled')) {
    return true;
  }

  if (entry) {
    if (entry.workerStart > 0) {
      //return true;
    } else if (entry.transferSize === 0) {
      //console.log("Source: Browser HTTP Cache (Disk/Memory)");
    } else {
      //console.log("Source: Network");
    }
  }
  
  return false;
}


function restoreState() {
    return Array.from(document.getElementsByTagName('input')).reduce((acc, input) => {
        let key = input.id || input.name || 'unnamed'
        if (key.substring(0, 6) == 'state_')
            acc[key] = input.value
        return acc
    }, {})
}

var startParameters = null;
window.startBlazor = function (type) {


    /*
    if(typeof(type) != "string") {
        if(!navigator.onLine) {
             return startBlazor("webassembly");
        }


        return shouldStartInWebAssembly('/version.json?t=' + Date.now())
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
                    console.error(e)
                }
            });
    }
    */


    var allParameters = [
        ...Blazor.parse(document)];
    if (startParameters == null)
        startParameters = allParameters[0]

    var mode = startParameters?.type || (type == "webassembly" ? "webassembly" : "server")
    if (window.location.hash == "#mode=webassembly")
        mode = 'webassembly'
    if (window.location.hash == "#mode=server")
        mode = 'server'

    var geminiSaidICouldnt = {
           

        //server: startParameters,
            
        auto: {
            type: mode,
            prerenderId: startParameters?.predrenderId,
            key: {
                locationHash: startParameters?.key?.locationHash,
                formattedKey: '',
            },
            sequence: startParameters?.sequence,
            descriptor: startParameters?.descriptor,
            assembly: "WebClient",
            typeName: "WebClient.Components.App",
            parameterDefinitions: "[]",
            parameterValues: "[]",
            start: startParameters?.start,
            end: startParameters?.end,
            uniqueId: 0,
        },
        
    };


    window.location.hash = "mode=" + geminiSaidICouldnt.auto.type
    window.myCustomState = () => restoreState()

    var blazorConfig = {

        dotnet: "/_framework/dotnet.js",

        preferMine: true,
        
        ssr: { disableStreamingContent: true },


        geminiSaidICouldnt: geminiSaidICouldnt.auto.start != null ? {
            server: [...Blazor.parse(document, { geminiSaidICouldnt })][0]
        } : {},


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
                environment: "Production",
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
        console.log('Done booting')
    } catch (e) {
        console.error(e)
        if (typeof (type) != "string") {
             startBlazor("webassembly");
        }
    }
}

window.manageServiceWorker = manageServiceWorker;

async function manageServiceWorker() {

    return; // TODO: 


    if (!('serviceWorker' in navigator)) return;

    // 1. Get the Server's Truth first (our Token/Version)
    let serverUpdate = null;
    try {
        const vRes = await fetch('/version.json?t=' + Date.now(), { cache: 'no-store' });
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
                try {
                    if(event.data.version)
                        swVersion = JSON.parse(new TextDecoder('utf-8').decode(event.data.version))[1];
                }
                catch (e) {}
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

    
    const swUrl = '/service-worker.published.js?t=' + Date.now();
    navigator.serviceWorker.register(swUrl)
        .then(reg => {
            console.info('Service Worker registered successfully:', reg.scope);
        })
        .catch(err => {
            console.error('Service Worker registration failed:', err);
        });
}

