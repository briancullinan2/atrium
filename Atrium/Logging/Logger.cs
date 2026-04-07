#if WINDOWS
using log4net;
#endif
using RazorSharp.Services;

namespace Atrium.Logging;

internal class Log : Log.ILog
{
#if WINDOWS
    // Use a ConcurrentDictionary to cache loggers for performance
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, log4net.ILog> _loggerCache = new();
#endif

    // demonstration for implementors to inject themselves
    //   instead of making up their own GetLogger
    public static MethodInfo? WrappedLogger { get; set; }
    public string? Filepath { get; set; }
    public string? Category { get; set; }


    public interface ILog
    {
        //static abstract SimpleLogger GetLogger(string filePath);
        Action<object, Exception?> this[string level] { get; set; }
        string? Filepath { get; set; }
        string? Category { get; set; }

    }

    static Log()
    {
        WrappedLogger = Assembly.GetEntryAssembly()?.GetType("Atrium.Logging.Log")?.GetMethod(nameof(GetLogger));
    }

    Action<object, Exception?> ILog.this[string level]
    {
        get => GetLogger(Filepath ?? Category ?? string.Empty)[level];
        set => GetLogger(Filepath ?? Category ?? string.Empty)[level] = value;
    }


    public static void Info(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        GetLogger(callerPath)[nameof(Info)](message, ex);
    }

    public static void Error(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        GetLogger(callerPath)[nameof(Error)](message, ex);
    }

    public static void Fatal(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        GetLogger(callerPath)[nameof(Fatal)](message, ex);
        // TODO: crash the app softly like it does on web and redirect to /error after its beem inserted
#if false
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
#endif
    }

    public static void Debug(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        GetLogger(callerPath)[nameof(Debug)](message, ex);
    }

    // the main project links down, and the LostLogger links up to here

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
        if (!typeof(Log).IsAssignableFrom(WrappedLogger?.DeclaringType))
        {
            var parentLogger = WrappedLogger?.Invoke(null, [filePath]) as ILog;
            parentLogger?.Category = category;
            parentLogger?.Filepath = filePath;
        }
        return simple!;
    }
}


public static class LoggingExtensions
{

}
