

const activeReapers = [];

export function clear(namespace) {
    delete window[namespace];
}


export function register(path, dotnetHelper, methodNames = [], servicable) {
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


    if (activeReapers[path]) {
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
        proxyTarget[name] = function () { /* Shadow function for Intellisense */ };
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
}


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

export function unregister(path) {
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

export function dispatchEvent(eventName, detail) {
    const event = new CustomEvent(eventName, {
        detail: detail,
        bubbles: true,
        cancelable: true
    });

    window.dispatchEvent(event);
}

// ahhh now i remember. in study sauce i rendered the html in a hidden container, then i can replace elements with elements

export function replace(selector, content) {
    const container = document.querySelector(selector);
    if (!container) return;

    // 1. Sanitize and parse
    const sanitizedHtml = content.replaceAll(/<script[^>\s\S]*?>[\s\S]*?<\/script>/igm, '');
    const template = document.createElement('template');
    template.innerHTML = sanitizedHtml;

    // 2. Perform the swap
    // Use spread operator to pass individual nodes to replaceWith
    container.replaceWith(...template.content.childNodes);
}


export function insert(id, content) {
    const container = document.querySelector(id)
    if (!container) return;

    const template = document.createElement('template');
    template.innerHTML = content.replaceAll(/<script[^>\s\S]*?>[\s\S]*?<\/script>/igm, '')

    const existingNodes = Array.from(container.children).map(n => n.getAttribute('data-id'));
    const newNodes = Array.from(template.content.children).map(n => n.getAttribute('data-id'));

    // Combine them and sort
    const allNodes = existingNodes.concat(newNodes).sort((a, b) => {
        const idA = a || "";
        const idB = b || "";

        // If one is empty/null and the other is not
        if ((idA === "") !== (idB === "")) {
            // TODO: eventually this will make headers stick to top, could also check for th, or properly scope table > tbody > sorted
            return idA === "" ? -1 : 1; // Put the empty one at the top
        }

        // If both have IDs, sort alphabetically
        return idA.localeCompare(idB);
    });

    // sort new incoming nodes by name so we insert the last one first
    //   this allows us to add new elements to container.children without
    //   corrupting the sorting indexes above as we insert the new elements
    const sortedElements = Array.from(template.content.children).sort((a, b) => {
        const idA = a.getAttribute('data-id') || "";
        const idB = b.getAttribute('data-id') || "";
        return idA.localeCompare(idB);
    });

    // when inserting nodes, start from the bottom up,
    //   this has a side effect of not interfering with scroll
    for (var i = sortedElements.length - 1; i >=0 ; i--) {
        var el = sortedElements[i]
        var id = el.getAttribute('data-id')
        var insertAt = allNodes.indexOf(id)
        if (insertAt == allNodes.length - 1)
            container.appendChild(el)
        else
            container.insertBefore(el, container.children[insertAt-i]) // tricksy math
    }

}

// returns results from multiple queries added together
export function walkTree(select, ctx, evaluate) {
    var result;
    if (Array.isArray(select)) {
        result = select.reduce((arr, query, i) => {
            // pass the previous results to the next statement as the context
            if (i > 0) {
                return arr.map(r => walkTree(query, r, evaluate))
            }
            var result = walkTree(query, ctx, evaluate)
            if (typeof result !== 'undefined') {
                if (Array.isArray(result)) {
                    return arr.concat(result)
                } else {
                    return arr.concat([result])
                }
            }
            return arr
        }, []);
    } else if (typeof select === 'function') {
        // this is just here because it could be
        //   called from the array reduce above
        result = select(ctx);
    } else if (typeof select === 'object') {
        result = Object.keys(select).reduce((obj, prop) => {
            obj[prop] = walkTree(select[prop], ctx, evaluate);
            return obj;
        }, {});
    } else {
        result = evaluate(select, ctx);
    }
    return typeof select === 'string' && Array.isArray(result)
        && result.length <= 1
        ? result[0]
        : result;
}

export function evaluateDom(select, ctx, query) {

    try {
        if (select.includes('//*')) {
            console.warn(`Possible slow query evaluation due to wildcard: ${select}`)
        }
        // defaults to trying for iterator type
        //   so it can automatically be ordered
        var iterator = document.evaluate(select, ctx, null,
            ((XPathResult || {}).ORDERED_NODE_ITERATOR_TYPE || 5), null)
        //var iterator = evaluate(select, ctx, null, 5, null)
        // TODO: create a pattern regonizer for bodyless while
        var co = []
        var m
        while (m = iterator.iterateNext()) {
            co.push(m.nodeValue || m)
        }
        return co
    } catch (e) {
        if (e.message.includes('Value should be a node-set')
            || e.message.includes('You should have asked')) {
            var result = document.evaluate(select, ctx, null,
                (XPathResult || {}).ANY_TYPE || 0, null)
            return result.resultType === ((XPathResult || {}).NUMBER_TYPE || 1)
                ? result.numberValue
                : result.resultType === ((XPathResult || {}).STRING_TYPE || 2)
                    ? result.stringValue
                    : result.resultType === ((XPathResult || {}).BOOLEAN_TYPE || 3)
                        ? result.booleanValue
                        : result.resultValue
        }
        throw e;
    }
}

// parse as html if it's string,
//   if there is no context convert the tree to html
export function selectDom(select, ctx) {
    if (!ctx) ctx = document;
    //var query = ctx.querySelector.bind(ctx.ownerDocument || document)
    //    || ctx.ownerDocument.querySelector.bind(ctx.ownerDocument|| document)
    return walkTree(select, ctx, (select, ctx) => {
        return evaluateDom(select, ctx /* ,query */)
    })
}

export function queryDom(select, ctx) {
    if (!ctx) ctx = document;
    return walkTree(select, ctx, (select, ctx) => {
        let result = ctx.querySelectorAll(select)
        let co = []
        for (let m of result) {
            if (m)
                co.push(m)
        }
        if (ctx.shadowRoot) {
            let shadowResult = ctx.shadowRoot.querySelectorAll(select)
            for (let m of shadowResult) {
                if (m)
                    co.push(m)
            }
        }
        return co.length == 1 ? co[0] : co
    })
}
