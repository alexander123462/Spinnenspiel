@echo off
setlocal
title Spinnenspiel - bauen und starten
set "PROJ=C:\Users\vorau\dev\Spinnenspiel"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"
set "EXE=%PROJ%\Builds\Windows64\Spinnenspiel.exe"
set "LOG=%PROJ%\Logs\bauen_und_starten.log"

if not exist "%UNITY%" goto :keinunity

rem Laeuft Unity gerade mit diesem Projekt? (Editor offen oder Hermes baut/testet)
powershell -NoProfile -Command "if (Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { $_.CommandLine -match 'Spinnenspiel' }) { exit 1 }"
if errorlevel 1 goto :besetzt

rem Laufendes Spiel schliessen, sonst ist die exe gesperrt
taskkill /im Spinnenspiel.exe /f >nul 2>&1

echo.
echo  Baue die neueste Version vom Spinnenspiel ...
echo  Unity laeuft unsichtbar im Hintergrund, das dauert ein paar Minuten.
echo.
"%UNITY%" -batchmode -quit -projectPath "%PROJ%" -executeMethod Spinnenspiel.EditorTools.BuildWindowsTool.BuildWindows -logFile "%LOG%"

findstr /l /c:"Windows Player built" "%LOG%" >nul 2>&1
if errorlevel 1 goto :fehler

echo  Fertig - Spiel startet.
start "" /d "%PROJ%\Builds\Windows64" "%EXE%"
exit /b 0

:besetzt
echo.
echo  Unity hat das Projekt gerade offen - entweder der Unity-Editor
echo  oder Hermes baut/testet gerade. Zwei Unity-Instanzen auf
echo  demselben Projekt gehen nicht gleichzeitig.
echo.
goto :altstarten

:fehler
echo.
echo  Der Build ist fehlgeschlagen. Die wichtigsten Meldungen:
echo.
findstr /l /c:"error CS" /c:"[Build]" /c:"Exception" "%LOG%"
echo.
echo  Komplettes Log: %LOG%
echo.
goto :altstarten

:altstarten
if not exist "%EXE%" goto :ende
choice /c JN /m " Stattdessen die zuletzt gebaute Version starten"
if errorlevel 2 goto :ende
start "" /d "%PROJ%\Builds\Windows64" "%EXE%"
exit /b 0

:keinunity
echo.
echo  Unity 6000.3.24f1 wurde nicht gefunden unter:
echo  %UNITY%
echo.

:ende
pause
exit /b 1
