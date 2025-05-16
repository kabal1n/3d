using System;

// Представляет 4×4 матрицу

namespace KursovayaKG
{
    public class Mat4
    {
        private readonly double[,] data = new double[4, 4];

        private Mat4() { }

        //Индексатор this[i, j] позволяет удобно обращаться к элементам матрицы:
        // matrix[2, 3] = 1.0;
        // var x = matrix[0, 0];
        public double this[int row, int col]
        {
            get => data[row, col];
            private set => data[row, col] = value;
        }

        // Операция умножения матриц 4×4
        public static Mat4 operator *(Mat4 a, Mat4 b)
        {
            var result = new Mat4();
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                        result[i, j] += a[i, k] * b[k, j];
            return result;
        }

        // Матрица поворота вокруг оси Y
        public static Mat4 CreateRotationY(double angleDeg)
        {
            double angleRad = angleDeg * Math.PI / 180.0;
            return new Mat4
            {
                [0, 0] = Math.Cos(angleRad),
                [0, 2] = Math.Sin(angleRad),
                [2, 0] = -Math.Sin(angleRad),
                [2, 2] = Math.Cos(angleRad),
                [1, 1] = 1,
                [3, 3] = 1
            };
        }

        // Ортогональное проецирование
        public static Mat4 CreateOrthographic(double left, double right, double bottom, double top, double near, double far)
        {
            return new Mat4
            {
                [0, 0] = 2 / (right - left),
                [1, 1] = 2 / (top - bottom),
                [2, 2] = -2 / (far - near),
                [0, 3] = -(right + left) / (right - left),
                [1, 3] = -(top + bottom) / (top - bottom),
                [2, 3] = -(far + near) / (far - near),
                [3, 3] = 1
            };
        }

        // Viewport-преобразование в экранные координаты
        public static Mat4 CreateViewport(double offsetX, double offsetY, double width, double height, double depth)
        {
            return new Mat4
            {
                [0, 0] = width / 2.0,
                [1, 1] = -height / 2.0,
                [2, 2] = depth / 2.0,
                [0, 3] = offsetX + width / 2.0,
                [1, 3] = offsetY + height / 2.0,
                [2, 3] = depth / 2.0,
                [3, 3] = 1
            };
        }

        // Матрица LookAt (позиционирование камеры)
        public static Mat4 CreateLookAt(Vec3 right, Vec3 up, Vec3 back, Vec3 position)
        {
            return new Mat4
            {
                [0, 0] = right.X,
                [0, 1] = right.Y,
                [0, 2] = right.Z,
                [1, 0] = up.X,
                [1, 1] = up.Y,
                [1, 2] = up.Z,
                [2, 0] = back.X,
                [2, 1] = back.Y,
                [2, 2] = back.Z,
                [0, 3] = -position.X,
                [1, 3] = -position.Y,
                [2, 3] = -position.Z,
                [3, 3] = 1
            };
        }

        // Матрица масштабирования
        public static Mat4 CreateScale(double scaleX, double scaleY, double scaleZ)
        {
            return new Mat4
            {
                [0, 0] = scaleX,
                [1, 1] = scaleY,
                [2, 2] = scaleZ,
                [3, 3] = 1
            };
        }
    }
}
