using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Bündelt alle generierten Meshes, Materialien und Terrain-Layer
    /// für die Urwald-Welt (10.1b1).
    /// </summary>
    [CreateAssetMenu(fileName = "JungleAssetLibrary", menuName = "Spinnenspiel/Jungle Asset Library")]
    public class JungleAssetLibrary : ScriptableObject
    {
        public Mesh[] trunks;
        public Mesh[] crownsNear;
        public Mesh[] crownsFar;
        public Material bark;
        public Material leaves;

        public Mesh bush;
        public Mesh fern;
        public Mesh reed;
        public Material bushMat;
        public Material fernMat;
        public Material reedMat;

        public Mesh[] logs;
        public Mesh[] rocks;
        public Material logMat;
        public Material rockMat;

        /// <summary>Reihenfolge: Earth, Grass, Mud, Rock.</summary>
        public TerrainLayer[] terrainLayers;

        public Material water;
    }
}
