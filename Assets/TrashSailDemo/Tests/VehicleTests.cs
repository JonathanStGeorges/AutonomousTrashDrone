using NUnit.Framework;
using UnityEngine;

namespace TrashSailDemo.Tests
{
    public sealed class VehicleTests
    {
        [Test]
        public void TwinThrustersUsePhysicalSteeringSignsAndConfiguredReverse()
        {
            var forward=DemoVessel.Allocate(.4f,0,true,0);Assert.AreEqual(forward.PortThrust,forward.StarboardThrust);Assert.Greater(forward.PortThrust,0);
            var right=DemoVessel.Allocate(0,.5f,true,0);Assert.Greater(right.PortThrust,right.StarboardThrust);
            float torque=Vector3.Cross(new Vector3(-.85f,0,0),Vector3.forward*right.PortThrust).y+Vector3.Cross(new Vector3(.85f,0,0),Vector3.forward*right.StarboardThrust).y;
            Assert.Greater(torque,0);var left=DemoVessel.Allocate(0,-.5f,true,0);Assert.Less(left.PortThrust,left.StarboardThrust);
            var noReverse=DemoVessel.Allocate(-1,0,false,0);Assert.AreEqual(0,noReverse.PortThrust);Assert.AreEqual(0,noReverse.StarboardThrust);
        }

        [Test]
        public void ConfiguredMixerAccountsForReversedMountAndRejectsSingularLayout()
        {
            var config=ScriptableObject.CreateInstance<DemoConfig>();
            try
            {
                config.PortThrusterEuler=new Vector3(0,180,0);
                var forward=DemoVessel.AllocateConfigured(.4f,0,config,0);
                Assert.Less(forward.PortThrust,0);Assert.Greater(forward.StarboardThrust,0);
                config.PortThrusterPosition=config.StarboardThrusterPosition;
                var singular=DemoVessel.AllocateConfigured(1,1,config,0);
                Assert.AreEqual(0,singular.PortThrust);Assert.AreEqual(0,singular.StarboardThrust);
            }
            finally{Object.DestroyImmediate(config);}
        }

        [Test]
        public void PhysicalCaptureRequiresActiveVolumeCapacityAndSafeSpeed()
        {
            Assert.That(DemoCollector.CaptureAllowed(true, 0, 3, 0.7f, 1.3f, true), Is.True);
            Assert.That(DemoCollector.CaptureAllowed(false, 0, 3, 0.7f, 1.3f, true), Is.False);
            Assert.That(DemoCollector.CaptureAllowed(true, 3, 3, 0.7f, 1.3f, true), Is.False);
            Assert.That(DemoCollector.CaptureAllowed(true, 0, 3, 1.4f, 1.3f, true), Is.False);
            Assert.That(DemoCollector.CaptureAllowed(true, 0, 3, 0.7f, 1.3f, false), Is.False);
        }

        [Test]
        public void ActuatorBoundsAndExpiryDepowerCollectorAndPropulsion()
        {
            ActuatorCommand command = new ActuatorCommand { Rudder = 4, SailTrim = 2, Motor = -3, CollectorEnabled = true, IssuedAt = 10 };
            ActuatorCommand fresh = DemoMath.ClampCommand(command, 10.1);
            Assert.That(fresh.Rudder, Is.EqualTo(1));
            Assert.That(fresh.SailTrim, Is.EqualTo(1));
            Assert.That(fresh.Motor, Is.Zero);
            ActuatorCommand expired = DemoMath.ClampCommand(command, 11);
            Assert.That(expired.CollectorEnabled, Is.False);
            Assert.That(expired.SailTrim, Is.Zero);
            Assert.That(expired.Rudder, Is.Zero);
        }

        [Test]
        public void IntakeCanCaptureFishOnceAndResetClearsCount()
        {
            DemoConfig config = ScriptableObject.CreateInstance<DemoConfig>();
            GameObject vesselRoot = new GameObject("Test vessel");
            DemoVessel vessel = vesselRoot.AddComponent<DemoVessel>();
            vessel.Config = config;
            vessel.Body = vesselRoot.GetComponent<Rigidbody>();
            GameObject intakeRoot = new GameObject("Test intake");
            intakeRoot.transform.SetParent(vesselRoot.transform);
            DemoCollector collector = intakeRoot.AddComponent<DemoCollector>();
            collector.Config = config;
            collector.Vessel = vessel;
            collector.CaptureVolume = intakeRoot.AddComponent<BoxCollider>();
            collector.CaptureVolume.size = Vector3.one * 2;
            collector.CaptureVolume.isTrigger = true;
            collector.Active = true;
            GameObject fish = new GameObject("Any fish label must not reject bycatch");
            Rigidbody fishBody = fish.AddComponent<Rigidbody>();
            BoxCollider fishCollider = fish.AddComponent<BoxCollider>();
            fish.AddComponent<DemoActor>().Kind = "fish";
            int events = 0;
            collector.Captured += ignored => events++;
            try
            {
                Physics.SyncTransforms();
                Assert.That(collector.TryCapture(fishCollider), Is.True);
                Assert.That(collector.TryCapture(fishCollider), Is.False);
                Assert.That(collector.CaptureCount, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(1));
                collector.ResetCollector();
                Assert.That(collector.CaptureCount, Is.Zero);
                Assert.That(collector.Active, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(fish);
                Object.DestroyImmediate(vesselRoot);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void FastFishEnteringActiveIntakeIsRecordedRatherThanSilentlyIgnored()
        {
            DemoConfig config = ScriptableObject.CreateInstance<DemoConfig>();
            GameObject vesselRoot = new GameObject("Overspeed test vessel");
            DemoVessel vessel = vesselRoot.AddComponent<DemoVessel>();
            vessel.Config = config;
            vessel.Body = vesselRoot.GetComponent<Rigidbody>();
            GameObject intakeRoot = new GameObject("Overspeed test intake");
            intakeRoot.transform.SetParent(vesselRoot.transform);
            DemoCollector collector = intakeRoot.AddComponent<DemoCollector>();
            collector.Config = config;
            collector.Vessel = vessel;
            collector.CaptureVolume = intakeRoot.AddComponent<BoxCollider>();
            collector.CaptureVolume.size = Vector3.one * 2;
            collector.CaptureVolume.isTrigger = true;
            collector.Active = true;
            GameObject fish = new GameObject("Fast fish");
            Rigidbody fishBody = fish.AddComponent<Rigidbody>();
            BoxCollider fishCollider = fish.AddComponent<BoxCollider>();
            fish.AddComponent<DemoActor>().Kind = "fish";
            fishBody.linearVelocity = Vector3.right * 2;
            int events = 0;
            collector.Captured += ignored => events++;
            try
            {
                Physics.SyncTransforms();
                Assert.That(collector.TryCapture(fishCollider), Is.True);
                Assert.That(collector.CaptureCount, Is.EqualTo(1));
                Assert.That(collector.OverspeedCaptureCount, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(1));
                collector.ResetCollector();
                Assert.That(collector.OverspeedCaptureCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(fish);
                Object.DestroyImmediate(vesselRoot);
                Object.DestroyImmediate(config);
            }
        }
    }
}
