
#if !BROWSER
using Microsoft.AspNetCore.Http;
using Microsoft.Maui;
using System.Runtime.CompilerServices;
#endif


namespace Hosting.Services;

public partial class CircuitProvider : ICircuitProvider
{

    public async Task<TResult?> InvokeAsync<TResult>(string? method, CancellationToken? token = null)
    {
        return await TaskExtensions.Debounce<string, object?[], TResult>(ExecuteAsyncDebounced<TResult>, DefaultTTL, method, [token]);
    }

    public async Task<TResult?> InvokeAsync<TResult>(string? method, params object?[]? parameters)
    {
        return await TaskExtensions.Debounce<string, object?[], TResult>(ExecuteAsyncDebounced<TResult>, DefaultTTL, method, parameters);
    }


    public async Task<TResult?> RespondCircuit<TResult>(MemberInfo? methodInfo, params object?[]? parameters)
    {
        if (methodInfo == null || methodInfo.DeclaringType == null)
            throw new InvalidOperationException("Couldn't find service provider: " + methodInfo);
        if (!IsSignalCircuit)
            throw new InvalidOperationException("Not a signal circuit: " + methodInfo);

        var Implementation = Service.GetRequiredService(methodInfo.DeclaringType);
        object? result;
        if (methodInfo is FieldInfo field)
        {
            result = field.GetValue(Implementation) is TResult;
        }
        else if (methodInfo is PropertyInfo property)
        {
            result = parameters?.Length > 0 ? property.GetValue(Implementation, parameters) : property.GetValue(Implementation);
        }
        else if (methodInfo is MethodInfo runable)
        {
            // TODO: put together a list of parameters and services
            result = runable.Invoke(Implementation, parameters);
        }
        else throw new InvalidOperationException("Nothing to do in: " + methodInfo);

        if (result == null || result.GetType().Extends(typeof(TResult)))
            return (TResult?)result;
        else throw new InvalidOperationException("Result did not type cast from: "
            + result.GetType().AssemblyQualifiedName
            + " to " + typeof(TResult).AssemblyQualifiedName);

    }

    internal int currentProgress = 0;



    public async Task<TResult?> RespondRemote<TResult>(MemberInfo methodInfo, params object?[]? parameters)
    {
        if (Http == null)
            throw new InvalidOperationException("Http client not available");

        var route = methodInfo.Route() ?? throw new InvalidOperationException("Member is not routable: " + methodInfo);

        var isPost = false; // parameters?.Any(p => p?.GetType().IsSimple() != true);

        var multiPartContent = new MultipartFormDataContent();

        // serialize primitives in the url string
        var url = methodInfo.Route() + "?" + string.Join("&", parameters?
            .Where(p => p != null && p.GetType().IsSimple())
            .Select(p => $"{p!.GetType().Name.ToLower()}={Uri.EscapeDataString(p.ToString() ?? "")}") ?? []);

        var methodParameters = methodInfo is MethodInfo method ? method.GetParameters()
            : methodInfo is PropertyInfo property ? property.GetIndexParameters()
            : [];

        for (var i = 0; i < parameters?.Length; i++)
        {
            isPost = true;
            var parameter = parameters[i];

            if (parameter == null) continue;
            if (parameter.GetType().IsSimple()) continue;

            var realParameter = methodParameters[i];
            var possibleName = realParameter.Name ?? "file";


            // special case for streams
            if (parameter is Stream stream)
            {
                var streamContent = new ProgressableStreamContent(stream, 4096, (sent) =>
                {
                    var percentage = (double)sent / stream.Length * 100;
                    // Update your Blazor Progress Bar variable here
                    currentProgress = (int)percentage;
                });

                var localPath = parameters?.OfType<string>().FirstOrDefault();
                multiPartContent.Add(streamContent, possibleName, localPath ?? possibleName);
            }
            else if (parameter is Expression expr)
            {
                // TODO: same thing as remote querymanager
            }
            else if (!parameter.GetType().IsSimple())
            {
                var jsonPayload = JsonSerializer.Serialize(parameter);
                var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                multiPartContent.Add(jsonContent, possibleName, possibleName);
            }
        }

        if (isPost)
        {
            var result = await Http.PostAsync(route, multiPartContent);
            var task = result.Content.ReadFromJsonAsync<TResult>();
            return await task;
        }
        else
        {
            var result = await Http.GetFromJsonAsync<TResult>(route);
            return result;
        }
        // TODO: add fun serialization
        /*
        var debouncedTyped = typeof(CircuitProvider).GetMethod(nameof(CircuitProvider.ExecuteAsyncDebounced))
            ?.MakeGenericMethod((methodInfo as MethodInfo)?.ReturnType)
            ?? throw new InvalidOperationException("Couldn't render ExecuteAsyncDebounced for: " + methodInfo);

        debouncedTyped.Invoke(this, [ methodInfo.Name, parameters ]);
        */
        // TODO: put special file upload handlers, and expression serializer in here automatically
        // TODO: use InvokeService extension but add named parameters from IFormFactor.QueryParameters context
    }




