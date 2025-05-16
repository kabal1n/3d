using System;

// Представляет вектор в 3D-пространстве (x, y, z, w = 1 — однородные координаты). Также выполняет:
// - сложение / вычитание / умножение на число
// - нормализацию
// - скалярное и векторное произведение
// - преобразование вектора матрицей

namespace KursovayaKG
{
    public class Vec3
    {
        private readonly double[] components = { 0, 0, 0, 1 };

        public double X { get => components[0]; private set => components[0] = value; }
        public double Y { get => components[1]; private set => components[1] = value; }
        public double Z { get => components[2]; private set => components[2] = value; }

        private double Length { get; }

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
            Length = Math.Sqrt(x * x + y * y + z * z);
        }

        private Vec3() { }

        private double this[int index]
        {
            get => components[index];
            set => components[index] = value;
        }

        public static Vec3 operator -(Vec3 a, Vec3 b) => // вычитание
            new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => // сложение
            new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator *(double scalar, Vec3 v) =>
            new Vec3(v.X * scalar, v.Y * scalar, v.Z * scalar);

        public static Vec3 operator /(Vec3 v, double scalar) =>
            new Vec3(v.X / scalar, v.Y / scalar, v.Z / scalar);

        public static double Dot(Vec3 a, Vec3 b) => // скалярное произведение
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => // векторное произведение
            new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );

        public static Vec3 operator *(Mat4 matrix, Vec3 v) // умножение на матрицу
        {
            var result = new Vec3();
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    result[i] += matrix[i, j] * v[j];
            return result;
        }

        public Vec3 Normalize() => // нормализация (длина = 1)
            new Vec3(X / Length, Y / Length, Z / Length);
    }
}
