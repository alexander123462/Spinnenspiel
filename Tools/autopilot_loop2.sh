#!/bin/bash
# Autopilot-Schleife: ruft Hermes immer wieder mit FRISCHEM Kontext auf, je ein Backlog-Punkt pro Lauf.
# Aufruf (Git Bash):  bash Tools/autopilot_loop.sh [ENDZEIT_HHMM]   z.B. bash Tools/autopilot_loop.sh 1700
# Log: Logs/autopilot_loop.log   Stoppen: Datei Logs/autopilot_stop anlegen oder Fenster schliessen.
set -u
cd "$(dirname "$0")/.."
ROOT=$(pwd -W 2>/dev/null || pwd)
HERMES="$HOME/AppData/Local/hermes/bin/hermes.exe"   # stabiler Pfad (die versionierten installs-Ordner werden von Hermes beim Update geloescht, Overseer 2026-10-25)
PROVIDER="custom:strata_iq3"; MODELL="qwen3.8-flash-next-iq3_s"
ENDE="${1:-}"
LOG=Logs/autopilot_loop.log
# Discord-Meldung, wenn die Schleife nicht weiterarbeiten kann.
# Die Webhook-Adresse steht ABSICHTLICH ausserhalb des Repos, damit sie nie committet wird:
#   C:\Users\vorau\.spinnenspiel_discord_webhook   (eine Zeile, nur die URL)
WEBHOOK_DATEI="$HOME/.spinnenspiel_discord_webhook"
melde() {
  [ -f "$WEBHOOK_DATEI" ] || return 0
  local url text
  url=$(tr -d ' \r\n' < "$WEBHOOK_DATEI")
  [ -n "$url" ] || return 0
  text=$(printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g' | tr '\n' ' ')
  printf '{"content":"%s"}' "$text" | curl -s -m 20 -H "Content-Type: application/json" -d @- "$url" >/dev/null 2>&1
}
# Harte Grenze fuer Docs/LERNEN.md (40 Zeilen, 8000 Zeichen): Hermes entscheidet selbst, was unwichtig ist und vergessen wird.
# Es wird NICHT nach Alter geloescht. Sicherheitskopie vorher in Logs/lernen_backup (wird nie gelesen).
begrenze_lernen() {
  local f=Docs/LERNEN.md n b try
  [ -f "$f" ] || return 0
  for try in 1 2; do
    n=$(grep -c '^- ' $f); b=$(wc -c < $f)
    if [ "$n" -le 40 ] && [ "$b" -le 8000 ]; then return 0; fi
    mkdir -p Logs/lernen_backup
    cp $f "Logs/lernen_backup/LERNEN_$(date +%Y%m%d_%H%M%S).md"
    echo "-- LERNEN.md ueber der Grenze ($n Zeilen, $b Zeichen): Hermes verdichtet, Versuch $try" >> $LOG
    cat > Logs/autopilot_lernen.txt <<LER
Arbeitsordner $ROOT. EINZIGE Aufgabe: Docs/LERNEN.md ist zu lang ($n Zeilen mit "- ", $b Zeichen; Grenze 40 Zeilen und 8000 Zeichen). Entscheide selbst, was am UNWICHTIGSTEN ist, und VERGISS es (Zeilen loeschen). Lege Doppeltes und Aehnliches zu einer Zeile zusammen. Behalte, was einen schon mehrfach aufgetretenen Fehler verhindert und was nicht aus dem Code oder aus AGENTS.md ersichtlich ist. Nichts anderes aendern, nichts committen, keine Tests starten. Wenn die Datei unter der Grenze ist: aufhoeren und in einer Zeile melden, was du vergessen hast.
LER
    "$HERMES" chat --query-file "$ROOT/Logs/autopilot_lernen.txt" --yolo -v \
      --provider "$PROVIDER" -m "$MODELL" --in "$ROOT" --source cli --run-budget 900 \
      2>&1 | tee -a Logs/autopilot_runde.out
  done
  n=$(grep -c '^- ' $f); b=$(wc -c < $f)
  if [ "$n" -gt 40 ] || [ "$b" -gt 8000 ]; then
    echo "-- WARNUNG: LERNEN.md bleibt ueber der Grenze ($n Zeilen, $b Zeichen)" >> $LOG
    melde "Spinnenspiel: LERNEN.md ist zu lang geblieben ($n Zeilen), Hermes konnte nicht genug vergessen."
  else
    git --no-optional-locks add Docs/LERNEN.md >/dev/null 2>&1
    git --no-optional-locks commit -q -m "LERNEN.md verdichtet (Hermes hat Unwichtiges vergessen)" -- Docs/LERNEN.md >/dev/null 2>&1
  fi
}
# Eingreifen von Claude bei neu blockierten Punkten ([!]): hoechstens ESK_MAX pro Tag, Abschalten per Datei Logs/no_claude.
ESK_MAX=4
eskaliere() {
  local vor="$1" nach id n datei
  [ -f Logs/no_claude ] && return 0
  nach=$(grep -o '^- \[!\] [A-Za-z0-9.]*' Docs/BACKLOG.md | sort)
  id=$(comm -13 <(printf '%s\n' "$vor") <(printf '%s\n' "$nach") | head -1 | sed 's/^- \[!\] //')
  [ -n "$id" ] || return 0
  datei="Logs/eskalation_$(date +%F).count"
  n=$(cat "$datei" 2>/dev/null || echo 0)
  if [ "$n" -ge "$ESK_MAX" ]; then
    echo "-- Eskalation fuer $id uebersprungen (Tageslimit $ESK_MAX erreicht)" >> $LOG
    return 0
  fi
  echo $((n+1)) > "$datei"
  echo "-- Eskalation an Claude fuer Punkt $id $(date '+%T')" >> $LOG
  melde "Spinnenspiel: Hermes ist bei Punkt $id blockiert, Claude greift ein ($(date '+%F %T'))."
  timeout 3600 "$HOME/.local/bin/claude" -p "$(cat Tools/overseer_eskalation.md)

Blockierter Punkt: $id" \
    --permission-mode acceptEdits \
    --allowedTools "Read" "Edit" "Write" "Grep" "Glob" "Bash(git:*)" "Bash(python:*)" "Bash(powershell:*)" "Bash(grep:*)" "Bash(ls:*)" "Bash(tasklist:*)" "Bash(date:*)" "Bash(cat:*)" "Bash(wc:*)" "Bash(head:*)" "Bash(tail:*)" \
    --disallowedTools "Bash(git push --force:*)" "Bash(git push origin main:*)" "Bash(rm:*)" "Bash(del:*)" \
    --max-turns 60 \
    >> "Logs/eskalation_$id.log" 2>&1
  echo "-- Eskalation $id beendet exit=$? $(date '+%T')" >> $LOG
}

# Hermes (Python) soll Ausgabe sofort ins Fenster schreiben (sonst gepuffert, Fenster wirkt tot).
export PYTHONUNBUFFERED=1 PYTHONIOENCODING=utf-8
# Rot-Gate: Namen bekannter roter Tests aus Docs/ROT.md (Basislinie) und Docs/ROT_NEU.md (von Hermes gemeldet, unbestaetigt).
rot_namen() {
  grep -h '^- Spinnenspiel\.Tests\.' "$@" 2>/dev/null | sed -E 's/^- (Spinnenspiel\.Tests\.[A-Za-z0-9_.]+).*/\1/' | sort -u
}

mkdir -p Logs
echo "== Start $(date '+%F %T') Endzeit=${ENDE:-keine}" >> $LOG

# Nach einer Pause per Schalter (Marker Logs/autopilot_resume_ok): die unterbrochene Hermes-Sitzung fortsetzen.
RESUME_ID=""
if [ -f Logs/autopilot_resume_ok ]; then
  rm -f Logs/autopilot_resume_ok
  RESUME_ID=$(tr -d '\033' < Logs/autopilot_runde.out 2>/dev/null | grep -a -o '\[[0-9]\{8\}_[0-9]\{6\}_[0-9a-f]\{6\}\]' | tail -1 | tr -d '[]')
  [ -n "$RESUME_ID" ] && echo "-- Setze Hermes-Sitzung $RESUME_ID fort" >> $LOG
fi

RUNDE=100
while :; do
  [ -f Logs/autopilot_stop ] && { echo "== Stopp-Datei gefunden $(date '+%T')" >> $LOG; melde "Spinnenspiel: Autopilot angehalten (Stopp-Datei) um $(date '+%F %T')."; break; }
  [ -n "$ENDE" ] && [ "$(date +%H%M)" -ge "$ENDE" ] && { echo "== Endzeit erreicht $(date '+%T')" >> $LOG; break; }
  # grep -c liefert bei null Treffern Exit 1; das alte "|| echo 0" hat daraus "0\n0" gemacht,
  # der Zahlenvergleich lief auf einen Fehler und die Schleife drehte leer weiter statt zu enden.
  # Strata-Gesundheit: ohne Server (abgestuerzt/pausiert) keine Runde starten, sonst drehen die Runden leer und starten Blender/Unity umsonst (Overseer 2026-10-25).
  if ! curl -s -m 5 http://127.0.0.1:8084/v1/models 2>/dev/null | grep -q '"id"'; then echo "-- Strata (Port 8084) antwortet nicht, warte 120 s $(date '+%T')" >> $LOG; sleep 120; continue; fi
  OFFEN=$(grep -c "^- \[ \]" Docs/BACKLOG.md 2>/dev/null | head -1)
  case "$OFFEN" in ''|*[!0-9]*) OFFEN=0;; esac
  [ "$OFFEN" -eq 0 ] && { echo "== Keine offenen Punkte mehr $(date '+%T')" >> $LOG; melde "Spinnenspiel: Autopilot hat NICHTS MEHR ZU TUN - alle Backlog-Punkte sind erledigt oder blockiert. $(date '+%F %T')"; break; }
  RUNDE=$((RUNDE+1))
  PUNKT=$(grep -m1 "^- \[ \]" Docs/BACKLOG.md | cut -c1-60)
  PUNKTVOLL=$(grep -m1 "^- \[ \]" Docs/BACKLOG.md)
  KUERZEL=$(printf '%s' "$PUNKTVOLL" | sed -E 's/^- \[ \] ([A-Za-z0-9.]+).*/\1/')
  # Rot zuerst: ist ein Test als rot bekannt, wird er vor dem naechsten Backlog-Punkt repariert (hoechstens 2 Versuche je Test).
  ROT_VOR=$(rot_namen Docs/ROT.md)
  ROTTEST=""
  for t in $( [ -z "$RESUME_ID" ] && rot_namen Docs/ROT.md Docs/ROT_NEU.md ); do
    n=$(cat "Logs/rot_versuche_$t.count" 2>/dev/null || echo 0)
    if [ "$n" -lt 2 ]; then ROTTEST="$t"; echo $((n+1)) > "Logs/rot_versuche_$t.count"; break; fi
  done
  if [ -n "$ROTTEST" ]; then
    PUNKT="ROT $ROTTEST"; KUERZEL="ROT"
    PUNKTVOLL="- [ ] ROT Behebe den roten Test $ROTTEST. Er steht in Docs/ROT.md oder Docs/ROT_NEU.md. Dies ist KEIN Backlog-Punkt: haenge nichts im Backlog ab. Finde die Ursache (Testcode, Logs/ci/*-tests.log, frueherer Commit per git log -S oder git log -5 --stat), behebe sie im SPIELCODE. Den Test nur aendern, wenn seine Annahme nachweislich falsch ist, und das in Docs/BERICHT.md begruenden. Kriterium: der Test ist gruen, der volle CI-Lauf hat keinen NEUEN roten Test, und sein Eintrag ist aus Docs/ROT.md bzw. Docs/ROT_NEU.md entfernt (im selben Commit). Danach aufhoeren."
    echo "-- ROT-Reparatur: $ROTTEST" >> $LOG
  fi
  echo "-- Runde $RUNDE $(date '+%T') offen=$OFFEN -> $PUNKT" >> $LOG

  cat > Logs/autopilot_auftrag.txt <<AUF
