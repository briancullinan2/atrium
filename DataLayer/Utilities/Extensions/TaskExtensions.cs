using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DataLayer.Utilities.Extensions
{
    public static class TaskExtensions
    {
        // 1. Chaining a Task that returns a Task (The Flattener)
        public static Task<TResult> Then<TResult>(this Task task, Func<Task, Task<TResult>> then)
        {
            return task.ContinueWith(t => then(t)).Unwrap();
        }

        // Overload for: Task<T> -> async (Task<T> t) => { ... }
        public static Task Then<T>(this Task<T> task, Func<Task<T>, Task> then)
        {
            // ContinueWith returns Task<Task>, Unwrap() collapses it to Task
            return task.ContinueWith(t => then(t)).Unwrap();
        }

        // 2. Chaining a Task<T> to another Task<TResult>
        public static Task<TResult> Then<T, TResult>(this Task<T> task, Func<T, Task<TResult>> then)
        {
            return task.ContinueWith(t => then(t.Result)).Unwrap();
        }

        // Chaining Task<T> to an async process that returns Task (No Result)
        public static Task Then<T>(this Task<T> task, Func<T, Task> then)
        {
            return task.ContinueWith(t => then(t.Result)).Unwrap();
        }

        // Chaining Task (No Result) to another async Task
        public static Task Then(this Task task, Func<Task, Task> then)
        {
            return task.ContinueWith(t => then(t)).Unwrap();
        }

        // Chaining Task<T> to an async process that returns Task (No Result)
        public static Task Then<T>(this Task<T> task, Action<T> then)
        {
            return task.ContinueWith(t => then(t.Result));
        }

        // Chaining Task (No Result) to another async Task
        public static Task Then(this Task task, Action<Task> then)
        {
            return task.ContinueWith(t => then(t));
        }

        // 3. Handling the ValueTask (JS Interop special)
        public static Task<TResult> Then<T, TResult>(this ValueTask<T> task, Func<T, Task<TResult>> then)
        {
            return task.AsTask().ContinueWith(t => then(t.Result)).Unwrap();
        }

        public static Task Catch(this Task task, Action<Exception> errorHandler)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    // Flatten() unpacks AggregateException layers common in TPL
                    errorHandler(t.Exception.Flatten());
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        // Overload for Task<T> if you want to handle errors on result-returning tasks
        public static Task<T> Catch<T>(this Task<T> task, Func<Exception, T> errorHandler)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    return errorHandler(t.Exception.Flatten());
                }
                return t.Result;
            });
        }




        public static Task Forget(this Task task, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    var ex = t.Exception.Flatten().InnerException ?? t.Exception;
                    if (ex is OperationCanceledException) return; // don't log
                    // Hook into your "serious" logger here
                    Console.WriteLine($"!!! Unwatched Task Failed in {caller} L:{line}: {ex?.Message}");

                    // Trigger your global handler manually
                    //GlobalExceptionHandler.Handle(ex);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
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



        public static CancellationTokenSource Timer(
            this Func<Task> action,
            int delay = 200,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer<bool>(action: async ct => { await action(); return true; }, delay, file, key);

        public static CancellationTokenSource Timer<TR>(
            this Func<Task<TR>> action,
            int delay = 200,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer(action: ct => action(), delay, file, key);


        public static CancellationTokenSource Timer<T1, TR>(
            this Func<T1?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer(action: ct => action(t1), delay, file, key);

        public static CancellationTokenSource Timer<T1, T2, TR>(
            this Func<T1?, T2?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer(action: ct => action(t1, t2), delay, file, key);

        public static CancellationTokenSource Timer<T1, T2, T3, TR>(
            this Func<T1?, T2?, T3?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer(action: ct => action(t1, t2, t3), delay, file, key);

        public static CancellationTokenSource Timer<T1, T2, T3, T4, TR>(
            this Func<T1?, T2?, T3?, T4?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer(action: ct => action(t1, t2, t3, t4), delay, file, key);

        public static CancellationTokenSource Timer<T1, T2, T3, T4, T5, TR>(
            this Func<T1?, T2?, T3?, T4?, T5?, Task<TR>> action,
            int delay = 200,
            T1? t1 = default,
            T2? t2 = default,
            T3? t3 = default,
            T4? t4 = default,
            T5? t5 = default,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
            => Timer(action: ct => action(t1, t2, t3, t4, t5), delay, file, key);


        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _timerRegistry = new();

        public static CancellationTokenSource Timer<T>(
            Func<CancellationToken, Task<T>> action,
            int interval,
            [CallerFilePath] string file = "",
            [CallerMemberName] string key = "")
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
            var uniqueKey = $"{assembly}_{file}_{key}";

            // If an active timer exists for this specific call site, return it
            if (_timerRegistry.TryGetValue(uniqueKey, out var existingCts) && !existingCts.IsCancellationRequested)
            {
                return existingCts;
            }

            var cts = new CancellationTokenSource();

            // Ensure we don't leak old ones if they were cancelled but still in the dict
            _timerRegistry.AddOrUpdate(uniqueKey, cts, (_, old) =>
            {
                old.Cancel();
                old.Dispose();
                return cts;
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
                    // Initial execution
                    await action(cts.Token);

                    while (await timer.WaitForNextTickAsync(cts.Token))
                    {
                        await action(cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    // Only remove if we are still the "current" timer for this key
                    _timerRegistry.TryRemove(KeyValuePair.Create(uniqueKey, cts));
                    cts.Dispose();
                }
            }, cts.Token);

            return cts;
        }

    }
}