    public async Task<T?> RespondHub<T>(string method, CancellationToken? ct = null) => await Connection.InvokeAsync<T>(method, ct);
    public async Task<T?> RespondHub<T>(string method, object?[]? parameters) => parameters?.Length switch
    {
        1 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0)),
        2 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1)),
        3 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2)),
        4 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2), parameters.ElementAt(3)),
        5 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2), parameters.ElementAt(3), parameters.ElementAt(4)),
        _ => await Connection.InvokeAsync<T>(method, new CancellationTokenSource().Token)
    };



#if !BROWSER
    public static async Task OnExecuteAsync(
        HttpContext Context,
        NavigationManager Nav,
        IFileManager FileManager,
        ICircuitProvider Circuit,
        IFormFactor Form)
    {
        try
        {
            var method = Nav.Uri;
            var potentialType = TypeExtensions.AllRoutable.FirstOrDefault(t => method?.Contains(t.Route()!, StringComparison.InvariantCultureIgnoreCase) == true);
            var methodInfo = TypeExtensions.AllRoutes.FirstOrDefault(t => !string.IsNullOrWhiteSpace(method) && t.Route() == method)
                ?? throw new InvalidOperationException("Method not routable: " + method + " are you trying to go here? " + potentialType);
            var type = methodInfo.DeclaringType ?? throw new InvalidOperationException("Method has no declaring type: " + method);


            var first = true;

            foreach (var file in Form.Files)
            {
                // Accessing the stream directly from the request
                using var stream = file.OpenReadStream();

                // Example: Save to disk in Arizona-based storage
                var databaseFile = await FileManager.UploadFile(stream, file.Name);

                // this allows the client to send multiple files at once but get a response as soon as the first one saves to disk
                if (!first) continue;
                first = false;

                Context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(databaseFile, JsonExtensions.Default);
                await Context.Response.WriteAsync(json);
                await Context.Response.Body.FlushAsync();
                await Context.Response.CompleteAsync();

            }

            // TODO:
            var debouncedTyped = typeof(CircuitProvider).GetMethod(nameof(CircuitProvider.ExecuteAsyncDebounced))
                ?.MakeGenericMethod(methodInfo.ReturnType)
                ?? throw new InvalidOperationException("Couldn't render ExecuteAsyncDebounced for: " + methodInfo);

            var result = await TaskExtensions.Debounce<string?, object?[], object?>(async (method, parameters)
                =>
            {
                var result = debouncedTyped.Invoke(Circuit, [method, parameters]);
                if (result is Task task) await task;
                return (result as dynamic)?.Result;
            }, Circuit.DefaultTTL, method, [.. Form.Files.Cast<object?>()]);

        }
        catch (Exception ex)
        {
            Context.Response.ContentType = "application/json";
            Context.Response.StatusCode = 500;
            var json = JsonSerializer.Serialize(ex.Message, JsonExtensions.Default);
            await Context.Response.WriteAsync(json);
        }

    }

#endif

    public async Task<TResult?> ExecuteAsyncDebounced<TResult>(
        string? method,
        object?[]? parameters
        // TODO: get force out of somewhere
        /* bool force = false */)
    {
        // TODO: the same Debounce and QueryNow does with parameters

        //if (_cachedValue != null && DateTime.Now < _lastFetched + TimeSpan.FromMilliseconds(DefaultTTL))
        //{
        //    return _cachedValue;
        //}

        if (method == null)
            throw new InvalidOperationException("Method cannot be null");

        // TODO: look up type based on method string input
        var potentialType = TypeExtensions.AllRoutable.FirstOrDefault(t => method?.Contains(t.Route()!, StringComparison.InvariantCultureIgnoreCase) == true);

        var methodInfo = TypeExtensions.AllRoutes.FirstOrDefault(t => !string.IsNullOrWhiteSpace(method) && t.Route() == method)
            ?? throw new InvalidOperationException("Method not routable: " + method + " are you trying to go here? " + potentialType);

        var type = methodInfo.DeclaringType ?? throw new InvalidOperationException("Method has no declaring type: " + method);

        /*
        // TODO: make these routable somehow
        MemberInfo? methodInfo = type.GetMethods(method).FirstOrDefault() as MemberInfo
            ?? type.GetProperties(method).FirstOrDefault() as MemberInfo
            ?? type.GetFields(method).FirstOrDefault() as MemberInfo;
        */


        if (methodInfo == null || !methodInfo.IsRoutable())
            throw new InvalidOperationException("Tried to invoke unroutable method: " + method + " on " + type.AssemblyQualifiedName);

        if (IsSignalCircuit)
        {
            return await RespondCircuit<TResult>(methodInfo, parameters);
        }
        if (IsHubConnected)
        {
            return await RespondHub<TResult>(method, parameters);
        }
        else if (OperatingSystem.IsBrowser())
        {
            return await RespondRemote<TResult>(methodInfo, parameters);
        }
        else
        {
            var result = methodInfo.InvokeService(Service, parameters);
            if (result is Task task)
            {
                await task;
                return (task as dynamic).Result;
            }
            else if (result is ValueTask valueTask)
            {
                await valueTask;
                return (valueTask as dynamic).Result;
            }
            else if (result is TResult typedResult)
                return typedResult;
            else if (result == null)
                return default;
            else throw new InvalidOperationException("Result did not type cast from: "
                + result.GetType().AssemblyQualifiedName
                + " to " + typeof(TResult).AssemblyQualifiedName);

        }
    }
}
