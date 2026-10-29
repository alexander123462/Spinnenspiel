"""Schaltet das automatische Eingreifen von Claude bei blockierten Backlog-Punkten in Tools/autopilot_loop2.sh ein.

Alex fuehrt dieses Skript selbst aus (Zustimmung): python Tools\\eskalation_aktivieren.py
Rueckgaengig: git checkout -- Tools/autopilot_loop2.sh   (oder Datei Logs/no_claude anlegen = nur ausschalten)

Was es tut: Nach jeder Hermes-Runde vergleicht die Schleife die blockierten Punkte ("- [!] ID") vor und nach der Runde.
Ist ein Punkt NEU blockiert, ruft sie Claude headless auf (claude -p) mit Tools/overseer_eskalation.md.
Grenzen: hoechstens 4 Eingriffe pro Tag, hoechstens 60 Minuten und 60 Arbeitsschritte je Eingriff,
erlaubt nur Lesen/Schreiben im Projekt, git, python, powershell; nie push nach main, kein rm/del.
"""
import re
import sys

PFAD = 'Tools/autopilot_loop2.sh'

FUNKTION = r'''# Eingreifen von Claude bei neu blockierten Punkten ([!]): hoechstens ESK_MAX pro Tag, Abschalten per Datei Logs/no_claude.
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

'''


def main():
    s = open(PFAD, encoding='utf-8', newline='').read()
    crlf = '\r\n' in s
    s = s.replace('\r\n', '\n')
    if 'eskaliere()' in s:
        print('Schon aktiv, nichts zu tun.')
        return 0
    a = 'mkdir -p Logs\n'
    h = '  "$HERMES" chat --query-file "$ROOT/Logs/autopilot_auftrag.txt"'
    b = '  begrenze_lernen\n'
    for t in (a, h, b):
        if s.count(t) != 1:
            print('Unerwartete Skriptform, Abbruch (nichts geaendert):', repr(t[:40]))
            return 1
    s = s.replace(a, FUNKTION + a, 1)
    s = s.replace(h, "  BLK_VOR=$(grep -o '^- \\[!\\] [A-Za-z0-9.]*' Docs/BACKLOG.md | sort)\n" + h, 1)
    s = s.replace(b, b + '  eskaliere "$BLK_VOR"\n', 1)
    if crlf:
        s = s.replace('\n', '\r\n')
    open(PFAD, 'w', encoding='utf-8', newline='').write(s)
    print('Eingreifen bei blockierten Punkten ist jetzt eingebaut.')
    print('Pruefen: bash -n Tools/autopilot_loop2.sh   |   Ausschalten: Datei Logs/no_claude anlegen')
    return 0


if __name__ == '__main__':
    sys.exit(main())
