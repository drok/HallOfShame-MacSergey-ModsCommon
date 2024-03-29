﻿using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ColossalFramework.Math.VectorUtils;

namespace ModsCommon.Utilities
{
    public enum TrajectoryType
    {
        Line,
        Bezier
    }
    public interface ITrajectory : IOverlay
    {
        TrajectoryType TrajectoryType { get; }
        float Length { get; }
        float Magnitude { get; }
        float DeltaAngle { get; }
        Vector3 Direction { get; }
        Vector3 StartDirection { get; }
        Vector3 EndDirection { get; }
        Vector3 StartPosition { get; }
        Vector3 EndPosition { get; }
        ITrajectory Cut(float t0, float t1);
        void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2);
        Vector3 Tangent(float t);
        Vector3 Position(float t);
        float Travel(float distance);
        float Travel(float start, float distance);
        float Distance(float from = 0f, float to = 1f);
        ITrajectory Invert();
        ITrajectory Copy();
    }
    public class BezierTrajectory : ITrajectory
    {
        public TrajectoryType TrajectoryType => TrajectoryType.Bezier;
        public Bezier3 Trajectory { get; }

        private float? _length = null;
        public float Length => _length ??= Trajectory.Length();
        public float Magnitude { get; }
        public float DeltaAngle { get; }
        public Vector3 Direction { get; }
        public Vector3 StartDirection { get; }
        public Vector3 EndDirection { get; }
        public Vector3 StartPosition => Trajectory.a;
        public Vector3 EndPosition => Trajectory.d;
        public BezierTrajectory(Bezier3 trajectory)
        {
            Trajectory = trajectory;

            Magnitude = (Trajectory.d - Trajectory.a).magnitude;
            DeltaAngle = Trajectory.DeltaAngle();
            Direction = (Trajectory.d - Trajectory.a).normalized;
            StartDirection = (Trajectory.b - Trajectory.a).normalized;
            EndDirection = (Trajectory.c - Trajectory.d).normalized;
        }
        public BezierTrajectory(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir, bool normalize = true) : this(GetBezier(startPos, startDir, endPos, endDir, normalize)) { }
        public BezierTrajectory(Vector3 startPos, Vector3 startDir, Vector3 endPos) : this(GetBezier(startPos, startDir, endPos)) { }

        public BezierTrajectory(BezierTrajectory trajectory) : this(trajectory.Trajectory) { }
        public BezierTrajectory(ITrajectory trajectory) : this(trajectory.StartPosition, trajectory.StartDirection, trajectory.EndPosition, trajectory.EndDirection) { }

        private static Bezier3 GetBezier(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir, bool normalize)
        {
            var bezier = new Bezier3()
            {
                a = startPos,
                d = endPos,
            };
            NetSegment.CalculateMiddlePoints(bezier.a, normalize ? startDir.normalized : startDir, bezier.d, normalize ? endDir.normalized : endDir, true, true, out bezier.b, out bezier.c);
            return bezier;
        }
        private static Bezier3 GetBezier(Vector3 startPos, Vector3 startDir, Vector3 endPos)
        {
            var startAngle = startDir.AbsoluteAngle();
            var dir = endPos - startPos;
            var strangeAngle = dir.AbsoluteAngle();
            var endAngle = strangeAngle + Mathf.PI + (strangeAngle - startAngle);

            var bezier = new Bezier3()
            {
                a = startPos,
                d = endPos,
            };

            if (Vector3.Dot(startDir, dir) < 0)
                NetSegment.CalculateMiddlePoints(bezier.a, dir, bezier.d, -dir, true, true, out bezier.b, out bezier.c);
            else
                NetSegment.CalculateMiddlePoints(bezier.a, startDir, bezier.d, endAngle.Direction(), true, true, out bezier.b, out bezier.c);

            return bezier;
        }

        public BezierTrajectory Cut(float t0, float t1) => new BezierTrajectory(Trajectory.Cut(t0, t1));
        ITrajectory ITrajectory.Cut(float t0, float t1) => Cut(t0, t1);
        public void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2)
        {
            Trajectory.Divide(out Bezier3 bezier1, out Bezier3 bezier2);
            trajectory1 = new BezierTrajectory(bezier1);
            trajectory2 = new BezierTrajectory(bezier2);
        }
        public Vector3 Tangent(float t) => Trajectory.Tangent(t);
        public Vector3 Position(float t) => Trajectory.Position(t);
        public float Travel(float distance) => Trajectory.Travel(distance);
        public float Travel(float start, float distance) => start + Trajectory.Cut(start, 1f).Travel(distance) * (1f - start);
        public float Distance(float from = 0f, float to = 1f) => Trajectory.Cut(from, to).Length();
        public BezierTrajectory Invert() => new BezierTrajectory(Trajectory.Invert());
        ITrajectory ITrajectory.Invert() => Invert();
        public BezierTrajectory Copy() => new BezierTrajectory(Trajectory);
        ITrajectory ITrajectory.Copy() => Copy();

        public void Render(OverlayData data) => Trajectory.RenderBezier(data);
        public override string ToString() => $"Bezier: {StartPosition} - {EndPosition}";


        public static implicit operator Bezier3(BezierTrajectory trajectory) => trajectory.Trajectory;
        public static explicit operator BezierTrajectory(Bezier3 bezier) => new BezierTrajectory(bezier);

        public override bool Equals(object obj) => Equals(obj as BezierTrajectory);
        public bool Equals(BezierTrajectory other)
        {
            if (other is null)
                return false;
            else if (ReferenceEquals(this, other))
                return true;
            else
                return Equal(Trajectory, other.Trajectory);
        }
        private static bool Equal(Bezier3 first, Bezier3 second) => first.a == second.a && first.b == second.b && first.c == second.c && first.d == second.d;

        public static bool operator ==(BezierTrajectory first, BezierTrajectory second)
        {
            if (first is null)
                return second is null;
            else
                return first.Equals(second);
        }
        public static bool operator !=(BezierTrajectory first, BezierTrajectory second) => !(first == second);
    }
    public class StraightTrajectory : ITrajectory
    {
        public TrajectoryType TrajectoryType => TrajectoryType.Line;
        public Line3 Trajectory { get; }
        public bool IsSection { get; }
        public float Length { get; }
        public float Magnitude => Length;
        public float DeltaAngle => 0f;
        public Vector3 Direction { get; }
        public Vector3 StartDirection => Direction;
        public Vector3 EndDirection => -Direction;
        public Vector3 StartPosition => Trajectory.a;
        public Vector3 EndPosition => Trajectory.b;
        public StraightTrajectory(Line3 trajectory, bool isSection = true)
        {
            Trajectory = trajectory;
            IsSection = isSection;

            Length = (Trajectory.b - Trajectory.a).magnitude;
            Direction = (Trajectory.b - Trajectory.a).normalized;
        }
        public StraightTrajectory(Vector3 start, Vector3 end, bool isSection = true) : this(new Line3(start, end), isSection) { }
        public StraightTrajectory(ITrajectory trajectory, bool isSection = true) : this(new Line3(trajectory.StartPosition, trajectory.EndPosition), isSection) { }

        public StraightTrajectory Cut(float t0, float t1) => new StraightTrajectory(Position(t0), Position(t1));
        ITrajectory ITrajectory.Cut(float t0, float t1) => Cut(t0, t1);
        public StraightTrajectory Cut(float t0, float t1, bool isSection) => new StraightTrajectory(Position(t0), Position(t1), isSection);

        public void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2)
        {
            var middle = (Trajectory.a + Trajectory.b) / 2;
            trajectory1 = new StraightTrajectory(Trajectory.a, middle);
            trajectory2 = new StraightTrajectory(middle, Trajectory.b);
        }
        public Vector3 Tangent(float t) => Direction;
        public Vector3 Position(float t) => Trajectory.a + (Trajectory.b - Trajectory.a) * t;
        public float Travel(float distance) => distance / Length;
        public float Travel(float start, float distance) => start + Travel(distance);
        public float Distance(float from = 0f, float to = 1f) => Length * (to - from);
        public StraightTrajectory Invert() => new StraightTrajectory(Trajectory.b, Trajectory.a, IsSection);
        ITrajectory ITrajectory.Invert() => Invert();
        public StraightTrajectory Copy() => new StraightTrajectory(Trajectory, IsSection);
        ITrajectory ITrajectory.Copy() => Copy();

        public void Render(OverlayData data) => Trajectory.GetBezier().RenderBezier(data);
        public override string ToString() => $"Straight: {StartPosition} - {EndPosition}";

        public static implicit operator Line3(StraightTrajectory trajectory) => trajectory.Trajectory;
        public static explicit operator StraightTrajectory(Line3 line) => new StraightTrajectory(line);

        public override bool Equals(object obj) => Equals(obj as StraightTrajectory);
        public bool Equals(StraightTrajectory other)
        {
            if (other is null)
                return false;
            else if (ReferenceEquals(this, other))
                return true;
            else
                return Equal(Trajectory, other.Trajectory);
        }
        private static bool Equal(Line3 first, Line3 second) => first.a == second.a && first.b == second.b;

        public static bool operator ==(StraightTrajectory first, StraightTrajectory second)
        {
            if (first is null)
                return second is null;
            else
                return first.Equals(second);
        }
        public static bool operator !=(StraightTrajectory first, StraightTrajectory second) => !(first == second);
    }
    public class TrajectoryBound : IOverlay
    {
        private static float Coef { get; } = Mathf.Sin(45 * Mathf.Deg2Rad);
        public ITrajectory Trajectory { get; }
        public float Size { get; }
        private List<Bounds> BoundsList { get; } = new List<Bounds>();
        public IEnumerable<Bounds> Bounds => BoundsList;
        public TrajectoryBound(ITrajectory trajectory, float size)
        {
            Trajectory = trajectory;
            Size = size;
            CalculateBounds();
        }

        private void CalculateBounds()
        {
            if (Trajectory == null)
                return;

            var size = Size * Coef;
            var t = 0f;
            while (t < 1f)
            {
                t = Trajectory.Travel(t, size / 2);
                BoundsList.Add(new Bounds(Trajectory.Position(t), Vector3.one * size));
            }
            BoundsList.Add(new Bounds(Trajectory.Position(0), Vector3.one * Size));
            BoundsList.Add(new Bounds(Trajectory.Position(1), Vector3.one * Size));
        }

        public bool IntersectRay(Ray ray) => BoundsList.Any(b => b.IntersectRay(ray));
        public bool Intersects(Bounds bounds) => BoundsList.Any(b => b.Intersects(bounds));

        public void Render(OverlayData data) => Trajectory.Render(data);
    }
    public class TrajectoryPoints
    {
        ITrajectory[] Trajectories { get; }
        int[] Counts { get; }
        int[] EndIndixes { get; }
        Vector2?[] Points { get; }
        public int Length => Points.Length;

        public Vector3 this[int index]
        {
            get
            {
                if (Points[index] == null)
                {
                    var t = GetT(index);
                    var i = Math.Min((int)t, Trajectories.Length - 1);
                    Points[index] = XZ(Trajectories[i].Position(t - i));
                }
                return Points[index].Value;
            }
        }

        public TrajectoryPoints(params ITrajectory[] trajectories)
        {
            Trajectories = trajectories;
            Counts = new int[Trajectories.Length];
            EndIndixes = new int[Trajectories.Length];

            for (var i = 0; i < Trajectories.Length; i += 1)
            {
                var length = Trajectories[i].Length;
                Counts[i] = Math.Max((int)(Mathf.Clamp(length, 0f, 200f) * 20), 2);
                EndIndixes[i] = EndIndixes[Math.Max(i - 1, 0)] + Counts[i];
            }

            Points = new Vector2?[EndIndixes.Last()];
        }

        private float GetT(int index)
        {
            var i = 0;
            while (index > EndIndixes[i])
                i += 1;

            return 1f / Counts[i] * (Counts[i] - EndIndixes[i] + index) + i;
        }
        public float Find(int startIndex, float distance, out int findIndex)
        {
            distance *= distance;

            findIndex = startIndex;
            var endIndex = EndIndixes.Last() - 1;
            var position = this[findIndex];

            while (findIndex <= endIndex)
            {
                var currentIndex = findIndex + (endIndex - findIndex >> 1);
                var diffirent = (this[currentIndex] - position).sqrMagnitude - distance;

                if (diffirent < 0)
                    findIndex = currentIndex + 1;
                else
                    endIndex = currentIndex - 1;
            }
            return GetT(findIndex);
        }
    }
}