Arbeitsordner $ROOT, Branch hdrp. Lies zuerst vollstaendig: .hermes.md und AGENTS.md im Projektordner.

ZUERST: git --no-optional-locks status --short ansehen. Geaenderte oder neue Dateien unter Assets/_Project/Scripts oder Assets/_Project/Tests stammen aus einer frueheren Runde DIESER Schleife, nicht von einem anderen Agenten. Behandle sie so:
- Gehoeren sie zum obersten offenen Punkt: diese Arbeit fertigstellen (Kompilierfehler beheben) und mit committen.
- Gehoeren sie zu etwas anderem: mit git checkout -- <datei> verwerfen bzw. neue Dateien loeschen.
Geaenderte .mat/.asset/.unity-Dateien sind Unity-Rauschen und bleiben unangetastet.
Erfinde nie eine andere Sitzung oder einen anderen Agenten als Ursache und markiere deswegen nichts als blockiert.

Erledige dann GENAU EINEN Punkt, naemlich diesen hier (woertlich aus Docs/BACKLOG.md, Zeilenanfang ohne Einrueckung):

$PUNKTVOLL

Such dir KEINEN anderen Punkt aus. Eingerueckte Unterpunkte ("  - [ ]") gehoeren zu einem blockierten Oberpunkt und sind NICHT dein Auftrag. Die zugehoerigen Unterschritte dieses Punktes stehen direkt darunter in der Datei - lies sie dort nach.

