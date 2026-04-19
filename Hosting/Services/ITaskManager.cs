
namespace Hosting.Services;

public interface ITaskManager
{
    // Execution Management
    Task<Guid> LaunchTaskAsync(Guid templateId, TimeSpan? duration = null);
    Task StopTaskAsync(Guid instanceId, bool force = false);

    // Observability
    IObservable<ProcessLogEntry> StreamLogs(Guid instanceId);
    Task<ProcessStats> GetMetrics(Guid instanceId);

    // State
    IEnumerable<ActiveProcess> GetActiveProcesses();
}

public enum TaskType
{
    RunOnce = 0,
    FileWatch = 1,
    Scheduled = 2,
    LogWatch = 3,
    ProcessWatch = 4,
    KeepAlive = 5,

}


public class DynamicParam
{
    public string Key { get; set; } = "";
    public string Type { get; set; } = "string";
    public string Value { get; set; } = "";
    public bool BoolValue { get; set; }
}

public record ProcessLogEntry(DateTime Timestamp, string Level, string Message);

public record ProcessStats(double CpuPercentage, long WorkingSetMemory, TimeSpan Uptime);

public class ActiveProcess
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Pid { get; set; }
    public TaskType Type { get; set; }
    public string Status { get; set; } = "Starting"; // Starting, Running, Crashed, Completed
    public DateTime StartedAt { get; set; }
}

public class TaskTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public TaskType DefaultType { get; set; }
    public bool AutoRestart { get; set; }
}

