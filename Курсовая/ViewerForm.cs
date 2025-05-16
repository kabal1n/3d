using System;
using System.Collections.Generic;
using System.Drawing; // для отрисовки (цвета, графика, формы)
using System.IO; 
using System.Linq;
using System.Windows.Forms; //  для создания формы и UI-элементов

// Это главная форма и логика отрисовки:
// - загружает.obj файл
// - строит 3D-сцену
// - применяет все матричные преобразовани
// - выполняет Z-буфер и освещение по Фонгу
// - рисует результат на экране
// - обрабатывает нажатия клавиш (вращение модели)



namespace KursovayaKG
{
    public class Object3D
    {
        public List<Point3D[]> faces = new List<Point3D[]>(); // список треугольников
        public List<Point3D[]> transformedFaces = new List<Point3D[]>(); //  список тех же треугольников, но после применения всех матриц преобразований
        public Color baseColor; //  цвет фигуры

        public Object3D(Color color)
        {
            baseColor = color;
        }
    }

    // главная форма приложения
    public sealed partial class ViewerForm : Form
    {
        private const string ObjFileName = "9.obj"; // сцена
        private Object3D circleObject = new Object3D(Color.FromArgb(100, 0, 0)); // звезда красным цветом
        private Object3D pyramidObject = new Object3D(Color.FromArgb(0, 0, 100)); // конус куб синим

        private Mat4 fullTransform; // итоговая матрицу: viewport * projection * lookAt * scale. Применяется к каждой вершине при отрисовке
        private static int angleY; // угол поворта, меняется при нажатии стрелок

        private readonly List<List<double>> depthMap = new List<List<double>>(); // Z-буфер — двумерная таблица глубин для каждого пикселя. чтобы ближние закрывали дальние

        private double minX = 1e6, maxX = -1e6; // Используются при построении ортогональной проекции — чтобы вычислить, какая часть сцены видна.
        private double minY = 1e6, maxY = -1e6;
        private double minZ = 1e6, maxZ = -1e6;

        public ViewerForm()
        {
            InitializeComponent();

            Width = 1280;
            Height = 760;
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                ControlStyles.OptimizedDoubleBuffer | 
                ControlStyles.UserPaint, true);

            ParseObjFile();
            BuildFullTransform();

            Paint += OnRender;
            KeyDown += OnKeyInput;
        }

        private void OnRender(object sender, PaintEventArgs e)
        {
            ApplyModelTransformations();
            DrawScene(e.Graphics);
        }

        private void OnKeyInput(object sender, KeyEventArgs e) // обрабатывает нажатия клавиш A/D или стрелок ←/→
        {
            if (e.KeyCode == Keys.Right)
                angleY -= 10; // изменяется угол
            else if (e.KeyCode == Keys.Left)
                angleY += 10; // изменяется угол

            Refresh(); // вызывает перерисовку формы
        }

        private void ParseObjFile() // читает .obj 
        {
            // v <x> <y> <z> - вершины
            // vn <x> <y> <z> - нормали
            // f a/b/c d/e/f g/h/i - треугорльники
            // o <имя> - названия объектов

            var vertexList = new List<Vec3>(); // список всех вершин
            var normalList = new List<Vec3>(); // список всех нормалей
            string currentObject = ""; // имя текущего объекта

            foreach (var line in File.ReadAllLines(ObjFileName))  // построчная читка файла
            {
                var parts = line.Replace('.', ',').Split(' '); // замена . на , и делим строку по пробелам

                switch (parts[0])
                {
                    case "o":  // новая фигура
                        currentObject = parts[1]; // сохраняем имя 
                        break;

                    case "v": // вершина
                        var vx = double.Parse(parts[1]);
                        var vy = double.Parse(parts[2]);
                        var vz = double.Parse(parts[3]);

                        // читаем координаты вершины и добавляем их в список vertexList
                        vertexList.Add(new Vec3(vx, vy, vz));

                        // обновляем границы сцены (минимумы и максимумы)
                        minX = Math.Min(minX, vx); maxX = Math.Max(maxX, vx);
                        minY = Math.Min(minY, vy); maxY = Math.Max(maxY, vy);
                        minZ = Math.Min(minZ, vz); maxZ = Math.Max(maxZ, vz);
                        break;

                    case "vn": // нормаль

                        // Создаём вектор нормали и добавляем его в список
                        normalList.Add(new Vec3(
                            double.Parse(parts[1]),
                            double.Parse(parts[2]),
                            double.Parse(parts[3])));
                        break;

                    case "f": // грань (треугольник)

                        // Каждая вершина записана в формате v_index//vn_index или v_index/vt_index/vn_index
                        // Мы берём только:
                        // - v_index — индекс вершины и
                        // - vn_index — индекс нормали
                        var indices = parts.Skip(1)
                                           .Select(p => p.Split('/'))
                                           .ToArray();

                        var polygon = indices.Select(index => // построение полигона
                        {
                            int vi = int.Parse(index[0]) - 1; // индекс вершины
                            int ni = int.Parse(index[2]) - 1; // индекс нормали
                            return new Point3D(vertexList[vi], normalList[ni]); // массив из 3 штук - 1 полигон
                        }).ToArray();

                        if (currentObject.Contains("Окружность")) // Если название текущего объекта содержит "Окружность", значит это круг, добавляем в circleObject
                            circleObject.faces.Add(polygon);
                        else // Иначе — во вторую фигуру (конус, куб)
                            pyramidObject.faces.Add(polygon);
                        break;
                }
            }
        }

