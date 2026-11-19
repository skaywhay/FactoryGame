using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Factory
{
    // Пост в зале: домик-станочек + подпись над ним + прогресс-бар.
    // Root — это пустышка-родитель, чтобы удобно двигать всю группу.
    public class Station
    {
        public Transform Root;
        public TextMesh Title;
        public TextMesh Info;
        public Transform ProgressFill;
        public readonly List<Transform> Spinners = new List<Transform>(); // что крутится
        public Color Accent = Color.white;
    }

    // Одна линия в зале: ряд из трёх станков + табличка «линия не построена».
    public class LineView
    {
        public Transform Root;
        public Transform Empty;
        public Station Sign;
        public Station[] Stations;

        // Красная бракованная деталь над складом. Видна только когда Defect активен.
        public GameObject Defect;
        public TextMesh DefectLabel;
    }

    // Метка бракованной детали. Она нужна, чтобы по клику из зала
    // понять, на какую линию жалуется игрок.
    public class DefectMarker : MonoBehaviour
    {
        public int Line;
    }

    public class HallRefs
    {
        public LineView[] Lines;
        public Station[] Generators;

        // Тут раньше лежал один Station Assembly, потом появились ещё рецепты
        // и пришлось сделать массив. Порядок = порядок Recipes.All.
        public Station[] Assembly;

        public Camera Camera;
    }

    // Собирает весь 3D-зал кодом. Координаты простые:
    //   X — влево-вправо, Z — глубина (дальше = больше), Y — вверх.
    // Камера смотрит с юга на север, поэтому «задняя» площадка — это большие Z.
    public static class HallBuilder
    {
        static Material _atlas;
        static Font _font;

        const float RowGap = 6.2f; // расстояние между линиями по Z

        static readonly Color BuyerColor = new Color(0.98f, 0.62f, 0.22f);
        static readonly Color ProducerColor = new Color(0.35f, 0.78f, 0.98f);
        static readonly Color SellerColor = new Color(0.55f, 0.90f, 0.45f);
        static readonly Color GeneratorColor = new Color(0.99f, 0.85f, 0.30f);
        static readonly Color AssemblyColor = new Color(0.95f, 0.58f, 0.72f);

        // Цвета линий. Должны совпадать с Hud.LineColors, иначе вкладки
        // и станки в зале будут разного цвета (уже наступал на это).
        static readonly Color[] LineColors =
        {
            new Color(0.98f, 0.74f, 0.28f),
            new Color(0.30f, 0.82f, 0.86f),
            new Color(0.72f, 0.56f, 0.98f)
        };

        // Камера смотрит немного сверху и сбоку. Цифры подбирал мышкой,
        // чтобы влезли и линии, и задняя площадка со сборкой.
        static readonly Vector3 CameraPos = new Vector3(2.2f, 20.5f, -19.5f);
        static readonly Vector3 CameraTarget = new Vector3(2.6f, 0.5f, 3.0f);

        // Собираем зал в фиксированном порядке: свет -> пол -> стены -> декор ->
        // линии -> генераторы -> сборка -> камера. Камера последняя, чтобы
        // сразу смотрела куда надо.
        public static HallRefs Build(Transform root)
        {
            LoadAssets();

            var refs = new HallRefs();

            CreateLighting();
            CreateFloor(root);
            CreateWalls(root);
            CreateProps(root);

            refs.Lines = new LineView[ProductInfo.Count];
            for (int i = 0; i < ProductInfo.Count; i++)
                refs.Lines[i] = CreateLine(root, i, RowCenter(i));

            // Генераторы уехали к стенам
            refs.Generators = new Station[2];
            refs.Generators[0] = CreateGenerator(root, new Vector3(-5f, 0f, 10.6f), 0);
            refs.Generators[1] = CreateGenerator(root, new Vector3(5f, 0f, 10.6f), 1);

            refs.Assembly = new Station[Recipes.All.Length];
            for (int i = 0; i < Recipes.All.Length; i++)
            {
                // -6 / 0 / +6
                float z = (i - 1) * 6f;
                refs.Assembly[i] = CreateAssembly(root, new Vector3(12f, 0f, z), i);
            }

            refs.Camera = CreateCamera(root);
            return refs;
        }

        // Центрируем три линии вокруг нуля: -6.2 / 0 / +6.2.
        static float RowCenter(int index)
        {
            return (index - 1) * RowGap;
        }

        // Грузим общий атлас из FactoryKit. Если текстуры нет — красим всё серым,
        // игра всё равно запустится (пусть и выглядит скучно).
        static void LoadAssets()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            _atlas = new Material(shader);
            _atlas.name = "Atlas";
            Texture2D texture = Resources.Load<Texture2D>("FactoryKit/Textures/colormap");
            if (texture != null) _atlas.mainTexture = texture;
            else _atlas.color = new Color(0.75f, 0.78f, 0.82f);
            if (_atlas.HasProperty("_Glossiness")) _atlas.SetFloat("_Glossiness", 0.25f);

            try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { _font = null; }
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { _font = null; }
            }
        }

        // Простая заливка цветом. Каждый вызов создаёт новый материал —
        // для прототипа нормально, для продакшена надо бы кэшировать.
        static Material Solid(string name, Color color, float smoothness = 0.15f)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        // Unlit нужен для прогресс-баров: они не должны зависеть от света.
        static Material Unlit(string name, Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        // Все модели из FactoryKit используют один атлас, поэтому материал
        // просто подменяем целиком, не разбираясь с подматериалами.
        static void ApplyAtlas(GameObject instance)
        {
            if (_atlas == null) return;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = _atlas;
        }

        // Spawn ставит модель на пол и масштабирует по высоте.
        // Высоту задаём вручную, потому что модели в наборе разного размера.
        static GameObject Spawn(string model, Transform parent, Vector3 position, float height, float yaw = 0f)
        {
            GameObject prefab = Resources.Load<GameObject>("FactoryKit/" + model);
            if (prefab == null) return null;

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = model;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            ApplyAtlas(instance);
            ScaleByHeight(instance, height);
            AlignBottom(instance, position.y);
            return instance;
        }

        // В отличие от Spawn, тут масштаб по самой большой стороне.
        // Нужно для шестерёнок: у них важна не высота, а диаметр.
        static GameObject SpawnSized(string model, Transform parent, Vector3 position, float maxSize, float yaw = 0f)
        {
            GameObject prefab = Resources.Load<GameObject>("FactoryKit/" + model);
            if (prefab == null) return null;

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = model;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            ApplyAtlas(instance);
            ScaleByMax(instance, maxSize);
            return instance;
        }

        static void ScaleByHeight(GameObject instance, float height)
        {
            Bounds bounds;
            if (!TryGetBounds(instance, out bounds) || bounds.size.y < 0.0001f) return;
            instance.transform.localScale *= height / bounds.size.y;
        }

        static void ScaleByMax(GameObject instance, float size)
        {
            Bounds bounds;
            if (!TryGetBounds(instance, out bounds)) return;

            float max = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (max < 0.0001f) return;
            instance.transform.localScale *= size / max;
        }

        static void AlignBottom(GameObject instance, float floorY)
        {
            Bounds bounds;
            if (!TryGetBounds(instance, out bounds)) return;
            instance.transform.position += Vector3.up * (floorY - bounds.min.y);
        }

        static bool TryGetBounds(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds(instance.transform.position, Vector3.zero);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        // Шестерёнка крутится вокруг своей оси, поэтому заводим отдельный pivot:
        // саму модель крутить нельзя, у неё центр не в центре.
        static Transform CreateCog(Transform parent, string model, Vector3 floorPoint, float diameter, float roll)
        {
            var pivot = new GameObject("Cog " + model);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = floorPoint + Vector3.up * (diameter * 0.5f + 0.09f);
            pivot.transform.localRotation = Quaternion.Euler(90f, 0f, roll);

            GameObject cog = SpawnSized(model, pivot.transform, Vector3.zero, diameter);
            if (cog == null)
            {
                Object.Destroy(pivot);
                return null;
            }

            Bounds bounds;
            if (TryGetBounds(cog, out bounds)) cog.transform.position += pivot.transform.position - bounds.center;
            return pivot.transform;
        }

        static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.44f, 0.47f, 0.55f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.10f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 58f;

            var key = new GameObject("Key Light");
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.95f, 0.85f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            var fill = new GameObject("Fill Light");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.55f, 0.68f, 1f);
            fillLight.intensity = 0.35f;
            fillLight.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, 145f, 0f);
        }

        // Пол и швы. Швы — просто тонкие кубики поверх плоскости,
        // чтобы пол не выглядел однотонным. Коллайдеры удаляем, они тут не нужны.
        static void CreateFloor(Transform root)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(0f, 0f, 2.5f);
            floor.transform.localScale = new Vector3(3.6f, 1f, 3f);
            floor.GetComponent<Renderer>().sharedMaterial = Solid("Floor", new Color(0.17f, 0.19f, 0.24f), 0.3f);
            Object.Destroy(floor.GetComponent<Collider>());

            Material seam = Solid("Seam", new Color(0.235f, 0.26f, 0.315f), 0.2f);
            for (int x = -6; x <= 6; x++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "Seam X";
                line.transform.SetParent(root, false);
                line.transform.position = new Vector3(x * 2.4f, 0.005f, 2.5f);
                line.transform.localScale = new Vector3(0.06f, 0.01f, 24f);
                line.GetComponent<Renderer>().sharedMaterial = seam;
                Object.Destroy(line.GetComponent<Collider>());
            }

            for (int z = -4; z <= 6; z++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "Seam Z";
                line.transform.SetParent(root, false);
                line.transform.position = new Vector3(0f, 0.005f, z * 2.4f + 1f);
                line.transform.localScale = new Vector3(24f, 0.01f, 0.06f);
                line.GetComponent<Renderer>().sharedMaterial = seam;
                Object.Destroy(line.GetComponent<Collider>());
            }

            // одна длинная плита под всеми постами: 2 генератора + 3 сборки
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Generator Pad";
            pad.transform.SetParent(root, false);
            pad.transform.position = new Vector3(0f, 0.02f, 11.0f);
            pad.transform.localScale = new Vector3(21f, 0.04f, 4.8f);
            pad.GetComponent<Renderer>().sharedMaterial = Solid("Pad", new Color(0.22f, 0.21f, 0.16f));
            Object.Destroy(pad.GetComponent<Collider>());
        }

        // Декор по краям. Слева-справа конвейеры и знаки, чтобы зал не казался пустым.
        // Ящики у генераторов убрал: они стояли ровно там, куда встали новые посты.
        static void CreateProps(Transform root)
        {
            // TODO: сюда просились ящики у ±9.4, но там теперь генераторы.
            // Оставил только конвейеры и знаки, иначе коробки торчат из станков.
            Spawn("conveyor", root, new Vector3(-9.5f, 0f, 0.5f), 0.5f, 90f);
            Spawn("conveyor", root, new Vector3(9.5f, 0f, 0.5f), 0.5f, 90f);
            Spawn("warning-traffic", root, new Vector3(-9.1f, 0f, -6.4f), 0.72f, 10f);
            Spawn("warning-traffic", root, new Vector3(9.1f, 0f, -6.4f), 0.72f, -15f);
            Spawn("warning-traffic", root, new Vector3(-9.1f, 0f, 4.6f), 0.72f, 20f);
            Spawn("warning-traffic", root, new Vector3(9.1f, 0f, 4.6f), 0.72f, -25f);
        }

        // Задняя стена собрана из кусочков structure-wall. Это не из-за красоты:
        // целая стена из одной модели смотрелась растянутой.
        static void CreateWalls(Transform root)
        {
            Material wall = Solid("Wall", new Color(0.17f, 0.19f, 0.24f), 0.1f);
            Material trim = Solid("Wall Trim", new Color(0.26f, 0.29f, 0.35f), 0.2f);

            for (int i = -9; i <= 9; i++)
            {
                GameObject piece = Spawn("structure-wall", root, new Vector3(i * 1.18f, 0f, 13.4f), 3.6f);
                if (piece != null) Paint(piece, wall);
            }

            Spawn("structure-corner-outer", root, new Vector3(-10.9f, 0f, 13.4f), 3.6f);
            Spawn("structure-corner-outer", root, new Vector3(10.9f, 0f, 13.4f), 3.6f, 90f);

            for (int i = 0; i < 3; i++)
            {
                GameObject left = Spawn("structure-wall", root, new Vector3(-10.9f, 0f, 12.2f - i * 1.18f), 3.6f, 90f);
                GameObject right = Spawn("structure-wall", root, new Vector3(10.9f, 0f, 12.2f - i * 1.18f), 3.6f, 90f);
                if (left != null) Paint(left, wall);
                if (right != null) Paint(right, wall);
            }

            Edge(root, trim, new Vector3(0f, 0.12f, 12.55f), new Vector3(21f, 0.9f, 0.14f));
        }

        static void Paint(GameObject instance, Material material)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = material;
        }

        // Создаёт одну линию: площадка + три станка + пустышка-заглушка,
        // которая видна, пока линия не построена.
        static LineView CreateLine(Transform root, int index, float z)
        {
            Color tone = LineColors[index];
            ProductKind kind = (ProductKind)index;
            var view = new LineView();

            var row = new GameObject("Line " + (index + 1));
            row.transform.SetParent(root, false);
            row.transform.localPosition = new Vector3(0f, 0f, z);
            view.Root = row.transform;

            CreateBay(row.transform, tone, 0.26f);

            view.Stations = new Station[3];
            view.Stations[0] = CreateBuyer(row.transform, new Vector3(-5.3f, 0f, 0f), tone);
            view.Stations[1] = CreateProducer(row.transform, new Vector3(0f, 0f, 0f), tone);
            view.Stations[2] = CreateSeller(row.transform, new Vector3(5.3f, 0f, 0f), tone);

            var empty = new GameObject("Empty " + (index + 1));
            empty.transform.SetParent(root, false);
            empty.transform.localPosition = new Vector3(0f, 0f, z);
            view.Empty = empty.transform;

            CreateBay(empty.transform, tone, 0.14f, true);

            view.Sign = CreateSign(root, index, z);
            CreateDefectMarker(row.transform, view, index);
            return view;
        }

        // Красная деталь над продавцом. Сделана из кубика, чтобы её было видно
        // даже боковым зрением, и оставил коллайдер — по нему ловим клик.
        static void CreateDefectMarker(Transform parent, LineView view, int index)
        {
            var marker = new GameObject("Defect " + (index + 1));
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(5.3f, 2.35f, 0.55f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Bad Part";
            cube.transform.SetParent(marker.transform, false);
            cube.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            cube.GetComponent<Renderer>().sharedMaterial = Solid("Defect Red " + index,
                new Color(0.93f, 0.12f, 0.12f), 0.45f);

            // Коллайдер кубика НЕ удаляем: FactoryGame делает по нему Raycast.
            marker.AddComponent<DefectMarker>().Line = index;

            view.DefectLabel = MakeLabel(marker.transform, "БРАК", new Vector3(0f, 0.78f, 0f),
                new Color(1f, 0.55f, 0.5f), 0.07f);

            marker.SetActive(false);
            view.Defect = marker;
        }

        static Station CreateSign(Transform root, int index, float z)
        {
            Color tone = LineColors[index];
            ProductKind kind = (ProductKind)index;

            Station sign = NewStation(root, "Sign " + (index + 1), new Vector3(-9.0f, 0f, z + 0.1f), tone);
            Vector3 toCamera = CameraPos - sign.Root.localPosition;
            sign.Root.localRotation = Quaternion.Euler(0f, Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg, 0f);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(sign.Root, false);
            board.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            board.transform.localScale = new Vector3(3.9f, 1.35f, 0.1f);
            board.GetComponent<Renderer>().sharedMaterial = Solid("Board " + index,
                Color.Lerp(tone, new Color(0.09f, 0.10f, 0.13f), 0.52f), 0.3f);
            Object.Destroy(board.GetComponent<Collider>());

            Edge(sign.Root, Solid("Sign Strip " + index, tone, 0.2f),
                new Vector3(0f, 1.3f, 0.06f), new Vector3(3.9f, 0.1f, 0.02f));

            for (int i = -1; i <= 1; i += 2)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                leg.transform.SetParent(sign.Root, false);
                leg.transform.localPosition = new Vector3(i * 1.68f, 0.6f, 0f);
                leg.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
                leg.GetComponent<Renderer>().sharedMaterial = Solid("Leg " + index, new Color(0.24f, 0.26f, 0.31f));
                Object.Destroy(leg.GetComponent<Collider>());
            }

            sign.Title = MakeLabel(sign.Root, ProductInfo.Name(kind).ToUpper(), new Vector3(0f, 2.14f, 0.062f),
                Color.Lerp(tone, Color.white, 0.34f), 0.062f);
            sign.Info = MakeLabel(sign.Root, "НЕ ПОСТРОЕНА · $" + ProductInfo.BuildCost(kind),
                new Vector3(0f, 1.67f, 0.062f), new Color(0.94f, 0.96f, 0.99f), 0.036f);
            return sign;
        }

        static void CreateBay(Transform parent, Color tone, float strength, bool hazard = false)
        {
            Color fill = Color.Lerp(new Color(0.09f, 0.10f, 0.13f), tone, strength);

            var bay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bay.name = "Bay";
            bay.transform.SetParent(parent, false);
            bay.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            bay.transform.localScale = new Vector3(15.5f, 0.03f, 3.5f);
            bay.GetComponent<Renderer>().sharedMaterial = Solid("Bay", fill, 0.1f);
            Object.Destroy(bay.GetComponent<Collider>());

            Material edge = Solid("Bay Edge", Color.Lerp(tone, Color.white, 0.08f), 0.2f);
            Edge(parent, edge, new Vector3(0f, 0.035f, -1.72f), new Vector3(15.7f, 0.02f, 0.12f));
            Edge(parent, edge, new Vector3(0f, 0.035f, 1.72f), new Vector3(15.7f, 0.02f, 0.12f));
            Edge(parent, edge, new Vector3(-7.63f, 0.035f, 0f), new Vector3(0.12f, 0.02f, 3.5f));
            Edge(parent, edge, new Vector3(7.63f, 0.035f, 0f), new Vector3(0.12f, 0.02f, 3.5f));

            if (!hazard) return;

            Material tape = Solid("Hazard Tape", Color.Lerp(tone, new Color(0.10f, 0.11f, 0.14f), 0.42f), 0.1f);
            for (float x = -7f; x <= 7.01f; x += 1.1f)
            {
                Edge(parent, tape, new Vector3(x, 0.038f, -1.5f), new Vector3(0.5f, 0.02f, 0.16f));
                Edge(parent, tape, new Vector3(x, 0.038f, 1.5f), new Vector3(0.5f, 0.02f, 0.16f));
            }
        }

        static void Edge(Transform parent, Material material, Vector3 position, Vector3 scale)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "Bay Edge";
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = position;
            edge.transform.localScale = scale;
            edge.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(edge.GetComponent<Collider>());
        }

        static Station CreateBuyer(Transform root, Vector3 position, Color tone)
        {
            Station station = NewStation(root, "Buyer", position, tone);
            CreatePad(station.Root, tone, 3.3f);

            Spawn("machine", station.Root, new Vector3(-0.55f, 0f, 0.25f), 1.55f, 180f);
            Spawn("hopper-high-square", station.Root, new Vector3(-0.55f, 1.4f, 0.25f), 1.1f, 180f);
            Spawn("box-large", station.Root, new Vector3(0.95f, 0f, -0.55f), 0.52f, 20f);
            Spawn("box-large", station.Root, new Vector3(1.28f, 0f, 0.05f), 0.46f, -12f);
            Spawn("box-small", station.Root, new Vector3(0.95f, 0.52f, -0.55f), 0.30f, 35f);

            station.Title = MakeLabel(station.Root, "ЗАКУПЩИК", new Vector3(0f, 4.36f, 0f), BuyerColor, 0.052f);
            station.Info = MakeLabel(station.Root, "Lv 1", new Vector3(0f, 4.05f, 0f), MutedText, 0.034f);
            station.ProgressFill = MakeProgress(station.Root, new Vector3(0f, 3.84f, 0f), 1.6f, BuyerColor);
            return station;
        }

        static Station CreateProducer(Transform root, Vector3 position, Color tone)
        {
            Station station = NewStation(root, "Producer", position, tone);
            CreatePad(station.Root, tone, 3.3f);

            Spawn("machine-fortified", station.Root, new Vector3(0.3f, 0f, 0.45f), 1.8f, 180f);
            Spawn("hopper-square", station.Root, new Vector3(0.3f, 1.75f, 0.45f), 0.8f, 180f);

            GameObject arm = Spawn("robot-arm-b", station.Root, new Vector3(-1.2f, 0f, -0.25f), 1.5f, 205f);
            if (arm != null && arm.transform.childCount > 0) station.Spinners.Add(arm.transform.GetChild(0));

            Spawn("box-large", station.Root, new Vector3(1.4f, 0f, -0.5f), 0.46f, 15f);
            Spawn("box-small", station.Root, new Vector3(1.38f, 0.46f, -0.5f), 0.28f, -25f);

            station.Title = MakeLabel(station.Root, "ПРОИЗВОДИТЕЛЬ", new Vector3(0f, 4.36f, 0f), ProducerColor, 0.05f);
            station.Info = MakeLabel(station.Root, "Lv 1", new Vector3(0f, 4.05f, 0f), MutedText, 0.034f);
            station.ProgressFill = MakeProgress(station.Root, new Vector3(0f, 3.84f, 0f), 1.6f, ProducerColor);
            return station;
        }

        static Station CreateSeller(Transform root, Vector3 position, Color tone)
        {
            Station station = NewStation(root, "Seller", position, tone);
            CreatePad(station.Root, tone, 3.3f);

            Spawn("scanner-high", station.Root, new Vector3(-1.0f, 0f, 0.4f), 1.7f, 180f);
            Spawn("screen-hanging-wide", station.Root, new Vector3(-1.0f, 1.85f, 1.0f), 0.78f, 0f);

            Spawn("box-long", station.Root, new Vector3(0.55f, 0f, -0.5f), 0.5f, 0f);
            Spawn("box-wide", station.Root, new Vector3(0.55f, 0.5f, -0.5f), 0.3f, 6f);
            Spawn("box-small", station.Root, new Vector3(0.35f, 0.8f, -0.5f), 0.24f, -20f);

            Spawn("box-large", station.Root, new Vector3(1.55f, 0f, 0.55f), 0.46f, -10f);
            Spawn("box-small", station.Root, new Vector3(1.55f, 0.46f, 0.55f), 0.26f, 18f);

            station.Title = MakeLabel(station.Root, "ПРОДАВЕЦ", new Vector3(0f, 4.36f, 0f), SellerColor, 0.052f);
            station.Info = MakeLabel(station.Root, "Lv 1", new Vector3(0f, 4.05f, 0f), MutedText, 0.034f);
            station.ProgressFill = MakeProgress(station.Root, new Vector3(0f, 3.84f, 0f), 1.6f, SellerColor);
            return station;
        }

        static readonly Color MutedText = new Color(0.82f, 0.86f, 0.92f);

        static Station CreateGenerator(Transform root, Vector3 position, int index)
        {
            Station station = NewStation(root, "Generator" + (index + 1), position, GeneratorColor);
            Spawn("structure-yellow-medium", station.Root, new Vector3(0f, 0f, 0.2f), 2.5f, 180f);
            Spawn("warning-traffic", station.Root, new Vector3(-1.4f, 0f, -0.75f), 0.75f, 12f);

            Transform big = CreateCog(station.Root, "cog-a", new Vector3(0f, 0f, -1.05f), 1.6f, 0f);
            if (big != null) station.Spinners.Add(big);
            Transform small = CreateCog(station.Root, "cog-b", new Vector3(1.24f, 0f, -0.92f), 0.85f, 22f);
            if (small != null) station.Spinners.Add(small);

            station.Title = MakeLabel(station.Root, "ГЕНЕРАТОР " + (index + 1), new Vector3(0f, 3.78f, 0f), GeneratorColor, 0.052f);
            station.Info = MakeLabel(station.Root, "4 энергии", new Vector3(0f, 3.44f, 0f), MutedText, 0.033f);
            return station;
        }

        // Один сборочный пост. Моделька та же, что была у старой «сборки»,
        // только подпись берём из рецепта и чуть разбавляем декор по индексу,
        // чтобы три поста не выглядели клонами.
        static Station CreateAssembly(Transform root, Vector3 position, int index)
        {
            Recipe recipe = Recipes.All[index];
            Station station = NewStation(root, "Assembly " + recipe.Name, position, AssemblyColor);
            CreatePad(station.Root, AssemblyColor, 3.2f);

            Spawn("machine-fortified", station.Root, new Vector3(0f, 0f, 0.1f), 1.75f, 180f);
            Spawn("hopper-square", station.Root, new Vector3(0f, 1.72f, 0.1f), 0.72f, 180f);

            GameObject arm = Spawn("robot-arm-b", station.Root, new Vector3(-1.35f, 0f, -0.35f), 1.35f, 205f);
            if (arm != null && arm.transform.childCount > 0) station.Spinners.Add(arm.transform.GetChild(0));

            Spawn("box-large", station.Root, new Vector3(1.25f, 0f, -0.5f), 0.42f, 18f);
            Spawn("box-small", station.Root, new Vector3(1.23f, 0.42f, -0.5f), 0.24f, -20f);

            // у последнего поста (турбина) деталей больше, пусть будет второй ящик
            if (index == Recipes.All.Length - 1)
                Spawn("box-long", station.Root, new Vector3(1.35f, 0f, 0.35f), 0.36f, 74f);

            station.Title = MakeLabel(station.Root, "СБОРКА · " + recipe.Name.ToUpper(),
                new Vector3(0f, 4.14f, 0f), AssemblyColor, 0.036f);
            station.Info = MakeLabel(station.Root, "НЕ ПОСТРОЕНА · $" + recipe.BuildCost,
                new Vector3(0f, 3.82f, 0f), MutedText, 0.032f);
            station.ProgressFill = MakeProgress(station.Root, new Vector3(0f, 3.60f, 0f), 1.6f, AssemblyColor);
            return station;
        }

        static Station NewStation(Transform root, string name, Vector3 position, Color accent)        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;

            return new Station { Root = go.transform, Accent = accent };
        }

        // Круглая площадка под станком. Чуть приподнята над полом,
        // иначе местами z-fighting и пол «мигает».
        static void CreatePad(Transform parent, Color accent, float size)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad";
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            pad.transform.localScale = new Vector3(size, 0.03f, size);
            pad.GetComponent<Renderer>().sharedMaterial = Solid("Pad " + parent.name, Color.Lerp(accent, new Color(0.12f, 0.13f, 0.16f), 0.74f));
            Object.Destroy(pad.GetComponent<Collider>());
        }

        // Подпись — обычный TextMesh. Размер characterSize подобран под масштаб зала;
        // если менять камеру, придётся подкрутить и его.
        static TextMesh MakeLabel(Transform parent, string text, Vector3 localPosition, Color color, float size)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            if (_font != null) mesh.font = _font;
            mesh.fontSize = 72;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mesh.font != null) renderer.sharedMaterial = mesh.font.material;
            return mesh;
        }

        // Прогресс-бар из двух квадов. Хитрость: заполнение растягивается
        // не от центра, а от левого края, поэтому есть отдельный pivot.
        static Transform MakeProgress(Transform parent, Vector3 localPosition, float width, Color color)
        {
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Progress Back";
            back.transform.SetParent(parent, false);
            back.transform.localPosition = localPosition;
            back.transform.localScale = new Vector3(width, 0.1f, 1f);
            back.GetComponent<Renderer>().sharedMaterial = Unlit("Progress Back", new Color(0.05f, 0.06f, 0.08f, 1f));
            Object.Destroy(back.GetComponent<Collider>());

            var pivot = new GameObject("Progress Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPosition + new Vector3(-width * 0.5f, 0f, -0.005f);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Progress Fill";
            fill.transform.SetParent(pivot.transform, false);
            fill.transform.localPosition = new Vector3(width * 0.5f, 0f, 0f);
            fill.transform.localScale = new Vector3(width, 0.075f, 1f);
            fill.GetComponent<Renderer>().sharedMaterial = Unlit("Progress Fill", color);
            Object.Destroy(fill.GetComponent<Collider>());

            return pivot.transform;
        }

        // Камера одна и статичная. Если её нет в сцене — создаём.
        // Позицию не меняем в рантайме, поэтому зал всегда выглядит одинаково.
        static Camera CreateCamera(Transform root)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            camera.transform.SetParent(root, false);
            camera.transform.position = CameraPos;
            camera.transform.LookAt(CameraTarget);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 160f;
            return camera;
        }
    }
}
