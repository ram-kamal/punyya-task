@echo off
title Punnya Heart Foundation - Workstation Desk
echo Starting Punnya Heart Workstation Desk...
if exist "%~dp0client\publish\PunnyaHeartDesk-Beta-win-x64\PunnyaHeartDesk.exe" (
    start "" "%~dp0client\publish\PunnyaHeartDesk-Beta-win-x64\PunnyaHeartDesk.exe"
) else (
    start "" "%~dp0client\src\PHF.Client.Wpf\bin\Debug\net8.0-windows\PunnyaHeartDesk.exe"
)