Vorgehen: in Schritte zerlegen; Code nur in Stuecken (max. ca. 60 Zeilen pro Schreibvorgang); bestehende Dateien nur per Patch aendern und vorher lesen; zu neuer Logik gehoert ein Test. WAEHREND der Entwicklung nur die betroffenen Tests laufen lassen (schneller): powershell -ExecutionPolicy Bypass -File $ROOT/Tools/ci.ps1 -Name r$RUNDE-f1 -Method Spinnenspiel.EditorTools.UrwaldSceneBuilder.Build -Tests -Filter "Spinnenspiel.Tests.<Klasse>.<Testname>*" (Namen -f2, -f3 fuer weitere Versuche). Erst wenn die gezielten Tests gruen sind, EINMAL der volle CI-Lauf vor dem Commit (ohne -Filter), nie mehr als zweimal voll pro Punkt:
powershell -ExecutionPolicy Bypass -File $ROOT/Tools/ci.ps1 -Name r$RUNDE -Method Spinnenspiel.EditorTools.UrwaldSceneBuilder.Build -Tests
Ergebnis aus Logs/ci/r$RUNDE-summary.txt lesen. Commit-Gate: Lies Docs/ROT.md. Committen, wenn der Test deines Punktes gruen ist und JEDER fehlgeschlagene Test in Docs/ROT.md steht (diese Fehlschlaege zaehlen NICHT als Fehlversuch und sind KEIN Grund, eigene Aenderungen zurueckzunehmen). Steht ein Fehlschlag NICHT in Docs/ROT.md, ist er deine Regression - dann nicht committen. Du darfst Docs/ROT.md NIE erweitern und Docs/ROT_NEU.md zaehlt nicht als Entschuldigung: ein neuer roter Test ist deine Regression, behebe ihn. Gelingt das in 3 Versuchen nicht, markiere den Punkt als [!] mit den gemessenen Werten (Claude greift dann ein). Nur die eigenen Dateien committen (nie git add -A). NEU: nach einem erfolgreichen Commit zusaetzlich "git push origin hdrp" ausfuehren, damit Alex den Fortschritt am Server sieht. Nie nach main pushen. Schlaegt der Push fehl, den Punkt trotzdem als [x] stehen lassen, den Fehler in einer Zeile melden und NICHT wiederholen. Danach Punkt in Docs/BACKLOG.md auf [x] setzen (mit Commit-Hash), eine Zeile in Docs/BERICHT.md anhaengen.

