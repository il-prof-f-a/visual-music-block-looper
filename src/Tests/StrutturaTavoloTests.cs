using System;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class StrutturaTavoloTests
    {
        [Test]
        public void Default_E44SeiRigheCollegate()
        {
            StrutturaTavolo struttura = StrutturaTavolo.Default;

            Assert.That(struttura.BattitiPerMisura, Is.EqualTo(4));
            Assert.That(struttura.RigheCollegate, Is.EqualTo(6));
            Assert.That(struttura.AInizio, Is.EqualTo(1));
            Assert.That(struttura.AFine, Is.EqualTo(6));
            Assert.That(struttura.StepPerMisura, Is.EqualTo(16));
            Assert.That(struttura.NumeroGruppi, Is.EqualTo(1));
            Assert.That(struttura.LunghezzaGiro, Is.EqualTo(6));
        }

        [TestCase(4, 6, 1, 6, true)]
        [TestCase(2, 3, 1, 3, true)]
        [TestCase(4, 1, 1, 1, true)]
        [TestCase(5, 6, 1, 6, false)]
        [TestCase(4, 4, 1, 4, false)]
        [TestCase(4, 6, 3, 3, false)]
        [TestCase(4, 6, 0, 2, false)]
        [TestCase(4, 6, 1, 7, false)]
        [TestCase(4, 1, 1, 2, false)]
        [TestCase(4, 2, 1, 3, false)]
        public void TryCrea_Valori(int battiti, int righeCollegate, int aInizio, int aFine, bool atteso)
        {
            bool ok = StrutturaTavolo.TryCrea(battiti, righeCollegate, aInizio, aFine, out StrutturaTavolo struttura);

            Assert.That(ok, Is.EqualTo(atteso));
            if (atteso)
            {
                Assert.That(struttura, Is.Not.Null);
                Assert.That(struttura.BattitiPerMisura, Is.EqualTo(battiti));
                Assert.That(struttura.RigheCollegate, Is.EqualTo(righeCollegate));
                Assert.That(struttura.AInizio, Is.EqualTo(aInizio));
                Assert.That(struttura.AFine, Is.EqualTo(aFine));
            }
            else
            {
                Assert.That(struttura, Is.Null);
            }
        }

        [Test]
        public void Posizione_Default()
        {
            StrutturaTavolo struttura = StrutturaTavolo.Default;

            PosizioneGiro p0 = struttura.Posizione(0);
            PosizioneGiro p16 = struttura.Posizione(16);
            PosizioneGiro p95 = struttura.Posizione(95);
            PosizioneGiro p96 = struttura.Posizione(96);

            Assert.That(p0.MisuraNelGiro, Is.EqualTo(0));
            Assert.That(p0.StepInMisura, Is.EqualTo(0));
            Assert.That(p0.RigheAttive, Is.EqualTo(new[] { 0 }));

            Assert.That(p16.MisuraNelGiro, Is.EqualTo(1));
            Assert.That(p16.StepInMisura, Is.EqualTo(0));
            Assert.That(p16.RigheAttive, Is.EqualTo(new[] { 1 }));

            Assert.That(p95.MisuraNelGiro, Is.EqualTo(5));
            Assert.That(p95.StepInMisura, Is.EqualTo(15));
            Assert.That(p95.RigheAttive, Is.EqualTo(new[] { 5 }));

            Assert.That(p96.MisuraNelGiro, Is.EqualTo(0));
            Assert.That(p96.StepInMisura, Is.EqualTo(0));
            Assert.That(p96.RigheAttive, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void Posizione_TreCoppieInTreQuarti()
        {
            StrutturaTavolo.TryCrea(3, 2, 1, 2, out StrutturaTavolo struttura);

            PosizioneGiro p12 = struttura.Posizione(12);
            PosizioneGiro p13 = struttura.Posizione(13);
            PosizioneGiro p24 = struttura.Posizione(24);

            Assert.That(p12.MisuraNelGiro, Is.EqualTo(1));
            Assert.That(p12.RigheAttive, Is.EqualTo(new[] { 1, 3, 5 }));

            Assert.That(p13.StepInMisura, Is.EqualTo(1));

            Assert.That(p24.MisuraNelGiro, Is.EqualTo(0));
            Assert.That(p24.RigheAttive, Is.EqualTo(new[] { 0, 2, 4 }));
        }

        [Test]
        public void Posizione_DueTerneConAB2a3()
        {
            StrutturaTavolo.TryCrea(4, 3, 2, 3, out StrutturaTavolo struttura);

            Assert.That(struttura.Posizione(0).RigheAttive, Is.EqualTo(new[] { 1, 4 }));
            Assert.That(struttura.Posizione(16).RigheAttive, Is.EqualTo(new[] { 2, 5 }));
            Assert.That(struttura.Posizione(32).RigheAttive, Is.EqualTo(new[] { 1, 4 }));
        }

        [Test]
        public void Posizione_SeiRigheParallele()
        {
            StrutturaTavolo.TryCrea(2, 1, 1, 1, out StrutturaTavolo struttura);

            PosizioneGiro p9 = struttura.Posizione(9);

            Assert.That(p9.StepInMisura, Is.EqualTo(1));
            Assert.That(p9.MisuraNelGiro, Is.EqualTo(0));
            Assert.That(p9.RigheAttive, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void Posizione_StepNegativoLanciaEccezione()
        {
            StrutturaTavolo struttura = StrutturaTavolo.Default;

            Assert.That(() => struttura.Posizione(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Uguaglianza_PerValore()
        {
            StrutturaTavolo.TryCrea(3, 2, 1, 2, out StrutturaTavolo a);
            StrutturaTavolo.TryCrea(3, 2, 1, 2, out StrutturaTavolo stessa);

            Assert.That(a.Equals(stessa), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(stessa.GetHashCode()));
            Assert.That(a.Equals(StrutturaTavolo.Default), Is.False);
        }
    }
}
