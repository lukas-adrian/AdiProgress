using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdiProgressClient
{
    public class AdiProgressClient : IDisposable
    {
        private NamedPipeClientStream _pipe;
        private StreamWriter _writer;
        private StreamReader _reader;
        private readonly string _category;
        private readonly string _taskId;
        private readonly bool _allowCancel;
        private readonly int _pid;
        private readonly long _startTime;
        private bool _isCancelled = false;
        private readonly long _parentHandle;
        private DateTime _lastReportTime = DateTime.MinValue;
        private readonly object _pipeLock = new object();
        private CancellationTokenSource _listenCts; 
        private readonly ConcurrentQueue<string> _incomingMessages = new ConcurrentQueue<string>();
        private readonly int _showDelayMs;
        private DateTime _firstUpdateTime = DateTime.MinValue;
        public static string ServerPath { get; set; }
        public bool IsCancelled => _isCancelled;

        public AdiProgressClient(string appName, bool allowCancel = true, long parentHandle = 0, int showAfterMs = 0)
        {
            _category = appName;
            _taskId = Guid.NewGuid().ToString();
            _allowCancel = allowCancel;
            _pid = Process.GetCurrentProcess().Id;
            _startTime = Process.GetCurrentProcess().StartTime.Ticks;
            _parentHandle = parentHandle;
            _showDelayMs = showAfterMs;
            
            EnsureAdiProgressRunning();
        }

        private void ConnectToPipe()
        {
            if (_pipe != null && _pipe.IsConnected) return;
        
            NamedPipeClientStream tempPipe = null;
            StreamWriter tempWriter = null;
            StreamReader tempReader = null;
        
            try
            {
                tempPipe = new NamedPipeClientStream(".", "AdiProgressPipe", 
                    PipeDirection.InOut, PipeOptions.Asynchronous);
        
                //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Connecting...");
                tempPipe.Connect(1000); 
                //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Connected!");
                
                tempWriter = new StreamWriter(tempPipe, new UTF8Encoding(false)) { AutoFlush = true };
                tempReader = new StreamReader(tempPipe, new UTF8Encoding(false));
                
                // Only assign to fields after everything succeeds
                Cleanup();
                _pipe = tempPipe;
                _writer = tempWriter;
                _reader = tempReader;
                
                _listenCts = new CancellationTokenSource();
                _ = Task.Run(() => ListenForMessages());
            }
            catch 
            {
                tempWriter?.Dispose();
                tempReader?.Dispose();
                tempPipe?.Dispose();
            }
        }
        
        private async Task ListenForMessages()
        {
            try
            {
                while (_pipe != null && _pipe.IsConnected && !_listenCts.Token.IsCancellationRequested)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;
            
                    _incomingMessages.Enqueue(line);
                    //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Received: {line}");
            
                    if ((line.Contains("\"Type\":\"Cancel\"") || line.Contains("\"Type\":1")) && line.Contains(_taskId))
                    {
                        //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Setting _isCancelled = true");
                        _isCancelled = true;
                        //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] IsCancelled is now: {_isCancelled}");
                        //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Event invoked");
                    }
                }
            }
            catch { }
        }

        public void ShowPleaseWaitSync(string status)
        {
            if (_pipe == null || !_pipe.IsConnected) ConnectToPipe();
            SendUpdate(0, status, true, forceNoCancel: true);
        }

        public async Task ShowPleaseWaitAsync(string status)
        {
            await Task.Run(() => ShowPleaseWaitSync(status));
        }

        public void UpdateProgressSync(int index, int totalCount, string status)
        {
            int percent = (totalCount > 0) ? (int)((double)index / totalCount * 100) : 0;
            if (percent > 0 && percent < 100 && (DateTime.Now - _lastReportTime).TotalMilliseconds < 150) return;

            if (_pipe == null || !_pipe.IsConnected) ConnectToPipe();
            SendUpdate(percent, status, false);
        }

        public async Task UpdateProgressAsync(int index, int totalCount, string status)
        {
            await Task.Run(() => UpdateProgressSync(index, totalCount, status));
        }

        private void SendUpdate(int percent, string status, bool indeterminate, bool forceNoCancel = false)
        {
            if (_firstUpdateTime == DateTime.MinValue)
                _firstUpdateTime = DateTime.Now;
    
            // Don't send if still within delay window
            if (_showDelayMs > 0 && (DateTime.Now - _firstUpdateTime).TotalMilliseconds < _showDelayMs)
                return;
            
            if (_pipe == null || !_pipe.IsConnected || _writer == null) return;

            try
            {
                string escapedStatus = status?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
                string json = "{" +
                              "\"Type\":0," +
                              $"\"PID\":{_pid}," +
                              $"\"StartTime\":{_startTime}," +
                              $"\"Category\":\"{_category}\"," +
                              $"\"TaskID\":\"{_taskId}\"," +
                              $"\"Progress\":{percent}," +
                              $"\"Status\":\"{escapedStatus}\"," +
                              $"\"ParentHandle\":{_parentHandle}," +
                              $"\"AllowCancel\":{(forceNoCancel ? "false" : (_allowCancel ? "true" : "false"))}," +
                              $"\"IsIndeterminate\":{(indeterminate ? "true" : "false")}" +
                              "}";

                lock (_pipeLock)
                {
                    //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Sending: {percent}% - {status}");
                    _writer.WriteLine(json);
                    _writer.Flush();
                }
                _lastReportTime = DateTime.Now;
            }
            catch { Cleanup(); }
        }

        private async Task ListenForCancel()
        {
            _listenCts = new CancellationTokenSource();
            try
            {
                while (_pipe != null && _pipe.IsConnected)
                {
                    var line = await _reader.ReadLineAsync();
                    //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] Received from server: {line}"); // ADD
                    if (line == null) break;
                    if (_listenCts.Token.IsCancellationRequested) break;
                    if (line.Contains("\"Type\":1") && line.Contains(_taskId))
                    {
                        //Console.WriteLine($"[CLIENT {_taskId.Substring(0,8)}] CANCEL DETECTED!");
                        _isCancelled = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Listen error: {ex.Message}");
            }
        }

        private void EnsureAdiProgressRunning()
        {
            if (Process.GetProcessesByName("AdiProgress").Length > 0) return;
            if (File.Exists(ServerPath)) Process.Start(ServerPath);
        }

        public void Dispose()
        {
            try
            {
                if (_pipe != null && _pipe.IsConnected)
                {
                    string closeMsg = "{\"Type\":2,\"TaskID\":\"" + _taskId + "\"}";
                    lock(_pipeLock) { _writer?.WriteLine(closeMsg); _writer?.Flush(); }
                }
            }
            catch { }
            finally { Cleanup(); }
        }

        private void Cleanup()
        {
            _listenCts?.Cancel();
            _listenCts?.Dispose();
            _listenCts = null;
            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _pipe?.Dispose(); } catch { }
            _writer = null; _reader = null; _pipe = null;
        }
    }
}