Roter Test deines Punktes: Pruefe ZUERST, ob die Annahme des Tests stimmt (Testaufbau, Vorbedingungen, Zaehler, Zeitfenster; vergleiche mit einem aehnlichen gruenen Test in derselben Datei), bevor du den Spielcode umbaust. Meist liegt der Fehler im Testaufbau. Lies bei jedem Fehlschlag die konkrete Meldung in Logs/ci/r$RUNDE-tests.log.

Beweisbild: Aendert dein Punkt etwas Sichtbares, erzeuge ein Bild (Screenshot-Test legt PNG unter Logs/ci ab), lass es mit python Tools/bild_fragen.py <png> beschreiben, trage die Beschreibung in Docs/BERICHT.md ein und nenne in der Discord-Nachricht den Bildpfad. Ohne Bildkontrolle nicht "fertig" melden bei sichtbaren Aenderungen.

Regeln: Unity muss geschlossen sein (tasklist | grep -i Unity.exe) - laeuft Unity, Punkt als "[!] Unity offen" markieren und aufhoeren. Maximal 3 Versuche; danach Punkt als "[!]" mit Grund markieren und aufhoeren. Eigene Aenderungen dabei NICHT zuruecknehmen, sondern im Arbeitsbaum lassen und im Backlog-Eintrag die geaenderten Dateien namentlich auflisten. Der volle CI-Lauf dauert laenger als 10 Minuten (Vordergrund-Grenze 600 s): starte ihn mit terminal(background=true, notify_on_complete=true) und warte darauf mit process wait (timeout bis 1500) - das ist AUSDRUECKLICH erlaubt und der vorgesehene Weg. Starte ihn nicht zweimal. Sonst keine Hintergrundprozesse, auf deren Ende du wartest. Geaenderte .mat/.asset-Dateien im Arbeitsbaum sind Unity-Rauschen und bleiben unangetastet. Nie mit einer Frage aufhoeren.

