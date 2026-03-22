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
        void Info(object message, Exception? ex = null);
        void Error(object message, Exception? ex = null);
        void Fatal(object message, Exception? ex = null);
        void Debug(object message, Exception? ex = null);
        event Action<object?, Exception?>? OnLogged;
        static abstract SimpleLogger GetLogger(string filePath);
    }


    public class SimpleLogger() : ILog
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SimpleLogger> _loggerCache = new();
        public event Action<object?, Exception?>? OnLogged;
        public object? Logger { get; set; }

        public static SimpleLogger GetLogger(string filePath)
        {
            string category = Path.GetFileNameWithoutExtension(filePath);
            var logger = _loggerCache.GetOrAdd(category, cat => new SimpleLogger() {
                Filepath = filePath,
                Category = category
            });
            return logger;
        }

        public static SimpleLogger GetLogger(string filePath, Type levels, object replacement)
        {
            string category = Path.GetFileNameWithoutExtension(filePath);
            if (_loggerCache.TryGetValue(category, out var logger)) return logger;

            var levelsLogger = GetLogger(filePath);
            levelsLogger.Logger = replacement ?? levelsLogger;

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

                    levelFunction.Item1.Invoke(levelsLogger.Logger, parameters);
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

        public virtual void Debug(object message, Exception? ex = null)
        {
            Console.WriteLine(Filepath + " : " + Category);
            Console.WriteLine(message);
            if (ex != null) Console.WriteLine(ex);
            OnLogged?.Invoke(message, ex);
        }

        public virtual void Error(object message, Exception? ex = null)
        {
            Console.WriteLine(Filepath + " : " + Category);
            Console.WriteLine(message);
            if (ex != null) Console.WriteLine(ex);
            OnLogged?.Invoke(message, ex);
        }

        public virtual void Fatal(object message, Exception? ex = null)
        {
            Console.WriteLine(Filepath + " : " + Category);
            Console.WriteLine(message);
            if (ex != null) Console.WriteLine(ex);
            OnLogged?.Invoke(message, ex);
        }

        public virtual void Info(object message, Exception? ex = null)
        {
            Console.WriteLine(Filepath + " : " + Category);
            Console.WriteLine(message);
            if (ex != null) Console.WriteLine(ex);
            OnLogged?.Invoke(message, ex);
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