        private void BuildFullTransform() // создаёт полную матрицу преобразования сцены
        {
            // Viewport переводит нормализованные координаты в экранные.
            // Центр экрана сдвигается в (250, -200).
            // Масштабируется по ширине / высоте формы(Width, Height).
            // depth определяет глубину Z-буфера.
            var viewport = Mat4.CreateViewport(250, -200, Width, Height, maxZ - minZ);

            // Преобразует 3D-модель в нормализованный вид без перспективы при использовании границ сцены
            var projection = Mat4.CreateOrthographic(minX, maxX, minY, maxY, maxZ, minZ);

            // Камера расположена в точке (0.6, 0.4, 0.8) и смотрит в центр сцены (0, 0, 0)
            var cameraPosition = new Vec3(0.6, 0.4, 0.8);
            var lookAtPoint = new Vec3(0, 0, 0);
            var forward = cameraPosition - lookAtPoint; // направление «взгляда» камеры (от камеры к центру)

            // Построение системы координат камеры (базиса)
            var right = Vec3.Cross(new Vec3(0, 1, 0), forward); // вектор вправо (X)
            var up = Vec3.Cross(forward, right); // вектор вверх (Y)
                                                 // Эти три вектора задают локальную систему координат камеры

            // Переводит сцену в систему координат камеры
            // То есть камера становится точкой(0, 0, 0), а всё остальное сдвигается относительно неё
            var view = Mat4.CreateLookAt(right, up, forward, cameraPosition);

            // Масштабирование объекта. Уменьшает модель по X, Y и Z.
            var scale = Mat4.CreateScale(0.2, 0.5, 0.2);

            // Объединение всех матриц
            // 1. Сначала масштаб(scale)
            // 2. Затем поворот и сдвиг через камеру(view)
            // 3. Потом проекция(projection)
            // 4. И перевод в экранные координаты(viewport)
            fullTransform = viewport * projection * view * scale;
        }

        private void ApplyModelTransformations() // поворот модели на ->/<-
        {
            // Создаёт матрицу поворота сцены на заданный угол angleY. Значение angleY меняется при нажатии клавиш A / D или ← / →.
            var rotationY = Mat4.CreateRotationY(angleY);

            // Для каждой грани (треугольника):
            circleObject.transformedFaces = circleObject.faces
                .Select(face => face
                    // Для каждой вершины (Point3D) преобразует её позицию: полная_матрица * поворот * координата
                    .Select(pt => new Point3D(fullTransform * rotationY * pt.Position, pt.NormalVec))
                    // Новый треугольник с обновлёнными координатами
                    .ToArray())
                // Сортирует треугольники по глубине (Z-среднему), чтобы ближние рисовались после дальних.
                .OrderBy(face => face.Average(v => v.Position.Z))
                .ToList();

            // Полностью аналогично circleObject — только с другим списком граней
            pyramidObject.transformedFaces = pyramidObject.faces
                .Select(face => face
                    .Select(pt => new Point3D(fullTransform * rotationY * pt.Position, pt.NormalVec))
                    .ToArray())
                .OrderBy(face => face.Average(v => v.Position.Z))
                .ToList();
        }

        private void DrawScene(Graphics g) // рисование всекх полигонов
        {
            InitDepthMap();

            // Берёт каждую грань из преобразованных фигур
            // Передаёт её в RenderTriangle(...) — метод, который:
            // 1. заполняет треугольник
            // 2. вычисляет освещённость(по нормалям)
            // 3. проверяет глубину по Z-буферу


            foreach (var face in circleObject.transformedFaces)
                RenderTriangle(face[0], face[1], face[2], g, circleObject.baseColor);

            foreach (var face in pyramidObject.transformedFaces)
                RenderTriangle(face[0], face[1], face[2], g, pyramidObject.baseColor);
        }

        private void InitDepthMap() // Инициализирует Z-буфер — двухмерный массив значений глубины (Z-координат) на каждый пиксель экрана.
        {
            depthMap.Clear(); // двумерная таблица
            for (int x = 0; x < Width; x++)
            {
                var row = new List<double>();
                for (int y = 0; y < Height; y++)
                    row.Add(double.MinValue);
                depthMap.Add(row); // Изначально все значения минимальные (double.MinValue), то что ближе будет рисоваться поверх
            }
        }

