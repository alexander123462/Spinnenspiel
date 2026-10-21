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

        /// <summary>A1: Busche-Varianten (Scan shrub_02/03/04), same length/order as the materials.
        /// bush/bushMat stay the first variant so older code keeps working.</summary>
        public Mesh[] bushVariants;
        public Material[] bushVariantMats;

        /// <summary>Blender-Riesenbläter, 3 Varianten (index 0..2). Je FBX 2 Material-Slots -> 2 Submeshes:
        /// Leaf (Cutout, index = giantLeafLeafSubmesh) + Stem (opaque Bark). giantLeafMats = Blatt-Material,
        /// giantLeafStemMats = Stemm-Material, same length/order. Submesh-Order measured at build (not a Blender contract).</summary>
        public Mesh[] giantLeaves;
        public Material[] giantLeafMats;
        public Material[] giantLeafStemMats;
        public int giantLeafLeafSubmesh = 1;

        /// <summary>Blender-Baumfarne, 3 Varianten (index 0..2), je 2 Submeshes: 0 = Stamm, 1 = Blatt.</summary>
        public Mesh[] treeFerns;
        public Material treeFernLeafMat;
        public Material treeFernStemMat;

        // Grundfarn (Blender fern_1..5.fbx, 5 Varianten): each FBX = 1 Mesh with 2 submeshes
        // (0 = Stem, 1 = Leaf, order measured). Ground fern is real geometry -> OPAQUE, no Cutout.
        public Mesh[] groundFerns;
        public Material groundFernLeafMat;
        public Material groundFernStemMat;
        public int groundFernLeafSubmesh;

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
        /// <summary>U4: Side length (m) of the square scatter patch (measured from mesh bounds).</summary>
        public float fernScatterPatchSize;

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

        /// <summary>WALD7: ONE shared Moos-Material (MAT_Moos) for all shells/cards — no per-object mats.</summary>
        public Material moosMat;
        /// <summary>HDRP/TerrainLit-Material fuer das Gelaende (sonst Fehler-Shader im gebauten Spiel).</summary>
        public Material terrainMat;

        /// <summary>Reihenfolge: Earth, Grass, Mud, Rock.</summary>
        public TerrainLayer[] terrainLayers;

        public Material water;
    }
}
