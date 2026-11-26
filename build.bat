@echo off
rem Compiles TaskbarBattery.exe with the C# compiler bundled with Windows (.NET Framework 4.x). No SDK required.
setlocal
set ROOT=%~dp0
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
  echo C# compiler not found: %CSC%
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /win32manifest:"%ROOT%app.manifest" /out:"%ROOT%TaskbarBattery.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  "%ROOT%src\*.cs"
exit /b %errorlevel%
