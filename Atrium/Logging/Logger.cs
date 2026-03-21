#if WINDOWS
using log4net;
#endif
using System.IO;
using System.Runtime.CompilerServices;


namespace Atrium.Logging
{
    public static class Log
    {
#if WINDOWS
        // Use a ConcurrentDictionary to cache loggers for performance
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ILog> _loggerCache = new();
#else
        public interface ILog
        {
            void Info(object message, Exception? ex = null);
            void Error(object message, Exception? ex = null);
            void Fatal(object message, Exception? ex = null);
            void Debug(object message, Exception? ex = null);
        }
#endif

        public static void Info(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Info(message, ex);
        }

        public static void Error(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Error(message, ex);
        }

        public static void Fatal(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Fatal(message, ex);
//#if WINDOWS
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // On Windows/Mac, this closes the main window
                if (Application.Current?.Windows.ElementAtOrDefault(0) is Window window)
                {
                    Application.Current.CloseWindow(window);
                }

                // 3. Force the process to terminate after a small delay 
                // to allow the logger to flush to Atrium.sqlite.db
                Task.Delay(500).ContinueWith(_ =>
                {
                    Environment.Exit(1); // Exit with error code 1 for Fatal
                });
            });
//#endif
        }

        public static void Debug(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Debug(message, ex);
        }

#if WINDOWS
        private static ILog GetLogger(string filePath)
        {
            // Extracts the class name from the file path to use as the category
            string category = Path.GetFileNameWithoutExtension(filePath);
            return _loggerCache.GetOrAdd(category, LogManager.GetLogger);
        }
#else
        private static SimpleLogger GetLogger(string filePath)
        {
            string category = Path.GetFileNameWithoutExtension(filePath);
            return new SimpleLogger() { Filepath = filePath, Category = category };
        }

        public class SimpleLogger() : ILog
        {
            public string? Filepath { get; set; }
            public string? Category { get; set; }

            public void Debug(object message, Exception? ex = null)
            {
                Console.WriteLine(Filepath + " : " + Category);
                Console.WriteLine(message);
                if (ex != null) Console.WriteLine(ex);
            }

            public void Error(object message, Exception? ex = null)
            {
                Console.WriteLine(Filepath + " : " + Category);
                Console.WriteLine(message);
                if (ex != null) Console.WriteLine(ex);
            }

            public void Fatal(object message, Exception? ex = null)
            {
                Console.WriteLine(Filepath + " : " + Category);
                Console.WriteLine(message);
                if (ex != null) Console.WriteLine(ex);
            }

            public void Info(object message, Exception? ex = null)
            {
                Console.WriteLine(Filepath + " : " + Category);
                Console.WriteLine(message);
                if (ex != null) Console.WriteLine(ex);
            }
        }
        
#endif
    }
}
