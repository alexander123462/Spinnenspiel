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

        /// <summary>Blender-Riesenbläter, 3 Varianten (index 0..2), mit passendem Material auf demselben Index.</summary>
        public Mesh[] giantLeaves;
        public Material[] giantLeafMats;

        /// <summary>Blender-Baumfarne, 3 Varianten (index 0..2), je 2 Submeshes: 0 = Blatt, 1 = Stamm.</summary>
        public Mesh[] treeFerns;
        public Material treeFernLeafMat;
        public Material treeFernStemMat;

        /// <summary>Scan-Bodendecker (Gras/Kraut), gleiche Länge und Reihenfolge wie die Materialien.</summary>
        public Mesh[] groundCover;
        public Material[] groundCoverMats;

        public Mesh[] logs;
        public Mesh[] rocks;
        public Material logMat;
        public Material rockMat;

        /// <summary>Reihenfolge: Earth, Grass, Mud, Rock.</summary>
        public TerrainLayer[] terrainLayers;

        public Material water;
    }
}
