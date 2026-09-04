@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "ROOT=%~dp0.."
set "PROTO_DIR=%ROOT%\Config"
set "CLIENT_OUT=%ROOT%\Client\LockStep\Assets\GameMain\Scripts\Generated"
set "SERVER_OUT=%ROOT%\Server\LockStep.Server\Generated"
set "GEN_DIR=%~dp0_proto_gen"
set "TOOLS_DIR=%~dp0protobuf-tools"
set "PROTOC="

if not exist "%PROTO_DIR%\lockstep.proto" (
  echo [gen-proto] missing "%PROTO_DIR%\lockstep.proto"
  exit /b 1
)

if exist "%~dp0protoc.exe" set "PROTOC=%~dp0protoc.exe"
if not defined PROTOC if exist "%TOOLS_DIR%\tools\windows_x64\protoc.exe" set "PROTOC=%TOOLS_DIR%\tools\windows_x64\protoc.exe"
if not defined PROTOC (
  for /d %%D in ("%USERPROFILE%\.nuget\packages\google.protobuf.tools\*") do (
    if exist "%%D\tools\windows_x64\protoc.exe" set "PROTOC=%%D\tools\windows_x64\protoc.exe"
  )
)

if not defined PROTOC (
  echo [gen-proto] downloading Google.Protobuf.Tools 3.36.0
  if not exist "%TOOLS_DIR%" mkdir "%TOOLS_DIR%"
  powershell -NoProfile -Command "Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/Google.Protobuf.Tools/3.36.0' -OutFile '%TOOLS_DIR%\tools.nupkg'"
  if errorlevel 1 (
    echo [gen-proto] download failed
    exit /b 1
  )
  copy /y "%TOOLS_DIR%\tools.nupkg" "%TOOLS_DIR%\tools.zip" >nul
  powershell -NoProfile -Command "Expand-Archive -Force '%TOOLS_DIR%\tools.zip' '%TOOLS_DIR%'"
  if exist "%TOOLS_DIR%\tools\windows_x64\protoc.exe" set "PROTOC=%TOOLS_DIR%\tools\windows_x64\protoc.exe"
)

if not defined PROTOC (
  echo [gen-proto] protoc.exe not found. Put it at Tools\protoc.exe or run this bat once with network.
  exit /b 1
)

echo [gen-proto] using "!PROTOC!"
if not exist "%GEN_DIR%" mkdir "%GEN_DIR%"
if not exist "%CLIENT_OUT%" mkdir "%CLIENT_OUT%"
if not exist "%SERVER_OUT%" mkdir "%SERVER_OUT%"

"!PROTOC!" --csharp_out="%GEN_DIR%" --proto_path="%PROTO_DIR%" "%PROTO_DIR%\lockstep.proto"
if errorlevel 1 (
  echo [gen-proto] protoc failed
  exit /b 1
)

if not exist "%GEN_DIR%\Lockstep.cs" (
  echo [gen-proto] expected "%GEN_DIR%\Lockstep.cs"
  exit /b 1
)

> "%GEN_DIR%\Lockstep.cs.tmp" echo #define GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
type "%GEN_DIR%\Lockstep.cs" >> "%GEN_DIR%\Lockstep.cs.tmp"
move /y "%GEN_DIR%\Lockstep.cs.tmp" "%GEN_DIR%\Lockstep.cs" >nul

copy /y "%GEN_DIR%\Lockstep.cs" "%CLIENT_OUT%\Lockstep.cs" >nul
copy /y "%GEN_DIR%\Lockstep.cs" "%SERVER_OUT%\Lockstep.cs" >nul
echo [gen-proto] wrote
echo   %CLIENT_OUT%\Lockstep.cs
echo   %SERVER_OUT%\Lockstep.cs
exit /b 0
