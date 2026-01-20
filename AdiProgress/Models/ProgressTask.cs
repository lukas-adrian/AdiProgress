namespace AdiProgress.Models;
using System.ComponentModel;

public class ProgressTask : INotifyPropertyChanged
{
    private int _progress;
    private string _status;
    private bool _isCancelling;

    public string ClientId { get; set; }
    public int PID { get; set; }
    public long StartTime { get; set; }
    public string Category { get; set; }
    public string TaskID { get; set; }
    public bool AllowCancel { get; set; }
    
    private bool _isIndeterminate;
    
    public bool IsIndeterminate 
    { 
        get => _isIndeterminate;
        set 
        { 
            _isIndeterminate = value; 
            OnPropertyChanged(nameof(IsIndeterminate)); 
            OnPropertyChanged(nameof(DisplayText)); // Ensure text refreshes
        }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(DisplayText)); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(DisplayText)); }
    }

    public bool IsCancelling
    {
        get => _isCancelling;
        set { _isCancelling = value; OnPropertyChanged(nameof(IsCancelling)); }
    }

    public string DisplayText => IsIndeterminate 
        ? Status 
        : $"{Status} - {Progress}%";

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}