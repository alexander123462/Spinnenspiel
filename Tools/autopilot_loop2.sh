#!/bin/bash
# Autopilot-Schleife: ruft Hermes immer wieder mit FRISCHEM Kontext auf, je ein Backlog-Punkt pro Lauf.
# Aufruf (Git Bash):  bash Tools/autopilot_loop.sh [ENDZEIT_HHMM]   z.B. bash Tools/autopilot_loop.sh 1700
# Log: Logs/autopilot_loop.log   Stoppen: Datei Logs/autopilot_stop anlegen oder Fenster schliessen.
set -u
cd "$(dirname "$0")/.."
ROOT=$(pwd -W 2>/dev/null || pwd)
HERMES="$HOME/AppData/Local/hermes/bin/hermes.exe"
PROVIDER="custom:strata"; MODELL="qwen3.8-flash-next-coder-iq1_m"
ENDE="${1:-}"
LOG=Logs/autopilot_loop.log
mkdir -p Logs
echo "== Start $(date '+%F %T') Endzeit=${ENDE:-keine}" >> $LOG

RUNDE=100
while :; do
  [ -f Logs/autopilot_stop ] && { echo "== Stopp-Datei gefunden $(date '+%T')" >> $LOG; break; }
  [ -n "$ENDE" ] && [ "$(date +%H%M)" -ge "$ENDE" ] && { echo "== Endzeit erreicht $(date '+%T')" >> $LOG; break; }
  OFFEN=$(grep -c "^- \[ \]" Docs/BACKLOG.md 2>/dev/null || echo 0)
  [ "$OFFEN" -eq 0 ] && { echo "== Keine offenen Punkte mehr $(date '+%T')" >> $LOG; break; }
  RUNDE=$((RUNDE+1))
  PUNKT=$(grep -m1 "^- \[ \]" Docs/BACKLOG.md | cut -c1-60)
  echo "-- Runde $RUNDE $(date '+%T') offen=$OFFEN -> $PUNKT" >> $LOG

  cat > Logs/autopilot_auftrag.txt <<AUF
Arbeitsordner $ROOT, Branch hdrp. Lies zuerst vollstaendig: C:/Users/vorau/Desktop/Spinnenspiel-Autopilot.md (Abschnitte 1, 3, 4) und Docs/LERNEN.md.

ZUERST: git --no-optional-locks status --short ansehen. Geaenderte oder neue Dateien unter Assets/_Project/Scripts oder Assets/_Project/Tests stammen aus einer frueheren Runde DIESER Schleife, nicht von einem anderen Agenten. Behandle sie so:
- Gehoeren sie zum obersten offenen Punkt: diese Arbeit fertigstellen (Kompilierfehler beheben) und mit committen.
- Gehoeren sie zu etwas anderem: mit git checkout -- <datei> verwerfen bzw. neue Dateien loeschen.
Geaenderte .mat/.asset/.unity-Dateien sind Unity-Rauschen und bleiben unangetastet.
Erfinde nie eine andere Sitzung oder einen anderen Agenten als Ursache und markiere deswegen nichts als blockiert.

Erledige dann GENAU EINEN Punkt: den obersten noch offenen Punkt ("- [ ]") in Docs/BACKLOG.md. Nicht mehrere, nicht weniger.

Vorgehen: in Schritte zerlegen; Code nur in Stuecken (max. ca. 60 Zeilen pro Schreibvorgang); bestehende Dateien nur per Patch aendern und vorher lesen; zu neuer Logik gehoert ein Test; dann voller CI-Lauf:
powershell -ExecutionPolicy Bypass -File $ROOT/Tools/ci.ps1 -Name r$RUNDE -Method Spinnenspiel.EditorTools.UrwaldSceneBuilder.Build -Tests
Ergebnis aus Logs/ci/r$RUNDE-summary.txt lesen. Nur bei "fehlgeschlagen 0" committen, und nur die eigenen Dateien (nie git add -A, kein push). Danach Punkt in Docs/BACKLOG.md auf [x] setzen (mit Commit-Hash), eine Zeile in Docs/BERICHT.md anhaengen und bis zu 3 Lehren in Docs/LERNEN.md.

Regeln: Unity muss geschlossen sein (tasklist | grep -i Unity.exe) - laeuft Unity, Punkt als "[!] Unity offen" markieren und aufhoeren. Maximal 3 Versuche; danach eigene Aenderungen dieses Punkts mit git checkout -- <datei> zuruecknehmen, Punkt als "[!]" mit Grund markieren und aufhoeren. Keine Hintergrundprozesse starten, auf deren Ende du wartest. Geaenderte .mat/.asset-Dateien im Arbeitsbaum sind Unity-Rauschen und bleiben unangetastet. Nie mit einer Frage aufhoeren.

Wenn der Punkt fertig (committet) oder als [!] markiert ist: sofort aufhoeren und in maximal 5 Zeilen melden: Punkt, Ergebnis, Commit-Hash, naechster offener Punkt.
AUF

  "$HERMES" chat --query-file "$ROOT/Logs/autopilot_auftrag.txt" -Q --yolo \
    --provider "$PROVIDER" -m "$MODELL" --in "$ROOT" --source tool --run-budget 2400 \
    >> Logs/autopilot_runde.out 2>&1
  echo "-- Runde $RUNDE Ende $(date '+%T') exit=$? HEAD=$(git --no-optional-locks log -1 --format=%h 2>/dev/null)" >> $LOG
  sleep 10
done
echo "== Schleife beendet $(date '+%F %T')" >> $LOG
