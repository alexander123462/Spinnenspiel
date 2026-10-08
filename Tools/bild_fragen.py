# -*- coding: utf-8 -*-
"""Fragt das lokale Vision-Modell (Gemma 4 E4B, RTX 2070, Port 8082) zu einem Bild.

Aufruf:  python Tools/bild_fragen.py <bild.png> "<frage>"
Startet den Vision-Server selbst, wenn er nicht laeuft (Desktop\\Vision-2070.bat).
Gibt nur die Antwort des Modells auf stdout aus. Rueckgabe 0 = ok.
"""
import base64, json, os, subprocess, sys, time, urllib.error, urllib.request

PORT = 8082
BASE = "http://127.0.0.1:%d/v1" % PORT
BAT = r"C:\Users\vorau\Desktop\Vision-2070.bat"
STANDARDFRAGE = "Beschreibe sachlich, was auf dem Bild zu sehen ist. Nenne Auffaelligkeiten wie fehlende Teile, Luecken, schwarze Flaechen oder Fehler."


def laeuft():
    try:
        urllib.request.urlopen(BASE + "/models", timeout=3).read()
        return True
    except Exception:
        return False


def starten():
    sys.stderr.write("[bild_fragen] Vision-Server laeuft nicht, starte %s ...\n" % BAT)
    if not os.path.exists(BAT):
        sys.stderr.write("[bild_fragen] FEHLER: %s fehlt.\n" % BAT)
        return False
    subprocess.Popen(["cmd", "/c", "start", "", "/min", BAT],
                     shell=False, close_fds=True)
    for _ in range(72):          # bis zu 6 Minuten (Modell laden)
        time.sleep(5)
        if laeuft():
            sys.stderr.write("[bild_fragen] Vision-Server bereit.\n")
            return True
    return False


def fragen(bild, frage):
    mime = "image/jpeg" if bild.lower().endswith((".jpg", ".jpeg")) else "image/png"
    with open(bild, "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    payload = {
        "model": "vision",
        "temperature": 0.2,
        "max_tokens": 400,
        "messages": [{"role": "user", "content": [
            {"type": "text", "text": frage},
            {"type": "image_url", "image_url": {"url": "data:%s;base64,%s" % (mime, b64)}},
        ]}],
    }
    req = urllib.request.Request(BASE + "/chat/completions",
                                 data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        d = json.loads(r.read().decode("utf-8"))
    return d["choices"][0]["message"]["content"].strip()


def main():
    if len(sys.argv) < 2:
        sys.stderr.write("Aufruf: python Tools/bild_fragen.py <bild> \"<frage>\"\n")
        return 1
    bild = sys.argv[1]
    frage = sys.argv[2] if len(sys.argv) > 2 else STANDARDFRAGE
    if not os.path.isfile(bild):
        sys.stderr.write("FEHLER: Bild nicht gefunden: %s\n" % bild)
        return 1
    if not laeuft() and not starten():
        sys.stderr.write("FEHLER: Vision-Server auf Port %d nicht erreichbar.\n" % PORT)
        return 2
    try:
        print(fragen(bild, frage))
    except Exception as e:
        sys.stderr.write("FEHLER bei der Anfrage: %s\n" % e)
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
