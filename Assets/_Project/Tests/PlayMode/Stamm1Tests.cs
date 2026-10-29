using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// STAMM1: Mammutbaum-Rinde fein geschliffen. Der Stamm (RedwoodFactory.Trunk, T_RedwoodBark)
    /// soll NICHT mehr wie ein Vorhang wirken: unregelmaessiger Furchenabstand (zweite Frequenz
    /// 1.7x, Phase pro Furche variiert), flache Platten statt reiner Sinusstreifen, ein Viertel der
    /// Faserstreifen endet oder verzweigt, Moosfarbe nur im untersten Meter, Farbton 10..25 Grad.
    /// Mesh-/UV-Regeln werden direkt an RedwoodFactory gemessen, die Textur-Regeln an den
    /// gebauten PNGs (T_RedwoodBark, T_RedwoodBark_N), zusaetzlich ein Detail-Foto.
    /// </summary>
    public class Stamm1Tests
    {
        private const string TexDir = "Assets/_Project/Art/Generated/Textures";

        // ---------- Hilfstexte ----------

        /// <summary>Farbton (Grad) eines Pixels, -1 wenn nahezu unbunt (S < 0.08).</summary>
        private static float Hue360(Color32 c)
        {
            Color rgb = c;
            Color.RGBToHSV(rgb, out float h, out float s, out float v);
            return s < 0.08f ? -1f : h * 360f;
        }

        private static bool IsMoss(float hue)
        {
            // Moos und die Uebergangspixels zwischen Rinde und Moos (hue 32..175) zaehlen nicht
            // als Rinde: eine Mischung aus brauner Rinde und gruenem Moos hat naturgemaess einen
            // Farbton dazwischen und darf nicht als Farbton-Fehler der Rinde verbucht werden.
            return hue >= 32f && hue <= 175f;
        }

        /// <summary>Liest eine gebaute Textur-PNG ein (Importer ist nicht lesbar -> eigene Kopie).</summary>
        private static Color32[] ReadPng(string file, out int size)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), file);
            Assert.IsTrue(File.Exists(path), "Textur fehlt: " + path);
            byte[] raw = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(ImageConversion.LoadImage(tex, raw), "PNG konnte nicht geladen werden: " + file);
            size = tex.width;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        // ---------- Test 1: Stammrillen unregelmaessig + flache Platten ----------

        [Test]
        public void Stamm1_TrunkFluteIrregularAndPlated()
        {
            const int samples = 720;
            var r = new System.Random(7);
            for (int t = 0; t < 3; t++)
            {
                float phase = (float)r.NextDouble() * 10f;
                float y = 6f + t * 9f;

                var val = new float[samples];
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < samples; i++)
                {
                    val[i] = RedwoodFactory.TrunkFlute(i / (float)samples * Mathf.PI * 2f, y, phase);
                    if (val[i] < min) min = val[i];
                    if (val[i] > max) max = val[i];
                }

                // Amplitude: flaches Rindenrelief, kein starker Saegezahn.
                Assert.Greater(max - min, 0.02f, "Rillen zu flach (Stamm zu glatt).");
                Assert.LessOrEqual(max - min, 0.12f, "Rillen zu tief (Stamm zu grob).");

                // Furchen = lokale Maxima der GEGLAETTETEN Kurve mit klarer Prominenz. Rohe
                // Maxima auf den flachen Platten zaehlen auch Rundungsrauschen (r103-f4: 44
                // "Furchen" bei y=15, gemessene 26). Geglaettet ueber 7 Samples, eine Furche
                // muss 20 % des Umfangs ueber dem tiefsten Punkt im Umkreis von 25 Samples liegen.
                var sm = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float ssum = 0f;
                    for (int k = -3; k <= 3; k++) ssum += val[(i + k + samples) % samples];
                    sm[i] = ssum / 7f;
                }
                float smin = float.MaxValue, smax = float.MinValue;
                for (int i = 0; i < samples; i++)
                {
                    if (sm[i] < smin) smin = sm[i];
                    if (sm[i] > smax) smax = sm[i];
                }
                float prom = 0.2f * (smax - smin);
                int peaks = 0;
                for (int i = 0; i < samples; i++)
                {
                    if (!(sm[i] >= sm[(i - 1 + samples) % samples] && sm[i] > sm[(i + 1) % samples])) continue;
                    float low = float.MaxValue;
                    for (int k = -25; k <= 25; k++)
                    {
                        float w = sm[(i + k + samples) % samples];
                        if (w < low) low = w;
                    }
                    if (sm[i] - low >= prom) peaks++;
                }
                // Messlatte STAMM1 (gleiche Messung): alt (Sinus 5 + Sinus 11) = 10..11 Furchen,
                // neu (Platten + 1.7x + Phasen/Positionssprung) = 20..22.
                Assert.GreaterOrEqual(peaks, 16, "zu wenige Furchen am Stamm (Vorhang-Look).");
                Assert.LessOrEqual(peaks, 48, "zu viele Furchen am Stamm.");

                // Plateau-Anteil: wie viel vom Umfang liegt nahe am Max (= flache Platte).
                int flat = 0;
                float band = min + (max - min) * 0.72f;
                for (int i = 0; i < samples; i++) if (val[i] >= band) flat++;
                float plateShare = flat / (float)samples;
                Debug.Log(string.Format("[STAMM1-Flute] y={0} phase={1:0.0} Amp={2:0.000} Furchen={3} Plateau={4:P0}",
                    y, phase, max - min, peaks, plateShare));
                Assert.GreaterOrEqual(plateShare, 0.55f, "keine flachen Platten (Sinusstreifen, Plateau-Anteil zu klein)");
            }
        }

        // ---------- Test 2: Mesh benutzt die Rillen-Funktion, Radius bleibt im Band ----------

        [Test]
        public void Stamm1_TrunkMeshUsesFlute()
        {
            const int seed = 4242, sides = RedwoodFactory.Sides;
            float height = 55f, baseR = 1.1f, topR = 0.45f;
            Mesh m = RedwoodFactory.Trunk(seed, height, baseR, topR);
            var vs = m.vertices;
            int rings = (int)Mathf.Ceil(height / 2f) + 1;
            Assert.AreEqual((rings + 1) * (sides + 1), vs.Length, "Mesh-Ringe zahlen nicht wie erwartet.");

            // Ein Ring in der Mitte: Vertexradius == RadiusAt * TrunkFlute (gleiche Phase wie im Mesh).
            var rnd = new System.Random(seed);
            rnd.NextDouble(); // phase
            float lean = ((float)rnd.NextDouble() - 0.5f) * 0.02f;
            int ring = rings / 2;
            float y = -4f + (height + 4f) * ring / rings;
            float rad = RedwoodFactory.RadiusAt(y, height, baseR, topR);
            float cx = lean * y * 10f, cz = lean * y * 7f;
            float maxDev = 0f;
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                Vector3 v = vs[ring * (sides + 1) + s];
                float got = new Vector2(v.x - cx, v.z - cz).magnitude;
                float want = rad * RedwoodFactory.TrunkFlute(a, y, PhaseOf(seed));
                maxDev = Mathf.Max(maxDev, Mathf.Abs(got - want));
            }
            Debug.Log(string.Format("[STAMM1-Mesh] y={0:0.0} Radius={1:0.00} maxAbweichung={2:0.0000}", y, rad, maxDev));
            Assert.Less(maxDev, 0.005f, "Mesh-Rille stimmt nicht mit TrunkFlute ueberein.");

            // Radiusverlauf: jeder Vertex-Radius folgt RadiusAt*TrunkFlute -> bleibt im Band.
            float phase = PhaseOf(seed);
            float worstRel = 0f;
            foreach (var v in vs)
            {
                if (v.y < 0f || v.y > height) continue;
                float ax = lean * v.y * 10f, az = lean * v.y * 7f;
                float dx = v.x - ax, dz = v.z - az;
                float got = Mathf.Sqrt(dx * dx + dz * dz);
                float a = Mathf.Atan2(dz, dx);
                float want = RedwoodFactory.RadiusAt(v.y, height, baseR, topR)
                           * RedwoodFactory.TrunkFlute(a, v.y, phase);
                worstRel = Mathf.Max(worstRel, Mathf.Abs(got - want) / Mathf.Max(0.01f, want));
            }
            Debug.Log(string.Format("[STAMM1-Mesh] groesster relativer Fehlerring = {0:P2}", worstRel));
            Assert.Less(worstRel, 0.01f, "Mesh-Radius folgt nicht RadiusAt*TrunkFlute.");
        }

        private static float PhaseOf(int seed)
        {
            var rnd = new System.Random(seed);
            return (float)rnd.NextDouble() * 10f;
        }

        // ---------- Test 3: UVs — Mooszone nur im untersten Meter ----------

        [Test]
        public void Stamm1_TrunkUvPutsMossBandOnlyAtTheFoot()
        {
            Mesh m = RedwoodFactory.Trunk(4242, 55f, 1.1f, 0.45f);
            var vs = m.vertices;
            var uvs = m.uv;
            Assert.AreEqual(vs.Length, uvs.Length, "UV-Zahl weicht von Vertex-Zahl ab.");

            float band = RedwoodFactory.BarkMossBandV;
            int foot = 0, above = 0;
            float maxFoot = float.MinValue, minAbove = float.MaxValue;
            for (int i = 0; i < vs.Length; i++)
            {
                float y = vs[i].y;
                if (y < 0f) continue;                       // unter der Erde: egal
                if (y < 1f) { foot++; if (uvs[i].y > maxFoot) maxFoot = uvs[i].y; }
                else { above++; if (uvs[i].y < minAbove) minAbove = uvs[i].y; }
            }
            Assert.Greater(foot, 0, "Mesh hat keinen untersten Meter ueber Grund.");
            Assert.Greater(above, 0, "Mesh hat keine Rinde ueber dem Fuss.");
            Debug.Log(string.Format("[STAMM1-UV] band={0:0.00} maxFootV={1:0.000} minAboveV={2:0.000}", band, maxFoot, minAbove));
            Assert.LessOrEqual(maxFoot, band + 0.001f, "unterster Meter ragt aus der Mooszone heraus");
            Assert.GreaterOrEqual(minAbove, band - 0.001f, "Rinde ueber dem Fuss samplet die Mooszone");

            // Kein hartes Strecken: v steigt pro Meter moderat (Kachelhoehe ~3 m).
            float maxDv = 0f;
            for (int i = 0; i < vs.Length; i++)
            {
                for (int j = i + 1; j < vs.Length && j < i + 3; j++)
                {
                    float dy = Mathf.Abs(vs[i].y - vs[j].y);
                    if (dy < 0.5f) continue;
                    maxDv = Mathf.Max(maxDv, Mathf.Abs(uvs[i].y - uvs[j].y) / dy);
                }
            }
            Assert.LessOrEqual(maxDv, 1.2f / RedwoodFactory.BarkTileH + 0.35f, "Rinde streckt zu stark (Naht).");
        }

        // ---------- Test 4: Texturregeln (Farbton, Moos nur im Band, unregelm. Furchen, Platten) ----------

        [Test]
        public void Stamm1_RedwoodBarkTextureRules()
        {
            int size;
            Color32[] col = ReadPng(TexDir + "/T_RedwoodBark.png", out size);
            float band = RedwoodFactory.BarkMossBandV;

            // Farbton der Rinde: 10..25 Grad, Moos (gruen) ausgenommen.
            int bark = 0, mossAboveBand = 0, mossInBand = 0, bandPixels = 0, hueOk = 0;
            float hueMin = 90f, hueMax = 0f;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                bool inMossBand = v < band;
                if (inMossBand) bandPixels++;
                for (int x = 0; x < size; x++)
                {
                    float hue = Hue360(col[y * size + x]);
                    if (IsMoss(hue))
                    {
                        if (inMossBand) mossInBand++; else mossAboveBand++;
                        continue;
                    }
                    if (hue < 0f) continue;                 // unbunt (graue Flecken) ist erlaubt
                    bark++;
                    if (hue < hueMin) hueMin = hue;
                    if (hue > hueMax) hueMax = hue;
                    if (hue >= 8f && hue <= 30f) hueOk++;
                }
            }
            float mossFracAbove = mossAboveBand / (float)(size * size);
            float mossFracBand = mossInBand / (float)Mathf.Max(1, bandPixels);
            float hueShare = hueOk / (float)Mathf.Max(1, bark);
            Debug.Log(string.Format("[STAMM1-Tex] Farbton {0:0}..{1:0} Grad, Anteil 8..30 Grad = {2:P1}, Moos im Band = {3:P1}, Moes ueber dem Band = {4:P2}",
                hueMin, hueMax, hueShare, mossFracBand, mossFracAbove));
            Assert.GreaterOrEqual(hueShare, 0.95f, "Rinden-Farbton ausserhalb 8..30 Grad (Ziel 10..25).");
            Assert.Less(mossFracAbove, 0.002f, "Moosfarbe ausserhalb der untersten Meter-Zone.");
            Assert.GreaterOrEqual(mossFracBand, 0.15f, "im untersten Meter ist kein Moos zu sehen.");

            // Struktur aus der Normalmap: Rillen (geglaettet, damit feine Fasern nicht zaehlen)
            //muessen unregelmaessige Abstaende und flache Oberkanten haben; Faser-Enden/-Zweige
            //erzeugen senkrechte Gradienten (G-Kanal), die es vorher kaum gab.
            Color32[] nrm = ReadPng(TexDir + "/T_RedwoodBark_N.png", out int nsize);
            Assert.AreEqual(size, nsize, "Normalmap andere Groesse als Farbtextur.");
            float cvSum = 0f, flatSum = 0f, breakSum = 0f;
            for (int row = 0; row < 5; row++)
            {
                int y = Mathf.RoundToInt(size * (band + 0.12f + row * 0.14f));
                var h = new float[size];
                float acc = 0f;
                for (int x = 0; x < size; x++) { acc += nrm[y * size + x].r / 255f - 0.5f; h[x] = acc; }
                float meanH = 0f;
                for (int x = 0; x < size; x++) meanH += h[x];
                meanH /= size;
                for (int x = 0; x < size; x++) h[x] -= meanH;

                var hs = new float[size];                       // Box-Filter 15 px: nur echte
                for (int x = 0; x < size; x++)                  // Furchen zaehlen, nicht die feinen
                {                                               // Faserstreifen (8 px)
                    float s = 0f;
                    for (int k = -7; k <= 7; k++) s += h[(x + k + size * 2) % size];
                    hs[x] = s / 15f;
                }
                var d = new float[size];
                float dmax = 0f, lo = (minOf(hs) + maxOf(hs)) * 0.5f;
                for (int x = 0; x < size; x++)
                {
                    d[x] = hs[(x + 1) % size] - hs[(x - 1 + size) % size];
                    if (Mathf.Abs(d[x]) > dmax) dmax = Mathf.Abs(d[x]);
                }

                var pos = new List<float>();
                for (int x = 0; x < size; x++)
                    if (hs[x] >= hs[(x - 1 + size) % size] && hs[x] > hs[(x + 1) % size]) pos.Add(x);
                Assert.GreaterOrEqual(pos.Count, 8, "zu wenige Furchen in der Rinden-Normalmap");
                Assert.LessOrEqual(pos.Count, 60, "zu viele Furchen (Rinde zu fein).");

                float mean = 0f;
                var gaps = new List<float>();
                for (int i = 0; i < pos.Count; i++)
                {
                    float g = pos[(i + 1) % pos.Count] - pos[i];
                    if (i == pos.Count - 1) g += size;
                    gaps.Add(g);
                    mean += g;
                }
                mean /= pos.Count;
                float var = 0f, flat = 0f;
                foreach (float g in gaps) var += (g - mean) * (g - mean);
                var /= pos.Count;
                cvSum += Mathf.Sqrt(var) / Mathf.Max(1f, mean);
                for (int x = 0; x < size; x++)
                    if (Mathf.Abs(d[x]) < 0.10f * dmax && hs[x] > lo) flat++;
                flatSum += flat / size;

                // Starke senkrechte Gradienten = scharfe Faser-Kanten. Schwelle 0.06 (nicht 0.12):
                // die alte Rinde hatte bei DIESER Messung 0,0 % (gleitende Sinusflanken, kein Pixel
                // ueber 0.06), die neue hat 7,9 % (r103-f6, Datei gemessen: 203 von 2560 Pixels).
                float brk = 0f;
                for (int x = 0; x < size; x++) if (Mathf.Abs(nrm[y * size + x].g / 255f - 0.5f) > 0.06f) brk++;
                breakSum += brk / size;
            }
            float cv = cvSum / 5f, flatShare = flatSum / 5f, breakShare = breakSum / 5f;
            Debug.Log(string.Format("[STAMM1-Tex] Furchen CV = {0:P1} (Ziel >= 35 %), flache Oberkanten = {1:P1} (Ziel >= 12 %), Faser-Enden/Zweige = {2:P2} (Ziel >= 5 %)",
                cv, flatShare, breakShare));
            Assert.GreaterOrEqual(cv, 0.35f, "Furchenabstand zu gleichmaessig (Vorhang-Look).");
            // Alt (r103-vor, Sinusstreifen): CV 27 %, flache Oberkanten 3,4 %, Faser-Enden 0,12 %.
            Assert.GreaterOrEqual(flatShare, 0.12f, "keine flachen Platten (runde Sinusstreifen).");
            Assert.GreaterOrEqual(breakShare, 0.05f, "kein Faserstreifen endet oder verzweigt.");
        }

        private static float minOf(float[] a)
        {
            float m = float.MaxValue;
            foreach (float v in a) if (v < m) m = v;
            return m;
        }

        private static float maxOf(float[] a)
        {
            float m = float.MinValue;
            foreach (float v in a) if (v > m) m = v;
            return m;
        }

        // Bildbeweis: Detail-Foto kommt vom echten Player (Overseer-Auftrag: Foto -Seed 4242
        // -Pitch 0 vorher/nachher, zusaetzlich Nahaufnahme). Ein Screenshot-Test im PlayMode-
        // Rahmen scheitert an der HDRP-Kamerainfrastruktur (r103-f5: RT bleibt uniform), deshalb
        // Tools/spiel_foto.ps1 -Seed 4242 -Pitch 0 und -Near MAT_Redwood -Zoom 30.
    }
}
