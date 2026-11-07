using NUnit.Framework;
using UnityEngine;
namespace TrashSailDemo.Tests
{
    public sealed class CoverageTests
    {
        [Test]
        public void FirstLaneRunsNorthAndAllWaypointsStayInsideBoundary()
        {
            var config=ScriptableObject.CreateInstance<DemoConfig>();config.LargeOcean=true;
            var root=new GameObject("Coverage test");var planner=root.AddComponent<CoveragePlanner>();
            try
            {
                planner.Initialize(config,Vector3.zero);
                Assert.That(planner.CurrentGoal.x,Is.Zero);Assert.That(planner.CurrentGoal.z,Is.GreaterThan(990));
                Assert.That(planner.Route.Count,Is.GreaterThan(100));
                foreach(var point in planner.Route){Assert.That(Mathf.Abs(point.x),Is.LessThanOrEqualTo(500));Assert.That(point.z,Is.InRange(0,1000));}
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
        }
        [Test]
        public void DiversionPreservesWaypointAndResetClearsTravelHistory()
        {
            var config=ScriptableObject.CreateInstance<DemoConfig>();var root=new GameObject("Coverage test");var planner=root.AddComponent<CoveragePlanner>();
            try
            {
                planner.Initialize(config,Vector3.zero);Vector3 goal=planner.CurrentGoal;
                planner.Suspend();planner.UpdateProgress(new VehicleState{Position=goal});
                Assert.That(planner.WaypointIndex,Is.Zero);Assert.That(planner.TravelledRoute.Count,Is.EqualTo(2));
                planner.Resume();Assert.That(planner.CurrentGoal,Is.EqualTo(goal));planner.UpdateProgress(new VehicleState{Position=goal});
                Assert.That(planner.WaypointIndex,Is.EqualTo(1));planner.ResetCoverage();
                Assert.That(planner.WaypointIndex,Is.Zero);Assert.That(planner.TravelledRoute.Count,Is.EqualTo(1));Assert.That(planner.Suspended,Is.False);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
        }
        [Test]
        public void SeedAndScenarioDoNotProvidePrivilegedRouteInputs()
        {
            var config=ScriptableObject.CreateInstance<DemoConfig>();var root=new GameObject("Coverage test");var planner=root.AddComponent<CoveragePlanner>();
            try
            {
                planner.Initialize(config,Vector3.zero);var expected=new System.Collections.Generic.List<Vector3>(planner.Route);
                config.Seed=998;config.Scenario=DemoScenario.CrossingFish;planner.ResetCoverage();
                CollectionAssert.AreEqual(expected,planner.Route);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
        }
        [TestCase(false)]
        [TestCase(true)]
        public void TurnsRemainInsideMissionBoundaryInterlock(bool large)
        {
            var config=ScriptableObject.CreateInstance<DemoConfig>();config.LargeOcean=large;
            var root=new GameObject("Coverage turn margin test");var planner=root.AddComponent<CoveragePlanner>();
            try
            {
                planner.Initialize(config,Vector3.zero);
                float length=large?config.MissionLength:config.BoundaryLength;
                float half=large?config.MissionWidth*.5f:config.BoundaryHalfWidth;
                foreach(var point in planner.Route)
                {
                    Assert.That(point.z,Is.LessThan(length-2));
                    Assert.That(point.z,Is.GreaterThan(2));
                    Assert.That(Mathf.Abs(point.x),Is.LessThan(half-2.5f));
                }
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
        }
    }
}
