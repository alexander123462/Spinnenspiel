using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Ermittelt den Oberflächentyp an einem Raycast-Treffer.
    /// Reihenfolge: Terrain-Splatmap (TerrainSurfaceMap) -> SurfaceTag am Kollider/Elternobjekt -> Ground.
    /// </summary>
    public static class SurfaceQuery
    {
        public static SurfaceType FromHit(RaycastHit hit)
        {
            Collider collider = hit.collider;
            if (collider == null)
            {
                return SurfaceType.Ground;
            }

            if (collider is TerrainCollider)
            {
                TerrainSurfaceMap map = collider.GetComponent<TerrainSurfaceMap>();
                if (map != null)
                {
                    return map.Sample(hit.point);
                }
            }

            SurfaceTag tag = collider.GetComponentInParent<SurfaceTag>();
            return tag != null ? tag.surfaceType : SurfaceType.Ground;
        }
    }
}
