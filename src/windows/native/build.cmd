@echo off
setlocal
where cl.exe >nul 2>nul
if errorlevel 1 (
  echo Open an x64 Native Tools Command Prompt with the Windows 11 SDK installed.
  exit /b 1
)
cl /nologo /O2 /MT /EHsc /std:c++17 "%~dp0Capture.cpp" /Fe:SoundMeshCapture.exe /link mmdevapi.lib ole32.lib
exit /b %errorlevel%
