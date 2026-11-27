@echo off
rem Compiles TaskbarBattery.exe with the C# compiler bundled with Windows (.NET Framework 4.x). No SDK required.
setlocal
set ROOT=%~dp0
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set CSC=%FW%\csc.exe
rem Windows Runtime metadata, for the Bluetooth LE advertisement watcher.
set WINMD=%WINDIR%\System32\WinMetadata

if not exist "%CSC%" (
  echo C# compiler not found: %CSC%
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /win32manifest:"%ROOT%app.manifest" /out:"%ROOT%TaskbarBattery.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.WindowsRuntime.dll" ^
  /r:"%FW%\System.Runtime.InteropServices.WindowsRuntime.dll" ^
  /r:"%WINMD%\Windows.Foundation.winmd" /r:"%WINMD%\Windows.Devices.winmd" /r:"%WINMD%\Windows.Storage.winmd" ^
  "%ROOT%src\*.cs"
exit /b %errorlevel%
