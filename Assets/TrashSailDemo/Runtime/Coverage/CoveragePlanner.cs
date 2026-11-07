using System.Collections.Generic;
using UnityEngine;
namespace TrashSailDemo
{
    // Boundary-only route generation: no population, object identity or target positions.
    public sealed class CoveragePlanner : MonoBehaviour
    {
        public DemoConfig Config;
        public float TurningRadius=3, ArrivalRadius=1;
        public IReadOnlyList<Vector3> Route => route;
        public IReadOnlyList<Vector3> TravelledRoute => travelled;
        public int WaypointIndex { get; private set; }
        public bool Suspended { get; private set; }
        public bool Complete => route.Count>0 && WaypointIndex>=route.Count;
        public Vector3 CurrentGoal => route.Count==0?start:route[Mathf.Min(WaypointIndex,route.Count-1)];
        readonly List<Vector3> route=new List<Vector3>();
        readonly List<Vector3> travelled=new List<Vector3>();
        Vector3 start;
        float trailSpacing=1;
        public void Initialize(DemoConfig config,Vector3 initial)
        {
            Config=config;start=DemoMath.Flat(initial);ResetCoverage();
        }
        public void ResetCoverage()
        {
            route.Clear();travelled.Clear();WaypointIndex=0;Suspended=false;trailSpacing=1;
            if(Config==null)return;
            float width=Config.LargeOcean?Config.MissionWidth:Config.BoundaryHalfWidth*2;
            float length=Config.LargeOcean?Config.MissionLength:Config.BoundaryLength;
            float spacing=Mathf.Clamp(Config.LaneSpacing,.5f,Mathf.Max(.5f,2*Config.UsefulDetectionRange));
            float radius=Mathf.Min(Mathf.Max(.25f,TurningRadius),spacing*.5f);
            // Leave room beyond the turn apex for the hull and mission boundary
            // interlock; the outermost curve must not reach the boundary itself.
            float boundaryMargin=Mathf.Max(2.5f,Config.VesselLength*.5f+Config.SafeMargin);
            float edge=radius+boundaryMargin;
            float bottom=edge,top=Mathf.Max(bottom+1,length-edge);
            var lanes=new List<float>{0};
            for(float x=spacing;x<=width*.5f-edge;x+=spacing)lanes.Add(x);
            for(float x=-spacing;x>=-width*.5f+edge;x-=spacing)lanes.Add(x);
            route.Add(new Vector3(0,0,top));
            bool north=true;
            for(int i=1;i<lanes.Count;i++)
            {
                float from=lanes[i-1],to=lanes[i],end=north?top:bottom;
                // Adjacent lanes use a sampled semicircle. The transfer between
                // opposite halves is explicit transit, not counted as observed area.
                float half=Mathf.Abs(to-from)*.5f,sign=Mathf.Sign(to-from);
                if(half<=spacing*.51f)
                {
                    float centre=(from+to)*.5f;
                    for(int segment=1;segment<=8;segment++)
                    {
                        float a=Mathf.PI*segment/8;
                        route.Add(new Vector3(centre-sign*half*Mathf.Cos(a),0,end+(north?1:-1)*radius*Mathf.Sin(a)));
                    }
                }
                else route.Add(new Vector3(to,0,end));
                north=!north;route.Add(new Vector3(to,0,north?top:bottom));
            }
            travelled.Add(start);
        }
        public void UpdateProgress(VehicleState state)
        {
            Vector3 point=DemoMath.Flat(state.Position);
            if(travelled.Count==0 || Vector3.Distance(travelled[travelled.Count-1],point)>=trailSpacing)
            {
                if(travelled.Count>=20000)
                {
                    for(int i=travelled.Count-2;i>0;i-=2)travelled.RemoveAt(i);
                    trailSpacing*=2;
                }
                travelled.Add(point);
            }
            if(Suspended || Complete || route.Count==0)return;
            // Advance one waypoint per measurement to preserve curved turn ordering.
            if(Vector3.Distance(point,CurrentGoal)<=Mathf.Max(.2f,ArrivalRadius))WaypointIndex++;
        }
        public void Suspend(){Suspended=true;}
        public void Resume(){Suspended=false;}
    }
}
