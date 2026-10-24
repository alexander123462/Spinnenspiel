' Startet den Watchdog ohne sichtbares Fenster (Fensterstil 0 = versteckt).
CreateObject("WScript.Shell").Run "powershell -NoProfile -ExecutionPolicy Bypass -File ""C:\Users\vorau\dev\Spinnenspiel\Tools\autopilot_watchdog.ps1""", 0, False
