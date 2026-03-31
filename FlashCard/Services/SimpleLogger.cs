using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FlashCard.Services
{
    public interface ILog
    {
        void Info(object message, Exception? ex = null);
        void Error(object message, Exception? ex = null);
        void Fatal(object message, Exception? ex = null);
        void Debug(object message, Exception? ex = null);
        static abstract SimpleLogger GetLogger(string filePath);
        Action<object, Exception?> this[string level] { get; set; }
        string? Filepath { get; set; }
        string? Category { get; set; }
    }


    public class SimpleLogger : ILog
    {
        public static IServiceProvider? Service { get; set; }
        private static readonly ConcurrentDictionary<string, SimpleLogger> _loggerCache = new();

        private static IQueryManager? Query { get; set; }
        private static IPageManager? Manager { get; set; }

        public SimpleLogger(IServiceProvider _services)
        {
            Service ??= _services;
            Manager = Service.GetRequiredService<IPageManager>();
            Query = Service.GetRequiredService<IQueryManager>();
            if (Query != null)
            {
                foreach (var pre in PreLog)
                {
                    //_ = pre.Save(Query);
                    var boringException = new Exception(pre.Title) { Source = pre.Source };
                    boringException.Data["OriginalStack"] = pre.Body;
                    Manager?.SetError(boringException);
                }
                PreLog.Clear();
            }
        }


        public object? WrappedLogger { get; set; }


        internal static ConcurrentStack<DataLayer.Entities.Message> PreLog { get; set; } = [];


        public static SimpleLogger GetLogger(string filePath)
        {
            return GetLogger(filePath, typeof(SimpleLogger));
        }

        public static SimpleLogger GetLogger(string filePath, Type levels, object? replacement = null)
        {
            string category = Path.GetFileNameWithoutExtension(filePath);
            if (_loggerCache.TryGetValue(category, out var logger)) return logger;

            var levelsLogger = _loggerCache.GetOrAdd(category, cat => new SimpleLogger(Service!)
            {
                Filepath = filePath,
                Category = category
            });
            levelsLogger.WrappedLogger = replacement ?? levelsLogger;

            var levelFunctions = levels.GetMethods()
                .Select(m => new Tuple<MethodInfo, ParameterInfo[]>(m, m.GetParameters()))
                .Where(data => data is (var Method, var Parameters) && Method.ReturnType == typeof(void)
                    && Method.GetParameters() is { } parameters
                    && parameters.Length > 1 && parameters.Length < 3
                    && parameters.FirstOrDefault() is ParameterInfo first
                    && (typeof(object).IsAssignableFrom(Nullable.GetUnderlyingType(first.ParameterType) ?? first.ParameterType)
                    || typeof(string).IsAssignableFrom(Nullable.GetUnderlyingType(first.ParameterType) ?? first.ParameterType)
                    || typeof(Exception).IsAssignableFrom(Nullable.GetUnderlyingType(first.ParameterType) ?? first.ParameterType)
                    || (parameters.ElementAtOrDefault(1) is ParameterInfo second
                        && (typeof(object).IsAssignableFrom(Nullable.GetUnderlyingType(second.ParameterType) ?? second.ParameterType)
                        || typeof(string).IsAssignableFrom(Nullable.GetUnderlyingType(second.ParameterType) ?? second.ParameterType)
                        || typeof(Exception).IsAssignableFrom(Nullable.GetUnderlyingType(second.ParameterType) ?? second.ParameterType))
                        ))
                )
                .OrderBy(methodInfo => methodInfo.Item2.Length)
                .DistinctBy(methodInfo => methodInfo.Item1.Name);


            foreach (var levelFunction in levelFunctions)
            {
                levelsLogger._levels[levelFunction.Item1.Name] = (obj, ex) => levelsLogger.WriteLog(levelFunction.Item1.Name, obj, ex, levelFunction.Item1);
            }
            return levelsLogger;
        }


        public static object? ParameterToObject(ParameterInfo p, object? obj, Exception? ex)
        {
            // 1. If the parameter is an Exception, give it 'ex'
            if (typeof(Exception).IsAssignableFrom(p.ParameterType))
                return ex;

            // 2. If it's the 'format' string in a Format method
            if (p.Name == "format" && p.ParameterType == typeof(string))
                return obj?.ToString();

            // 3. If it's the standard 'message' object
            if (p.ParameterType == typeof(object))
                return obj;

            // 4. Default to null for things like IFormatProvider or params arrays 
            // (unless you want to add logic for those too)
            return null;
        }


        public static int LoggingErrorCount { get; set; } = 0;
        public static bool StopSavingLogs { get; set; } = false;


        public static async Task DoAppendForget(
            string Source,
            string Title,
            Exception? exception
        )
        {
            var stackWhenCalled = new System.Diagnostics.StackTrace(true).ToString();
            if (StopSavingLogs)
            {
                return;
            }
            try
            {
                DataLayer.Entities.Message newMessage;
                if (exception != null)
                {
                    newMessage = new DataLayer.Entities.Message
                    {
                        Source = Source,
                        Title = (Title ?? exception.Message).Limit(DataLayer.Entities.Message.Metadata.MaxLength[x => x.Title] ?? 1024),
                        Body = (exception.Data["OriginalStack"] as string ?? exception.StackTrace ?? stackWhenCalled).Limit(DataLayer.Entities.Message.Metadata.MaxLength[nameof(DataLayer.Entities.Message.Body)] ?? 4096),
                        Created = DateTime.UtcNow,
                        IsActive = true,
                        MessageType = 4
                    };
                }
                else
                {
                    newMessage = new DataLayer.Entities.Message
                    {
                        Source = Source,
                        Title = Title.Limit(DataLayer.Entities.Message.Metadata.MaxLength[nameof(DataLayer.Entities.Message.Title)] ?? 1024),
                        Body = stackWhenCalled.Limit(DataLayer.Entities.Message.Metadata.MaxLength[nameof(DataLayer.Entities.Message.Body)] ?? 4096),
                        Created = DateTime.UtcNow,
                        IsActive = true,
                        MessageType = 4
                    };
                }

                if (Manager == null)
                {
                    PreLog.Push(newMessage);
                }
                else
                {
                    _ = newMessage.Save(Query);
                    // so we get a stack trace with it
                    if (exception != null) Manager?.SetError(exception);
                    else
                    {
                        var reportEx = exception ?? new Exception(Title) { Source = Source };
                        reportEx.Data["OriginalStack"] = (exception?.Data["OriginalStack"] as string ?? exception?.StackTrace ?? stackWhenCalled);
                        Manager?.SetError(reportEx);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                LoggingErrorCount++;
                if (LoggingErrorCount >= 5)
                {
                    StopSavingLogs = true;
                }
            }
        }



#pragma warning disable IDE1006 // Naming Styles
        private Dictionary<string, Action<object, Exception?>> _levels { get; set; } = [];
#pragma warning restore IDE1006 // Naming Styles


        public virtual Action<object, Exception?> this[string level]
        {
            get
            {
                if (_levels.TryGetValue(level, out var log)) return log;
                _levels[level] = (message, ex) => WriteLog(level, message, ex, null);
                return _levels[level];
            }
            set
            {
                _levels[level] = value;
            }
        }

        public string? Filepath { get; set; }
        public string? Category { get; set; }

        public virtual void Info(object msg, Exception? ex = null) => WriteLog(nameof(Info), msg, ex);
        public virtual void Error(object msg, Exception? ex = null) => WriteLog(nameof(Error), msg, ex);
        public virtual void Fatal(object msg, Exception? ex = null) => WriteLog(nameof(Fatal), msg, ex);
        public virtual void Debug(object msg, Exception? ex = null) => WriteLog(nameof(Debug), msg, ex);


        private void InvokeWrappedLogger(MethodInfo levelDelegate, /* string level, */ object message, Exception? ex)
        {
            if (WrappedLogger != this && levelDelegate != null)
            {
                var parameters = levelDelegate?.GetParameters()
                    .Select(p => ParameterToObject(p, message, ex)).ToArray();
                // prevent accidental recursion from implementors, the only way to arrive here is to overload the methods above
                if (!typeof(SimpleLogger).IsAssignableFrom(levelDelegate?.DeclaringType))
                {
                    levelDelegate?.Invoke(WrappedLogger, parameters);
                }
            }
            else
            {
                Task.Run(() =>
                {
                    if (ex != null) Manager?.SetError(ex);
                    else Manager?.SetError(new Exception(message.ToString()) { Source = Category });
                });

                // TODO: I don't know if this is wise, it generates loops of errors
                //var reportEx = ex ?? new Exception(message.ToString()) { Source = Category };
                // its not wrapped by log4net so try and save the error anyways
                //_ = DoAppendForget(Category ?? "Internal", message.ToString() ?? ex?.Message ?? string.Empty, reportEx);
            }
        }


        private void WriteLog(string level, object message, Exception? ex = null, MethodInfo? levelDelegate = null)
        {
            var stackWhenCalled = new System.Diagnostics.StackTrace(true).ToString();
            ex?.Data["OriginalStack"] = stackWhenCalled;
            // 1. Incorporate Node-style attributes [Service][Folder][File]
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var folder = Category?.Split('\\').SkipLast(1).LastOrDefault();
            var file = Category?.Split('\\').LastOrDefault();

            // Build the "Pre-pended" message like your Node notebook
            string formattedPrefix = $"[{timestamp}][{level.ToUpper()}][{folder}][{file}]";
            string finalMessage = $"{formattedPrefix} {message}";

            // 2. Output to Console (Immediate Feedback)

            Console.WriteLine(finalMessage);
            //if (ex != null) Console.WriteLine(ex);


            // 5. If a heavy logger (log4net) is attached via Reflection, invoke it
            if (levelDelegate != null)
            {
                var reportEx = ex ?? new Exception(message.ToString()) { Source = Category };
                reportEx.Data["OriginalStack"] = stackWhenCalled;
                InvokeWrappedLogger(levelDelegate, /* level, */ message, reportEx);
            }

        }
    }

    namespace Logging
    {
        public static class Log
        {

            public static void Info(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath)[nameof(Info)](message, ex);
            }

            public static void Error(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath)[nameof(Error)](message, ex);
            }

            public static void Fatal(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath)[nameof(Fatal)](message, ex);
            }

            public static void Debug(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath)[nameof(Debug)](message, ex);
            }
        }
    }
}
