# Laedt Geo- und Main-Helfer in den aktuellen Namensraum (pro MCP-Aufruf noetig).
import bpy, os
import numpy
ROOT = r'C:\Users\vorau\dev\Spinnenspiel'
GEO = os.path.join(ROOT, 'ArtSource', 'Riesenblatt', 'riesenblatt1_geometrie.py')
MAIN = os.path.join(ROOT, 'ArtSource', 'Riesenblatt', 'riesenblatt1_main.py')
exec(open(GEO, encoding="utf-8").read(), globals())
mc = open(MAIN, encoding='utf-8').read()
# Pfad-Konstanten (vor make_object definiert) mitladen.
head = mc[:mc.index('def make_object')]
code = head + mc[mc.index('def make_object'):mc.index('def export_fbx')]
exec(code, globals())
