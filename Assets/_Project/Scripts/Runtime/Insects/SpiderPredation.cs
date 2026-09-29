using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Beute 2 (7.3a3): Die Spinne merkt sich Beute im eigenen Netz, läuft zu ihr,
    /// wickelt sie im Kokon ein, trägt sie zur Nabe zurück und frisst sie.
    /// </summary>
    public class SpiderPredation : MonoBehaviour
    {
        /// <summary>Multipliziert alle Jagd-Dauern (kleiner = schneller, für Tests).</summary>
        public float durationScale = 1f;

        /// <summary>True, solange eine Jagd läuft.</summary>
        public bool IsHunting { get; private set; }

        /// <summary>Aktuelle Beute im Netz (null, wenn keine).</summary>
        public FlyingInsect Target { get; private set; }

        /// <summary>Wird ausgelöst, wenn die Beute gefressen (und entfernt) wurde.</summary>
        public event System.Action<FlyingInsect> PreyEaten;

        private SpiderWebBuilder _builder;
        private SpiderSilk _silk;
        private SpiderHunger _hunger;

        private void Awake()
        {
            _builder = GetComponent<SpiderWebBuilder>();
            _silk = GetComponent<SpiderSilk>();
            _hunger = GetComponent<SpiderHunger>();
        }

        private void OnEnable()
        {
            FlyingInsect.AnyCaught += OnInsectCaught;
        }

        private void OnDisable()
        {
            FlyingInsect.AnyCaught -= OnInsectCaught;
        }

        /// <summary>Fang-Handler: Beute nur merken, wenn ich in meinem eigenen Netz sitze.</summary>
        private void OnInsectCaught(FlyingInsect insect, OrbWeb web)
        {
            if (_builder == null || IsHunting)
            {
                return;
            }
            if (!_builder.IsSittingInWeb || web != _builder.Web)
            {
                return;
            }
            Target = insect;
        }

        private void Update()
        {
            // Beute entkommen oder entfernt: Ziel verwerfen (Jagd selbst läuft weiter).
            if (Target != null && !IsHunting
                && (Target.CaughtIn == null || Target.State != FlyingInsect.InsectState.Caught))
            {
                Target = null;
            }

            if (Target == null || IsHunting || _builder == null || !_builder.IsSittingInWeb)
            {
                return;
            }

            // In der Nabe sitzt: Spinne dreht sich zur Beute.
            Quaternion facing = _builder.FacingRotation(Target.transform.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, 6f * Time.deltaTime);

            // E (Tastatur) oder X (Gamepad): Jagd starten.
            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
            if (pressed)
            {
                StartHunt(Target);
            }
        }

        private void OnGUI()
        {
            if (Target == null || IsHunting)
            {
                return;
            }
            GUI.Label(new Rect(Screen.width / 2f - 80f, Screen.height - 36f, 160f, 24f),
                "E / X: Beute holen");
        }

        /// <summary>Jagd starten (auch aufrufbar aus Tests). Läuft nur einmal gleichzeitig.</summary>
        public void StartHunt(FlyingInsect prey)
        {
            if (prey == null || IsHunting || _builder == null || _builder.Web == null)
            {
                return;
            }
            StartCoroutine(Hunt(prey));
        }

        /// <summary>
        /// Jagd: zur Beute laufen, einwickeln, Biss, zurück zur Nabe tragen, fressen.
        /// Verschwindet die Beute, bricht die Jagd sauber ab und die Flags werden
        /// zurückgesetzt (Idempotenz durch Cleanup()).
        /// </summary>
        private IEnumerator Hunt(FlyingInsect prey)
        {
            IsHunting = true;
            _builder.SuppressRelease = true;
            prey.Hold();

            Vector3 hub = _builder.Web.Layout.Hub;
            Vector3 p = prey.CaughtPoint;

            // 1) Zur Beute laufen (Ziel leicht Richtung Nabe; Body-Offset addiert WalkTo selbst).
            Vector3 toPrey = p + (hub - p).normalized * 0.04f;
            float d1 = Vector3.Distance(transform.position, toPrey);
            yield return _builder.WalkTo(toPrey, d1 / 0.5f * durationScale, _builder.FacingRotation(p - hub));
            if (prey == null)
            {
                Cleanup();
                yield break;
            }

            // 2) Einwickeln (2.5 s): Spinnpose halten, Kokon wächst, Netz vibriert mits.
            if (_silk != null)
            {
                _silk.BeginSpinningPose();
            }
            float wrapTime = 2.5f * durationScale;
            float t = 0f;
            float lastVibe = 0f;
            while (t < wrapTime && prey != null)
            {
                t += Time.deltaTime;
                if (_silk != null)
                {
                    _silk.UpdateSpinningPose(prey.transform.position);
                }
                prey.SetWrapProgress(Mathf.Clamp01(t / wrapTime));
                if (t - lastVibe >= 0.3f && _builder.Web != null)
                {
                    lastVibe = t;
                    _builder.Web.Vibrate(0.3f);
                }
                yield return null;
            }
            if (prey != null)
            {
                prey.SetWrapProgress(1f);
            }
            if (_silk != null)
            {
                _silk.EndSpinningPose();
            }
            if (prey == null)
            {
                Cleanup();
                yield break;
            }

            // 3) Biss: kurze Pause (die Beute ist im Kokon fest).
            yield return new WaitForSeconds(0.4f * durationScale);

            // 4) Zurücktragen: Beute vor den Kopf (an die Cheliceren) heften, zur Nabe laufen.
            prey.transform.SetParent(transform, true);
            prey.transform.localPosition = new Vector3(0f, -0.01f, 0.07f);
            float d2 = Vector3.Distance(transform.position, hub);
            yield return _builder.WalkTo(hub, d2 / 0.5f * durationScale, _builder.FacingRotation(Vector3.down));
            if (prey == null)
            {
                Cleanup();
                yield break;
            }

            // 5) Fressen (4 s): Beute schrumpft, Hunger sinkt, Beute wird entfernt.
            float eatTime = 4f * durationScale;
            t = 0f;
            while (t < eatTime && prey != null)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(t / eatTime));
                prey.transform.localScale = Vector3.one * s;
                yield return null;
            }

            if (prey != null)
            {
                if (_hunger != null)
                {
                    _hunger.Eat(0.35f);
                }
                System.Action<FlyingInsect> handler = PreyEaten;
                prey.Remove();
                if (handler != null)
                {
                    handler(prey);
                }
            }

            Cleanup();
        }

        /// <summary>Setzt alle Jagd-Flags zurück (Mehrfachaufruf harmlos).</summary>
        private void Cleanup()
        {
            Target = null;
            IsHunting = false;
            if (_builder != null)
            {
                _builder.SuppressRelease = false;
            }
            if (_silk != null && _silk.IsSpinning)
            {
                _silk.EndSpinningPose();
            }
        }
    }
}
