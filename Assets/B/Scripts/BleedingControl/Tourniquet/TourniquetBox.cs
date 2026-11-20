using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    // Oriented box used only for the tourniquet zone check (part volumes and block zones). Pure geometry, no physics.
    internal readonly struct TourniquetBox
    {
        public readonly Vector3 Center;
        // Half-extent vectors along the box axes (direction times half size).
        public readonly Vector3 X;
        public readonly Vector3 Y;
        public readonly Vector3 Z;

        public TourniquetBox(Vector3 center, Vector3 x, Vector3 y, Vector3 z)
        {
            Center = center;
            X = x;
            Y = y;
            Z = z;
        }

        // Box of the given centre and size in the local space of matrix (position, rotation and scale applied).
        public static TourniquetBox FromMatrix(Matrix4x4 matrix, Vector3 center, Vector3 size) =>
            new TourniquetBox(matrix.MultiplyPoint3x4(center), matrix.MultiplyVector(new Vector3(size.x * 0.5f, 0f, 0f)),
                matrix.MultiplyVector(new Vector3(0f, size.y * 0.5f, 0f)), matrix.MultiplyVector(new Vector3(0f, 0f, size.z * 0.5f)));

        // Separating axis test: face axes of both boxes and their nine cross products.
        public bool Overlaps(in TourniquetBox other)
        {
            var d = other.Center - Center;
            return !Separated(X, d, other) && !Separated(Y, d, other) && !Separated(Z, d, other)
                && !Separated(other.X, d, other) && !Separated(other.Y, d, other) && !Separated(other.Z, d, other)
                && !Separated(Vector3.Cross(X, other.X), d, other) && !Separated(Vector3.Cross(X, other.Y), d, other)
                && !Separated(Vector3.Cross(X, other.Z), d, other) && !Separated(Vector3.Cross(Y, other.X), d, other)
                && !Separated(Vector3.Cross(Y, other.Y), d, other) && !Separated(Vector3.Cross(Y, other.Z), d, other)
                && !Separated(Vector3.Cross(Z, other.X), d, other) && !Separated(Vector3.Cross(Z, other.Y), d, other)
                && !Separated(Vector3.Cross(Z, other.Z), d, other);
        }

        bool Separated(Vector3 axis, Vector3 d, in TourniquetBox other)
        {
            // Parallel edges give no axis; the face axes already cover that case.
            if (axis.sqrMagnitude < 1e-16f)
                return false;
            var a = Mathf.Abs(Vector3.Dot(X, axis)) + Mathf.Abs(Vector3.Dot(Y, axis)) + Mathf.Abs(Vector3.Dot(Z, axis));
            var b = Mathf.Abs(Vector3.Dot(other.X, axis)) + Mathf.Abs(Vector3.Dot(other.Y, axis)) + Mathf.Abs(Vector3.Dot(other.Z, axis));
            return Mathf.Abs(Vector3.Dot(d, axis)) > a + b;
        }
    }
}
