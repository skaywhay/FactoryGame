using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Помощник быстрой настройки 3D-карты цеха (FactoryHall_Map.fbx) в Unity.
/// Просто повесьте этот скрипт на корневой объект FactoryHall_Map на сцене!
/// Работает и в Built-in Render Pipeline, и в URP / HDRP.
/// </summary>
[ExecuteAlways]
public class FactoryMapSetup : MonoBehaviour
{
    [Header("1. Подгонка масштаба и координат")]
    [Tooltip("Перетащите сюда ваш старый объект пола (Plane/Cube), чтобы автоматически подогнать масштаб и центр 1-в-1")]
    public Renderer existingFloorRenderer;

    [Tooltip("Размер одной клетки сетки в юнитах Unity (по умолчанию в модели 1 клетка = 2.0 м, поле 13x9 = 26x18 м)")]
    public float gridCellSize = 2.0f;

    [Header("2. Управление зонами (опционально из вашего кода игры)")]
    public GameObject line1GearsZone;
    public GameObject line2CasingsZone;
    public GameObject line3MicrochipsZone;

    [Header("3. Оживление цеха (опционально в Play Mode)")]
    [Tooltip("Плавное движение кран-балки вдоль задней зоны в режиме игры")]
    public bool animateBridgeCrane = false;
    public float craneSpeed = 0.35f;
    public float craneTravelRange = 1.6f;

    private Transform _craneTransform;
    private Vector3 _craneStartLocalPos;
    private bool _craneInitialized;

    private void OnEnable()
    {
        AutoBindZones();
        InitCrane();
    }

    private void Update()
    {
        if (!Application.isPlaying || !animateBridgeCrane) return;
        if (!_craneInitialized) InitCrane();
        if (_craneTransform != null)
        {
            float offsetZ = Mathf.Sin(Time.time * craneSpeed) * craneTravelRange;
            _craneTransform.localPosition = _craneStartLocalPos + new Vector3(0f, 0f, offsetZ);
        }
    }

    private void InitCrane()
    {
        _craneTransform = FindDeepChild(transform, "Bridge_Crane_Yellow");
        if (_craneTransform != null)
        {
            _craneStartLocalPos = _craneTransform.localPosition;
            _craneInitialized = true;
        }
    }

    public void AutoBindZones()
    {
        if (line1GearsZone == null)
        {
            var t = FindDeepChild(transform, "Zone_Line1_Gears_Yellow");
            if (t != null) line1GearsZone = t.gameObject;
        }
        if (line2CasingsZone == null)
        {
            var t = FindDeepChild(transform, "Zone_Line2_Casings_Cyan");
            if (t != null) line2CasingsZone = t.gameObject;
        }
        if (line3MicrochipsZone == null)
        {
            var t = FindDeepChild(transform, "Zone_Line3_Microchips_Purple");
            if (t != null) line3MicrochipsZone = t.gameObject;
        }
    }

    /// <summary>
    /// Автоматически совмещает центр и масштаб цеха с вашим текущим полом (Plane).
    /// Внутренняя сетка цеха: 13 клеток по X (26 м) x 9 клеток по Z (18 м), поверхность пола на Y = 0.
    /// </summary>
    public void MatchToExistingFloor()
    {
        if (existingFloorRenderer == null)
        {
            Debug.LogWarning("[FactoryMapSetup] Перетащите старый объект пола (Plane) в поле Existing Floor Renderer!");
            return;
        }

        Bounds b = existingFloorRenderer.bounds;
        // Внутренняя ширина рабочей зоны модели = 26.0 юнитов (13 клеток по 2.0м)
        float scaleFactor = b.size.x / 26.0f;
        gridCellSize = 2.0f * scaleFactor;

        transform.localScale = Vector3.one * scaleFactor;
        transform.position = new Vector3(b.center.x, b.max.y, b.center.z);

        Debug.Log($"[FactoryMapSetup] Карта подогнана под '{existingFloorRenderer.name}': позиция={transform.position}, масштаб={scaleFactor:F3} (клетка={gridCellSize:F2} м)");
    }

    public void ApplyGridCellSize()
    {
        float scaleFactor = Mathf.Max(0.05f, gridCellSize / 2.0f);
        transform.localScale = Vector3.one * scaleFactor;
        Debug.Log($"[FactoryMapSetup] Применён размер клетки {gridCellSize:F2} м -> Scale = {scaleFactor:F3}");
    }

