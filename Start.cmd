@echo off
powershell.exe -NoProfile -Command "Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File ""%~dp0src\App\Tray.ps1""'"
