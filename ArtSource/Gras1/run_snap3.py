# Selbstkritik-Screenshots: 3 Blickwinkel nacheinander (per MCP ausgefuehrt).
base = 'C:/Users/vorau/dev/Spinnenspiel/'
tag = globals().get('SNAP_TAG', 'd1')
for view in ('front', 'side', 'top'):
    SNAP_OUT = base + 'Logs/modell/Gras1/nachher/' + tag + '_' + view + '.png'
    SNAP_VIEW = view
    exec(open(base + 'ArtSource/Gras1/snap.py', encoding='utf-8').read())
