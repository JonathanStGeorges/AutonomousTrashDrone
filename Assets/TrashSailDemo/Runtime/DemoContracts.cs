using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    // Contract v1. Unity metres, seconds, +Y up, +Z vessel forward, +X starboard.
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
        public Vector3 Position, Velocity, WindTo;
        public Quaternion Rotation;
        public double Timestamp;
        public float WaterLevel;
        public bool Ideal;
        public Vector3 Forward => Rotation * Vector3.forward;
    }
    [Serializable] public struct ActuatorCommand
    {
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
    public enum DemoScenario { ClearCollection, CrossingFish, UnderwaterHazard, OccludedTarget, CameraFailure, SonarFailure, Upwind, LowWind, MetadataVariant, AmbiguousAssociation }
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
            return command;
        }
    }
}
