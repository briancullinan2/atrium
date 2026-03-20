using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace DataLayer.Utilities.Extensions
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    var ex = t.Exception.Flatten().InnerException;
                    // Hook into your "serious" logger here
                    Console.WriteLine($"!!! Unwatched Task Failed in {caller} L:{line}: {ex?.Message}");

                    // Trigger your global handler manually
                    //GlobalExceptionHandler.Handle(ex);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