    /// <summary>
    /// Добавляет единый плоский BoxCollider на уровне пола (Y = 0), чтобы клики мышкой и физика работали идеально.
    /// </summary>
    public void SetupCleanFloorCollider()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, -0.25f, 0f);
        col.size = new Vector3(28f, 0.5f, 20f);
        Debug.Log("[FactoryMapSetup] Плоский BoxCollider пола (Y = 0) успешно настроен!");
    }

    /// <summary>
    /// Автоматически настраивает шейдеры (URP или Built-in Standard) и включает Emission (свечение окон и линий).
    /// </summary>
    public void AutoConfigureMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        Shader standard = Shader.Find("Standard");
        Shader targetShader = urpLit != null ? urpLit : (hdrpLit != null ? hdrpLit : standard);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        int updated = 0;

        foreach (Renderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                // Если материал встроен в FBX (read-only), создаём редактируемую копию в памяти/ассетах
                #if UNITY_EDITOR
                string assetPath = AssetDatabase.GetAssetPath(m);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    string dir = System.IO.Path.GetDirectoryName(assetPath) + "/Materials";
                    if (!AssetDatabase.IsValidFolder(dir))
                    {
                        string parentDir = System.IO.Path.GetDirectoryName(assetPath);
                        AssetDatabase.CreateFolder(parentDir, "Materials");
                    }
                    string newMatPath = dir + "/" + m.name + ".mat";
                    Material extracted = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);
                    if (extracted == null)
                    {
                        extracted = new Material(m);
                        AssetDatabase.CreateAsset(extracted, newMatPath);
                    }
                    m = extracted;
                    mats[i] = m;
                    changed = true;
                }
                #endif

                Color baseCol = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.color : Color.gray);

                if (targetShader != null && m.shader != targetShader)
                {
                    m.shader = targetShader;
                }

                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
                if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);

                // Настройка свечения (Emission) для светящихся элементов цеха
                ConfigureEmissionByName(m);
                updated++;
            }
            if (changed) r.sharedMaterials = mats;
        }

        #if UNITY_EDITOR
        AssetDatabase.SaveAssets();
        #endif
        Debug.Log($"[FactoryMapSetup] Обработано материалов: {updated}. Пайплайн: {(urpLit != null ? "URP" : "Built-in / Standard")}");
    }

    private void ConfigureEmissionByName(Material m)
    {
        string n = m.name;
        Color emColor = Color.black;
        float intensity = 0f;

        if (n.Contains("Mat_Glow_Skylight"))      { emColor = new Color(0.38f, 0.65f, 0.98f); intensity = 2.0f; }
        else if (n.Contains("Mat_Glow_WarmLamp")) { emColor = new Color(0.99f, 0.88f, 0.35f); intensity = 2.5f; }
        else if (n.Contains("Mat_Glow_Amber"))    { emColor = new Color(0.96f, 0.62f, 0.05f); intensity = 1.8f; }
        else if (n.Contains("Mat_Glow_Cyan"))     { emColor = new Color(0.08f, 0.78f, 0.88f); intensity = 1.8f; }
        else if (n.Contains("Mat_Glow_Purple"))   { emColor = new Color(0.68f, 0.35f, 0.97f); intensity = 1.8f; }
        else if (n.Contains("Mat_Glow_GreenLED")) { emColor = new Color(0.12f, 0.82f, 0.52f); intensity = 2.2f; }

        if (intensity > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emColor * intensity);
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform res = FindDeepChild(child, targetName);
            if (res != null) return res;
        }
        return null;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FactoryMapSetup))]
public class FactoryMapSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        FactoryMapSetup script = (FactoryMapSetup)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Быстрая авто-настройка в 1 клик", EditorStyles.boldLabel);

        if (GUILayout.Button("1. Авто-настроить материалы (URP / Built-in + Свечение)", GUILayout.Height(30)))
        {
            Undo.RecordObject(script.gameObject, "Configure Factory Materials");
            script.AutoConfigureMaterials();
        }

        if (GUILayout.Button("2. Подогнать масштаб и центр под старый пол (Existing Floor)", GUILayout.Height(28)))
        {
            Undo.RecordObject(script.transform, "Match Existing Floor");
            script.MatchToExistingFloor();
        }

        if (GUILayout.Button("   Или применить указанный Grid Cell Size", GUILayout.Height(24)))
        {
            Undo.RecordObject(script.transform, "Apply Grid Cell Size");
            script.ApplyGridCellSize();
        }

        if (GUILayout.Button("3. Добавить чистый BoxCollider пола (Y = 0)", GUILayout.Height(26)))
        {
            Undo.RecordObject(script.gameObject, "Setup Floor Collider");
            script.SetupCleanFloorCollider();
        }
    }
}
#endif
