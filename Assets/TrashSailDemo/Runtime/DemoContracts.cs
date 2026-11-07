using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    // Contract v2. Unity metres, seconds, +Y up, +Z vessel forward, +X starboard.
    // All timestamps use Time.timeAsDouble (simulation seconds). Owned arrays remain
    // immutable until a later frame replaces the reference; no asynchronous work queues.
    [Serializable] public sealed class CameraFrame
    {
        public long Sequence;
        public double CaptureTime, CompletionTime;
        public int Width, Height;
        public Color32[] Pixels; // RGB(A), bottom-left origin, row-major.
        public Matrix4x4 CameraToWorld;
        public float VerticalFov;
        public bool Valid = true;
    }
    [Serializable] public sealed class SonarFrame
    {
        public long Sequence;
        public double CaptureTime, CompletionTime;
        public int Bearings, RangeBins;
        public float MinRange, MaxRange, HorizontalFov, VerticalFov;
        public float[] Intensity; // [rangeBin * Bearings + bearing], same degraded data as preview.
        public Vector3 Origin;
        public Quaternion Rotation;
        public bool Valid = true;
    }
    [Serializable] public sealed class VisualTrack
    {
        public int Id, Hits;
        public string Label = "unknown";
        public float Score, Uncertainty;
        public Rect PixelRect;
        public Vector3 Position, Velocity;
        public bool RangeValid;
        public CanonicalCategory Category=CanonicalCategory.Unknown;
        public bool Eligible, Conflict;
        public string Provenance="", Profile="combined";
        public ModelEvidence[] Evidence=Array.Empty<ModelEvidence>();
        public double ResultWallTime;
        public double LastSeen;
        public double Age(double now) => Math.Max(0, now - LastSeen);
    }
    [Serializable] public sealed class ContactTrack
    {
        public int Id, Hits;
        public Vector3 Position, Velocity; // XZ estimate; Y=estimated surface, not measured elevation.
        public float Uncertainty, Range, Bearing, LateralExtent, RangeExtent;
        public double LastSeen;
        public double Age(double now) => Math.Max(0, now - LastSeen);
    }
    [Serializable] public struct VehicleState
    {
        public Vector3 Position, Velocity, WindTo,AngularVelocity;
        public Quaternion Rotation;
        public double Timestamp;
        public float WaterLevel;
        public bool Ideal;
        public Vector3 Forward => Rotation * Vector3.forward;
    }
    [Serializable] public struct ActuatorCommand
    {
        public float PortThrust,StarboardThrust; // [-1,1]; reverse only if configured.
        public float RequestedSpeed,RequestedHeading; // m/s, world clockwise degrees from +Z.
        public float Rudder; // [-1,1], positive turns starboard.
        public float SailTrim; // [0,1], zero depowers.
        public float Motor; // [0,1], ignored unless explicitly enabled.
        public bool CollectorEnabled;
        public double IssuedAt;
        public static ActuatorCommand Safe(double now) => new ActuatorCommand { IssuedAt = now };
    }
    public interface IVehicleActuator { void ApplyCommand(ActuatorCommand command); }
    public interface IOwnStateSource { VehicleState State { get; } }
    public interface ICameraFrameSource { CameraFrame LatestFrame { get; } }
    public interface ISonarFrameSource { SonarFrame LatestFrame { get; } }
    public enum DemoScenario { ClearCollection, CrossingFish, UnderwaterHazard, OccludedTarget, CameraFailure, SonarFailure, ThrusterCalibration, NoWind, MetadataVariant, AmbiguousAssociation }
    public enum MissionPhase { IDLE, SEARCH, TRACK_CANDIDATE, ASSESS_APPROACH, APPROACH, FINAL_CHECK, COLLECT, VERIFY, AVOID_OR_REPLAN, SENSOR_DEGRADED, RETURN_HOME, MISSION_COMPLETE, MANUAL }
    [Serializable] public sealed class MissionStatus
    {
        public MissionPhase Phase;
        public string Reason = "Ready";
        public int TargetId = -1;
        public float ClosestClearance = float.PositiveInfinity;
        public Vector3 Goal;
        public bool Tacking;
        public string AssociationGate = "No target association";
        public int TackCount, AvoidanceCount, DegradedCount;
    }
    public static class DemoMath
    {
        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        public static float Heading(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        public static float Bearing(Vector3 worldDelta, Quaternion rotation) => Heading(Quaternion.Inverse(rotation) * worldDelta);
        public static bool Fresh(double now, double captured, float maxAge) => captured >= 0 && now >= captured && now - captured <= maxAge;
        public static bool Associate(VisualTrack target, ContactTrack contact, double now, float maxAge = 0.7f)
        {
            if (target == null || !target.RangeValid || target.Age(now) > maxAge || contact.Age(now) > maxAge) return false;
            return Flat(target.Position - contact.Position).magnitude < Mathf.Clamp(target.Uncertainty + contact.Uncertainty, 0.3f, 0.8f);
        }
        public static ActuatorCommand ClampCommand(ActuatorCommand command, double now, float timeout = 0.5f)
        {
            if (!Fresh(now, command.IssuedAt, timeout)) return ActuatorCommand.Safe(now);
            command.Rudder = Mathf.Clamp(command.Rudder, -1, 1);
            command.SailTrim = Mathf.Clamp01(command.SailTrim);
            command.Motor = Mathf.Clamp01(command.Motor);
            command.PortThrust=Mathf.Clamp(command.PortThrust,-1,1);
            command.StarboardThrust=Mathf.Clamp(command.StarboardThrust,-1,1);
            return command;
        }
    }
    public enum CanonicalCategory { Unknown, CollectableRubbish, OtherRubbish, Wildlife, Obstacle }
    [Serializable] public sealed class ModelEvidence
    {
        public string model,label,category;
        public int classId;
        public float score;
        public float[] box; // original image xyxy, top-left pixels.
        public float[] polygon; // flattened xy pairs, original top-left pixels.
        public bool eligible,conflict;
    }
    [Serializable] public sealed class InferenceRequest
    {
        public string sessionId,cameraId="onboard",profile="combined",imageBase64;
        public long sequence;
        public double captureTime;
        public int width,height;
    }
    [Serializable] public sealed class InferenceResult
    {
        public bool ok,empty;
        public string error,sessionId,cameraId,profile,device;
        public long sequence;
        public double captureTime,wallInferenceMs,wallCompleted;
        public int width,height;
        public ModelEvidence[] detections;
        public string[] models;
    }
}
