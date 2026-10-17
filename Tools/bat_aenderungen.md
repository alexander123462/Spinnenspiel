# Aenderungen an den Start-Dateien (17.10.2026) - zum Rueckgaengigmachen

Alle Aenderungen sind direkt in den Originalen gemacht (keine Kopien). Rueckgaengig = die alten Werte unten wieder eintragen.

## Desktop: "ALTERNATIV Qwen3.8-27B + Hermes (beide Karten, NICHT mit 1 und 2).bat" (frueher Qwen3.8.bat)
- BUG: Die Zeilen `-np 4 -c 98304` und `-fa on` hatten kein `^` am Ende. Dadurch endete der Befehl dort, alles danach (-ngl, Tensor-Split, KV-Cache, Spec, Sampling, --jinja, --alias ...) wurde NICHT an llama-server uebergeben.
  - alt: `-np 4 -c 98304` (ohne ^)  -> neu: `-np 1 ^`  (-c 98304 steht schon oben; 1 Slot = voller Kontext fuer Hermes)
  - alt: `-fa on` (ohne ^)           -> neu: `-fa on ^`
- alt: `-ub 256 ^`                   -> neu: `-ub 512 ^`   (schnelleres Prefill)
- alt: `-ts 2.7,1 ^` (doppelt zu --tensor-split) -> entfernt
- alt: `--tensor-split 0.85,0.15 ^` -> neu: `--tensor-split 0.70,0.30 ^`  (5080 war zu 95 % voll, 2070 nur halb)
- unveraendert: `--no-mmproj-offload` (Bilder laufen dadurch auf der CPU; entfernen, wenn VRAM reicht - ungetestet)

## Desktop: "ALTERNATIV Qwen3.8-27B ungefiltert (...).bat"
- neu eingefuegt: `-ub 512 ^` (vor -np 1)

## C:\AI\HermesVision\Start-HermesVision.bat (Vision-KI, 2070)
- alt: `-ub 256` -> neu: `-ub 512`
- alt: `--image-min-tokens 1024` -> neu: `--image-min-tokens 768` (schneller, etwas weniger Bilddetail)

## C:\AI\Strata\strata-coder-iq1_m.json (Coder-KI)
- alt: `--max-context` 131072 -> neu: 90000 (Hermes nutzt 80000; spart VRAM/KV)
- Das IQ2_XS-Modell (strata-qwen-iq2_xs.json) wurde NICHT angefasst.
