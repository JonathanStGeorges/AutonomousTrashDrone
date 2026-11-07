using NUnit.Framework;
using UnityEngine;
namespace TrashSailDemo.Tests
{
    public class DemoContractTests
    {
        [Test] public void StaleOrFutureCommandsDepowerAndCloseCollector()
        {
            var c=new ActuatorCommand{IssuedAt=1,SailTrim=1,Motor=1,CollectorEnabled=true};
            var stale=DemoMath.ClampCommand(c,2);Assert.AreEqual(0,stale.Motor);Assert.IsFalse(stale.CollectorEnabled);
            c.IssuedAt=3;Assert.AreEqual(0,DemoMath.ClampCommand(c,2).SailTrim);
        }
        [Test] public void ActuatorsAreClamped(){var c=DemoMath.ClampCommand(new ActuatorCommand{IssuedAt=1,Rudder=4,SailTrim=-2,Motor=9},1.1);Assert.AreEqual(1,c.Rudder);Assert.AreEqual(0,c.SailTrim);Assert.AreEqual(1,c.Motor);}
        [Test] public void StaleAssociationIsRejected()
        {
            var target=new VisualTrack{RangeValid=true,Position=new Vector3(0,0,4),LastSeen=1,Uncertainty=.2f};
            var contact=new ContactTrack{Position=new Vector3(0,0,4),LastSeen=1,Uncertainty=.2f};
            Assert.IsTrue(DemoMath.Associate(target,contact,1.1));Assert.IsFalse(DemoMath.Associate(target,contact,2));
            contact.Position=new Vector3(0,0,5);Assert.IsFalse(DemoMath.Associate(target,contact,1.1));
        }
        [Test] public void CollectorRequiresPositiveFreshAlignedTrack()
        {
            var target=new VisualTrack{RangeValid=true,Label="rubbish",Hits=4,Score=.85f,LastSeen=1};
            Assert.IsTrue(DemoMission.MayCollect(target,true,true,true,true,.8f,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,false,true,true,.8f,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,true,false,true,.8f,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,true,true,true,2,1.3f,0,1.1));
            Assert.IsFalse(DemoMission.MayCollect(target,true,true,true,true,.8f,1.3f,20,1.1));
            target.Label="unknown";Assert.IsFalse(DemoMission.MayCollect(target,true,true,true,true,.8f,1.3f,0,1.1));
        }
        [Test] public void UpwindLegsAreOutsideNoGoAndAlternate()
        {
            var a=DemoMission.SailingDirection(Vector3.forward,Vector3.back*5,40,1,out bool tack);
            var b=DemoMission.SailingDirection(Vector3.forward,Vector3.back*5,40,-1,out _);
            Assert.IsTrue(tack);Assert.Greater(Vector3.Angle(a,Vector3.forward),40);Assert.Greater(a.z,0);Assert.Less(a.x*b.x,0);
        }
        [Test] public void BearingConventionIsStarboardPositive(){Assert.AreEqual(90,DemoMath.Bearing(Vector3.right,Quaternion.identity),.01f);Assert.AreEqual(0,DemoMath.Bearing(Vector3.right,Quaternion.Euler(0,90,0)),.01f);}
    }
}
