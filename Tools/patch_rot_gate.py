"""Baut das verschaerfte Rot-Gate in Tools/autopilot_loop2.sh.neu ein (greift nach dem naechsten Neustart der Schleife).

1) Hermes darf Docs/ROT.md nicht erweitern: neue Eintraege werden nach Docs/ROT_NEU.md verschoben (Basislinie erweitert nur Claude).
2) Rot zuerst: stehen Tests in Docs/ROT.md oder Docs/ROT_NEU.md, ist die naechste Runde die Reparatur des ersten (hoechstens 2 Versuche je Test,
   danach landet er in Logs/rot_uebersprungen.txt und der Overseer kuemmert sich).
"""
import shutil
import sys

SRC = 'Tools/autopilot_loop2.sh'
DST = 'Tools/autopilot_loop2.sh.neu'

s = open(SRC, encoding='utf-8', newline='').read()
crlf = '\r\n' in s
s = s.replace('\r\n', '\n')
if 'ROT_VOR=' in s:
    print('Schon eingebaut.')
    sys.exit(0)

# --- Hilfsfunktionen vor "mkdir -p Logs" ---
FUNK = r'''# Rot-Gate: Namen bekannter roter Tests aus Docs/ROT.md (Basislinie) und Docs/ROT_NEU.md (von Hermes gemeldet, unbestaetigt).
rot_namen() {
  grep -h '^- Spinnenspiel\.Tests\.' "$@" 2>/dev/null | sed -E 's/^- (Spinnenspiel\.Tests\.[A-Za-z0-9_.]+).*/\1/' | sort -u
}

'''
a = 'mkdir -p Logs\n'
assert s.count(a) == 1
s = s.replace(a, FUNK + a, 1)

# --- Rot zuerst: nach der Zeile mit KUERZEL= ---
k = [l for l in s.split('\n') if l.startswith('  KUERZEL=')]
assert len(k) == 1
ROTZUERST = r'''  # Rot zuerst: ist ein Test als rot bekannt, wird er vor dem naechsten Backlog-Punkt repariert (hoechstens 2 Versuche je Test).
  ROT_VOR=$(rot_namen Docs/ROT.md)
  ROTTEST=""
  for t in $(rot_namen Docs/ROT.md Docs/ROT_NEU.md); do
    n=$(cat "Logs/rot_versuche_$t.count" 2>/dev/null || echo 0)
    if [ "$n" -lt 2 ]; then ROTTEST="$t"; echo $((n+1)) > "Logs/rot_versuche_$t.count"; break; fi
  done
  if [ -n "$ROTTEST" ]; then
    PUNKT="ROT $ROTTEST"; KUERZEL="ROT"
    PUNKTVOLL="- [ ] ROT Behebe den roten Test $ROTTEST. Er steht in Docs/ROT.md oder Docs/ROT_NEU.md. Dies ist KEIN Backlog-Punkt: haenge nichts im Backlog ab. Finde die Ursache (Testcode, Logs/ci/*-tests.log, frueherer Commit per git log -S oder git log -5 --stat), behebe sie im SPIELCODE. Den Test nur aendern, wenn seine Annahme nachweislich falsch ist, und das in Docs/BERICHT.md begruenden. Kriterium: der Test ist gruen, der volle CI-Lauf hat keinen NEUEN roten Test, und sein Eintrag ist aus Docs/ROT.md bzw. Docs/ROT_NEU.md entfernt (im selben Commit). Danach aufhoeren."
    echo "-- ROT-Reparatur: $ROTTEST" >> $LOG
  fi
'''
idx = s.index(k[0])
end = s.index('\n', idx) + 1
# PUNKTVOLL wird vor KUERZEL gesetzt; der Rot-Block muss NACH beiden stehen
s = s[:end] + ROTZUERST + s[end:]

# --- Gate-Satz im Auftrag verschaerfen ---
old = 'Steht ein Fehlschlag NICHT in Docs/ROT.md, ist er deine Regression - dann nicht committen.'
assert old in s
new = old + ' Du darfst Docs/ROT.md NIE erweitern und Docs/ROT_NEU.md zaehlt nicht als Entschuldigung: ein neuer roter Test ist deine Regression, behebe ihn. Gelingt das in 3 Versuchen nicht, markiere den Punkt als [!] mit den gemessenen Werten (Claude greift dann ein).'
s = s.replace(old, new, 1)

# --- Nach der Runde: Erweiterungen verschieben, uebersprungene melden (vor "sleep 10") ---
NACH = r'''  # Rot-Gate nach der Runde: neue Eintraege in Docs/ROT.md gehoeren Claude, nicht Hermes -> nach Docs/ROT_NEU.md verschieben.
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
'''
b = '  sleep 10\n'
assert s.count(b) == 1
s = s.replace(b, NACH + b, 1)

open(DST, 'w', encoding='utf-8', newline='').write(s.replace('\n', '\r\n') if crlf else s)
print('Gebaut:', DST)
