// Описывает вершину треугольника в 3D-модели

namespace KursovayaKG
{
    public class Point3D
    {
        public Vec3 Position;  // Координата вершины в пространстве
        public Vec3 NormalVec; // Нормаль в вершине

        public Point3D(Vec3 pos, Vec3 normal)
        {
            Position = pos;
            NormalVec = normal;
        }
    }
}
