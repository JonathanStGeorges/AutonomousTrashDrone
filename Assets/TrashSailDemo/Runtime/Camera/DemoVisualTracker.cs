using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    public sealed class DemoVisualTracker
    {
        readonly List<VisualTrack> tracks = new List<VisualTrack>();
        readonly HashSet<int> matched = new HashSet<int>();
        int nextId = 1;
        double lastAgeUpdate;
        public IReadOnlyList<VisualTrack> Tracks => tracks;
        public void Reset() { tracks.Clear(); matched.Clear(); nextId = 1; lastAgeUpdate = 0; }

        public void UpdateModels(CameraFrame frame, InferenceResult result, float waterLevel, float memory, double wallTime)
        {
            matched.Clear();
            // One track per model/class: incompatible overlapping evidence is never suppressed.
            foreach (ModelEvidence evidence in result.detections)
            {
                Rect box=DemoInferenceProtocol.BottomLeftBox(evidence,frame.Width,frame.Height);
                if(box.width<=0 || box.height<=0) continue;
                CanonicalCategory category;
                if(!Enum.TryParse(evidence.category,out category)) category=CanonicalCategory.Unknown;
                bool surface=category==CanonicalCategory.CollectableRubbish || category==CanonicalCategory.OtherRubbish;
                Vector3 measured=Vector3.zero;float uncertainty=5;
                bool valid=surface && DemoCameraGeometry.TryWaterIntersection(frame,DemoInferenceProtocol.WaterContact(evidence,box,frame.Height),waterLevel,out measured,out uncertainty);
                VisualTrack best=null;float distance=100000;
                foreach(VisualTrack old in tracks)
                {
                    if(matched.Contains(old.Id) || old.Provenance!=evidence.model || old.Label!=evidence.label || old.Age(frame.CaptureTime)>memory) continue;
                    float d=valid && old.RangeValid ? DemoMath.Flat(measured-old.Position).magnitude : Vector2.Distance(box.center,old.PixelRect.center)/20;
                    if(d<2 && d<distance) {best=old;distance=d;}
                }
                if(best==null) {best=new VisualTrack {Id=nextId++};tracks.Add(best);}
                double elapsed=frame.CaptureTime-best.LastSeen;
                if(valid && best.RangeValid && elapsed>0.025 && elapsed<2)
                    best.Velocity=Vector3.Lerp(best.Velocity,Vector3.ClampMagnitude((measured-best.Position)/(float)elapsed,1.5f),.15f);
                best.Position=measured;best.RangeValid=valid;best.Uncertainty=valid?uncertainty:5;
                best.PixelRect=box;best.Label=evidence.label;best.Score=evidence.score;
                best.Category=category;best.Eligible=evidence.eligible && !evidence.conflict && category==CanonicalCategory.CollectableRubbish;
                best.Conflict=evidence.conflict;best.Provenance=evidence.model;best.Profile=result.profile;
                best.Evidence=new[]{evidence};best.ResultWallTime=wallTime;best.Hits++;best.LastSeen=frame.CaptureTime;matched.Add(best.Id);
            }
            Age(frame.CompletionTime,memory);
        }

        public void Age(double now, float memory)
        {
            float elapsed = lastAgeUpdate > 0 ? Mathf.Clamp((float)(now - lastAgeUpdate), 0, 1) : 0;
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                VisualTrack track = tracks[i];
                if (track.Age(now) > memory) tracks.RemoveAt(i);
                else if (track.Age(now) > 0.2) track.Uncertainty = Mathf.Min(5f, track.Uncertainty + 0.3f * elapsed);
            }
            lastAgeUpdate = now;
        }
    }
}
