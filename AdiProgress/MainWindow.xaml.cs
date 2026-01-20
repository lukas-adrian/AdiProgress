using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AdiProgress.Models;
using AdiProgress.Services;

namespace AdiProgress;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly TaskManager _taskManager;
    private readonly PipeServer _pipeServer;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    public MainWindow()
    {
        InitializeComponent();
        //opens the console with outputs
        //AllocConsole();
        _taskManager = new TaskManager();
        _pipeServer = new PipeServer(_taskManager);

        this.Title = App.Settings.WindowTitle;
        HeaderTitle.Text = App.Settings.WindowTitle.ToUpper();
        DataContext = _taskManager;

        _taskManager.AllTasksCompleted += OnAllTasksCompleted;
        _pipeServer.Start();

        // IF YOU DON'T SEE THIS IN THE OUTPUT WINDOW, THE APP IS DEADLOCKED
        System.Diagnostics.Debug.WriteLine("UI Thread: MainWindow Constructor Finished!");

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Prevent focus stealing
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }

    private void OnAllTasksCompleted(object sender, EventArgs e)
    {
        // Start idle timer - close after 10 seconds of no tasks
        var timer = new System.Timers.Timer(10000);
        timer.Elapsed += (s, args) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_taskManager.TaskGroups.Count == 0)
                {
                    Close();
                }
            });
            timer.Stop();
        };
        timer.Start();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            // This allows the user to drag the window by clicking the header
            this.DragMove();
        }
    }

    private void CancelTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is ProgressTask task)
        {
            _taskManager.MarkCancelling(task.TaskID);
            _pipeServer.SendCancel(task.PID, task.StartTime, task.TaskID);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _pipeServer.Stop();
        base.OnClosed(e);
    }
}