        private static Color ComputePhongColor(Vec3 normal, Color baseCol) //рассчитывает цвет в каждой точке на основе модели освещения Фонг
        {
            float rf = baseCol.R / 255f;
            float gf = baseCol.G / 255f;
            float bf = baseCol.B / 255f;

            var lightDir = new Vec3(1, 1, 0).Normalize();
            var reflection = new Vec3(0, 0, 0);
            var viewDir = lightDir;

            var dotNL = Vec3.Dot(normal, lightDir);
            if (dotNL >= 0)
                reflection = (lightDir - 2 * dotNL * normal).Normalize();

            float ambient = 1.0f;
            float diffuse = (float)Math.Max(Vec3.Dot(normal.Normalize(), lightDir), 0.0) * 0.5f;
            double specular = Math.Pow(Math.Max(0, Vec3.Dot(reflection, lightDir)), 32) * 0.05;

            int r = Clamp((int)(255 * (ambient * rf + diffuse + specular)));
            int g = Clamp((int)(255 * (ambient * gf + diffuse + specular)));
            int b = Clamp((int)(255 * (ambient * bf + diffuse + specular)));

            return Color.FromArgb(r, g, b); // цвет пикселя - результат
        }

        private static int Clamp(int val, int min = 0, int max = 255) =>
            Math.Min(max, Math.Max(min, val));

        private void RenderTriangle(Point3D v0, Point3D v1, Point3D v2, Graphics g, Color col) // сердце 3D-отображения
        {

            // Сортируем вершины так, чтобы v0 — верхняя, v2 — нижняя.
            if (v0.Position.Y < v2.Position.Y) (v0, v2) = (v2, v0);
            if (v0.Position.Y < v1.Position.Y) (v0, v1) = (v1, v0);
            if (v1.Position.Y < v2.Position.Y) (v1, v2) = (v2, v1);

            double x1 = 0, x2 = 0, z1 = 0, z2 = 0;
            Vec3 n1 = new Vec3(0, 0, 0), n2 = new Vec3(0, 0, 0);

            // Каждая строка — горизонтальный отрезок между левой и правой границей треугольника. идем сверху вниз по Y
            for (double y = v0.Position.Y; y >= v2.Position.Y; y--)
            {
                bool upper = y >= v1.Position.Y;
                bool lower = y <= v1.Position.Y;

                if (upper) // Верхняя часть треугольника
                {
                    double t0 = (v0.Position.Y - y) / (v0.Position.Y - v2.Position.Y);
                    double t1 = (v0.Position.Y - y) / (v0.Position.Y - v1.Position.Y);

                    // Используем линейную интерполяцию(Lerp) между точками. Получаем координаты концов отрезка x1, x2, z1, z2, n1, n2.
                    x1 = Lerp(v0.Position.X, v2.Position.X, t0);
                    x2 = Lerp(v0.Position.X, v1.Position.X, t1);
                    z1 = Lerp(v0.Position.Z, v2.Position.Z, t0);
                    z2 = Lerp(v0.Position.Z, v1.Position.Z, t1);

                    n1 = v0.NormalVec + t0 * (v2.NormalVec - v0.NormalVec);
                    n2 = v0.NormalVec + t1 * (v1.NormalVec - v0.NormalVec);
                }
                else if (lower) // Нижняя часть:
                {
                    double t0 = (v2.Position.Y - y) / (v2.Position.Y - v0.Position.Y);
                    double t1 = (v2.Position.Y - y) / (v2.Position.Y - v1.Position.Y);

                    // Используем линейную интерполяцию(Lerp) между точками. Получаем координаты концов отрезка x1, x2, z1, z2, n1, n2.
                    x1 = Lerp(v2.Position.X, v0.Position.X, t0);
                    x2 = Lerp(v2.Position.X, v1.Position.X, t1);
                    z1 = Lerp(v2.Position.Z, v0.Position.Z, t0);
                    z2 = Lerp(v2.Position.Z, v1.Position.Z, t1);

                    n1 = v2.NormalVec - t0 * (v2.NormalVec - v0.NormalVec);
                    n2 = v2.NormalVec - t1 * (v2.NormalVec - v1.NormalVec);
                }

                for (double x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++) // Цикл по пикселям в строке
                {
                    // для каждого x интерполируем нормаль, интерполируем z, сравниваем с depthMap
                    double alpha = (x - x1) / (x2 - x1);
                    var normal = n1 + alpha * (n2 - n1);
                    double z = Lerp(z1, z2, alpha);

                    // проверка глубины отрезка
                    if (depthMap[(int)x][(int)y] <= z)
                    {
                        depthMap[(int)x][(int)y] = z; // если пиксели ближе - обновляем depthMap
                        var pixelColor = ComputePhongColor(normal.Normalize(), col);
                        g.FillRectangle(new SolidBrush(pixelColor), (int)x - 1, (int)y - 1, 2, 2); // Рисуем квадратик 2×2 пикселя для видимости
                    }
                }
            }
        }

        // Линейная интерполяция между a и b по параметру t (от 0 до 1).
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
