using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    // Operational boundary: only estimated own state, image tracks and sonar tracks.
    public sealed class DemoMission : MonoBehaviour
    {
        public DemoConfig Config;
        public DemoVessel Vessel;
        public DemoOwnState OwnState;
        public DemoCameraPerception CameraPerception;
        public DemoSonarPerception SonarPerception;
        public MissionStatus Status = new MissionStatus();
        public bool Running, Manual;
        public event Action<string, string> Decision;
        public VisualTrack SelectedTarget { get; private set; }
        public bool CollectorPermitted { get; private set; }
        public int TargetCaptures = 3;
        public float DesiredSpeed;
        readonly HashSet<int> retired = new HashSet<int>();
        double clearSince = -1, verifyUntil, tackStarted, missionStarted;
        float tackSign = 1;
        int previousCapture, searchWaypoint;
        Vector3 home;
        double associationHoldStarted=-1;
        bool pendingAssociation;
        readonly List<ContactTrack> avoidanceContacts=new List<ContactTrack>();
        Vector3 previousAvoidDirection;
        bool sensorFailureLatched;
        public double MissionDuration => Math.Max(0, Time.timeAsDouble - missionStarted);
        public void ResetMission()
        {
            Running=false; Manual=false; retired.Clear(); SelectedTarget=null;
            Status=new MissionStatus(); previousCapture=0; searchWaypoint=0; clearSince=-1;
            verifyUntil=0; tackStarted=Time.timeAsDouble; tackSign=1;
            home=OwnState != null ? OwnState.State.Position : Vector3.zero;
            CollectorPermitted=false; DesiredSpeed=0;
            associationHoldStarted=-1;
            previousAvoidDirection=Vector3.zero;
            sensorFailureLatched=false;
        }
        public void StartMission() { sensorFailureLatched=false; Running=true; Manual=false; missionStarted=Time.timeAsDouble; Transition(MissionPhase.SEARCH,"Mission started: bounded coverage using sensor observations"); }
        public void StopMission() { Running=false; CollectorPermitted=false;SelectedTarget=null;Status.TargetId=-1;if(CameraPerception!=null)CameraPerception.SelectedTrackId=-1; Transition(MissionPhase.IDLE,"Mission stopped: collector closed, sails depowered; residual motion remains"); if(Vessel!=null)Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble)); }
        void Transition(MissionPhase phase,string reason)
        {
            if(Status.Phase==phase && Status.Reason==reason)return;
            Status.Phase=phase; Status.Reason=reason; Decision?.Invoke(phase.ToString(),reason);
        }
        public static bool MayCollect(VisualTrack target, bool cameraFresh, bool sonarFresh, bool clear, bool capacity,
            float speed, float maxSpeed, float bearing, double now)
        {
            return target!=null && target.RangeValid && target.Label=="rubbish" && target.Hits>=3 && target.Score>=0.72f
                && target.Age(now)<0.65 && cameraFresh && sonarFresh && clear && capacity && speed<=maxSpeed && Mathf.Abs(bearing)<13;
        }
        public static Vector3 SailingDirection(Vector3 wanted,Vector3 windTo,float noGo,float side,out bool tack)
        {
            wanted=DemoMath.Flat(wanted).normalized; Vector3 upwind=-DemoMath.Flat(windTo).normalized;
            tack=windTo.magnitude>=0.5f && Vector3.Angle(wanted,upwind)<noGo+7;
            return tack ? Quaternion.AngleAxis(side*(noGo+14),Vector3.up)*upwind : wanted;
        }
        void FixedUpdate()
        {
            if(Config==null || Vessel==null || OwnState==null || CameraPerception==null || SonarPerception==null)return;
            double now=Time.timeAsDouble; VehicleState state=OwnState.State;
            CollectorPermitted=false; DesiredSpeed=0;
            var command=ActuatorCommand.Safe(now);
            if(!Running)
            {
                if(Manual)
                {
                    Transition(MissionPhase.MANUAL,"Manual actuators; collector remains interlocked closed");
                    command.Rudder=Input.GetAxisRaw("Horizontal"); command.SailTrim=Input.GetKey(KeyCode.W)?0.7f:0;
                    command.Motor=Input.GetKey(KeyCode.W)?0.35f:0;
                }
                Vessel.ApplyCommand(command); return;
            }
            bool cameraFresh=CameraPerception.LatestFrame!=null && CameraPerception.LatestFrame.Valid && DemoMath.Fresh(now,CameraPerception.LatestFrame.CaptureTime,Config.CameraMaxAge);
            bool sonarFresh=SonarPerception.LatestFrame!=null && SonarPerception.LatestFrame.Valid && DemoMath.Fresh(now,SonarPerception.LatestFrame.CaptureTime,Config.SonarMaxAge);
            if(!cameraFresh || !sonarFresh)
            {
                sensorFailureLatched=true;
                if(Status.Phase!=MissionPhase.SENSOR_DEGRADED) Status.DegradedCount++;
                Transition(MissionPhase.SENSOR_DEGRADED,!cameraFresh ? "Camera stale/unavailable: close intake and depower" : "Sonar stale/unavailable: close intake and depower");
                command.Rudder=0; Vessel.ApplyCommand(command); return;
            }
            if(sensorFailureLatched)
            {Transition(MissionPhase.SENSOR_DEGRADED,"Sensor fault latched: fresh feeds restored; remain depowered until Start mission");Vessel.ApplyCommand(command);return;}
            if(Vessel.Collector.CaptureCount>previousCapture)
            {
                previousCapture=Vessel.Collector.CaptureCount;
                if(SelectedTarget!=null)retired.Add(SelectedTarget.Id);
                SelectedTarget=null; verifyUntil=now+Config.TrackMemory+0.4;
                Transition(MissionPhase.VERIFY,"Physical intake crossing recorded; depower and wait for retained sonar contact memory to clear");
            }
            if(previousCapture>=TargetCaptures)
            { Transition(MissionPhase.MISSION_COMPLETE,"Requested capture count reached; sails depowered and intake closed"); Running=false; Vessel.ApplyCommand(command); return; }
            if(now<verifyUntil) { Vessel.ApplyCommand(command); return; }
            if(SelectedTarget!=null && (SelectedTarget.Age(now)>0.75 || !SelectedTarget.RangeValid || retired.Contains(SelectedTarget.Id)))
            { SelectedTarget=null; Transition(MissionPhase.SEARCH,"Target lost or uncertain; reacquire from fresh pixels"); }
            if(SelectedTarget==null && Config.Scenario!=DemoScenario.Upwind && Config.Scenario!=DemoScenario.LowWind)
            {
                float best=float.PositiveInfinity;
                foreach(var track in CameraPerception.Tracks)
                {
                    if(retired.Contains(track.Id) || track.Label!="rubbish" || track.Hits<3 || track.Score<0.72f || !track.RangeValid || track.Age(now)>0.4)continue;
                    float distance=DemoMath.Flat(track.Position-state.Position).magnitude;
                    if(distance<best && track.Uncertainty<2.2f){best=distance;SelectedTarget=track;}
                }
                if(SelectedTarget!=null)Transition(MissionPhase.TRACK_CANDIDATE,"Persistent positive RGB heuristic track selected");
            }
            Status.TargetId=SelectedTarget!=null?SelectedTarget.Id:-1;
            CameraPerception.SelectedTrackId=Status.TargetId;
            Vector3 goal=SelectedTarget!=null ? SelectedTarget.Position+SelectedTarget.Velocity*Mathf.Min((float)SelectedTarget.Age(now),0.5f) : SearchGoal(state);
            bool returning=MissionDuration>Config.TimeBudget;
            if(returning){SelectedTarget=null;goal=home;Transition(MissionPhase.RETURN_HOME,"Mission time budget expired; target released and intake closed");}
            Vector3 delta=DemoMath.Flat(goal-state.Position); float distanceToGoal=delta.magnitude;
            Vector3 direction=delta.sqrMagnitude>0.01f?delta.normalized:state.Forward;
            Status.Goal=goal;
            bool hazard=FindHazard(state,direction,distanceToGoal,now,out Vector3 avoidDirection,out string hazardReason);
            bool clearanceHold=false;
            if(hazard)
            {
                if(Status.Phase!=MissionPhase.AVOID_OR_REPLAN)Status.AvoidanceCount++;
                clearSince=-1; direction=avoidDirection;
                Transition(MissionPhase.AVOID_OR_REPLAN,hazardReason+"; intake closed, steer clear with residual motion");
                DesiredSpeed=pendingAssociation?0:0.7f;clearanceHold=pendingAssociation;
            }
            else if(Status.Phase==MissionPhase.AVOID_OR_REPLAN && (clearSince<0 || now-clearSince<Config.ClearancePeriod))
            {
                if(clearSince<0)clearSince=now;
                direction=state.Forward;
                DesiredSpeed=0;clearanceHold=true; Transition(MissionPhase.AVOID_OR_REPLAN,"Depowered clearance hold; retain heading while contact uncertainty resolves");
                hazard=true;
            }
            else
            {
                clearSince=-1;
                DesiredSpeed=SelectedTarget!=null ? (distanceToGoal<5?0.72f:1.1f) : 1.1f;
                if(SelectedTarget!=null && !returning && (distanceToGoal>=4 || Status.Phase!=MissionPhase.COLLECT))Transition(distanceToGoal<4?MissionPhase.FINAL_CHECK:MissionPhase.APPROACH,distanceToGoal<4?"Align intake; require fresh camera, sonar, narrow association and clear swept path":"Approach estimated floating target with finite actuator limits");
                else if(SelectedTarget==null && Status.Phase!=MissionPhase.RETURN_HOME)Transition(MissionPhase.SEARCH,"Bounded coverage: only pixel detections create rubbish targets");
            }
            float halfWidth=Config.BoundaryHalfWidth-2.5f;
            if(Mathf.Abs(state.Position.x)>halfWidth || state.Position.z>Config.BoundaryLength-2 || state.Position.z< -4)
            {direction=DemoMath.Flat(new Vector3(0,0,Config.BoundaryLength*0.45f)-state.Position).normalized;hazard=true;Transition(MissionPhase.AVOID_OR_REPLAN,"Mission boundary margin reached; return inward");}
            bool outwardAtBoundary=Mathf.Abs(state.Position.x)>10 && Mathf.Sign(state.Position.x)==tackSign && now-tackStarted>2;
            if(now-tackStarted>10 || outwardAtBoundary)
            {tackSign=-tackSign;tackStarted=now;}
            bool tacking=false;
            if(!Config.MotorAssistance)direction=SailingDirection(direction,state.WindTo,Config.NoGoAngle,tackSign,out tacking);
            if(tacking && !Status.Tacking)Status.TackCount++;
            // Record alternating tack legs even when no-go demand remains continuous.
            if(tacking && now-tackStarted<Time.fixedDeltaTime*1.5)Status.TackCount++;
            Status.Tacking=tacking;
            if(!Config.MotorAssistance && state.WindTo.magnitude<0.5f)
            {Transition(MissionPhase.SENSOR_DEGRADED,"Insufficient wind: propulsion stalled/deferred; no hidden motor");Vessel.ApplyCommand(command);return;}
            float error=Vector3.SignedAngle(state.Forward,direction,Vector3.up);
            command.Rudder=Mathf.Clamp(error/30f,-1,1);
            float forward=Vector3.Dot(DemoMath.Flat(state.Velocity),state.Forward);
            command.SailTrim=Mathf.Clamp01((DesiredSpeed-forward)*0.75f+0.2f);
            command.Motor=Config.MotorAssistance?Mathf.Clamp01((DesiredSpeed-forward)*1.1f+0.15f):0;
            if(Mathf.Abs(error)>55){command.SailTrim*=0.35f;command.Motor=Mathf.Min(command.Motor,0.24f);}
            if(clearanceHold){command.SailTrim=0;command.Motor=0;}
            float targetBearing=SelectedTarget!=null?DemoMath.Bearing(SelectedTarget.Position-state.Position,state.Rotation):180;
            CollectorPermitted=!returning && distanceToGoal<4 && MayCollect(SelectedTarget,cameraFresh,sonarFresh,!hazard,Vessel.Collector.Fill<1,
                DemoMath.Flat(state.Velocity).magnitude,Config.MaxCollectionSpeed,targetBearing,now);
            command.CollectorEnabled=CollectorPermitted;
            if(CollectorPermitted)Transition(MissionPhase.COLLECT,"Positive track aligned; fresh sensors and measured corridor clear; awaiting physical intake entry");
            else if(!hazard && SelectedTarget!=null && distanceToGoal<4 && !returning)Transition(MissionPhase.FINAL_CHECK,"Intake interlocked until alignment, freshness, speed and clearance pass");
            Vessel.ApplyCommand(command);
        }
        Vector3 SearchGoal(VehicleState state)
        {
            if(Config.Scenario==DemoScenario.Upwind || Config.Scenario==DemoScenario.LowWind)return new Vector3(0,0,30);
            Vector3[] waypoints={new Vector3(0,0,10),new Vector3(0,0,26),new Vector3(9,0,30),new Vector3(9,0,9),new Vector3(-9,0,9),new Vector3(-9,0,30)};
            if(DemoMath.Flat(waypoints[searchWaypoint%waypoints.Length]-state.Position).magnitude<3)searchWaypoint++;
            return waypoints[searchWaypoint%waypoints.Length];
        }
        bool FindHazard(VehicleState state,Vector3 direction,float goalDistance,double now,out Vector3 avoid,out string reason)
        {
            avoid=direction;reason="";Status.ClosestClearance=float.PositiveInfinity;pendingAssociation=false;Status.AssociationGate="No echo match";
            float speed=DemoMath.Flat(state.Velocity).magnitude;
            float horizon=Mathf.Max(6f,speed*5f+Config.VesselLength);
            horizon=Mathf.Min(horizon,goalDistance+0.4f);
            int matches=0;
            foreach(var contact in SonarPerception.Tracks)if(DemoMath.Associate(SelectedTarget,contact,now))matches++;
            bool hazard=false;
            avoidanceContacts.Clear();
            foreach(var contact in SonarPerception.Tracks)
            {
                if(contact.Age(now)>Config.TrackMemory)continue;
                float expectedHalfWidth=0;
                if(SelectedTarget!=null && CameraPerception.LatestFrame!=null)
                {
                    var image=CameraPerception.LatestFrame;
                    expectedHalfWidth=SelectedTarget.PixelRect.width/image.Height*Mathf.Tan(image.VerticalFov*Mathf.Deg2Rad*.5f)*DemoMath.Flat(SelectedTarget.Position-state.Position).magnitude;
                }
                bool associated=matches==1 && DemoMath.Associate(SelectedTarget,contact,now) && contact.Uncertainty<0.95f
                    && contact.Hits>=3 && SelectedTarget!=null && DemoMath.Flat(contact.Velocity-SelectedTarget.Velocity).magnitude<0.5f
                    && contact.LateralExtent<Mathf.Clamp(expectedHalfWidth+0.18f,0.24f,0.5f) && contact.RangeExtent<0.3f;
                bool geometric=DemoMath.Associate(SelectedTarget,contact,now);
                if(geometric)Status.AssociationGate=associated?"Unique persistent extent/velocity-consistent match":matches>1?"Multiple conflicting echoes":contact.Hits<3?"Awaiting sonar persistence":contact.Uncertainty>=0.95f?"Uncertainty too large":DemoMath.Flat(contact.Velocity-SelectedTarget.Velocity).magnitude>=0.5f?"Relative velocity conflict":"Echo extent exceeds visual target footprint";
                Vector3 relative=DemoMath.Flat(contact.Position+contact.Velocity*Mathf.Min(1.3f,contact.Range/Mathf.Max(speed,0.4f))-state.Position);
                float ahead=Vector3.Dot(relative,direction), side=Vector3.Dot(relative,Vector3.Cross(Vector3.up,direction));
                float extent=Mathf.Max(.35f,Mathf.Max(contact.LateralExtent,contact.RangeExtent));
                float envelope=Config.VesselWidth*0.5f+Config.SafeMargin+contact.Uncertainty+extent+speed*(Config.SonarLatency+0.2f);
                float clearance=relative.magnitude-(Config.VesselLength*0.5f+contact.Uncertainty);
                if(!associated)Status.ClosestClearance=Mathf.Min(Status.ClosestClearance,clearance);
                if(associated)continue;
                avoidanceContacts.Add(contact);
                Vector3 actualDirection=speed>0.15f?DemoMath.Flat(state.Velocity).normalized:state.Forward;
                float actualAhead=Vector3.Dot(relative,actualDirection),actualSide=Vector3.Dot(relative,Vector3.Cross(Vector3.up,actualDirection));
                float rear=-Config.VesselLength*.5f-Config.SafeMargin-contact.Uncertainty-extent;
                bool actualThreat=actualAhead>rear && actualAhead<horizon+1.8f && Mathf.Abs(actualSide)<envelope;
                if((ahead>rear && ahead<horizon+1.8f && Mathf.Abs(side)<envelope) || actualThreat)
                {
                    hazard=true;
                    avoid=Quaternion.AngleAxis(side>=0?-65:65,Vector3.up)*direction;
                    reason=matches>1?"Ambiguous target/contact association":"Unknown sonar contact intersects predicted hull/intake envelope";
                    if(geometric && matches==1 && contact.Hits<3 && ahead>4.5f)
                    {
                        if(associationHoldStarted<0)associationHoldStarted=now;
                        if(now-associationHoldStarted<0.7){pendingAssociation=true;avoid=state.Forward;reason="Narrow target echo awaiting persistence: close intake and depower";}
                    }
                }
            }
            if(!pendingAssociation)associationHoldStarted=-1;
            if(hazard && !pendingAssociation)avoid=ChooseAvoidance(state,direction,avoidanceContacts);
            return hazard;
        }
        Vector3 ChooseAvoidance(VehicleState state,Vector3 wanted,List<ContactTrack> contacts)
        {
            float baseHeading=DemoMath.Heading(wanted),currentHeading=DemoMath.Heading(state.Forward);
            float best=float.NegativeInfinity;Vector3 selected=wanted;
            // Predict a finite 25deg/s turn and gradual speed response, including the
            // swept oriented hull/intake envelope. This is a bounded local planner,
            // not a centre-point waypoint follower or a ground-truth collision query.
            foreach(float offset in new[]{40f,-40f,65f,-65f,90f,-90f,120f,-120f,160f,-160f})
            {
                float target=baseHeading+offset;Vector3 headingDirection=Quaternion.Euler(0,target,0)*Vector3.forward;
                if(!Config.MotorAssistance && Vector3.Angle(headingDirection,-state.WindTo)<Config.NoGoAngle+7)continue;
                Vector3 position=DemoMath.Flat(state.Position),velocity=DemoMath.Flat(state.Velocity);float heading=currentHeading,minClear=float.PositiveInfinity,integral=0,endClear=0;
                for(float t=0;t<=6;t+=0.4f)
                {
                    Quaternion rotation=Quaternion.Euler(0,heading,0);
                    float stepClear=float.PositiveInfinity;
                    foreach(var contact in contacts)
                    {
                        Vector3 predicted=contact.Position+contact.Velocity*(t+(float)contact.Age(Time.timeAsDouble));
                        Vector3 local=Quaternion.Inverse(rotation)*DemoMath.Flat(predicted-position);
                        float dx=Mathf.Max(0,Mathf.Abs(local.x)-Config.VesselWidth*.5f);
                        float dz=Mathf.Max(0,Mathf.Max(-Config.VesselLength*.5f-local.z,local.z-(Config.VesselLength*.5f+.7f)));
                        float extent=Mathf.Max(.35f,Mathf.Max(contact.LateralExtent,contact.RangeExtent));
                        float clearance=Mathf.Sqrt(dx*dx+dz*dz)-contact.Uncertainty-extent-Config.SafeMargin-0.2f*t;
                        minClear=Mathf.Min(minClear,clearance);
                        stepClear=Mathf.Min(stepClear,clearance);
                    }
                    integral+=Mathf.Clamp(stepClear,-4,4)*.4f;endClear=stepClear;
                    Vector3 forward=rotation*Vector3.forward,right=rotation*Vector3.right;
                    float longitudinal=Vector3.Dot(velocity,forward),lateral=Vector3.Dot(velocity,right);
                    float sailTrim=Mathf.Clamp01((.7f-longitudinal)*.75f+.2f),motor=Config.MotorAssistance?Mathf.Clamp01((.7f-longitudinal)*1.1f+.15f):0;
                    if(Mathf.Abs(Mathf.DeltaAngle(heading,target))>55){sailTrim*=.35f;motor=Mathf.Min(motor,.24f);}
                    float wind=state.WindTo.magnitude;
                    float drive=wind<.5f?0:Config.MaxSailForce*DemoVessel.SailPolar(Vector3.Angle(forward,-state.WindTo),Config.NoGoAngle)*Mathf.Clamp(wind*wind/25,0,2)*sailTrim;
                    drive+=Config.MaxMotorForce*motor;
                    velocity+=(forward*(drive-longitudinal*(18+24*Mathf.Abs(longitudinal)))-right*lateral*(95+40*Mathf.Abs(lateral)))/Config.VesselMass*.4f;
                    heading=Mathf.MoveTowardsAngle(heading,target,25*Mathf.Clamp01(Mathf.Abs(longitudinal)/.7f)*.4f);
                    position+=velocity*.4f;
                }
                float score=minClear*3+integral*.15f+endClear*.2f+Vector3.Dot(headingDirection,wanted)*.25f;
                if(previousAvoidDirection.sqrMagnitude>0 && Vector3.Angle(headingDirection,previousAvoidDirection)>80)score-=.5f;
                if(score>best){best=score;selected=headingDirection;}
            }
            previousAvoidDirection=selected;return selected;
        }
    }
}
