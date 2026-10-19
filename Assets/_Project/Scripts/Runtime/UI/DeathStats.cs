using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// K6 Statistik beim Tod: Zaehnsystem die Ueberlebenszeit, gefressene Insekten und
    /// gebauten Netze. Shows am Tod a screen with the values plus the best values from
    /// earlier partien (persisted together with J6 in the savegame file).
    /// HUD-Stil like PreyHint (J2): OnGUI directly, no own Canvas.
    /// </summary>
    public class DeathStats : MonoBehaviour
    {
        /// <summary>The pure logic (K5-Lehre: testable model class).</summary>
        public readonly PartieStatsModel Stats = new PartieStatsModel();

        private SpiderHunger _hunger;
        private SpiderGrowth _growth;
        private SaveGame _save;
        private SpiderEnergy _energy;
        private int _seenBuiltWebs;
        private float _seenDamageTime = -1f;

        public bool DeathTriggered { get => Stats.DeathShown; }

        private void Awake()
        {
            _hunger = transform.GetComponent<SpiderHunger>();
            _growth = transform.GetComponent<SpiderGrowth>();
            _save = transform.GetComponent<SaveGame>();
            _energy = transform.GetComponent<SpiderEnergy>();
        }

        private void Update()
        {
            // Lazy re-acquire: Tests (and hot-reloads) may add SpiderHunger/Growth/SaveGame
            // after Awake already ran; GetComponent is only paid while a ref is still missing.
            if (_hunger == null)
            {
                _hunger = transform.GetComponent<SpiderHunger>();
            }
            if (_growth == null)
            {
                _growth = transform.GetComponent<SpiderGrowth>();
            }
            if (_save == null)
            {
                _save = transform.GetComponent<SaveGame>();
            }
            if (_energy == null)
            {
                _energy = transform.GetComponent<SpiderEnergy>();
            }

            // While alive, mirror the best values from the save (J6-file) into the model.
            if (_save != null && !Stats.DeathShown)
            {
                Stats.LoadBests(_save.BestSurvivalTime, _save.BestEaten, _save.BestWebs);
            }

            // Survival time: every second alive (death is SpiderHunger full).
            Stats.TickAlive(Time.deltaTime);

            // Eaten insects: read from the single source of truth (K5 counter).
            // S5: feed the model per NEW insect so the day-counters stay in sync with the totals.
            if (_growth != null)
            {
                while (Stats.EatenCount < _growth.EatenCount)
                {
                    Stats.OnInsectEaten();
                }
                if (Stats.EatenCount > _growth.EatenCount)
                {
                    Stats.EatenCount = _growth.EatenCount;
                }
            }

            // Built webs: count newly built webs once (OrbWeb.All is the registry).
            var built = 0;
            foreach (OrbWeb web in OrbWeb.All)
            {
                if (web.IsBuilt)
                {
                    built++;
                }
            }
            if (built > _seenBuiltWebs)
            {
                for (var i = _seenBuiltWebs; i < built; i++)
                {
                    Stats.OnWebBuilt();
                }
                _seenBuiltWebs = built;
            }

            // T11: feed damage events from SpiderEnergy (last source + when) into the model,
            // translated into survival-time so the 10 s window matches real time.
            if (_energy != null && _energy.LastDamageTime > _seenDamageTime)
            {
                var elapsedSinceHit = UnityEngine.Time.time - _energy.LastDamageTime;
                Stats.OnDamage(_energy.LastDamageKind, Stats.SurvivalTime - elapsedSinceHit);
                _seenDamageTime = _energy.LastDamageTime;
            }

            // Tod: Hunger full (SpiderHunger 1.0) ODER Energie leer (J2c, SpiderEnergy 0).
            if ((_hunger != null && _hunger.Hunger >= 0.999f)
                || (_energy != null && _energy.Energy <= 0f))
            {
                Stats.ReportDeath();
            }

            // J2c: on the death screen, N starts a fresh partie (counters reset, bests kept).
            if (Stats.DeathShown)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.nKey.wasPressedThisFrame)
                {
                    StartNewPartie();
                }
            }
        }

        /// <summary>J2c: reset the counters and let the spider live again — energy back, hunger cleared.</summary>
        private void StartNewPartie()
        {
            Stats.StartNewPartie();
            if (_energy != null)
            {
                _energy.ResetEnergy();
            }
            if (_hunger != null)
            {
                _hunger.Hunger = 0f;
            }
            _seenBuiltWebs = 0;
            _seenDamageTime = -1f;
        }

        private void OnGUI()
        {
            if (!Stats.DeathShown)
            {
                return;
            }

            // Dark panel behind the text (readable over any sky).
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(16f, Screen.height * 0.28f, 420f, 96f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Tick rows: one white tick per eaten insect and per built web (visible count).
            foreach (var t in Stats.DeathScreenTicks(Screen.width, Screen.height))
            {
                GUI.DrawTexture(new Rect(t.x, t.y, t.z, t.w), Texture2D.whiteTexture);
            }

            // The exact line from the model (single source of truth for UI and tests).
            GUI.color = new Color(1f, 0.92f, 0.55f, 0.95f);
            GUI.Label(new Rect(24f, Screen.height * 0.30f, 620f, 26f), Stats.FormatLine());
            GUI.color = Color.white;

            // Next actions (Step 1: "Neue Partie" and "Menue").
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(24f, Screen.height * 0.30f + 64f, 620f, 24f), "[N] Neue Partie   [M] Menue");
            GUI.color = Color.white;
        }
    }
}
