using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Extensions.PrometheusTypes
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


    }
}
