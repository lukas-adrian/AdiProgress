namespace AdiProgress.Protocol;

public enum MessageType
{
    Update,
    Cancel,
    TaskComplete,
    TaskCancelled
}

public class ProgressMessage
{
    public MessageType Type { get; set; }
    public int PID { get; set; }
    public long StartTime { get; set; }
    public string Category { get; set; }
    public string TaskID { get; set; }
    public int Progress { get; set; }
    public string Status { get; set; }
    public bool AllowCancel { get; set; }
    public long ParentHandle { get; set; }
    public bool IsIndeterminate { get; set; }
}
