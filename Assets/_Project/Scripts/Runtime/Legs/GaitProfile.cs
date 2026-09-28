using System;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Parameter einer Gangart. Mehrere Profile (Krabbeln, Laufen, Sprint, Klettern ...)
    /// können zur Laufzeit über SpiderGaitController.SetProfile gewechselt werden.
    /// </summary>
    [Serializable]
    public struct GaitProfile
    {
        public string name;

        [Tooltip("Schrittdauer (s) bei sehr langsamer Bewegung.")]
        public float slowStepDuration;

        [Tooltip("Schrittdauer (s) bei Referenzgeschwindigkeit.")]
        public float fastStepDuration;

        [Tooltip("Geschwindigkeit (m/s), bei der fastStepDuration erreicht wird.")]
        public float referenceSpeed;

        [Tooltip("Minimaler Abstand (m) zwischen Fuß und Zielposition, ab dem ein Schritt ausgelöst wird.")]
        public float minStepDistance;

        [Tooltip("Schritt-Schwelle = max(minStepDistance, Geschwindigkeit * Schrittdauer * strideFactor).")]
        public float strideFactor;

        [Tooltip("Wie weit der Fuß vor der Ruheposition aufgesetzt wird, in Schrittdauern * Geschwindigkeit.")]
        public float leadFactor;

        [Tooltip("Grundhöhe des Schrittbogens (m).")]
        public float stepHeight;

        [Tooltip("Maximale Anzahl gleichzeitig schwingender Beine.")]
        public int maxSimultaneousSteps;

        [Tooltip("Verzögerung je Beinpaar (Anteil der Schrittdauer), Welle von hinten nach vorne.")]
        public float waveDelay;

        /// <summary>Alternierender Tetrapoden-Gang, Standard für normales Laufen und Sprint.</summary>
        public static GaitProfile Walk => new GaitProfile
        {
            name = "Walk",
            slowStepDuration = 0.2f,
            fastStepDuration = 0.09f,
            referenceSpeed = 1.2f,
            minStepDistance = 0.03f,
            strideFactor = 0.8f,
            leadFactor = 0.5f,
            stepHeight = 0.03f,
            maxSimultaneousSteps = 4,
            waveDelay = 0.08f
        };

        /// <summary>Vorsichtiges Krabbeln: langsamere, höhere Schritte, max. 2 Beine gleichzeitig.</summary>
        public static GaitProfile Crawl => new GaitProfile
        {
            name = "Crawl",
            slowStepDuration = 0.28f,
            fastStepDuration = 0.16f,
            referenceSpeed = 0.6f,
            minStepDistance = 0.025f,
            strideFactor = 0.7f,
            leadFactor = 0.5f,
            stepHeight = 0.04f,
            maxSimultaneousSteps = 2,
            waveDelay = 0.1f
        };
    }
}
