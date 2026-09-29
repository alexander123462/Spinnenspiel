using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace Spinnenspiel.Tests
{
    /// <summary>Netz 1 (Teil 1): Geometrie eines Radnetzes. Rahmen: Rechteck in der XY-Ebene.</summary>
    public class OrbWebTests
    {
        private static OrbWebLayout CreateLayout()
        {
            var anchors = new[]
            {
                new Vector3(-0.4f, 0f, 0f),
                new Vector3(0.4f, 0f, 0f),
                new Vector3(0.45f, 0.8f, 0f),
                new Vector3(-0.45f, 0.8f, 0f)
            };
            return new OrbWebLayout(anchors, 24, 0.025f, 0.04f, 42);
        }

        [Test]
        public void NormalIstUngefaehrParallelZuZ()
        {
            var layout = CreateLayout();
            float angle = Vector3.Angle(layout.Normal, Vector3.forward);
            Assert.That(angle, Is.LessThan(5f),
                "Normale weicht mehr als 5° von +/-Z ab: " + angle.ToString("0.00", CultureInfo.InvariantCulture));
        }

        [Test]
        public void RadiusEndenLiegenAmRahmen()
        {
            var layout = CreateLayout();
            Assert.AreEqual(24, layout.RadiusEnds.Count, "Erwartet 24 Radius-Enden");
            for (int i = 0; i < layout.RadiusEnds.Count; i++)
            {
                float d = layout.DistanceToFrame(layout.RadiusEnds[i]);
                Assert.That(d, Is.LessThan(0.02f),
                    "Radius " + i + " liegt " + d.ToString("0.000", CultureInfo.InvariantCulture) + " m vom Rahmen-Polygon.");
            }
        }

        [Test]
        public void HubLiegtOberhalbDesSchwerpunkts()
        {
            var layout = CreateLayout();
            Vector3 centroid = Vector3.zero;
            foreach (var a in layout.FrameAnchors) centroid += a;
            centroid /= layout.FrameAnchors.Length;
            Assert.Greater(layout.Hub.y, centroid.y,
                "Hub (" + layout.Hub + ") ist nicht oberhalb des Schwerpunkts (" + centroid + ").");
        }

        [Test]
        public void FangspiraleIstAusreichendLang()
        {
            var layout = CreateLayout();
            Assert.Greater(layout.CaptureSpiral.Count, 150,
                "Fangspirale hat nur " + layout.CaptureSpiral.Count + " Punkte.");
        }

        [Test]
        public void FangspiralpunkteLiegenInnerhalbDesRahmens()
        {
            var layout = CreateLayout();
            for (int i = 0; i < layout.CaptureSpiral.Count; i++)
            {
                Vector3 p = layout.CaptureSpiral[i];
                Assert.IsTrue(layout.IsInsideFrame(p),
                    "Fangspiralpunkt " + i + " (" + p + ") liegt außerhalb des Rahmens.");
            }
        }

        [Test]
        public void FangspiraleHatKeineSpruenge()
        {
            var layout = CreateLayout();
            for (int i = 1; i < layout.CaptureSpiral.Count; i++)
            {
                float d = Vector3.Distance(layout.CaptureSpiral[i - 1], layout.CaptureSpiral[i]);
                Assert.That(d, Is.LessThan(0.15f),
                    "Sprung an Index " + i + " ist " + d.ToString("0.000", CultureInfo.InvariantCulture) + " m.");
            }
        }

        [Test]
        public void OrbWebBautAlleFäden()
        {
            var layout = CreateLayout();
            var go = new GameObject("OrbWebTest");
            var web = go.AddComponent<OrbWeb>();
            web.Build(layout);

            int lines = 0;
            foreach (Transform child in go.transform)
                if (child.GetComponent<LineRenderer>() != null) lines++;

            Object.DestroyImmediate(go);
            Assert.GreaterOrEqual(lines, 26,
                "Erwartet >= 26 LineRenderer als Kinder, gefunden: " + lines);
        }

        [Test]
        public void Progress_RevealsStages()
        {
            var layout = CreateLayout();
            var go = new GameObject("OrbWebProgressTest");
            var web = go.AddComponent<OrbWeb>();
            web.Build(layout);

            // Bei Stage Radii (t=0.5): ca. Hälfte der Radien sichtbar, Fangspirale unsichtbar.
            web.SetProgress(OrbWeb.WebStage.Radii, 0.5f);
            int visible = 0;
            foreach (Transform child in go.transform)
            {
                if (!child.name.StartsWith("Radius"))
                    continue;
                LineRenderer lr = child.GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount > 0)
                    visible++;
            }

            int total = layout.RadiusCount;
            Assert.That(visible, Is.InRange(total / 2 - 2, total / 2 + 2),
                "Sichtbare Radien bei Radii/0.5: " + visible + " von " + total + ", erwartet ca. " + (total / 2) + " (±2).");

            LineRenderer capture = go.transform.Find("CaptureSpiral").GetComponent<LineRenderer>();
            Assert.That(capture.positionCount, Is.Zero,
                "Fangspirale soll bei Stage Radii unsichtbar sein, positionCount=" + capture.positionCount + ".");

            // Bei Done: Hilfsspirale weg, Fangspirale voll.
            web.SetProgress(OrbWeb.WebStage.Done, 1f);
            LineRenderer aux = go.transform.Find("AuxSpiral").GetComponent<LineRenderer>();
            Assert.That(aux.positionCount, Is.Zero,
                "Hilfsspirale soll bei Done unsichtbar sein, positionCount=" + aux.positionCount + ".");
            Assert.AreEqual(layout.CaptureSpiral.Count, capture.positionCount,
                "Fangspirale soll bei Done vollständig sein (" + layout.CaptureSpiral.Count + " Punkte), gefunden: " + capture.positionCount + ".");

            Object.DestroyImmediate(go);
        }
    }
}
