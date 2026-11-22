using System;

namespace Cricket.Simulation.Core.Math
{
    /// <summary>
    /// Engine-independent 2D coordinate vector (in meters) used by pure C# simulation
    /// and rules modules without referencing UnityEngine.
    /// Convention on pitch/ground plane: X = lateral (+off-side for right-hand batter), Z = pitch axis.
    /// </summary>
    [Serializable]
    public readonly struct SimVector2 : IEquatable<SimVector2>
    {
        public float X { get; }
        public float Z { get; }

        public SimVector2(float x, float z)
        {
            X = x;
            Z = z;
        }

        public static SimVector2 Zero => new SimVector2(0f, 0f);

        public float SqrMagnitude => (X * X) + (Z * Z);
        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public float DistanceTo(SimVector2 other)
        {
            float dx = X - other.X;
            float dz = Z - other.Z;
            return (float)System.Math.Sqrt((dx * dx) + (dz * dz));
        }

        public bool Equals(SimVector2 other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is SimVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"({X:F2}, {Z:F2})";
    }

    /// <summary>
    /// Engine-independent 3D coordinate vector (in meters).
    /// Convention: X = lateral, Y = vertical height above pitch surface, Z = pitch axis.
    /// </summary>
    [Serializable]
    public readonly struct SimVector3 : IEquatable<SimVector3>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public SimVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SimVector3 Zero => new SimVector3(0f, 0f, 0f);

        public float SqrMagnitude => (X * X) + (Y * Y) + (Z * Z);
        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public bool Equals(SimVector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is SimVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
