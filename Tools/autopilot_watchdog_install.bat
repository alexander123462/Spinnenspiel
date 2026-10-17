@echo off
schtasks /Create /F /SC MINUTE /MO 20 /TN SpinnenspielWatchdog /TR "powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File C:\Users\vorau\dev\Spinnenspiel\Tools\autopilot_watchdog.ps1"
echo Watchdog eingerichtet (alle 20 Min). Entfernen: schtasks /Delete /TN SpinnenspielWatchdog /F
pause
