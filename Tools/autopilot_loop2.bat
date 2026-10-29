@echo off
set LOGB=C:\Users\vorau\dev\Spinnenspiel\Logs\autopilot_bat.log
echo [%DATE% %TIME%] bat2 start, Param=[%1] >> "%LOGB%"
"C:\Program Files\Git\bin\bash.exe" -c "cd /c/Users/vorau/dev/Spinnenspiel && bash Tools/autopilot_loop2.sh %1"
echo [%DATE% %TIME%] bat2 ende >> "%LOGB%"
