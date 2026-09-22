// A stand-in for the simulation's PhysicsWorld, so the simulation still
// compiles outside Unity. The real one is Unity's physics engine, which only
// exists inside the editor or a built game, so this one refuses to be built:
// every test that creates a run stops at once with NeedsUnityPhysicsException,
// and the runner reports it as skipped, never as passed. Those tests run in
// the editor through tools\RunUnityTests.ps1.
//
// Keep the members in step with Assets/Paniq/Runtime/Simulation/PhysicsWorld.cs:
// if they drift, this harness stops compiling, which is the reminder.

using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>Thrown by the stand-in physics: this test needs Unity's physics engine.</summary>
    public sealed class NeedsUnityPhysicsException : Exception
    {
        public NeedsUnityPhysicsException()
            : base("This test needs Unity's physics engine; run it in the editor with tools\\RunUnityTests.ps1.")
        {
        }
    }

    internal sealed class PhysicsWorld : IDisposable
    {
        public const int SubMillimetre = 100;
        public const int RotationScale = 10000;

        internal enum StaticKind
        {
            None,
            Floor,
            Wall,
            Table,
            Door,
            Fence
        }

        internal struct Reading
        {
            public long X, Y, Z;
            public long CentreX, CentreZ;
            public long VelocityX, VelocityY, VelocityZ;
            public int RotationX, RotationY, RotationZ, RotationW;
            public int Heading;
            public int UprightPercent;
            public int SpinDegreesPerTick;
            public int BottomMillimetres;
            public bool Sleeping;

            public LogicalPosition Position => new LogicalPosition((int)(X / SubMillimetre), (int)(Z / SubMillimetre));

            public LogicalPosition Centre => new LogicalPosition((int)(CentreX / SubMillimetre), (int)(CentreZ / SubMillimetre));

            public long HorizontalSpeed => IntegerMath.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);
        }

        internal readonly struct Contact
        {
            public int BodyA { get; }
            public int BodyB { get; }
            public StaticKind Static { get; }
            public int StaticIndex { get; }
            public long Impulse { get; }
            public LogicalPosition Point { get; }
            public int PointHeight { get; }
            public bool Began { get; }
        }

        internal static void DisposeEveryWorld()
        {
        }

        public PhysicsWorld(PhysicsFeelSettings feel, LogicalBounds building, int wallRestitutionPercent)
        {
            throw new NeedsUnityPhysicsException();
        }

        public void Dispose()
        {
        }

        public void SetWalls(IReadOnlyList<NavigationGrid.Wall> stretches) => throw new NeedsUnityPhysicsException();
        public int AddTable(LogicalBounds bounds) => throw new NeedsUnityPhysicsException();
        public void RemoveTable(int index) => throw new NeedsUnityPhysicsException();
        public int AddDoor(LogicalPosition centre, bool alongX, int width, bool shut) => throw new NeedsUnityPhysicsException();
        public void SetDoorShut(int index, bool shut) => throw new NeedsUnityPhysicsException();
        public bool IsAnyBodyInDoorway(int door, int ignoreHandle) => throw new NeedsUnityPhysicsException();
        public int BodyCount => throw new NeedsUnityPhysicsException();

        public int AddBody(string name, ObjectShapes.Part[] parts, long x, long y, long z, int heading, int massGrams,
            int frictionPercent, int bouncinessPercent) => throw new NeedsUnityPhysicsException();

        public int AddPerson(string name, LogicalPosition position, int heading, int radius, int height, int massGrams) =>
            throw new NeedsUnityPhysicsException();

        public void SetUpright(int handle, bool upright, int heading) => throw new NeedsUnityPhysicsException();
        public void Face(int handle, int heading) => throw new NeedsUnityPhysicsException();
        public void SetGrip(int handle, int frictionPercent, int bouncinessPercent) => throw new NeedsUnityPhysicsException();
        public void IgnoreEachOther(int first, int second, bool ignore) => throw new NeedsUnityPhysicsException();
        public void LayAlong(int handle, int heading) => throw new NeedsUnityPhysicsException();

        public bool IsClearToStand(LogicalPosition spot, int radius, int height, int ownHandle) =>
            throw new NeedsUnityPhysicsException();

        public bool IsBuildingBetween(LogicalPosition from, LogicalPosition to) => throw new NeedsUnityPhysicsException();

        public bool IsClearToLie(LogicalPosition middle, int heading, int radius, int length, int ownHandle) =>
            throw new NeedsUnityPhysicsException();

        public void LayDown(int handle, int heading, int radius) => throw new NeedsUnityPhysicsException();
        public Reading Read(int handle) => throw new NeedsUnityPhysicsException();
        public void SetSolid(int handle, bool solid) => throw new NeedsUnityPhysicsException();
        public void SetPinned(int handle, bool pinned) => throw new NeedsUnityPhysicsException();
        public void Place(int handle, long x, long y, long z, int heading) => throw new NeedsUnityPhysicsException();
        public void Drive(int handle, LogicalPosition position, int heading) => throw new NeedsUnityPhysicsException();
        public void SetVelocity(int handle, long vx, long vy, long vz) => throw new NeedsUnityPhysicsException();
        public void AddVelocity(int handle, long vx, long vy, long vz) => throw new NeedsUnityPhysicsException();
        public void SetSpin(int handle, int aboutX, int aboutY, int aboutZ) => throw new NeedsUnityPhysicsException();
        public void SetMass(int handle, int massGrams) => throw new NeedsUnityPhysicsException();
        public void Resize(int handle, int percent) => throw new NeedsUnityPhysicsException();
        public IReadOnlyList<Contact> Contacts => throw new NeedsUnityPhysicsException();
        public int DeepestPressMillimetres => throw new NeedsUnityPhysicsException();
        public (int BodyA, int BodyB, StaticKind Static) DeepestPressPair => throw new NeedsUnityPhysicsException();
        public void Step() => throw new NeedsUnityPhysicsException();
    }
}
