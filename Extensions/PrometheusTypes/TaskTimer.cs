using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Extensions.PrometheusTypes
{
    public static partial class TaskExtensions
    {


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
