using System.Runtime.CompilerServices;
using System.Reflection;


namespace DataLayer.Utilities
{
    internal class Log : Log.ILog
    {

        // demonstration for implementors to inject themselves
        //   instead of making up their own GetLogger
        public static MethodInfo? WrappedLogger { get; set; }
        public string? Filepath { get; set; }
        public string? Category { get; set; }


        public interface ILog
        {
            static abstract Log GetLogger(string filePath);
            Action<object, Exception?> this[string level] { get; set; }
            string? Filepath { get; set; }
            string? Category { get; set; }

        }

        static Log()
        {
            WrappedLogger = Assembly.GetEntryAssembly()?.GetType("Atrium.Logging.Log")?.GetMethod(nameof(GetLogger));
            if (WrappedLogger == null)
            {
                var flashCard = Assembly.GetEntryAssembly()?.GetReferencedAssemblies()
                    .FirstOrDefault(a => a.FullName.Contains("FlashCard"));
                Console.WriteLine("Loading assembly: " + flashCard);
                if (flashCard != null)
                {
                    var logger = Assembly.Load(flashCard.FullName).GetType("FlashCard.Services.SimpleLogger");
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
        }

        public static void Debug(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
        {
            GetLogger(callerPath)[nameof(Debug)](message, ex);
        }

        public static Log GetLogger(string filePath)
        {
            // Extracts the class name from the file path to use as the category
            string category = Path.GetFileNameWithoutExtension(filePath);
            var parentLogger = (!typeof(Log).IsAssignableFrom(WrappedLogger?.DeclaringType)
                ? WrappedLogger?.Invoke(null, [filePath])
                : throw new InvalidOperationException("Logger not implemented.")) ?? throw new InvalidOperationException("Logger not working.");
            var simpleLogger = new Log
            {
                Category = category,
                Filepath = filePath,             // TODO: [System.Runtime.CompilerServices.IndexerName("Levels")]
                                                 // lookup [DefaultMember("Item")]
                MappedLevels = parentLogger.GetType().GetProperty("Item"),
                WrappedInstance = parentLogger
            };
            return simpleLogger;
        }
    }


    public static class LoggingExtensions
    {

    }

}
