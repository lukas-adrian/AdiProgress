using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AdiProgress.Protocol;

namespace AdiProgress.Services
{
    public class PipeServer
    {
        private const string PipeName = "AdiProgressPipe";
        private readonly TaskManager _taskManager;
        private CancellationTokenSource _cts;

        public PipeServer(TaskManager taskManager)
        {
            _taskManager = taskManager;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenForClients(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }
        
        private async Task ListenForClients(CancellationToken ct)
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite, 
                AccessControlType.Allow));
    
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            
            while (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"ListenForClients -> [{DateTime.Now:HH:mm:ss.fff}] Creating new pipe instance..."); // ADD
                // 1. Prepare the instance
                NamedPipeServerStream  server = null;

                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Creating new pipe instance...");
                    server = NamedPipeServerStreamAcl.Create(
                        PipeName,
                        PipeDirection.InOut,
                        10,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        4096,
                        4096,
                        pipeSecurity
                    );
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Pipe created successfully"); // ADD

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for connection...");
                    await server.WaitForConnectionAsync(ct);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client connected! Spawning handler...");

                    _ = Task.Run(() => HandleClient(server));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ListenForClients -> [{DateTime.Now:HH:mm:ss.fff}] PIPE CREATE ERROR: {ex.Message}"); // CHANGE
                    server.Dispose();
                    if (ex is OperationCanceledException) break;
                    await Task.Delay(100, ct);
                }
            }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() } 
        };
        
       
        private async Task HandleClient(NamedPipeServerStream pipe)
        {
            string clientId = null;
            // We create these outside so the finally block can see them if needed
            StreamReader reader = null;
            StreamWriter writer = null;

            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server: Client connected. Setting up streams...");
                reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
                writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

                while (pipe.IsConnected)
                {
                    // This is the most important debug line:
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server: Waiting for data...");
                    var json = await reader.ReadLineAsync();
            
                    if (json == null) 
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server: Client disconnected. Cleaning up {clientId}");
                        break;
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server: Received {json.Length} bytes.");

                    try 
                    {
                        var msg = JsonSerializer.Deserialize<ProgressMessage>(json, _jsonOptions);
                        clientId = $"{msg.PID}_{msg.StartTime}_{msg.TaskID}";
                        _clientWriters[clientId] = writer;

                        Console.WriteLine($"[DEBUG] Parsed Indeterminate: {msg?.IsIndeterminate}");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RECV TaskID={msg.TaskID} Type={msg.Type} Progress={msg.Progress}%");

                        Application.Current.Dispatcher.BeginInvoke(() => HandleMessage(msg, writer));
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server DATA ERROR: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection lost: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Finally cleanup START for {clientId}");
    
                if (clientId != null) 
                {
                    var parts = clientId.Split('_');
                    if (parts.Length >= 3)
                        _clientWriters.TryRemove(parts[2], out _);
                }
    
                try { reader?.Dispose(); } 
                catch (Exception ex) { Console.WriteLine($"Reader dispose error: {ex.Message}"); }
    
                try { writer?.Dispose(); } 
                catch (Exception ex) { Console.WriteLine($"Writer dispose error: {ex.Message}"); }
    
                try 
                { 
                    if (pipe.IsConnected) pipe.Disconnect(); 
                } 
                catch (Exception ex) { Console.WriteLine($"Disconnect error: {ex.Message}"); }
    
                try { pipe.Dispose(); } 
                catch (Exception ex) { Console.WriteLine($"Pipe dispose error: {ex.Message}"); }
    
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Finally cleanup DONE for {clientId}");
            }
        }
        
        private void HandleMessage(ProgressMessage msg, StreamWriter writer)
        {
            // Console.WriteLine($"[RECV] Time: {DateTime.Now:HH:mm:ss}");
            // Console.WriteLine($"       Type: {msg.Type}");
            // Console.WriteLine($"       TaskID: {msg.TaskID}");
            // Console.WriteLine($"       Category: {msg.Category}");
            // Console.WriteLine($"       Progress: {msg.Progress}%");

            if (msg == null)
            {
                Console.WriteLine("!!! [ERROR] Received a null message object !!!");
                return;
            }

            switch (msg.Type)
            {
                case MessageType.Update:
                    Console.WriteLine(" -> Calling UpdateTask...");
                    _taskManager.UpdateTask(msg);
                    break;

                case MessageType.TaskComplete:
                    Console.WriteLine(" -> Calling RemoveTask...");
                    _taskManager.RemoveTask(msg.TaskID);
                    break;
            
                case MessageType.Cancel:
                    Console.WriteLine(" -> Client sent Cancel confirmation.");
                    break;
            }
        }

        private readonly ConcurrentDictionary<string, StreamWriter> _clientWriters = new();

        public void SendCancel(int pid, long startTime, string taskId)
        {
            var clientId = $"{pid}_{startTime}_{taskId}";
            
            if (_clientWriters.TryGetValue(clientId, out var writer))
            {
                try
                {
                    var cancelMsg = new ProgressMessage
                    {
                        Type = MessageType.Cancel,
                        PID = pid,
                        StartTime = startTime,
                        TaskID = taskId
                    };
                    
                    var json = JsonSerializer.Serialize(cancelMsg, _jsonOptions);
                    writer.WriteLine(json);
                    writer.Flush(); // Ensure it leaves the buffer immediately
                    Console.WriteLine($"[Server] Cancel sent to {clientId}");
                }
                catch { }
            }
            else 
            {
                Console.WriteLine($"[Server] Failed to find writer for key: {clientId}");
            }
        }
    }
}