using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;

namespace Hosting.Services
{
    public partial class CircuitProvider : ICircuitProvider
    {

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


        public async Task<TResult?> RespondRemote<TResult>(MemberInfo methodInfo)
        {
            if (Http == null)
                throw new InvalidOperationException("Http client not available");

            if (methodInfo is MethodInfo runable)
            {
                var serviceTypes = runable.GetParameters().ToServices(Service);
                // TODO: dont serialize services, they will get rebuilt on the ends
            }
            // TODO: add fun serialization
            result = await Http.GetFromJsonAsync<TResult>(Path);
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


        public static async Task<TResult?> OnExecuteAsync<TResult>(ICircuitProvider service, string method, params object?[]? parameters)
        {
            return await TaskExtensions.Debounce(service.ExecuteAsyncDebounced<, TResult>, service.DefaultTTL, method, parameters);
        }

    }
}
