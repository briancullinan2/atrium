using System.Runtime.CompilerServices;

namespace Interfacing.Services;

public class LostLogger : ILog
{

    // demonstration for implementors to inject themselves
    //   instead of making up their own GetLogger
    public static MethodInfo? WrappedLogger { get; set; }
    public string? Filepath { get; set; }
    public string? Category { get; set; }



    static LostLogger()
    {
        WrappedLogger = Assembly.GetEntryAssembly()?.GetType("Atrium.Logging.Log4NetLogger")?.GetMethod(nameof(GetLogger));
        if (WrappedLogger == null)
        {
            var flashCard = Assembly.GetEntryAssembly()?.GetReferencedAssemblies()
                .FirstOrDefault(a => a.FullName.Contains("RazorSharp"));
            Console.WriteLine("Loading assembly: " + flashCard);
            if (flashCard != null)
            {
                var logger = Assembly.Load(flashCard.FullName).GetType("RazorSharp.Services.SimpleLogger");
                Console.WriteLine("Using logger: " + logger);
                WrappedLogger = logger?.GetMethod(nameof(GetLogger), [typeof(string)]);
                Console.WriteLine("Using logger method: " + WrappedLogger);
            }
        }
    }


    public object? WrappedInstance { get; set; }
    public PropertyInfo? MappedLevels { get; set; }

    public Action<object, Exception?> this[string level]
    {
        get => MappedLevels?.GetValue(WrappedInstance, [level]) as Action<object, Exception?>
            ?? throw new InvalidOperationException("Log level doesn't exist");
        set => MappedLevels?.SetValue(WrappedInstance, value, [level]);
    }

    public void Info(object message, Exception? ex = null)
    {
        this[nameof(Info)](message, ex);
    }

    public void Error(object message, Exception? ex = null)
    {
        this[nameof(Error)](message, ex);
    }

    public void Fatal(object message, Exception? ex = null)
    {
        this[nameof(Fatal)](message, ex);
    }

    public void Debug(object message, Exception? ex = null)
    {
        this[nameof(Debug)](message, ex);
    }


    public static LostLogger GetLogger(string filePath)
    {
        var parentLogger = (!typeof(LostLogger).IsAssignableFrom(WrappedLogger?.DeclaringType)
            ? WrappedLogger?.Invoke(null, [filePath])
            : throw new InvalidOperationException("Logger not implemented.")) ?? throw new InvalidOperationException("Logger not working.");
        return GetLogger(filePath, typeof(ILog), parentLogger);
    }

    public static LostLogger GetLogger(string filePath, Type levels, object? replacement = null)
    {
        // Extracts the class name from the file path to use as the category
        string category = Path.GetFileNameWithoutExtension(filePath);
        var simpleLogger = new LostLogger
        {
            Category = category,
            Filepath = filePath,             // TODO: [System.Runtime.CompilerServices.IndexerName("Levels")]
                                             // lookup [DefaultMember("Item")]
            MappedLevels = (replacement?.GetType() ?? levels).GetProperty("Item"),
            WrappedInstance = replacement
        };
        return simpleLogger;
    }
}


public static class Log
{

    public static void Info(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        LostLogger.GetLogger(callerPath)[nameof(Info)](message, ex);
    }

    public static void Error(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        LostLogger.GetLogger(callerPath)[nameof(Error)](message, ex);
    }

    public static void Fatal(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        LostLogger.GetLogger(callerPath)[nameof(Fatal)](message, ex);
    }

    public static void Debug(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
    {
        LostLogger.GetLogger(callerPath)[nameof(Debug)](message, ex);
    }
}
