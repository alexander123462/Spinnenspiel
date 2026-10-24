@echo off
rem Startet die Autopilot-Schleife. Aufruf: autopilot_loop.bat 1700
set LOGB=C:\Users\vorau\dev\Spinnenspiel\Logs\autopilot_bat.log
echo [%DATE% %TIME%] bat start, Param=[%1] >> "%LOGB%"
"C:\Program Files\Git\bin\bash.exe" -c "cd /c/Users/vorau/dev/Spinnenspiel && bash Tools/autopilot_loop.sh %1"
echo [%DATE% %TIME%] bat ende, errorlevel=%ERRORLEVEL% >> "%LOGB%"
