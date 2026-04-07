using Extensions.JsonVoorhees;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Json;

namespace Hosting.Services
{
    public partial class CircuitProvider : ICircuitProvider
    {

        public async Task<TResult?> InvokeAsync<TResult>(string method, CancellationToken? token = null)
        {
            return await TaskExtensions.Debounce<Type, string, object[], TResult>(ExecuteAsyncDebounced<TResult>, DefaultTTL, method, [token]);
        }

        public async Task<TResult?> InvokeAsync<TResult>(string method, params object?[]? parameters)
        {
            return await TaskExtensions.Debounce<Type, string, object[], TResult>(ExecuteAsyncDebounced<TResult>, DefaultTTL, method, parameters);
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
            if (methodInfo is PropertyInfo property)
            {
                result = parameters?.Length > 0 ? property.GetValue(Implementation, parameters) : property.GetValue(Implementation);
            }
            if (methodInfo is MethodInfo runable)
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


            if (methodInfo is MethodInfo runable)
            {
                var serviceTypes = runable.GetParameters().ToServices(Service);
                // TODO: dont serialize services, they will get rebuilt on the ends

            }

            if(parameters?.FirstOrDefault(o => o.GetType().Extends(typeof(Stream))) is Stream stream)
            {
                var content = new MultipartFormDataContent();

                var streamContent = new ProgressableStreamContent(stream, 4096, (sent) =>
                {
                    var percentage = (double)sent / stream.Length * 100;
                    // Update your Blazor Progress Bar variable here
                    currentProgress = (int)percentage;
                });

                content.Add(streamContent, "file", Path.GetFileName(localPath));
                url = QueryHelpers.AddQueryString(url, "source", source ?? "Uploads");

                result = await Http.GetFromJsonAsync<TResult>(route);
            }
            else
            {
                result = await Http.GetFromJsonAsync<TResult>(route);
            }
            // TODO: add fun serialization


            // TODO: put special file upload handlers, and expression serializer in here automatically
            // TODO: use InvokeService extension but add named parameters from IFormFactor.QueryParameters context
        }




        public async Task<T?> RepondHub<T>(string method, CancellationToken? ct = null) => await Connection.InvokeAsync<T>(method, ct);
        public async Task<T?> RepondHub<T>(string method, object?[]? parameters) => parameters?.Length switch
        {
            1 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0)),
            2 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1)),
            3 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2)),
            4 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2), parameters.ElementAt(3)),
            5 => await Connection.InvokeAsync<T>(method, parameters.ElementAt(0), parameters.ElementAt(1), parameters.ElementAt(2), parameters.ElementAt(3), parameters.ElementAt(4)),
            _ => await Connection.InvokeAsync<T>(method, new CancellationTokenSource().Token)
        };



#if !BROWSER
        public static async Task OnExecuteAsync(HttpContext context, IFileManager FileManager, ICircuitProvider Circuit, IFormFactor Form)
        {
            try
            {

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

                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(databaseFile, JsonExtensions.Default);
                    await context.Response.WriteAsync(json);
                    await context.Response.Body.FlushAsync();
                    await context.Response.CompleteAsync();

                }

                // TODO:
                return await TaskExtensions.Debounce(service.ExecuteAsyncDebounced<, TResult>, service.DefaultTTL, method, parameters);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                var json = JsonSerializer.Serialize(ex.Message, JsonExtensions.Default);
                await context.Response.WriteAsync(json);
            }

        }

#endif

        public async Task<TResult?> ExecuteAsyncDebounced<TResult>(
            Type type,
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

            // TODO: look up type based on method string input


            MemberInfo? methodInfo = type.GetMethods(method).FirstOrDefault() as MemberInfo
                ?? type.GetProperties(method).FirstOrDefault() as MemberInfo
                ?? type.GetFields(method).FirstOrDefault() as MemberInfo;

            if (methodInfo == null || !methodInfo.IsRoutable())
                throw new InvalidOperationException("Tried to invoke unroutable method: " + method + " on " + type.AssemblyQualifiedName);


        }
    }
}