Wenn du einen Punkt als [!] markierst, also nicht weiterkommst: schreibe ZUSAETZLICH eine kurze Nachricht auf Discord, auf Deutsch, hoechstens 3 Zeilen - welcher Punkt, woran es scheitert, was du schon versucht hast. Du bist mit Discord verbunden und weisst, wie das geht. Auch bei JEDEM erfolgreich abgeschlossenen Punkt (Commit) schreibst du auf Discord, auf Deutsch, in 2 bis 4 kurzen Saetzen und so, dass ein Laie es versteht: was jetzt im Spiel anders ist (z. B. 'Die Spinne hat jetzt eine Energieanzeige'), ob die Tests gruen sind (nur 'alle Tests bestanden' oder 'X Tests rot'), und welcher Punkt als naechstes kommt. Keine Dateinamen, keine Commit-Hashes, kein Fachjargon.

Wenn der Punkt fertig (committet) oder als [!] markiert ist: sofort aufhoeren und in maximal 5 Zeilen melden: Punkt, Ergebnis, Commit-Hash, naechster offener Punkt.
AUF

  BLK_VOR=$(grep -o '^- \[!\] [A-Za-z0-9.]*' Docs/BACKLOG.md | sort)
  QF="$ROOT/Logs/autopilot_auftrag.txt"; RES_ARGS=""
  if [ -n "$RESUME_ID" ]; then
    printf '%s\n' "Du wurdest unterbrochen (Pause am PC). Mach GENAU dort weiter, wo du aufgehoert hast. Pruefe zuerst kurz git status, damit du siehst, was schon geaendert ist. Der Auftrag und alle Regeln bleiben unveraendert (siehe oben). Wenn du den Punkt fertig hast: committen, pushen, Backlog-Punkt abhaken, aufhoeren." > "$ROOT/Logs/autopilot_weiter.txt"
    QF="$ROOT/Logs/autopilot_weiter.txt"; RES_ARGS="--resume $RESUME_ID"; RESUME_ID=""
  fi
  # Hermes schreibt in eine Datei (nicht in eine Pipe): MCP-Kindprozesse (blender-mcp/uv) erben sonst die Pipe, halten tee offen
  # und die Schleife haengt nach dem Rundenende (Overseer 2026-10-25, MODELL1).
  HLOG="Logs/hermes_runde.tmp"; : > "$HLOG"
  "$HERMES" chat --query-file "$QF" $RES_ARGS --yolo -v \
    --provider "$PROVIDER" -m "$MODELL" --in "$ROOT" --source cli --run-budget 7200 \
    > "$HLOG" 2>&1 &
  HPID=$!
  tail -n +1 -f "$HLOG" 2>/dev/null | tee -a Logs/autopilot_runde.out &
  wait $HPID; HEX=$?
  sleep 3; pkill -f "tail -n +1 -f $HLOG" 2>/dev/null; taskkill //F //IM blender-mcp.exe >/dev/null 2>&1
  # K2a: Unity-Rauschen (.mat/.asset/.unity, only fileID restamps) auto-reset after each round.
  powershell -ExecutionPolicy Bypass -File "$ROOT/Tools/reset_unity_rauschen.ps1" >> Logs/autopilot_rausch.out 2>&1
  eskaliere "$BLK_VOR"
  echo "-- Runde $RUNDE Ende $(date '+%T') exit=$HEX HEAD=$(git --no-optional-locks log -1 --format=%h 2>/dev/null)" >> $LOG
  if grep -q "^- \[!\] $KUERZEL " Docs/BACKLOG.md 2>/dev/null; then
    melde "Spinnenspiel: Hermes kommt bei Punkt $KUERZEL nicht weiter und hat ihn als blockiert markiert ($(date '+%F %T')). Grund steht in Docs/BACKLOG.md. Die Schleife macht mit dem naechsten Punkt weiter."
  fi
  # Rot-Gate nach der Runde: neue Eintraege in Docs/ROT.md gehoeren Claude, nicht Hermes -> nach Docs/ROT_NEU.md verschieben.
  VERSCHOBEN=""
  for t in $(comm -13 <(printf '%s\n' "$ROT_VOR") <(rot_namen Docs/ROT.md)); do
    [ -f Docs/ROT_NEU.md ] || printf '%s\n\n' '# Von Hermes gemeldete rote Tests (unbestaetigt). Werden vor neuen Punkten repariert. Nur Claude nimmt sie in die Basislinie Docs/ROT.md auf.' > Docs/ROT_NEU.md
    grep -F -- "- $t" Docs/ROT.md >> Docs/ROT_NEU.md
    grep -v -F -- "- $t" Docs/ROT.md > Docs/ROT.md.tmp && mv Docs/ROT.md.tmp Docs/ROT.md
    VERSCHOBEN="$VERSCHOBEN $t"
  done
  if [ -n "$VERSCHOBEN" ]; then
    echo "-- ROT.md-Erweiterung zurueckgenommen:$VERSCHOBEN" >> $LOG
    melde "Spinnenspiel: Hermes wollte neue rote Tests in die Basislinie schreiben:$VERSCHOBEN. Verschoben nach Docs/ROT_NEU.md, werden zuerst repariert."
    git --no-optional-locks add Docs/ROT.md Docs/ROT_NEU.md >/dev/null 2>&1
    git --no-optional-locks commit -q -m "ROT.md: neue Eintraege von Hermes nach ROT_NEU.md verschoben (nur Claude erweitert die Basislinie)" -- Docs/ROT.md Docs/ROT_NEU.md >/dev/null 2>&1
  fi
  for t in $(rot_namen Docs/ROT.md Docs/ROT_NEU.md); do
    n=$(cat "Logs/rot_versuche_$t.count" 2>/dev/null || echo 0)
    if [ "$n" -ge 2 ] && ! grep -q -F "$t" Logs/rot_uebersprungen.txt 2>/dev/null; then
      echo "$t" >> Logs/rot_uebersprungen.txt
      melde "Spinnenspiel: Roter Test $t konnte Hermes in 2 Versuchen nicht beheben, uebersprungen (Overseer kuemmert sich)."
    fi
  done
  sleep 10
done
echo "== Schleife beendet $(date '+%F %T')" >> $LOG
melde "Spinnenspiel: Autopilot-Schleife beendet $(date '+%F %T')."
