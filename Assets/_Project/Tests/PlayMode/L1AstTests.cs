using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>L1: Die Spinne darf auf einem niedrigen Ast (Gelaende unter ihr) nicht stehen bleiben, sondern muss vorwaerts und rueckwaerts laufen.</summary>
    public class L1AstTests
    {
        private static IEnumerator Lauf(float astHoehe, float radius, Vector2 eingabe, string name)
        {
            SceneManager.LoadScene("Testwelt");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            var mv = spider.GetComponent<SpiderMovement>();
            var legs = spider.GetComponentsInChildren<SpiderLeg>();

            Vector3 spawn = WorldLayout.SpawnPoint;
            RaycastHit g;
            Assert.IsTrue(Physics.Raycast(spawn + Vector3.up * 30f, Vector3.down, out g, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            Vector3 mitte = g.point + Vector3.up * astHoehe;

            // Waagerechter Ast (Kapsel entlang X), 12 m lang, Mitte ueber dem Spawn.
            var ast = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ast.name = "TestAst";
            ast.transform.position = mitte;
            ast.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            ast.transform.localScale = new Vector3(radius * 2f, 6f, radius * 2f);
            ast.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;

            Vector3 start = mitte + Vector3.left * 3f + Vector3.up * (radius + 0.02f);
            Vector3 fwd = Vector3.right;
            if (eingabe.y < 0f) { start = mitte + Vector3.right * 3f + Vector3.up * (radius + 0.02f); fwd = Vector3.left; }
            mv.Teleport(start, Quaternion.LookRotation(fwd, Vector3.up));
            foreach (var leg in legs) leg.SnapToGround();
            yield return new WaitForSeconds(0.4f);

            Vector3 p0 = spider.transform.position;
            mv.SetExternalMoveLocal(new Vector2(0f, 1f)); // vorwaerts relativ zur Blickrichtung
            float t = 0f;
            while (t < 4f) { t += Time.deltaTime; yield return null; }
            mv.SetExternalMoveLocal(null);
            float weg = Vector3.Distance(new Vector3(spider.transform.position.x, 0f, spider.transform.position.z), new Vector3(p0.x, 0f, p0.z));
            Debug.Log("[L1] " + name + " hoehe=" + astHoehe + " radius=" + radius + " weg=" + weg.ToString("F2") + " y=" + spider.transform.position.y.ToString("F2") + " attached=" + mv.IsAttached);
            Assert.Greater(weg, 1.5f, "L1 " + name + ": Spinne kommt auf dem Ast nicht voran (weg=" + weg.ToString("F2") + " m in 4 s).");
            Object.Destroy(ast);
        }

        [UnityTest] public IEnumerator Ast_Niedrig_Vorwaerts() { yield return Lauf(0.5f, 0.25f, new Vector2(0f, 1f), "niedrig 0.5"); }
        [UnityTest] public IEnumerator Ast_Niedrig_Rueckwaerts() { yield return Lauf(0.5f, 0.25f, new Vector2(0f, -1f), "niedrig rueck"); }
        [UnityTest] public IEnumerator Ast_Sehr_Niedrig() { yield return Lauf(0.15f, 0.15f, new Vector2(0f, 1f), "sehr niedrig"); }
        [UnityTest] public IEnumerator Ast_Hoch() { yield return Lauf(2.5f, 0.25f, new Vector2(0f, 1f), "hoch 2.5"); }
    }
}
