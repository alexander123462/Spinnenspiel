using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Spinnenspiel.Tests
{
    // F1: per-Instance-Farbvariation der Urwald (saettig, eintoenig-frei). Testet die
    // reine Math der Tint ohne Scene: Deterministic und near-white so ein MeshRenderer
    // mit MaterialPropertyBlock stay saettig statt ausgewaschen.
    [TestFixture]
    public class InstanceTintTests
    {
        [Test]
        public void Tint_IsDeterministic()
        {
            var a = Spinnenspiel.JungleWorldGenerator.ComputeTint(0.11f, 0.37f, 0.62f);
            var b = Spinnenspiel.JungleWorldGenerator.ComputeTint(0.11f, 0.37f, 0.62f);
            Assert.That(a.r, Is.EqualTo(b.r));
            Assert.That(a.g, Is.EqualTo(b.g));
            Assert.That(a.b, Is.EqualTo(b.b));
        }

        [Test]
        public void Tint_StaysNearWhite()
        {
            // Multiplicative near-white: alle Channels in [0.7..1.1]-Band, kein Wasching.
            // Lower bound = (1 - maxSatBlend) * minValFactor = 0.8 * 0.9 = 0.72.
            for (int i = 0; i < 24; i++)
            {
                float h = (i * 0.137f) % 1f;
                float s = (i * 0.419f) % 1f;
                float v = (i * 0.271f) % 1f;
                var c = Spinnenspiel.JungleWorldGenerator.ComputeTint(h, s, v);
                Assert.That(c.r, Is.InRange(0.7f, 1.1f));
                Assert.That(c.g, Is.InRange(0.7f, 1.1f));
                Assert.That(c.b, Is.InRange(0.7f, 1.1f));
            }
        }

        [Test]
        public void Tint_VariesHueAcrossInstances()
        {
            // Different hashes -> different tint (spread), not all equal.
            var distinct = new HashSet<Color>();
            for (int i = 0; i < 12; i++)
            {
                float h = (i * 0.37f) % 1f;
                var c = Spinnenspiel.JungleWorldGenerator.ComputeTint(h, 0.5f, 0.5f);
                distinct.Add(new Color(
                    Mathf.Round(c.r * 100f) / 100f,
                    Mathf.Round(c.g * 100f) / 100f,
                    Mathf.Round(c.b * 100f) / 100f, 1f));
            }
            Assert.That(distinct.Count, Is.AtLeast(6));
        }
    }
}
