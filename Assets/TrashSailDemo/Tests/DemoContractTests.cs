using NUnit.Framework;
using UnityEngine;
namespace TrashSailDemo.Tests
{
    public class DemoContractTests
    {
        [Test] public void StaleOrFutureCommandsDepowerAndCloseCollector()
        {
            var c=new ActuatorCommand{IssuedAt=1,PortThrust=1,StarboardThrust=-1,CollectorEnabled=true};
            var stale=DemoMath.ClampCommand(c,2);Assert.AreEqual(0,stale.PortThrust);Assert.AreEqual(0,stale.StarboardThrust);Assert.IsFalse(stale.CollectorEnabled);
            c.IssuedAt=3;Assert.AreEqual(0,DemoMath.ClampCommand(c,2).PortThrust);
        }
        [Test] public void ActuatorsAreClamped(){var c=DemoMath.ClampCommand(new ActuatorCommand{IssuedAt=1,PortThrust=4,StarboardThrust=-2},1.1);Assert.AreEqual(1,c.PortThrust);Assert.AreEqual(-1,c.StarboardThrust);}
        [Test] public void StaleAssociationIsRejected()
        {
            var target=new VisualTrack{RangeValid=true,Position=new Vector3(0,0,4),LastSeen=1,Uncertainty=.2f};
            var contact=new ContactTrack{Position=new Vector3(0,0,4),LastSeen=1,Uncertainty=.2f};
            Assert.IsTrue(DemoMath.Associate(target,contact,1.1));Assert.IsFalse(DemoMath.Associate(target,contact,2));
            contact.Position=new Vector3(0,0,5);Assert.IsFalse(DemoMath.Associate(target,contact,1.1));
        }
        [Test] public void CollectorRequiresPositiveFreshAlignedTrack()
        {
            var target=new VisualTrack{RangeValid=true,Label="fixture bottle",Hits=4,Score=.85f,LastSeen=1,Eligible=true,Category=CanonicalCategory.CollectableRubbish,Evidence=new[]{new ModelEvidence{model="test fixture"}}};
            Assert.IsTrue(DemoMission.MayCollect(target,true,true,true,true,.8f,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,false,true,true,.8f,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,true,false,true,.8f,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,true,true,true,2,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,true,true,true,.8f,1.3f,20,1.1));
            target.Eligible=false;Assert.IsFalse(DemoMission.MayCollect(target,true,true,true,true,.8f,1.3f,0,1.1));
        }
        [Test] public void ThrusterControlWorksWithoutWindAndTurnsAtRest()
        {
            var config=ScriptableObject.CreateInstance<DemoConfig>();var state=new VehicleState{Rotation=Quaternion.identity,WindTo=Vector3.zero};
            try{var forward=DemoVessel.Control(state,.6f,0,config,0);Assert.Greater(forward.PortThrust+forward.StarboardThrust,0);var right=DemoVessel.Control(state,0,90,config,0);Assert.Greater(right.PortThrust,0);Assert.Less(right.StarboardThrust,0);}finally{Object.DestroyImmediate(config);}
        }
        [Test] public void BearingConventionIsStarboardPositive(){Assert.AreEqual(90,DemoMath.Bearing(Vector3.right,Quaternion.identity),.01f);Assert.AreEqual(0,DemoMath.Bearing(Vector3.right,Quaternion.Euler(0,90,0)),.01f);}
    }
}
