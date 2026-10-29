using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// TEXTUR2: Der Bodenfarn (Grundfarn) traegt jetzt ein Atlas-Texturpaar
    /// (MAT_Grundfarn_Leaf + MAT_Grundfarn_Stem). Prueft: ein Mesh je Variante mit
    /// 2 Submeshes (Slot 0 Stiel, 1 Blatt), UVs je Fieder in eigener Zelle,
    /// Albedo (mindestens 512 px) und Normalmap in beiden Materialien gesetzt.
    /// </summary>
    public class Modell0FarnTexturTests
    {
        private const int Grid = 22; // UV-Raster wie im FBX (textur2_uv.py)

        private static IEnumerator LoadWorld(System.Action<JungleWorldGenerator> done)
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            float timeout = 60f;
            JungleWorldGenerator gen = null;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(gen, "Welt nicht fertig.");
            done(gen);
        }

        [UnityTest]
        public IEnumerator Modell0_Grundfarn_Textur()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            var ferns = gen.library.groundFerns;
            Assert.IsNotNull(ferns, "TEXTUR2: keine groundFerns in der Bibliothek.");
            Assert.That(ferns.Length, Is.GreaterThanOrEqualTo(1));

            // Kriterium "fern_1..5.fbx UVs je Fieder": jede Variante pruefen.
            for (int fi = 0; fi < ferns.Length; fi++)
            {
                Mesh m = ferns[fi];
                Assert.IsNotNull(m, "TEXTUR2: groundFerns[" + fi + "] null");
                CheckVariant(m, fi);
            }

            Assert.AreEqual(1, gen.library.groundFernLeafSubmesh, "TEXTUR2: groundFernLeafSubmesh erwartet 1");

            // Albedo- und Normalmap in Blatt- UND Stielmaterial gesetzt, mindestens 512 px.
            var leaf = gen.library.groundFernLeafMat;
            var stem = gen.library.groundFernStemMat;
            Assert.IsNotNull(leaf, "TEXTUR2: groundFernLeafMat null");
            Assert.IsNotNull(stem, "TEXTUR2: groundFernStemMat null");
            foreach (var mat in new Material[] { leaf, stem })
            {
                var albedo = mat.GetTexture("_BaseColorMap") as Texture2D;
                var normal = mat.GetTexture("_NormalMap") as Texture2D;
                Debug.Log("[TEXTUR2] " + mat.name + ": Albedo "
                          + (albedo == null ? "null" : albedo.width + "x" + albedo.height)
                          + " -> mindestens 512 -> "
                          + (albedo != null && albedo.width >= 512 && albedo.height >= 512 ? "OK" : "NOK")
                          + "; Normal gesetzt -> " + (normal != null ? "OK" : "NOK"));
                Assert.IsNotNull(albedo, "TEXTUR2: " + mat.name + " ohne _BaseColorMap");
                Assert.That(albedo.width, Is.GreaterThanOrEqualTo(512), "TEXTUR2: Albedo breiter als 512 px erforderlich");
                Assert.That(albedo.height, Is.GreaterThanOrEqualTo(512), "TEXTUR2: Albedo hoeher als 512 px erforderlich");
                Assert.IsNotNull(normal, "TEXTUR2: " + mat.name + " ohne _NormalMap");
            }
        }

        private static void CheckVariant(Mesh mesh, int variant)
        {
            // Ein Mesh mit 2 Submeshes: Slot 0 Stiel, Slot 1 Blatt (unveraenderliche Reihenfolge).
            Debug.Log("[TEXTUR2] Variante " + (variant + 1) + ": subMeshCount " + mesh.subMeshCount + " -> 2 -> "
                      + (mesh.subMeshCount == 2 ? "OK" : "NOK"));
            Assert.AreEqual(2, mesh.subMeshCount, "TEXTUR2: Grundfarn-Mesh erwartet 2 Submeshes (Stiel/Blatt)");

            // UVs: vollstaendig, alle in 0..1, jede Fieder-Insel in eigener Gitterzelle.
            var uvs = mesh.uv;
            Debug.Log("[TEXTUR2] Variante " + (variant + 1) + ": UV-Eintraege " + uvs.Length + " / VertexCount " + mesh.vertexCount);
            Assert.AreEqual(mesh.vertexCount, uvs.Length, "TEXTUR2: Mesh ohne vollstaendige UVs");
            bool inRange = true;
            var cells = new HashSet<int>();
            foreach (var uv in uvs)
            {
                if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) inRange = false;
                int cx = Mathf.Min(Grid - 1, Mathf.Max(0, Mathf.FloorToInt(uv.x * Grid)));
                int cy = Mathf.Min(Grid - 1, Mathf.Max(0, Mathf.FloorToInt(uv.y * Grid)));
                cells.Add(cy * Grid + cx);
            }
            // Exakter Ueberlappungs-Check: Anzahl zusammenhaengender Inseln (per
            // Positionen verschweisster Union-Find ueber Dreiecke) muss gleich der Anzahl
            // belegter Zellen sein. Dann liegt jede Insel in genau einer Zelle und keine
            // Zelle enthält zwei Inseln. Variante 1 hat 8 Stiel- + 190 Fieder-Inseln = 198.
            int islands = CountIslands(mesh);
            Debug.Log("[TEXTUR2] Variante " + (variant + 1) + ": UV in 0..1 -> " + (inRange ? "OK" : "NOK")
                      + ", Inseln " + islands + " -> mindestens 100 (mind. 20 Fiederpaare + Stueck) -> "
                      + (islands >= 100 ? "OK" : "NOK")
                      + ", belegte Zellen " + cells.Count + " == Inseln -> "
                      + (cells.Count == islands ? "OK" : "NOK"));
            Assert.IsTrue(inRange, "TEXTUR2: UVs ausserhalb 0..1");
            Assert.That(islands, Is.GreaterThanOrEqualTo(100),
                "TEXTUR2: erwartet >= 100 Inseln (mindestens 20 Fiederpaare + Stiel)");
            Assert.AreEqual(islands, cells.Count,
                "TEXTUR2: jede UV-Insel muss eine eigene Zelle haben (keine Ueberlappung)");

            // Dreiecksanzahl je Variante: unveraendert gegen MODELL0 (gemessen an HEAD-FBX:
            // 1908/1968/2292/2544/3384), Kriterium ist +/- 5 Prozent.
            int tris = mesh.triangles.Length / 3;
            int expected = ExpectedTris[variant];
            float dev = Mathf.Abs(tris - expected) / (float)expected;
            Debug.Log("[TEXTUR2] Variante " + (variant + 1) + ": Dreiecke " + tris
                      + " erwartet " + expected + " Abweichung " + (dev * 100f).ToString("F1")
                      + "% -> maximal 5% -> " + (dev <= 0.05f ? "OK" : "NOK"));
            Assert.That(dev, Is.LessThanOrEqualTo(0.05f), "TEXTUR2: Dreiecksanzahl gegenueber MODELL0 veraendert");
        }

        // Gemessen an den FBX von Commit HEAD~ (MODELL0-Stand) mit Blender 5.1.
        private static readonly int[] ExpectedTris = { 1908, 1968, 2292, 2544, 3384 };

        [UnityTest]
        public IEnumerator Modell0_FotoPose_ZumGrundfarn()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            // FOTO2: Der Pose-Finder fuer -fotonear findet den naechsten Grundfarn und
            // stellt eine Kamera-Pose in sichtbarer Entfernung davon auf.
            var cam = UnityEngine.Camera.main;
            Assert.IsNotNull(cam, "Keine Hauptkamera.");
            bool ok = Spinnenspiel.FotoModusRunner.TryGetNearPose(
                "MAT_Grundfarn_Leaf", cam.transform.position, 0f, out Vector3 pos, out Quaternion rot);
            float dist = Vector3.Distance(pos, cam.transform.position);
            Debug.Log("[TEXTUR2] TryGetNearPose -> " + (ok ? "OK" : "NOK")
                      + ", Kameraposition " + pos.ToString("F2") + " (" + dist.ToString("F1")
                      + " m von Spawn) -> 0..300 m -> " + (ok && dist < 300f ? "OK" : "NOK"));
            Assert.IsTrue(ok, "TEXTUR2: kein Grundfarn-Pose-Fund (fotonear).");
            Assert.That(dist, Is.LessThan(300f), "TEXTUR2: naechster Grundfarn unerwartet weit weg.");
        }

        /// <summary>Zusammenhaengende Mesh-Inseln: Union-Find ueber Dreiecke, die sich an
        /// (auf 0.1 mm quantisierten) identischen Vertex-Positionen beruehren. Zaehlt auch dann
        /// richtig, wenn der FBX-Importer pro Flaeche eigene Vertices anlegt (FACE-Smoothing).</summary>
        private static int CountIslands(Mesh mesh)
        {
            int[] tris = mesh.triangles;
            Vector3[] verts = mesh.vertices;
            int triCount = tris.Length / 3;
            int[] parent = new int[triCount];
            for (int i = 0; i < triCount; i++) parent[i] = i;

            var firstTriAtPos = new Dictionary<long, int>(tris.Length);
            for (int t = 0; t < triCount; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 v = verts[tris[t * 3 + k]];
                    long qx = (long)System.Math.Round(v.x * 10000.0);
                    long qy = (long)System.Math.Round(v.y * 10000.0);
                    long qz = (long)System.Math.Round(v.z * 10000.0);
                    // Positionsschluessel: 21 Bit je Achse (reicht fuer -1000..1000 m in 0.1 mm).
                    long key = ((qx + 1048576L) << 42) | ((qy + 1048576L) << 21) | (qz + 1048576L);
                    if (firstTriAtPos.TryGetValue(key, out int other)) Union(parent, t, other);
                    else firstTriAtPos[key] = t;
                }
            }

            var roots = new HashSet<int>();
            for (int t = 0; t < triCount; t++) roots.Add(Find(parent, t));
            return roots.Count;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
