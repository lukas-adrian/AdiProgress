using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using AdiProgress.Models;
using AdiProgress.Protocol;

namespace AdiProgress.Services;

public class TaskManager
    {
        public ObservableCollection<TaskGroup> TaskGroups { get; }
        public event EventHandler AllTasksCompleted;
        private System.Timers.Timer _monitorTimer;
        
        public TaskManager()
        {
            TaskGroups = new ObservableCollection<TaskGroup>();
            
            // Start monitoring dead processes
            _monitorTimer = new System.Timers.Timer(5000); // Every 5 seconds
            _monitorTimer.Elapsed += CheckForDeadProcesses;
            _monitorTimer.Start();
        }
        
        private void CheckForDeadProcesses(object sender, System.Timers.ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var group in TaskGroups.ToList())
                {
                    foreach (var task in group.Tasks.ToList())
                    {
                        try
                        {
                            var process = Process.GetProcessById(task.PID);
                    
                            // Check if it's the SAME process (not PID reuse)
                            if (process.StartTime.Ticks != task.StartTime)
                            {
                                // Different process with same PID - remove task
                                group.Tasks.Remove(task);
                            }
                            // Process exists and matches - keep it
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist - remove task
                            group.Tasks.Remove(task);
                        }
                    }
            
                    // Remove empty groups
                    if (group.Tasks.Count == 0)
                    {
                        TaskGroups.Remove(group);
                    }
                }
        
                // Trigger auto-close if no tasks
                if (TaskGroups.Count == 0)
                {
                    AllTasksCompleted?.Invoke(this, EventArgs.Empty);
                }
            });
        }
        
        // public void StartIdleTimer()
        // {
        //     var timer = new System.Timers.Timer(5000); // 5 sec after last client
        //     timer.Elapsed += (s, e) => {
        //         Application.Current.Dispatcher.BeginInvoke(() => {
        //             if (TaskGroups.Count == 0) {
        //                 Application.Current.MainWindow?.Close();
        //             }
        //         });
        //     };
        //     timer.AutoReset = false;
        //     timer.Start();
        // }

        public void UpdateTask(ProgressMessage msg)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Console.WriteLine($"[UpdateTask] TaskID={msg.TaskID}, Category={msg.Category}, Progress={msg.Progress}");
                Console.WriteLine($"[UpdateTask] Existing groups: {TaskGroups.Count}, Tasks in '{msg.Category}': {TaskGroups.FirstOrDefault(g => g.Category == msg.Category)?.Tasks.Count ?? 0}");

                var group = TaskGroups.FirstOrDefault(g => g.Category == msg.Category);
                if (group == null)
                {
                    group = new TaskGroup { Category = msg.Category };
                    TaskGroups.Add(group);
                }

                // Look for the task using only the TaskID
                var task = group.Tasks.FirstOrDefault(t => t.TaskID == msg.TaskID);

                if (task == null)
                {
                    task = new ProgressTask
                    {
                        ClientId = msg.TaskID, 
                        PID = msg.PID,
                        StartTime = msg.StartTime,
                        Category = msg.Category,
                        TaskID = msg.TaskID,
                        AllowCancel = msg.AllowCancel
                    };
                    group.Tasks.Add(task);
    
                    CheckVisibility(msg.ParentHandle); 
                }
                else
                {
                    if (Application.Current.MainWindow?.Visibility != Visibility.Visible)
                        CheckVisibility(msg.ParentHandle);
                }

                task.Progress = msg.Progress;
                task.Status = msg.Status;
                task.IsIndeterminate = msg.IsIndeterminate;
            }));
        }
        
        // public void RemoveTasksByClient(string clientId)
        // {
        //     Application.Current.Dispatcher.BeginInvoke(() =>
        //     {
        //         foreach (var group in TaskGroups.ToList())
        //         {
        //             var tasksToRemove = group.Tasks.Where(t => t.ClientId == clientId).ToList();
        //             foreach (var task in tasksToRemove)
        //             {
        //                 group.Tasks.Remove(task);
        //             }
        //
        //             if (group.Tasks.Count == 0)
        //                 TaskGroups.Remove(group);
        //         }
        //         CheckVisibility();
        //         
        //         if (TaskGroups.Count == 0)
        //         {
        //             AllTasksCompleted?.Invoke(this, EventArgs.Empty);
        //         }
        //     });
        // }
        
        private readonly object _taskLock = new object();
        public void RemoveTask(string taskId)
        {
            //BeginInvoke for async, invoke would make problems 
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                lock (_taskLock)
                {
                    Console.WriteLine($"[DEBUG] Removing Task {taskId}. Remaining before: {TaskGroups.Sum(g => g.Tasks.Count)}");
            
                    foreach (var group in TaskGroups.ToList())
                    {
                        var task = group.Tasks.FirstOrDefault(t => t.TaskID == taskId);
                        if (task != null)
                        {
                            group.Tasks.Remove(task);
                            if (group.Tasks.Count == 0) TaskGroups.Remove(group);
                        }
                    }

                    int totalRemaining = TaskGroups.Sum(g => g.Tasks.Count);
                    Console.WriteLine($"[DEBUG] After removal, remaining: {totalRemaining}");
            
                    if (totalRemaining == 0)
                    {
                        Console.WriteLine("[Server] ALL tasks finished. Waiting before hide...");
                        _ = Task.Delay(100).ContinueWith(_ =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (TaskGroups.Sum(g => g.Tasks.Count) == 0) // Recheck
                                {
                                    Console.WriteLine("[Server] Still zero, hiding now.");
                                    Application.Current.MainWindow.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    Console.WriteLine("[Server] New tasks arrived, staying visible.");
                                }
                            });
                        });
                    }
                }
            });
        }
        
        private void CheckVisibility(long parentHandle = 0)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;

            // Count EVERY individual task currently running
            int activeTaskCount = TaskGroups.Sum(g => g.Tasks.Count);

            if (activeTaskCount > 0)
            {
                if (!mainWindow.IsVisible)
                {
                    if (parentHandle != 0)
                    {
                        var helper = new WindowInteropHelper(mainWindow);
                        helper.Owner = (IntPtr)parentHandle;
                        mainWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                    else
                    {
                        mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    mainWindow.Show();
                    mainWindow.Activate(); 
                }
            }
            else
            {
                // Only hide if the absolute last task is gone
                mainWindow.Hide();
            }
        }

        public void MarkCancelling(string taskId)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var group in TaskGroups)
                {
                    // Find by TaskID only (the unique GUID)
                    var task = group.Tasks.FirstOrDefault(t => t.TaskID == taskId);

                    if (task != null)
                    {
                        task.IsCancelling = true;
                        break;
                    }
                }
            });
        }
    }

    public class TaskGroup
    {
        public string Category { get; set; }
        public ObservableCollection<ProgressTask> Tasks { get; set; }

        public TaskGroup()
        {
            Tasks = new ObservableCollection<ProgressTask>();
        }
    }