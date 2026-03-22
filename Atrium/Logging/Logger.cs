#if WINDOWS
using log4net;
#endif
using FlashCard.Services;
using System.IO;
using System.Runtime.CompilerServices;


namespace Atrium.Logging
{
    public static class Log
    {
#if WINDOWS
        // Use a ConcurrentDictionary to cache loggers for performance
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, log4net.ILog> _loggerCache = new();
#endif

        public static void Info(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Levels[nameof(Info)](message, ex);
        }

        public static void Error(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Levels[nameof(Error)](message, ex);
        }

        public static void Fatal(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath).Levels[nameof(Fatal)](message, ex);
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
            GetLogger(callerPath).Levels[nameof(Debug)](message, ex);
        }

        public static SimpleLogger GetLogger(string filePath)
        {
            // Extracts the class name from the file path to use as the category
            string category = Path.GetFileNameWithoutExtension(filePath);
#if WINDOWS
            var logger = _loggerCache.GetOrAdd(category, LogManager.GetLogger);
            var simple = SimpleLogger.GetLogger(filePath, typeof(log4net.ILog), logger);
#else
            var simple = SimpleLogger.GetLogger(filePath);
#endif
            return simple;
        }
    }


    public static class LoggingExtensions
    {

    }

}
