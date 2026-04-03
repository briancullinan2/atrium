using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Extensions.PrometheusTypes
{
    public static partial class TaskExtensions
    {

        private static readonly ConcurrentDictionary<string, object> registry = new();

        public static async Task<T?> Debounce<T>(
            Func<CancellationToken, Task<T>> action,
            int delay = 200,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
        {
            // Fix for the "System.Private.CoreLib" issue: 
            // Get the Entry Assembly name to scope the key to your specific app.
            var assembly = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
            var uniqueKey = $"{assembly}_{file}_{key}";

            var tcs = new TaskCompletionSource<T?>();
            var cts = new CancellationTokenSource();
            var entry = (tcs, cts);

            // Update the registry: Cancel the old one, but DO NOT Dispose it here.
            registry.AddOrUpdate(uniqueKey, entry, (_, old) =>
            {
                var (oldTcs, oldCts) = ((TaskCompletionSource<T?>, CancellationTokenSource))old;
                oldCts.Cancel();
                // oldCts.Dispose(); // <--- REMOVE THIS. It causes the ObjectDisposedException.
                return entry;
            });

            try
            {
                // Use the token from the NEWEST CTS
                await Task.Delay(delay, cts.Token);

                var result = await action(cts.Token);

                tcs.TrySetResult(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                // Request Collapsing: Wait for the result of whoever superseded us.
                if (registry.TryGetValue(uniqueKey, out var latest) &&
                    latest is (TaskCompletionSource<T?> latestTcs, _))
                {
                    return await latestTcs.Task;
                }
                return default;
            }
            finally
            {
                // Only the "last" one standing cleans up the registry
                if (registry.TryGetValue(uniqueKey, out var current) && current.Equals(entry))
                {
                    registry.TryRemove(uniqueKey, out _);
                }

                // Safely dispose of our own resources now that we are done.
                cts.Dispose();
            }
        }


        public static Task Debounce(
            this Func<Task> action,
            int delay = 200,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce<bool>(action: async ct => { await action(); return true; }, delay, file, key);


        public static Task<TR?> Debounce<TR>(
            this Func<Task<TR>> action,
            int delay = 200,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(), delay, file, key);

        public static Task<TR?> Debounce<T1, TR>(
            this Func<T1?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1), delay, file, key);

        // T1, T2 Overload
        public static Task<TR?> Debounce<T1, T2, TR>(
            this Func<T1?, T2?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2), delay, file, key);

        public static Task<TR?> Debounce<T1, T2, T3, TR>(
            this Func<T1?, T2?, T3?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2, t3), delay, file, key);

        public static Task<TR?> Debounce<T1, T2, T3, T4, TR>(
            this Func<T1?, T2?, T3?, T4?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2, t3, t4), delay, file, key);

        public static Task<TR?> Debounce<T1, T2, T3, T4, T5, TR>(
            this Func<T1?, T2?, T3?, T4?, T5?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            T5? t5 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2, t3, t4, t5), delay, file, key);


        // actions


        public static Task Debounce<T1>(
            this Func<T1?, Task> action,
            int delay = 200,
            T1? t1 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce<bool>(action: async ct => { await action(t1); return true; }, delay, file, key);


        public static Task Debounce<T1, T2>(
            this Func<T1?, T2?, Task> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2), delay, file, key);


        public static Task Debounce<T1, T2, T3>(
            this Func<T1?, T2?, T3?, Task> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2, t3), delay, file, key);



        public static Task Debounce<T1, T2, T3, T4>(
            this Func<T1?, T2?, T3?, T4?, Task> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2, t3, t4), delay, file, key);


        public static Task Debounce<T1, T2, T3, T4, T5>(
            this Func<T1?, T2?, T3?, T4?, T5?, Task> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            T5? t5 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: ct => action(t1, t2, t3, t4, t5), delay, file, key);




        public static Task Debounce<T1>(
            this Action<T1?> action,
            int delay = 200,
            T1? t1 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce<bool>(action: async ct => { action(t1); return true; }, delay, file, key);


        public static Task Debounce<T1, T2>(
            this Action<T1?, T2?> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: async ct => { action(t1, t2); return true; }, delay, file, key);


        public static Task Debounce<T1, T2, T3>(
            this Action<T1?, T2?, T3?> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: async ct => { action(t1, t2, t3); return true; }, delay, file, key);



        public static Task Debounce<T1, T2, T3, T4>(
            this Action<T1?, T2?, T3?, T4?> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: async ct => { action(t1, t2, t3, t4); return true; }, delay, file, key);


        public static Task Debounce<T1, T2, T3, T4, T5>(
            this Action<T1?, T2?, T3?, T4?, T5?> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            T5? t5 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Debounce(action: async ct => { action(t1, t2, t3, t4, t5); return true; }, delay, file, key);



        /*
        public static Task<T?> Debounce<T>(
            this string functionName,
            int delay = 200,
            object?[]? parameters = null,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
        {
            var caller = Assembly.GetCallingAssembly();
            var callerClass = file.ToType(caller);
            var method = callerClass?.GetMethods(functionName).FirstOrDefault()
                ?? throw new InvalidOperationException("Could not find method: " + functionName + " on " + callerClass);
            var result = Debounce<T>(async cts => {
                var resultInner = method.Invoke(null, parameters ?? []);
                if(resultInner is Task task)
                {
                    await task;
                    return (resultInner as dynamic).Result;
                }
                return (T)resultInner!;
            }, delay, file, functionName + key);
            return result;
        }


        public static Task Debounce(
            this string functionName,
            int delay = 200,
            object?[]? parameters = null,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
        {
            var caller = Assembly.GetCallingAssembly();
            var callerClass = file.ToType(caller);
            var method = callerClass?.GetMethods(functionName).FirstOrDefault()
                ?? throw new InvalidOperationException("Could not find method: " + functionName + " on " + callerClass);
            var result = Debounce<object?>(async cts => {
                var resultInner = method.Invoke(null, parameters ?? []);
                if (resultInner is Task task)
                {
                    await task;
                    return (resultInner as dynamic).Result;
                }
                return resultInner;
            }, delay, file, functionName + key);
            return result;
        }
        */

    }
}
