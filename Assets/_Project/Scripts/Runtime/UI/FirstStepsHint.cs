using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// S9 Einstieg: thin glue around FirstStepsHintModel. Polls the situation each frame
    /// (movement input, jump, silk shot, web-built timer, caught insect in a web), feeds
    /// the pure model and shows the still-un-used hints as small lines under the help box.
    /// The used-hint bitmask travels with GameSettings ("Hinweise aus" -> nothing shows).
    /// </summary>
    public class FirstStepsHint : MonoBehaviour
    {
        private readonly FirstStepsHintModel Model = new FirstStepsHintModel();

        private SpiderInput _input;
        private SpiderSilk _silk;
        private GameSettings _settings;

        /// <summary>Builder/Test: restore the persisted bitmask of used hints.</summary>
        public void SetUsedMask(int mask)
        {
            Model.LoadUsedMask(mask);
        }

        private void Start()
        {
            if (_settings == null)
            {
                _settings = transform.GetComponent<GameSettings>();
            }
            if (_settings != null)
            {
                SetUsedMask(_settings.FirstStepsUsedMask);
                Model.HintsEnabled = _settings.EintranceHintsEnabled;
            }
        }

        private void Update()
        {
            if (_input == null)
            {
                _input = transform.GetComponent<SpiderInput>();
            }
            if (_silk == null)
            {
                _silk = transform.GetComponent<SpiderSilk>();
            }

            // Feed the situation into the pure model.
            var hasBuiltWeb = false;
            foreach (OrbWeb web in OrbWeb.All)
            {
                if (web != null && web.IsBuilt)
                {
                    hasBuiltWeb = true;
                    break;
                }
            }
            Model.AdvanceTime(Time.deltaTime, hasBuiltWeb);

            // A caught insect hanging in a web triggers the collect-hint situation.
            foreach (FlyingInsect f in Object.FindObjectsByType<FlyingInsect>(FindObjectsSortMode.None))
            {
                if (f.State == FlyingInsect.InsectState.Caught)
                {
                    Model.NotifyCaughtInsect();
                    break;
                }
            }

            // First use of a control marks its hint used (hint vanishes forever).
            if (_input != null)
            {
                if (_input.Move.magnitude > 0.1f)
                {
                    Model.MarkUsed(FirstStepsHintModel.HintId.Move);
                }
                if (_input.JumpPressed)
                {
                    Model.MarkUsed(FirstStepsHintModel.HintId.Jump);
                }
                if (_input.SilkPressed)
                {
                    Model.MarkUsed(FirstStepsHintModel.HintId.Silk);
                }
            }
            if (_silk != null && _silk.IsSpinning)
            {
                // The spinning pose means the player used the silk control.
                Model.MarkUsed(FirstStepsHintModel.HintId.Silk);
            }
            foreach (OrbWeb web in OrbWeb.All)
            {
                if (web != null && web.IsBuilt)
                {
                    Model.MarkUsed(FirstStepsHintModel.HintId.Net);
                }
            }
            var growth = transform.GetComponent<SpiderGrowth>();
            if (growth != null && growth.EatenCount > 0)
            {
                Model.MarkUsed(FirstStepsHintModel.HintId.Collect);
            }

            // Persist progress so a reload keeps used hints gone.
            if (_settings != null && _settings.FirstStepsUsedMask != Model.UsedMask())
            {
                _settings.SetFirstStepsUsed(Model.UsedMask());
            }
        }

        /// <summary>Public so the PlayMode-Test can read what OnGUI shows this frame.</summary>
        public bool GetAnyVisible() => Model.VisibleCount() > 0;

        private void OnGUI()
        {
            var shown = 0;
            foreach (FirstStepsHintModel.HintId id in FirstStepsHintModel.AllHints)
            {
                if (!Model.IsVisible(id))
                {
                    continue;
                }
                var y = Screen.height * 0.55f + shown * 20f;
                GUI.color = new Color(1f, 1f, 0.7f, 0.95f);
                GUI.Label(new Rect(24f, y, 320f, 18f),
                    new GUIContent(FirstStepsHintModel.HintText[(int)id]));
                shown++;
            }
            GUI.color = Color.white;
        }
    }
}
