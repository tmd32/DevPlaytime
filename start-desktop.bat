@echo off
cd /d "%~dp0"
if exist "%~dp0publish-latest\DevPlaytime.exe" (
  start "" "%~dp0publish-latest\DevPlaytime.exe"
  exit /b
)
if exist "%~dp0publish\DevPlaytime.exe" (
  start "" "%~dp0publish\DevPlaytime.exe"
  exit /b
)
if exist "%ProgramFiles%\dotnet\dotnet.exe" (
  "%ProgramFiles%\dotnet\dotnet.exe" run --project "%~dp0DevPlaytimeDesktop.csproj"
  exit /b
)
echo .NET Desktop Runtime or SDK is required.
pause
