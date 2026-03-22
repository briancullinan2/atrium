using DataLayer.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

namespace FlashCard.Services
{
    public interface ILog
    {
        void Info(object message, Exception? ex = null, [CallerFilePath] string cp = "");
        void Error(object message, Exception? ex = null, [CallerFilePath] string cp = "");
        void Fatal(object message, Exception? ex = null, [CallerFilePath] string cp = "");
        void Debug(object message, Exception? ex = null, [CallerFilePath] string cp = "");
        event Action<object?, Exception?>? OnLogged;
        static abstract SimpleLogger GetLogger(string filePath);
    }


    public class SimpleLogger() : ILog
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SimpleLogger> _loggerCache = new();
        public event Action<object?, Exception?>? OnLogged;
        public object? WrappedLogger { get; set; }
#pragma warning disable IDE1006 // Naming Styles
        private static IServiceProvider? _services { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        internal static IServiceProvider? Services
        {
            get
            {
                return _services;
            }
            set
            {
                _services = value;
                foreach (var pre in PreLog)
                {
                    _ = pre.Save();
                }

                var pageManager = _services?.GetService<IPageManager>();
                if (PreLog.LastOrDefault() is DataLayer.Entities.Message newMessage)
                {
                    pageManager?.SetError(new Exception(newMessage.Title, new Exception(newMessage.Body)));
                }
                PreLog.Clear();
            }
        }
        internal static List<DataLayer.Entities.Message> PreLog { get; set; } = [];


        public static SimpleLogger GetLogger(string filePath)
        {
            return GetLogger(filePath, typeof(SimpleLogger));
        }

        public static SimpleLogger GetLogger(string filePath, Type levels, object? replacement = null)
        {
            string category = Path.GetFileNameWithoutExtension(filePath);
            if (_loggerCache.TryGetValue(category, out var logger)) return logger;

            var levelsLogger = _loggerCache.GetOrAdd(category, cat => new SimpleLogger() {
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
                levelsLogger.Levels[levelFunction.Item1.Name] = (obj, ex) =>
                {
                    var parameters = levelFunction.Item2.Select(p => ParameterToObject(p, obj, ex)).ToArray();

                    levelFunction.Item1.Invoke(levelsLogger.WrappedLogger, parameters);
                };
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


        public static async Task DoAppendForget(
            string Source,
            string Title,
            Exception? exception
        )
        {
            try
            {
                DataLayer.Entities.Message newMessage;
                if (exception != null)
                {
                    newMessage = new DataLayer.Entities.Message
                    {
                        Source = Source,
                        Title = exception.Message.Limit(DataLayer.EntityMetadata.Message.MaxLength[x => x.Title] ?? 1024),
                        Body = exception.StackTrace?.Limit(DataLayer.EntityMetadata.Message.MaxLength[nameof(DataLayer.Entities.Message.Body)] ?? 4096),
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
                        Title = Title.Limit(DataLayer.EntityMetadata.Message.MaxLength[nameof(DataLayer.Entities.Message.Title)] ?? 1024),
                        Body = new System.Diagnostics.StackTrace(true).ToString().Limit(DataLayer.EntityMetadata.Message.MaxLength[nameof(DataLayer.Entities.Message.Body)] ?? 4096),
                        Created = DateTime.UtcNow,
                        IsActive = true,
                        MessageType = 4
                    };
                }

                if (Services == null)
                {
                    PreLog.Add(newMessage);
                }
                else
                {
                    _ = newMessage.Save();
                    var pageManager = Services.GetService<IPageManager>();
                    pageManager?.SetError(new Exception(newMessage.Title, new Exception(newMessage.Body)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }



        public virtual Dictionary<string, Action<object, Exception?>> Levels { get; set; } = [];


        public virtual Action<object, Exception?> this[string level]
        {
            get
            {
                if (Levels.TryGetValue(level, out var log)) return log;
                var levelDelegate = GetType().GetMethod(level);
                if(levelDelegate == null) GetType().GetMethod(nameof(Debug));
                Levels[level] = (message, ex) => levelDelegate?.Invoke(this, [message, ex]);
                return Levels[level];
            }
            set
            {
                Levels[level] = value;
            }
        }

        public string? Filepath { get; set; }
        public string? Category { get; set; }

        public virtual void Info(object msg, Exception? ex = null, [CallerFilePath] string cp = "") => WriteLog(nameof(Info), msg, ex, cp);
        public virtual void Error(object msg, Exception? ex = null, [CallerFilePath] string cp = "") => WriteLog(nameof(Error), msg, ex, cp);
        public virtual void Fatal(object msg, Exception? ex = null, [CallerFilePath] string cp = "") => WriteLog(nameof(Fatal), msg, ex, cp);
        public virtual void Debug(object msg, Exception? ex = null, [CallerFilePath] string cp = "") => WriteLog(nameof(Debug), msg, ex, cp);


        private void InvokeWrappedLogger(string level, object message, Exception? ex)
        {
            if (WrappedLogger != null && Levels.TryGetValue(level, out var Log))
            {
                Log(message, ex);
                return;
            }

            if (WrappedLogger == null)
            {
                Console.WriteLine(Filepath + " : " + Category);
                Console.WriteLine(message);
                if (ex != null) Console.WriteLine(ex);
                OnLogged?.Invoke(message, ex);
            }

        }


        private void WriteLog(string level, object message, Exception? ex, string callerPath)
        {
            // 1. Incorporate Node-style attributes [Service][Folder][File]
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var folder = Path.GetFileName(Path.GetDirectoryName(callerPath)) ?? "Root";
            var file = Path.GetFileNameWithoutExtension(callerPath);

            // Build the "Pre-pended" message like your Node notebook
            string formattedPrefix = $"[{timestamp}][{level.ToUpper()}][{folder}][{file}]";
            string finalMessage = $"{formattedPrefix} {message}";

            // 2. Output to Console (Immediate Feedback)
            Console.WriteLine(finalMessage);
            if (ex != null) Console.WriteLine(ex);

            // 3. Fire events for UI (IPageManager)
            OnLogged?.Invoke(message, ex);

            // 4. Fire off to Database (Fire and Forget)
            _ = DoAppendForget(file, message.ToString() ?? "", ex);

            // 5. If a heavy logger (log4net) is attached via Reflection, invoke it
            InvokeWrappedLogger(level, message, ex);
        }
    }

    namespace Logging
    {
        public static class Log
        {

            public static void Info(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath).Levels[nameof(Info)](message, ex);
            }

            public static void Error(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath).Levels[nameof(Error)](message, ex);
            }

            public static void Fatal(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath).Levels[nameof(Fatal)](message, ex);
            }

            public static void Debug(object message, Exception? ex = null, [CallerFilePath] string callerPath = "")
            {
                SimpleLogger.GetLogger(callerPath).Levels[nameof(Debug)](message, ex);
            }
        }
    }
}
