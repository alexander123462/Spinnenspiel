#!/bin/bash
# Erzeugt die 1k-Baumtexturen (Git Bash). Log: ArtSource/Baeume/texturen.log
cd "$(dirname "$0")/../.."
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b -P ArtSource/Baeume/ph_baum_texturen.py -- AssetSource/ph_baeume_2k Assets/_Project/Art/Plants/PH_Baeume 2>&1 \
  | grep -E "Albedo|Error|Traceback|File \"|line [0-9]|fertig" > ArtSource/Baeume/texturen.log
