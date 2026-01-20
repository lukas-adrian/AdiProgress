@echo off
echo Starting Chaos Test...

:: Start .NET 8 Clients
start TestClient\bin\x64\Release\net8.0-windows\TestClient.exe Application1
start TestClient\bin\x64\Release\net8.0-windows\TestClient.exe OtherApplication
::start TestClient\bin\x64\Release\net8.0-windows\TestClient.exe NET8Client-3

:: Start .NET 4.8 Clients
::start TestClient\bin\x64\Release\net48\TestClient.exe NET48Client-1
::start TestClient\bin\x64\Release\net48\TestClient.exe NET48Client-2
::start TestClient\bin\x64\Release\net48\TestClient.exe NET48Client-3

echo All clients launched!
pause