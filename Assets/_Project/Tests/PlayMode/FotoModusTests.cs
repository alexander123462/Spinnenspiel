using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// FOTO1: Prueft den Foto-Modus. Ohne -fotopfad bleibt alles beim alten (kein Runner,
    /// Welt startet wie bisher); mit Pfad setzt TryInstall den Welt-Seed und legt den Runner an.
    /// </summary>
    public class FotoModusTests
    {
        [TearDown]
        public void TearDown()
        {
            var go = GameObject.Find("FotoModus");
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
            JungleWorldGenerator.NextSeed = 0;
        }

        [Test]
        public void FotoModus_OhneFotopfad_BleibtInaktiv()
        {
            Assert.IsFalse(FotoModus.TryInstall("", FotoModus.DefaultSeed, 0f, 25f),
                "Ohne Fotopfad darf der Foto-Modus nicht starten.");
            Assert.IsNull(Object.FindFirstObjectByType<FotoModusRunner>(),
                "Ohne Fotopfad darf kein Runner existieren.");
            Assert.AreEqual(0, JungleWorldGenerator.NextSeed,
                "Ohne Fotopfad darf der Seed nicht gesetzt werden.");
        }

        [Test]
        public void FotoModus_MitFotopfad_SetztSeedUndRunner()
        {
            Assert.IsTrue(FotoModus.TryInstall("Logs/ci/foto_test.png", 4242, 35f, 90f));
            Assert.AreEqual(4242, JungleWorldGenerator.NextSeed,
                "Seed muss vor dem Szenenladen gesetzt sein.");
            var runner = Object.FindFirstObjectByType<FotoModusRunner>();
            Assert.IsNotNull(runner, "Runner muss angelegt sein.");
        }

        [Test]
        public void FotoModus_Standardwerte_SindWieImBacklog()
        {
            Assert.AreEqual(4242, FotoModus.DefaultSeed);
            Assert.AreEqual(0f, FotoModus.DefaultPitch);
            Assert.AreEqual(25f, FotoModus.DefaultWarteS);
            // Unbekannte Flags liefern die Standardwerte zurueck.
            Assert.AreEqual(4242, FotoModus.GetArgInt("-fotoseed_gibt_es_nicht", 4242));
            Assert.AreEqual(25f, FotoModus.GetArgFloat("-fotowarte_gibt_es_nicht", 25f));
        }
    }
}
