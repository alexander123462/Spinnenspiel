using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Spinnenspiel
{
    /// <summary>
    /// J4: Ein Vogel kreist über die Spinne und greift ihr, wenn sie laengere Zeit im
    /// Freien unter offenem Himmel sitzt. Ist ein Strahl senkrecht upward an her
    /// Geometrie-Objekt (Blaehter, Dickicht) frei ODER die Spinne in her own Netz sits,
    /// is she safe. After 8 s exposed a pre-warning fires (shadow + cry), then the
    /// attack 3 s later. Reaching cover before the attack withdraws the bird.
    /// </summary>
    public class BirdThreat : MonoBehaviour
    {
        [Header("Timing (s)")]
        [Tooltip("Exposure in the open that triggers the pre-warning.")]
        [SerializeField] private float warnTime = 8f;
        [Tooltip("Delay from the pre-warning to the strike.")]
        [SerializeField] private float attackDelay = 3f;

        [Header("Sky ray")]
        [Tooltip("Start the up-ray this far above the body so the spider never hits itself (m).")]
        [SerializeField] private float rayHeadOffset = 0.15f;
        [Tooltip("How high to probe for open sky (m).")]
        [SerializeField] private float rayMaxHeight = 200f;

        /// <summary>Seconds the spider has sat exposed under open sky (reset on cover).</summary>
        public float ExposureSeconds;

        /// <summary>True while the spider currently sits exposed under open sky.</summary>
        public bool IsExposed;

        /// <summary>True once the pre-warning has fired in the current threat cycle.</summary>
        public bool Warned;

        /// <summary>True once the strike has landed in the current threat cycle.</summary>
        public bool Attacked;

        /// <summary>Fires when the strike lands (bird attacks).</summary>
        public UnityEvent AttackFired;

        /// <summary>Fires when the bird withdraws because the spider reached cover.</summary>
        public UnityEvent WithdrawnFired;

        private BirdThreatModel _model;
        private Transform _spiderTransform;
        private SpiderWebBuilder _webBuilder;

        private void OnEnable()
        {
            _model = new BirdThreatModel();
            // S1: danger frequency scales with difficulty (Hart reacts faster -> shorter warn time).
            float threatMultiplier = GameDifficulty.BirdThreatMultiplier();
            _model.warnTime = threatMultiplier > 0f ? warnTime / threatMultiplier : warnTime;
            _model.attackDelay = attackDelay;
            _spiderTransform = transform;
            _webBuilder = GetComponent<SpiderWebBuilder>();
        }

        private void Update()
        {
            if (_spiderTransform == null || _model == null)
            {
                return;
            }

            bool sittingInWeb = _webBuilder != null && _webBuilder.IsSittingInWeb;
            IsExposed = !sittingInWeb && HasOpenSkyAbove();

            bool wasAttacked = _model.Attacked;
            bool wasWarned = _model.Warned;
            _model.Tick(IsExposed, Time.deltaTime);

            ExposureSeconds = _model.ExposureSeconds;
            Warned = _model.Warned;
            Attacked = _model.Attacked;

            if (!wasAttacked && _model.Attacked)
            {
                AttackFired.Invoke();
            }
            if (wasWarned && !_model.Warned)
            {
                WithdrawnFired.Invoke();
            }
        }

        /// <summary>True when a vertical ray above the spider reaches open sky (no cover).</summary>
        private bool HasOpenSkyAbove()
        {
            Vector3 origin = _spiderTransform.position + Vector3.up * rayHeadOffset;
            return !Physics.Raycast(origin, Vector3.up, rayMaxHeight);
        }
    }
}
