using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Auftrag 9.6: Brückennetz zwischen zwei Baumstämmen in der echten Testwelt.
    /// Ein Faden in 2 m Höhe spannt die "Brücke", die Spinne baut darunter ein
    /// Netz; Geometrie wird geprüft und ein Beweisfoto gespeichert.
    /// </summary>
    public class WorldWebTests
    {
        /// <summary>Kleinste Distanz zweier Faden-Probenpunkte, die noch als "am Faden" zählt.</summary>
        private static float MinDistToThread(SilkThread thread, Vector3 p)
        {
            float best = float.MaxValue;
            int n = 32;
            for (int i = 0; i <= n; i++)
            {
                Vector3 s = thread.PointAt(i / (float)n);
                float d = Vector3.Distance(s, p);
                if (d < best)
                {
                    best = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Auftrag 9.6: In der Testwelt gibt es zwei Stämme im waagerechten Abstand
        /// 1.5–4 m. Ein Faden in 2 m Höhe verbindet sie; die Spinne baut darunter
        /// ein Netz. Prüfung: Rahmenbreite &lt;= 1.25 m, obere Ecken &lt;= 0.1 m vom
        /// Faden, &gt;= 2 Ankerfäden, untere Anker tiefer als die unteren Ecken.
        /// Foto: Logs/ci/weltbrueckennetz.png.
        /// </summary>
        [UnityTest]
        public IEnumerator World_BridgeWebBetweenTrees()
        {
            SceneManager.LoadScene("Testwelt");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht in Testwelt gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");

            // Alle Stämme (GO "Trunk" mit MeshCollider + SurfaceTag) sammeln.
            var trunks = new System.Collections.Generic.List<Transform>();
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == "Trunk" && go.GetComponent<MeshCollider>() != null && go.GetComponent<SurfaceTag>() != null)
                {
                    trunks.Add(go.transform);
                }
            }
            Assert.That(trunks.Count, Is.GreaterThanOrEqualTo(2),
                "Brauche mindestens 2 Stämme, gefunden: " + trunks.Count);

            // Paare im waagerechten Abstand 1.5–4 m; nächstes (kleinstes) Paar nehmen.
            Transform a = null;
            Transform b = null;
            float best = float.MaxValue;
            for (int i = 0; i < trunks.Count; i++)
            {
                for (int j = i + 1; j < trunks.Count; j++)
                {
                    float dx = trunks[i].position.x - trunks[j].position.x;
                    float dz = trunks[i].position.z - trunks[j].position.z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist >= 1.5f && dist <= 4.0f && dist < best)
                    {
                        best = dist;
                        a = trunks[i];
                        b = trunks[j];
                    }
                }
            }
            Assert.IsNotNull(a,
                "Kein Stämmepaar im waagerechten Abstand 1.5–4 m (Stämme: " + trunks.Count + ")");
            float baumAbstand = best;

            // Brückenfaden 2 m über dem jeweiligen Boden, Enden auf der Rinde.
            float groundA, groundB;
            GroundBelow(a.position, spider.transform, out groundA);
            GroundBelow(b.position, spider.transform, out groundB);
            float threadY = Mathf.Max(groundA, groundB) + 2.0f;
            Vector3 pa = BarkPoint(a, b, threadY);
            Vector3 pb = BarkPoint(b, a, threadY);
            GameObject threadGo = new GameObject("WeltBrueckenFaden");
            SilkThread faden = threadGo.AddComponent<SilkThread>();
            faden.Init(pa, pb, 0.02f);
            yield return new WaitForSeconds(0.3f);

            // Spinne auf den Faden setzen.
            float tMid = faden.ClosestT((pa + pb) * 0.5f);
            movement.AttachToThread(faden, tMid);
            yield return new WaitForSeconds(0.3f);

            // Warten, bis der Placer den Platz unter dem Faden akzeptiert.
            float t = 0f;
            while (!placer.CanBuild && t < 3f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(placer.CanBuild,
                "CanBuild bleibt false unter dem Brückenfaden (BlockReason: " + placer.BlockReason +
                ", Faden über Boden A/B: " + (threadY - groundA).ToString("F2") + "/" + (threadY - groundB).ToString("F2") +
                " m, Baumabstand " + baumAbstand.ToString("F2") + " m)");

            // Netz bauen (beschleunigt).
            builder.durationScale = 0.1f;
            Assert.IsTrue(placer.TryPlaceWebNow(), "TryPlaceWebNow scheitert zwischen den Bäumen");
            Assert.IsTrue(builder.IsBuilding, "Builder baut nicht");

            float elapsed = 0f;
            while (builder.IsBuilding && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(builder.IsBuilding, "Netzbau nach 15 s noch nicht fertig");
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "CurrentStage != Done");

            OrbWeb web = builder.Web;
            Assert.IsNotNull(web, "builder.Web fehlt nach dem Bau");
            Assert.IsTrue(web.IsBuilt, "Netz ist nicht gebaut (Layout fehlt)");
            Vector3[] frame = web.Layout.FrameAnchors;
            Assert.AreEqual(4, frame.Length, "Rahmen hat nicht 4 Ecken");

            // Obere Ecken = die zwei höchsten Rahmenanker.
            System.Array.Sort(frame, (x, y) => y.y.CompareTo(x.y));
            Vector3 oben0 = frame[0];
            Vector3 oben1 = frame[1];
            Vector3 unten0 = frame[2];
            Vector3 unten1 = frame[3];

            // Rahmenbreite (obere Kante) <= 1.25 m.
            float breite = Vector3.Distance(oben0, oben1);
            Assert.LessOrEqual(breite, 1.25f,
                "Rahmenbreite zu gross: " + breite + " m (Baumabstand " + baumAbstand + " m)");

            // Untere Kante: breiter als obere (Rahmen läuft nach unten aus).
            float untenBreite = Vector3.Distance(unten0, unten1);
            Assert.LessOrEqual(untenBreite, 1.8f, "Untere Rahmenkante zu breit: " + untenBreite + " m");
            Assert.GreaterOrEqual(untenBreite, breite - 0.01f, "Rahmen soll nach unten breiter werden");

            // Obere Ecken <= 0.1 m vom Faden.
            Assert.LessOrEqual(MinDistToThread(faden, oben0), 0.1f,
                "Obere linke Ecke nicht am Faden: " + oben0);
            Assert.LessOrEqual(MinDistToThread(faden, oben1), 0.1f,
                "Obere rechte Ecke nicht am Faden: " + oben1);

            // Ankerfäden: >= 2, jeweils unter der zugehörigen Rahmen-Ecke.
            var ankerLinien = new System.Collections.Generic.List<LineRenderer>();
            foreach (LineRenderer lr in web.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr.name.StartsWith("Anker_"))
                {
                    ankerLinien.Add(lr);
                }
            }
            Assert.That(ankerLinien.Count, Is.GreaterThanOrEqualTo(2),
                "Erwarte mindestens 2 Ankerfäden, gefunden: " + ankerLinien.Count);

            foreach (LineRenderer lr in ankerLinien)
            {
                Vector3 start = lr.GetPosition(0);
                Vector3 ende = lr.GetPosition(lr.positionCount - 1);
                // Ende ist der Ankerpunkt (Start liegt am Rahmen).
                Vector3 ecke = Vector3.Distance(start, unten0) < Vector3.Distance(start, unten1) ? unten0 : unten1;
                Assert.Less(ende.y, ecke.y,
                    "Ankerpunkt nicht unter der Rahmen-Ecke: Anker " + ende + ", Ecke " + ecke);
            }

            // Beweisfoto: 3 m vor der Netzmitte entlang der Normalen.
            Vector3 netzMitte = web.Layout.Hub;
            GameObject camGo = new GameObject("WeltBrueckennetzCam");
            Camera cam = camGo.AddComponent<Camera>();
            camGo.transform.position = netzMitte + web.PlaneNormal * 3f;
            camGo.transform.LookAt(netzMitte);
            var rt = new RenderTexture(960, 540, 24);
            rt.Create();
            cam.targetTexture = rt;
            SaveFrame(cam, rt, "Logs/ci/weltbrueckennetz.png");
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(threadGo);
        }

        /// <summary>
        /// Bodenhöhe unter einem Stamm: Raycast senkrecht nach unten (max. 100 m),
        /// Treffer mit SurfaceTag Wood oder unter der Spinne werden übersprungen.
        /// Fallback ohne Treffer: Stammbasis.
        /// </summary>
        private static bool GroundBelow(Vector3 trunkPos, Transform spiderRoot, out float groundY)
        {
            var hits = Physics.RaycastAll(trunkPos + Vector3.up * 30f, Vector3.down, 100f);
            foreach (RaycastHit hit in hits)
            {
                Transform t = hit.collider.transform;
                if (IsUnder(spiderRoot, t)) continue;
                SurfaceTag tag = t.GetComponentInParent<SurfaceTag>();
                if (tag != null && tag.surfaceType == SurfaceType.Wood) continue;
                groundY = hit.point.y;
                return true;
            }
            groundY = trunkPos.y;
            return false;
        }

        /// <summary>
        /// Fadenende auf der Rinde des anderen Stamms auf Höhe y: Raycast vom
        /// eigenen Stamm (Start +0.05 m Richtung other) zum anderen Stamm, erster
        /// Treffer mit SurfaceTag Wood auf dem ANDEREN Stamm. Fallback: eigene
        /// Stammmitte + 0.25 m Richtung other.
        /// </summary>
        private static Vector3 BarkPoint(Transform own, Transform other, float y)
        {
            Vector3 dir = other.position - own.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return own.position;
            dir.Normalize();
            Vector3 start = new Vector3(own.position.x, y, own.position.z) + dir * 0.05f;
            Vector3 to = new Vector3(other.position.x, y, other.position.z) - start;
            if (to.sqrMagnitude > 1e-6f)
            {
                to.Normalize();
                var hits = Physics.RaycastAll(start, to, 10f);
                foreach (RaycastHit hit in hits)
                {
                    Transform t = hit.collider.transform;
                    if (!IsUnder(other, t)) continue;
                    SurfaceTag tag = t.GetComponentInParent<SurfaceTag>();
                    if (tag != null && tag.surfaceType == SurfaceType.Wood) return hit.point;
                }
            }
            return new Vector3(own.position.x, y, own.position.z) + dir * 0.25f;
        }

        /// <summary>Echt, ob t == root oder Kind von root.</summary>
        private static bool IsUnder(Transform root, Transform t)
        {
            while (t != null)
            {
                if (t == root) return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>Render die Kamera in die RenderTexture und speichert als PNG ab.</summary>
        private static void SaveFrame(Camera cam, RenderTexture rt, string file)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(file, png);
        }
    }
}
