@echo off
title LockStep Server
cd /d "%~dp0"
dotnet run --project "%~dp0LockStep.Server\LockStep.Server.csproj"
if errorlevel 1 pause
