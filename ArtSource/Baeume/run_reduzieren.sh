#!/bin/bash
# Reduziert alle Poly-Haven-Baeume (Git Bash). Log: ArtSource/Baeume/reduzieren.log
cd "$(dirname "$0")/../.."
BL="C:/Program Files/Blender Foundation/Blender 5.1/blender.exe"
: > ArtSource/Baeume/reduzieren.log
for id in ${@:-island_tree_02 island_tree_01 island_tree_03 jacaranda_tree tree_small_02}; do
  out=Assets/_Project/Art/Plants/PH_Baeume/$id; mkdir -p $out
  echo "=== $id" >> ArtSource/Baeume/reduzieren.log
  "$BL" -b -P ArtSource/Baeume/ph_baum_reduzieren.py -- AssetSource/polyhaven/models/$id/${id}_2k.fbx $id $out 2>&1 \
    | grep -E "Objekte|LOD|->|DEBUG|Error|Traceback|File \"|line [0-9]|fertig" >> ArtSource/Baeume/reduzieren.log
done
echo "=== ENDE" >> ArtSource/Baeume/reduzieren.log
