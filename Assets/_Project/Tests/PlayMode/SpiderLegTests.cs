using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Milestone 2: Fußplatzierung, Gangkoordination und Körperanpassung.</summary>
    public class SpiderLegTests
    {
        private const string SceneName = "M1_Testgelaende";
        private const float GroundTolerance = 0.015f;

        // Gait-Messung braucht Echtzeit: Die Zeitraffer-Fixture (TestSpeed) gibt
        // jeder Frame eine feste 20 ms Spielzeit, das aendert das
        // Physik/IK-Verhaeltnis beim Gehen/Sprinten. Deshalb hier wieder auf
        // 0 zuruecksetzen (Echtzeit) und danach den Fixture-Wert wiederherstellen.
        [SetUp]
        public void ResetToRealtime() { Time.captureDeltaTime = 0f; }

        [TearDown]
        public void RestoreTimeLapse() { Time.captureDeltaTime = 1f / 50f; }

        private static float GroundHeightAt(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            Assert.Fail("Kein Boden unter " + position);
            return 0f;
        }

        private static void AssertPlantedFeetOnGround(SpiderLeg[] legs, string phase)
        {
            foreach (SpiderLeg leg in legs)
            {
                if (leg.IsStepping || !leg.IsGrounded)
                {
                    continue;
                }

                float ground = GroundHeightAt(leg.FootPosition);
                float error = leg.FootPosition.y - ground;
                Assert.Less(Mathf.Abs(error), GroundTolerance, phase + ": Fuß " + leg.name + " liegt " + error.ToString("F4") + " m neben dem Boden");
            }
        }

        [UnityTest]
        public IEnumerator Legs_StayOnGround_AlternateAndAdaptBody()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            SpiderGaitController gait = spider.GetComponent<SpiderGaitController>();
            SpiderBodyController body = spider.GetComponent<SpiderBodyController>();
            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.IsNotNull(gait, "SpiderGaitController fehlt");
            Assert.IsNotNull(body, "SpiderBodyController fehlt");
            Assert.AreEqual(8, legs.Length, "Die Spinne braucht 8 Beine");

            foreach (SpiderLeg leg in legs)
            {
                Assert.IsTrue(leg.IsGrounded, "Bein " + leg.name + " hat nach dem Start keinen Boden");
            }

            AssertPlantedFeetOnGround(legs, "Stand");

            int[] stepsBefore = new int[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                stepsBefore[i] = legs[i].StepCount;
            }

            gait.ResetStatistics();
            movement.SetExternalMoveInput(new Vector2(0.3f, 1f));
            float time = 0f;
            while (time < 3f)
            {
                time += Time.deltaTime;
                AssertPlantedFeetOnGround(legs, "Laufen");
                Assert.LessOrEqual(gait.SteppingCount, 5, "Zu viele Beine schwingen gleichzeitig");
                yield return null;
            }

            Assert.LessOrEqual(gait.MaxSteppingObserved, 5, "Zu viele Beine schwingen gleichzeitig");
            for (int i = 0; i < legs.Length; i++)
            {
                Assert.GreaterOrEqual(legs[i].StepCount - stepsBefore[i], 2, "Bein " + legs[i].name + " hat beim Laufen kaum Schritte gemacht");
            }

            movement.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(0, gait.SteppingCount, "Beine zappeln im Stand weiter");
            AssertPlantedFeetOnGround(legs, "Stand nach dem Laufen");

            float bodyY = body.Body.localPosition.y - body.AverageFootHeight;
            Assert.That(bodyY, Is.InRange(0.03f, 0.1f), "Körperhöhe über den Füßen unplausibel: " + bodyY);
        }

        [UnityTest]
        public IEnumerator Legs_GaitMeasurement_WalkAndSprint()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "Die Spinne braucht 8 Beine");

            List<string> report = new List<string>();
            List<string> fehler = new List<string>();
            string[] laeufe = { "Gehen", "Sprint" };
            bool[] sprints = { false, true };

            for (int run = 0; run < 2; run++)
            {
                string lauf = laeufe[run];

                // Messlauf starten: Sprint-Zustand, Vorwärts-Eingabe, 1 s einlaufen lassen.
                movement.SetExternalSprint(sprints[run]);
                movement.SetExternalMoveInput(new Vector2(0f, 1f));
                yield return new WaitForSeconds(1f);

                // Messstart: Schrittzähler erfassen, Messwerte initialisieren.
                int[] stepsBefore = new int[legs.Length];
                float[] dzMin = new float[legs.Length];
                float[] dzMax = new float[legs.Length];
                float[] dzSum = new float[legs.Length];
                int[] dzSamples = new int[legs.Length];
                float[] extMax = new float[legs.Length];
                float[] vorneZmin = new float[legs.Length];
                for (int i = 0; i < legs.Length; i++)
                {
                    stepsBefore[i] = legs[i].StepCount;
                    dzMin[i] = float.MaxValue;
                    dzMax[i] = float.MinValue;
                    extMax[i] = 0f;
                    vorneZmin[i] = float.MaxValue;
                }

                // 3 s messen: pro Frame und Bein nur stehende Füße.
                float time = 0f;
                while (time < 3f)
                {
                    time += Time.deltaTime;

                    // Load-Hitch-Filter: on Frames with abnormal Delta skip the Pose-Sampling
                    // (the Spider jumped far in one Step -> the Leg-Pose is a Mess-Artifact, not Simulation).
                    if (Time.deltaTime > 0.2f)
                    {
                        yield return null;
                        continue;
                    }

                    for (int i = 0; i < legs.Length; i++)
                    {
                        SpiderLeg leg = legs[i];
                        if (leg.IsStepping || !leg.IsGrounded)
                        {
                            continue;
                        }

                        Vector3 footL = spider.transform.InverseTransformPoint(leg.FootPosition);
                        Vector3 restL = spider.transform.InverseTransformPoint(leg.RestTarget.position);
                        float dz = footL.z - restL.z;
                        dzMin[i] = Mathf.Min(dzMin[i], dz);
                        dzMax[i] = Mathf.Max(dzMax[i], dz);
                        dzSum[i] += dz;
                        dzSamples[i]++;

                        float ext = (leg.FootPosition - leg.HipPosition).magnitude / leg.MaxReach;
                        extMax[i] = Mathf.Max(extMax[i], ext);

                        if (leg.PairIndex == 0)
                        {
                            vorneZmin[i] = Mathf.Min(vorneZmin[i], footL.z);
                        }
                    }

                    yield return null;
                }

                // Messlauf beenden: Eingaben zurücksetzen, 1 s warten.
                movement.SetExternalMoveInput(null);
                movement.SetExternalSprint(null);
                yield return new WaitForSeconds(1f);

                // Berichtszeilen und Verstöße für diesen Lauf.
                for (int i = 0; i < legs.Length; i++)
                {
                    SpiderLeg leg = legs[i];
                    int steps = leg.StepCount - stepsBefore[i];
                    float dzMittel = dzSamples[i] > 0 ? dzSum[i] / dzSamples[i] : 0f;

                    string fmt(float v)
                    {
                        return v.ToString("F3", CultureInfo.InvariantCulture);
                    }

                    string vorne = leg.PairIndex == 0 ? fmt(vorneZmin[i]) : "-";
                    report.Add(string.Format(CultureInfo.InvariantCulture, "{0} | {1} | {2} | {3} | {4} | {5} | {6} | {7}",
                        lauf, leg.name, steps.ToString(CultureInfo.InvariantCulture),
                        dzSamples[i] > 0 ? fmt(dzMin[i]) : "-",
                        dzSamples[i] > 0 ? fmt(dzMax[i]) : "-",
                        dzSamples[i] > 0 ? fmt(dzMittel) : "-",
                        fmt(extMax[i]), vorne));

                    if (steps < 3)
                    {
                        fehler.Add(string.Format(CultureInfo.InvariantCulture, "{0} Bein {1}: {2} Schritte (min. 3)", lauf, leg.name, steps));
                    }

                    if (extMax[i] > 0.95f)
                    {
                        fehler.Add(string.Format(CultureInfo.InvariantCulture, "{0} Bein {1}: extMax {2} > 0.95", lauf, leg.name, fmt(extMax[i])));
                    }

                    if (leg.PairIndex == 0 && vorneZmin[i] < 0.07f)
                    {
                        fehler.Add(string.Format(CultureInfo.InvariantCulture, "{0} Bein {1}: vorneZmin {2} < 0.07", lauf, leg.name, fmt(vorneZmin[i])));
                    }

                    if (Mathf.Abs(dzMittel) > 0.02f)
                    {
                        fehler.Add(string.Format(CultureInfo.InvariantCulture, "{0} Bein {1}: |dzMittel| {2} > 0.02", lauf, leg.name, fmt(Mathf.Abs(dzMittel))));
                    }
                }
            }

            // Bericht schreiben, BEVOR geprüft wird.
            string logDir = Path.Combine(Application.dataPath, "../Logs/ci");
            Directory.CreateDirectory(logDir);
            File.WriteAllLines(Path.Combine(logDir, "gait_report.txt"), report);

            Assert.IsEmpty(fehler, string.Join("\n", fehler));
        }

        [UnityTest]
        public IEnumerator Legs_UseBlenderMeshes()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");

            string[] suffixes = { "_Femur", "_Tibia", "_Tarsus" };
            List<MeshFilter> segmentFilters = new List<MeshFilter>();
            foreach (MeshFilter mf in spider.GetComponentsInChildren<MeshFilter>())
            {
                foreach (string suffix in suffixes)
                {
                    if (mf.gameObject.name.EndsWith(suffix))
                    {
                        segmentFilters.Add(mf);
                        break;
                    }
                }
            }

            Assert.AreEqual(24, segmentFilters.Count,
                "Bein-Segment-MeshFilters unter der Spinne: " + segmentFilters.Count + " (es braucht 24)");

            List<string> report = new List<string>();
            List<string> fehler = new List<string>();
            foreach (MeshFilter mf in segmentFilters)
            {
                Mesh mesh = mf.sharedMesh;
                Assert.IsNotNull(mesh, "sharedMesh null auf " + mf.gameObject.name);
                Vector3 size = mesh.bounds.size;
                report.Add(mf.gameObject.name + " | " + mesh.name + " | size.y " + size.y.ToString("F4", CultureInfo.InvariantCulture));

                if (mesh.name.Contains("Capsule"))
                {
                    fehler.Add(mf.gameObject.name + ": sharedMesh.name '" + mesh.name + "' contains Capsule");
                }

                if (size.y < 1.9f || size.y > 2.1f)
                {
                    fehler.Add(mf.gameObject.name + " (" + mesh.name + "): size.y " + size.y.ToString("F4", CultureInfo.InvariantCulture) + " outside 1.9..2.1");
                }
            }

            string logDir = Path.Combine(Application.dataPath, "../Logs/ci");
            Directory.CreateDirectory(logDir);
            File.WriteAllLines(Path.Combine(logDir, "leg_mesh_report.txt"), report);

            Assert.IsEmpty(fehler, string.Join("\n", fehler));
        }

        [UnityTest]
        public IEnumerator Body_UsesScanMeshesAndDoubleSided()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");

            string[] bodyNames = { "Abdomen", "Cephalothorax" };
            List<string> fehler = new List<string>();
            foreach (string name in bodyNames)
            {
                Transform part = spider.transform.Find("Body/" + name);
                Assert.IsTrue(part != null, "Koerper-Part fehlt: Body/" + name);
                MeshFilter mf = part.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, "MeshFilter fehlt auf " + name);
                Mesh mesh = mf.sharedMesh;
                Assert.IsNotNull(mesh, "sharedMesh null auf " + name);
                if (mesh.name.Contains("Sphere"))
                {
                    fehler.Add(name + ": Kugel-Fallback still in use (mesh.name " + mesh.name + ")");
                }

                MeshRenderer mr = part.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, "MeshRenderer fehlt auf " + name);
                Material mat = mr.sharedMaterial;
                Assert.IsNotNull(mat, "Material null auf " + name);
                float doubleSided = mat.GetFloat("_DoubleSidedEnable");
                if (Math.Abs(doubleSided - 1f) > 0.001f)
                {
                    fehler.Add(name + ": _DoubleSidedEnable = " + doubleSided.ToString("F1") + " (needs 1 for Haerchens)");
                }
            }

            Assert.IsEmpty(fehler, string.Join("\n", fehler));
        }
    }
}
