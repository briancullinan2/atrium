using System;
using System.Collections.Generic;
using System.Text;

namespace Interfacing.Services;

public interface ILog
{
    void Info(object message, Exception? ex = null);
    void Error(object message, Exception? ex = null);
    void Fatal(object message, Exception? ex = null);
    void Debug(object message, Exception? ex = null);
    //static abstract SimpleLogger GetLogger(string filePath);
    Action<object, Exception?> this[string level] { get; set; }
    string? Filepath { get; set; }
    string? Category { get; set; }
}

public interface IHasLog
{
    static abstract Task DoAppendForget(
            string Source,
            string Title,
            Exception? exception
        );
}
