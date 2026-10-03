using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Sicherheitsfaden (Dragline): Beim Laufen setzt die Spinne in Abständen Anker
    /// auf die Oberfläche und klebt sie mit kleinen Haftscheiben fest.
    /// Der zuletzt gelegte Faden wird als LineRenderer vom letzten Anker bis zu den
    /// Spinnwarzen gezeichnet. Im Faden-Modus ist die Dragline ausgeblendet; beim
    /// Verlassen des Fadens startet der Anker an der neuen Position neu (ResetAnchor).
    /// </summary>
    public class SpiderDragline : MonoBehaviour
    {
        /// <summary>Abstand (m), nach dem ein neuer Anker gesetzt wird.</summary>
        [SerializeField] private float anchorSpacing = 0.6f;

        /// <summary>Lesezugriff auf <see cref="anchorSpacing"/> (z. B. für Tests).</summary>
        public float AnchorSpacing => anchorSpacing;

        /// <summary>Maximalzahl sichtbarer Haftscheiben (älteste werden entfernt).</summary>
        [SerializeField] private int maxDiscs = 30;

        /// <summary>Deckkraft (0..1) der Draglinen-Farbe.</summary>
        [Range(0f, 1f)] [SerializeField] private float alpha = 0.35f;

        /// <summary>Linienbreite (m).</summary>
        [SerializeField] private float width = 0.0012f;

        /// <summary>Position des letzten Ankers (Welt).</summary>
        public Vector3 LastAnchor { get; private set; }

        /// <summary>Flächennormale am letzten Anker.</summary>
        public Vector3 LastAnchorNormal { get; private set; } = Vector3.up;

        /// <summary>Anzahl gesetzter Anker (inkl. Erstanker).</summary>
        public int AnchorCount { get; private set; }

        /// <summary>Dragline-Darstellung (letzter Anker → Spinnwarzen).</summary>
        public LineRenderer Line { get; private set; }

        private SpiderSilk silk;
        private SpiderMovement movement;
        private readonly List<GameObject> discs = new List<GameObject>();
        private Material draglineMaterial;
        private bool wasOnThread;

        private void Awake()
        {
            silk = GetComponent<SpiderSilk>();
            movement = GetComponent<SpiderMovement>();

            Line = gameObject.AddComponent<LineRenderer>();
            Line.positionCount = 2;
            Line.useWorldSpace = true;
            Line.startWidth = width;
            Line.endWidth = width;
            Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Line.receiveShadows = false;
            Line.material = Material;
            Line.enabled = false;
        }

        private void Start()
        {
            // Ersten Anker an der aktuellen Position setzen.
            Vector3 n = movement != null ? movement.SurfaceNormal : Vector3.up;
            PlaceAnchor(transform.position, n);
        }

        private void LateUpdate()
        {
            if (movement == null)
            {
                return;
            }

            bool onThread = movement.CurrentThread != null;
            if (onThread)
            {
                // Faden-Modus: Dragline ausblenden.
                if (Line != null)
                {
                    Line.enabled = false;
                }

                wasOnThread = true;
                return;
            }

            // Gerade den Faden verlassen: Anker an der neuen Position neu setzen.
            if (wasOnThread)
            {
                wasOnThread = false;
                ResetAnchor(transform.position, movement.SurfaceNormal);
            }

            // Beim Laufen: neuen Anker + Haftscheibe, sobald der Abstand überschritten ist.
            if (movement.IsAttached &&
                (transform.position - LastAnchor).sqrMagnitude > anchorSpacing * anchorSpacing)
            {
                PlaceAnchor(transform.position, movement.SurfaceNormal);
            }

            if (Line != null)
            {
                Line.enabled = true;
                Line.SetPosition(0, LastAnchor);
                Vector3 end = silk != null && silk.spinnerets != null ? silk.spinnerets.position : transform.position;
                Line.SetPosition(1, end);
            }
        }

        /// <summary>Setzt den Anker manuell (z. B. beim Verlassen des Seidenfadens).</summary>
        public void ResetAnchor(Vector3 p, Vector3 n)
        {
            PlaceAnchor(p, n);
        }

        /// <summary>Neuen Anker setzen, Haftscheibe dort platzieren, Limit ältester Scheiben beachten.</summary>
        private void PlaceAnchor(Vector3 p, Vector3 n)
        {
            LastAnchor = p;
            LastAnchorNormal = n;
            AnchorCount++;
            CreateDisc(p, n);
        }

        /// <summary>Kleine abgeflachte Haftscheibe (Sphere ohne Collider) nach der Flächennormale ausgerichtet.</summary>
        private void CreateDisc(Vector3 center, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            disc.name = "DraglineDisc";
            var collider = disc.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            disc.transform.SetParent(transform, false);
            disc.transform.position = center + n * 0.001f;
            disc.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            disc.transform.localScale = new Vector3(0.008f, 0.0015f, 0.008f);
            var renderer = disc.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = Material;
            }

            discs.Add(disc);
            while (discs.Count > maxDiscs)
            {
                Destroy(discs[0]);
                discs.RemoveAt(0);
            }
        }

        /// <summary>Eigene Material-Instanz für die Dragline: weiß mit <see cref="alpha"/> (wie SilkThread, transparent).</summary>
        private Material Material
        {
            get
            {
                if (draglineMaterial == null)
                {
                    var mat = Spinnenspiel.Rendering.PipelineMaterials.Unlit(new Color(1f, 1f, 1f, alpha));
                    Spinnenspiel.Rendering.PipelineMaterials.SetTransparent(mat, true);
                    draglineMaterial = mat;
                }

                return draglineMaterial;
            }
        }
    }
}
