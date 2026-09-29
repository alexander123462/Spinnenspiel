using System.Collections;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Die Spinne baut das Radnetz selbst und sichtbar: zuerst Überweg zum
    /// Startpunkt, dann Rahmen, Radien, Hilfsspirale und Fangspirale in der
    /// echten Reihenfolge. Am Ende sitzt sie kopfunter in der Nabe;
    /// Bewegungseingabe oder Release lässt sie sich am Sicherheitsfaden fallen.
    /// </summary>
    public class SpiderWebBuilder : MonoBehaviour
    {
        /// <summary>Skaliert alle Stufendauern (1 = Standard).</summary>
        public float durationScale = 1f;
        /// <summary>Abstand des Spinnenkörpers zur Netzebene (m).</summary>
        public float bodyOffset = 0.03f;

        /// <summary>True, solange der Netzbau läuft.</summary>
        public bool IsBuilding { get; private set; }
        /// <summary>Aktuelle Bauphase (Done nach Abschluss).</summary>
        public OrbWeb.WebStage CurrentStage { get; private set; }
        /// <summary>Das aktuell gebaute Netz (null, wenn nie gebaut).</summary>
        public OrbWeb Web { get; private set; }

        /// <summary>True, wenn die Spinne nach dem Bau in der Nabe sitzt.</summary>
        public bool IsSittingInWeb => _sittingInWeb;

        /// <summary>Netznormale, zeigt zur Seite der Spinne.</summary>
        public Vector3 Normal => _normal;

        /// <summary>Unterdrückt das Loslassen (Move/Release) in der Nabe, z.B. während einer Animation.</summary>
        public bool SuppressRelease { get; set; }

        private SpiderMovement _movement;
        private SpiderSilk _silk;
        private SpiderInput _input;
        private LineRenderer _bauFaden;
        private Vector3 _normal = Vector3.up;
        private Quaternion _currentRotation = Quaternion.identity;
        private bool _sittingInWeb;

        private static readonly OrbWeb.WebStage[] Stages =
        {
            OrbWeb.WebStage.Frame,
            OrbWeb.WebStage.Radii,
            OrbWeb.WebStage.AuxSpiral,
            OrbWeb.WebStage.CaptureSpiral
        };

        // Stufendauern in Sekunden (jeweils * durationScale).
        private static readonly float[] StageDurations = { 5f, 12f, 5f, 20f };

        private void Awake()
        {
            _movement = GetComponent<SpiderMovement>();
            _silk = GetComponent<SpiderSilk>();
            _input = GetComponent<SpiderInput>();
        }

        /// <summary>Startet den sichtbaren Netzbau des übergebenen Netzes.</summary>
        public void StartBuild(OrbWeb web)
        {
            if (web == null || web.Layout == null || IsBuilding)
            {
                return;
            }

            Web = web;
            CurrentStage = OrbWeb.WebStage.Frame;
            _sittingInWeb = false;
            _currentRotation = transform.rotation;

            // Netznormale zeigt zur Seite der Spinne, danach bleibt sie fest.
            Vector3 toSpider = transform.position - web.Layout.Hub;
            _normal = web.Layout.Normal;
            if (Vector3.Dot(_normal, toSpider) < 0f)
            {
                _normal = -_normal;
            }

            web.showAux = true;
            if (_movement != null)
            {
                _movement.SetScripted(true);
            }

            if (_silk != null)
            {
                _silk.BeginSpinningPose();
            }

            CreateBauFaden(web);
            IsBuilding = true;
            StartCoroutine(BuildRoutine());
        }

        private void Update()
        {
            // Spinne sitzt nach dem Bau in der Nabe: bei Bewegungseingabe oder
            // Release lässt sie sich wie echt am Sicherheitsfaden fallen.
            if (!_sittingInWeb || _input == null)
            {
                return;
            }

            if (SuppressRelease)
            {
                return;
            }

            bool moveInput = _input.Move.magnitude > 0.3f;
            bool release = _input.ReleasePressed;
            if (!moveInput && !release)
            {
                return;
            }

            _sittingInWeb = false;
            if (_movement != null)
            {
                _movement.SetScripted(false);
                _movement.LetGo();
            }
        }

        private void OnDestroy()
        {
            if (_bauFaden != null)
            {
                Destroy(_bauFaden.gameObject);
            }
        }

        private IEnumerator BuildRoutine()
        {
            OrbWeb web = Web;

            for (int s = 0; s < Stages.Length; s++)
            {
                OrbWeb.WebStage stage = Stages[s];
                CurrentStage = stage;
                Vector3[] path = web.ConstructionPath(stage);
                if (path == null || path.Length < 2)
                {
                    continue;
                }

                // 1. Überweg zum Startpunkt der Phase: noch keine Fäden.
                web.SetProgress(stage, 0f);
                HideBauFaden();
                yield return WalkTo(path[0], 0.4f * durationScale, LookTowards(path[0]));

                // 2. Pfad in der Stufendauer mit konstanter Geschwindigkeit ablaufen.
                float total = PathLength(path);
                float stageTime = Mathf.Max(StageDurations[s] * durationScale, 0.05f);
                float speed = total / stageTime;
                float travelled = 0f;
                while (travelled < total)
                {
                    travelled = Mathf.Min(travelled + speed * Time.deltaTime, total);
                    int i;
                    float f;
                    PointOnPath(path, travelled, out i, out f);
                    Vector3 point = Vector3.Lerp(path[i], path[i + 1], f);
                    web.SetProgress(stage, (i + f) / (path.Length - 1f));

                    // Blickrichtung: Laufrichtung in der Ebene, sanft gedreht.
                    Vector3 dir = path[i + 1] - path[i];
                    Vector3 flatDir = dir - _normal * Vector3.Dot(dir, _normal);
                    if (flatDir.sqrMagnitude > 1e-6f)
                    {
                        Quaternion target = Quaternion.LookRotation(flatDir.normalized, _normal);
                        _currentRotation = Quaternion.Slerp(_currentRotation, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
                    }

                    transform.position = point + _normal * bodyOffset;
                    transform.rotation = _currentRotation;

                    // 4./5. Spinnpose + sichtbar gezogener Faden.
                    if (_silk != null && _silk.Spinnerets != null)
                    {
                        _silk.UpdateSpinningPose(point);
                        ShowBauFaden(_silk.Spinnerets.position, point);
                    }

                    yield return null;
                }

                web.SetProgress(stage, 1f);
            }

            // 3. Fertig: Fangspirale komplett, Hilfsspirale weg.
            web.SetProgress(OrbWeb.WebStage.Done, 1f);
            web.showAux = false;

            // Zum Hub laufen, Kopf nach unten (echte Radnetzspinnen).
            Vector3 hub = web.Layout.Hub;
            Vector3 down = -Vector3.up;
            Vector3 flatDown = down - _normal * Vector3.Dot(down, _normal);
            Quaternion hubRotation = flatDown.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(flatDown.normalized, _normal)
                : _currentRotation;
            yield return WalkTo(hub, 1f * durationScale, hubRotation);
            HideBauFaden();

            if (_silk != null)
            {
                _silk.EndSpinningPose();
            }

            CurrentStage = OrbWeb.WebStage.Done;
            IsBuilding = false;
            _sittingInWeb = true;
        }

        /// <summary>Dreh-Ziel für eine Richtung: in die Netzebene projiziert, Kopf zeigt in Richtung.</summary>
        public Quaternion FacingRotation(Vector3 direction)
        {
            Vector3 proj = direction - _normal * Vector3.Dot(direction, _normal);
            if (proj.sqrMagnitude < 1e-6f)
            {
                return _currentRotation;
            }

            return Quaternion.LookRotation(proj.normalized, _normal);
        }

        /// <summary>Setzt die Spinne sofort fertig in der Nabe: Pose wie nach dem Bau, Beine greifen, Web als Done markiert.</summary>
        public void SitAtHub(OrbWeb web)
        {
            if (web == null || web.Layout == null)
            {
                return;
            }

            Web = web;

            // Netznormale zeigt zur Seite der Spinne (wie in StartBuild).
            Vector3 toSpider = transform.position - web.Layout.Hub;
            _normal = web.Layout.Normal;
            if (Vector3.Dot(_normal, toSpider) < 0f)
            {
                _normal = -_normal;
            }

            if (_movement != null)
            {
                _movement.SetScripted(true);
            }

            // Kopf nach unten wie am Ende von BuildRoutine.
            Vector3 flatDown = -Vector3.up - _normal * Vector3.Dot(-Vector3.up, _normal);
            Quaternion hubRotation = flatDown.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(flatDown.normalized, _normal)
                : _currentRotation;

            transform.position = web.Layout.Hub + _normal * bodyOffset;
            transform.rotation = hubRotation;
            _currentRotation = hubRotation;

            web.SetProgress(OrbWeb.WebStage.Done, 1f);
            web.showAux = false;

            CurrentStage = OrbWeb.WebStage.Done;
            IsBuilding = false;
            _sittingInWeb = true;
        }

        /// <summary>Läuft linear zum Ziel (Position + sanfte Drehung) in der Dauer.</summary>
        public IEnumerator WalkTo(Vector3 target, float duration, Quaternion targetRotation)
        {
            duration = Mathf.Max(duration, 0.001f);
            Vector3 from = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector3.Lerp(from, target, k) + _normal * bodyOffset;
                _currentRotation = Quaternion.Slerp(_currentRotation, targetRotation, 1f - Mathf.Exp(-10f * Time.deltaTime));
                transform.rotation = _currentRotation;
                yield return null;
            }

            transform.position = target + _normal * bodyOffset;
            _currentRotation = targetRotation;
            transform.rotation = _currentRotation;
        }

        /// <summary>Dreh-Ziel Richtung Punkt (in der Netzebene), sonst aktuelle Rotation.</summary>
        private Quaternion LookTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            Vector3 flat = dir - _normal * Vector3.Dot(dir, _normal);
            if (flat.sqrMagnitude < 1e-6f)
            {
                return _currentRotation;
            }

            return Quaternion.LookRotation(flat.normalized, _normal);
        }

        /// <summary>Gesamtlänge einer Punktfolge.</summary>
        private static float PathLength(Vector3[] path)
        {
            float total = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                total += (path[i + 1] - path[i]).magnitude;
            }

            return total;
        }

        /// <summary>
        /// Liefert Segmentindex <paramref name="index"/> und Anteil
        /// <paramref name="fraction"/> an der Gesamtlänge <paramref name="distance"/>.
        /// </summary>
        private static void PointOnPath(Vector3[] path, float distance, out int index, out float fraction)
        {
            float acc = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float seg = (path[i + 1] - path[i]).magnitude;
                if (distance <= acc + seg || i == path.Length - 2)
                {
                    index = i;
                    fraction = seg > 1e-6f ? Mathf.Clamp01((distance - acc) / seg) : 0f;
                    return;
                }

                acc += seg;
            }

            index = path.Length - 2;
            fraction = 1f;
        }

        /// <summary>Legt den BauFaden (gezo gener Faden) als Kind der Spinne an.</summary>
        private void CreateBauFaden(OrbWeb web)
        {
            if (_bauFaden != null)
            {
                return;
            }

            var go = new GameObject("BauFaden");
            go.transform.SetParent(transform, false);
            _bauFaden = go.AddComponent<LineRenderer>();
            _bauFaden.positionCount = 2;
            _bauFaden.useWorldSpace = true;
            _bauFaden.receiveShadows = false;
            _bauFaden.startWidth = web.threadWidth;
            _bauFaden.endWidth = web.threadWidth;
            LineRenderer source = web.GetComponentInChildren<LineRenderer>();
            if (source != null)
            {
                _bauFaden.sharedMaterial = source.sharedMaterial;
            }

            _bauFaden.enabled = false;
        }

        private void ShowBauFaden(Vector3 from, Vector3 to)
        {
            if (_bauFaden == null)
            {
                return;
            }

            _bauFaden.enabled = true;
            _bauFaden.SetPosition(0, from);
            _bauFaden.SetPosition(1, to);
        }

        private void HideBauFaden()
        {
            if (_bauFaden != null)
            {
                _bauFaden.enabled = false;
            }
        }
    }
}
