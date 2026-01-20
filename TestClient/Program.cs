using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TestClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting multiple test tasks...");
            string name;
            if (args.Length == 0)
                name = Console.ReadLine();
            else
                name = args[0];
            
            if(string.IsNullOrWhiteSpace(name))
                name = "TestApp" + DateTime.Now.Millisecond.ToString();

            IntPtr Handle = Process.GetCurrentProcess().MainWindowHandle;
            long handleAsLong = Handle.ToInt64();
            
            AdiProgressClient.AdiProgressClient.ServerPath = @"D:\Development\AdiSoft\AdiProgress\AdiProgress\bin\x64\debug\net8.0-windows\AdiProgress.exe";
            // Define three separate tasks running in parallel
             var task1 = RunTaskAsync(name, "Async simple slow", 1000, false, handleAsLong); // Slow task
             var task2 = RunTaskAsync(name, "Async simple fast", 500, false, handleAsLong);    // Fast task
             var task3 = RunTaskAsync(name, "Async very very simple fast", 50, false, handleAsLong);    // Fast task, will not shown
             var task4 = RunCancelableTaskAsync(name, "Async with cancel", 1000, handleAsLong); // Task with Cancel button
             var task5 = RunShowWaitingAsync(name, "Please wait... async", 10000, false, handleAsLong);
             await Task.WhenAll(task1, task2, task3, task4, task5);
    
            for (int i = 0; i < 2; i++)
            {
                string sText = $"wait sync... in loop {i + 1}/2";
                RunShowWaitingSync(name, sText, 2000, false, handleAsLong);
            }
            
            for (int i = 0; i < 2; i++)
            {
                string sText = $"with cancel... in loop {i + 1}/2";
                RunCancelableTaskSync(name, sText, 500, handleAsLong);
            }
            for (int i = 0; i < 2; i++)
            {
                string sText = $"in loop {i + 1}/2";
                RunTaskSync(name, sText, 500, false, handleAsLong);;
            }

        }
        
        static async Task RunTaskAsync(string name, string text, int delay, bool allowCancel, long Handle)
        {
            using (var progress = new AdiProgressClient.AdiProgressClient(name, allowCancel: allowCancel, parentHandle: Handle, showAfterMs: 500))
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    await progress.UpdateProgressAsync(i, 100, $"Working on {text}: {i}%");
                    await Task.Delay(delay);
                }
            }
        }
        
        static void RunTaskSync(string name, string text, int delay, bool allowCancel, long Handle)
        {
            using (var progress = new AdiProgressClient.AdiProgressClient(name, allowCancel: allowCancel, parentHandle: Handle))
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    progress.UpdateProgressSync(i,100, $"Working on {text}: {i}%");
                    Thread.Sleep(delay);
                }
            }
        }
        
        static async Task RunShowWaitingAsync(string name, string text, int delay, bool allowCancel, long Handle)
        {
            using (var progress = new AdiProgressClient.AdiProgressClient(name, allowCancel: allowCancel, parentHandle: Handle))
            {
                await progress.ShowPleaseWaitAsync(text);
                await Task.Delay(delay);
            }
        }
        
        static void RunShowWaitingSync(string name, string text, int delay, bool allowCancel, long Handle)
        {
            using (var progress = new AdiProgressClient.AdiProgressClient(name, allowCancel: allowCancel, parentHandle: Handle))
            {
                progress.ShowPleaseWaitSync(text);
                Thread.Sleep(delay);
            }
        }
    
        static async Task RunCancelableTaskAsync(string name, string text, int delay, long Handle)
        {
            using (var progress = new AdiProgressClient.AdiProgressClient(name, allowCancel: true, parentHandle: Handle))
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    if (progress.IsCancelled) // ADD this
                    {
                        Console.WriteLine($"[{name}] IsCancelled detected!");
                        return;
                    }

                    await progress.UpdateProgressAsync(i, 100, $"Working on {text}: {i}%");
                    await Task.Delay(delay);
                    
                    for (int s = 0; s < delay; s += 100)
                    {
                        if (progress.IsCancelled) return; 
                        Task.Delay(100);
                    }
                }
            }
        }
        
        static void RunCancelableTaskSync(string name, string text, int delay, long Handle)
        {
            using (var progress = new AdiProgressClient.AdiProgressClient(name, allowCancel: true, parentHandle: Handle))
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    if (progress.IsCancelled) 
                    {
                        Console.WriteLine($"[{name}] IsCancelled detected!");
                        return;
                    }

                    progress.UpdateProgressSync(i, 100, $"Working on {text}: {i}%");
                    
                    //cancel will take too long, looks weired
                    for (int s = 0; s < delay; s += 100)
                    {
                        if (progress.IsCancelled) return; 
                        //System.Windows.Forms.Application.DoEvents(); 
                        Thread.Sleep(100);
                    }
                }
            }
        }
    }
}

