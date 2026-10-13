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

        /// <summary>Blender-Baumfarne, 3 Varianten (index 0..2), je 2 Submeshes: 0 = Stamm, 1 = Blatt.</summary>
        public Mesh[] treeFerns;
        public Material treeFernLeafMat;
        public Material treeFernStemMat;

        /// <summary>Scan-Bodendecker (Gras/Kraut), gleiche Länge und Reihenfolge wie die Materialien.</summary>
        public Mesh[] groundCover;
        public Material[] groundCoverMats;

        /// <summary>U4: Farn-Scatter-Patch (Blender scatter.py, determinist Poisson-Disc).
        /// fernScatterMeshes = 245 Instance-Meshes (2 Submeshes je: 0 = Stem, 1 = Leaf),
        /// fernScatterXZ = XZ-Positions per Instance (y=0, Y comes from SampleHeight at runtime),
        /// fernScatterCount = Anzahl Instances im Patch.</summary>
        public Mesh[] fernScatterMeshes;
        public Vector3[] fernScatterXZ;
        public int fernScatterCount;
        public Material fernScatterLeafMat;
        public Material fernScatterStemMat;
        /// <summary>U4: Index of the Leaf-Submesh within each Instance-Mesh (measured, 0 or 1).</summary>
        public int fernScatterLeafSubmesh;

        /// <summary>Poly-Haven-Baumes. Index 0..4 = Baumart (Jacaranda, island_tree_01..03, tree_small_02).
        /// phTreeLod0/1[i]=LOD-Mesh, phTreeWoodMats[i]/phTreeLeafMats[i]=Holz/Blätter-Material.
        /// phTreeScales[i] = Worldscale so das Jacaranda ca. 25-35 m high. (Nested arrays not serialized by Unity!)</summary>
        public Mesh[] phTreeLod0;
        public Mesh[] phTreeLod1;
        public Material[] phTreeWoodMats;
        public Material[] phTreeLeafMats;
        public float[] phTreeScales;
        /// <summary>PH-Tree FBX-Materialslotnames per Variant, '|' -separators (index = Submesh,
        /// order = LOD0-MeshRenderer.sharedMaterials). Nested arrays NOT serialized by Unity!</summary>
        public string[] phTreeSlotNames;

        public Mesh[] logs;
        public Mesh[] rocks;
        public Material logMat;
        public Material rockMat;

        /// <summary>Reihenfolge: Earth, Grass, Mud, Rock.</summary>
        public TerrainLayer[] terrainLayers;

        public Material water;
    }
}
