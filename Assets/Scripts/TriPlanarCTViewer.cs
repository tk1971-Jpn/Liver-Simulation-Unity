using System;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways] // エディタモードでもスクリプトを実行可能にする
public class TriPlanarCTViewer : MonoBehaviour
{
    // ===== 入力（Resources） =====
    [Header("Input (Resources)")]
    public string resourcesSubFolder = "CT";
    public FilterMode filterMode = FilterMode.Bilinear;

    // ===== ボクセル寸法（mm） =====
    [Header("Voxel size (mm)")]
    public float voxelSizeXmm = 0.683f;
    public float voxelSizeYmm = 0.683f;
    public float voxelSizeZmm = 0.683f;

    // ===== スライスインデックス =====
    [Header("Index (0-based)")]
    public int indexX = 0; // Sagittal
    public int indexY = 0; // Coronal
    public int indexZ = 0; // Axial

    // ===== Z順序/操作 =====
    [Header("Z Order / Controls")]
    public bool reverseZStackOnLoad = false;
    public bool reverseZScroll = false;

    // ===== 面ごとの反転 =====
    [Header("Per-Plane Flip")]
    public bool flipAxialLR = false, flipAxialUD = false;
    public bool flipCoronalLR = false, flipCoronalIS = false;
    public bool flipSagittalAP = false, flipSagittalIS = false;

    // ===== 表示/操作 =====
    [Header("Options")]
    public bool twoSided = true;
    public bool wrapAround = true;
    public bool useKeyboard = true;

    [Header("UI & Debug")]
    public bool showToolsWindow = true;
    public bool showSlidersWindow = true;
    public bool debugLog = true;

    // 内部変数
    private Texture2D[] _axialSlices = Array.Empty<Texture2D>();
    private Color32[][] _axialPix;
    private int W, H, D;
    private int _lastX, _lastY, _lastZ;

    private GameObject _quadAxial, _quadCoronal, _quadSagittal;
    private Texture2D _texCoronal, _texSagittal;
    private const float MIN_SIDE = 0.001f;

    private Rect toolsWindowRect = new Rect(16, 16, 420, 170);
    private Rect slidersWindowRect = new Rect(16, 200, 560, 120);

    // インスペクターで値が変更されたときに呼ばれる（エディタ用）
    private void OnValidate()
    {
        if (_axialPix != null && _axialPix.Length > 0)
        {
            ApplyAll(true);
        }
    }

    private void Awake()
    {
        InitializeData();
    }

    private void OnEnable()
    {
        if (_axialPix == null || _axialPix.Length == 0)
        {
            InitializeData();
        }
    }

    private void InitializeData()
    {
        // データのロード
        _axialSlices = Resources.LoadAll<Texture2D>(resourcesSubFolder);
        if (_axialSlices == null || _axialSlices.Length == 0) return;

        Array.Sort(_axialSlices, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (reverseZStackOnLoad && _axialSlices.Length > 1) Array.Reverse(_axialSlices);

        W = _axialSlices[0].width;
        H = _axialSlices[0].height;
        D = _axialSlices.Length;
        _lastX = Mathf.Max(0, W - 1);
        _lastY = Mathf.Max(0, H - 1);
        _lastZ = Mathf.Max(0, D - 1);

        // ピクセルデータのキャッシュ（Read/Write Enabled必須）
        _axialPix = new Color32[D][];
        for (int z = 0; z < D; z++)
        {
            _axialSlices[z].wrapMode = TextureWrapMode.Clamp;
            _axialSlices[z].filterMode = filterMode;
            try { _axialPix[z] = _axialSlices[z].GetPixels32(); }
            catch { if (debugLog) Debug.LogError("PNG not readable: " + _axialSlices[z].name); }
        }

        SpawnOrResetQuads();
        ApplyAll(true);
    }

    private void Update()
    {
        // キーボード操作はPlayモード中のみ有効にする
        if (!Application.isPlaying || !useKeyboard || D == 0) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftArrowKey.wasPressedThisFrame) StepZ(reverseZScroll ? 1 : -1);
        if (kb.rightArrowKey.wasPressedThisFrame) StepZ(reverseZScroll ? -1 : 1);
        if (kb.upArrowKey.wasPressedThisFrame) StepY(1);
        if (kb.downArrowKey.wasPressedThisFrame) StepY(-1);
        if (kb.aKey.wasPressedThisFrame) StepX(-1);
        if (kb.dKey.wasPressedThisFrame) StepX(1);
    }

    private void SpawnOrResetQuads()
    {
        // エディタモードでの破壊はDestroyImmediateを使用
        if (_quadAxial) DestroyImmediate(_quadAxial);
        if (_quadCoronal) DestroyImmediate(_quadCoronal);
        if (_quadSagittal) DestroyImmediate(_quadSagittal);

        _quadAxial = CreateSliceQuad("Quad_Axial", new Vector3(0, 0, 0));
        _quadCoronal = CreateSliceQuad("Quad_Coronal", new Vector3(-90, 0, 0));
        _quadSagittal = CreateSliceQuad("Quad_Sagittal", new Vector3(0, 90, 0));

        _texCoronal = new Texture2D(W, D, TextureFormat.RGBA32, false);
        _texSagittal = new Texture2D(D, H, TextureFormat.RGBA32, false);

        _quadCoronal.GetComponent<Renderer>().sharedMaterial.mainTexture = _texCoronal;
        _quadSagittal.GetComponent<Renderer>().sharedMaterial.mainTexture = _texSagittal;
    }

    private GameObject CreateSliceQuad(string name, Vector3 rotation)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(rotation);
        
        // マテリアルの設定
        Renderer r = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat == null) mat = new Material(Shader.Find("Unlit/Texture"));
        
        if (twoSided) {
            mat.SetFloat("_Cull", 0); // Double sided
        }
        r.sharedMaterial = mat; // エディタモードではsharedMaterialを推奨
        return go;
    }

    private void ApplyAll(bool forceRebuild)
    {
        if (D == 0 || _quadAxial == null) return;

        float sx = W * voxelSizeXmm * 0.001f;
        float sy = H * voxelSizeYmm * 0.001f;
        float sz = D * voxelSizeZmm * 0.001f;

        _quadAxial.transform.localScale = new Vector3(sx, sy, 1f);
        _quadCoronal.transform.localScale = new Vector3(sx, sz, 1f);
        _quadSagittal.transform.localScale = new Vector3(sz, sy, 1f);

        UpdatePositions();

        indexX = Mathf.Clamp(indexX, 0, _lastX);
        indexY = Mathf.Clamp(indexY, 0, _lastY);
        indexZ = Mathf.Clamp(indexZ, 0, _lastZ);

        ApplyAxialTexture(indexZ);
        if (forceRebuild) {
            RebuildCoronal(indexY);
            RebuildSagittal(indexX);
        }
    }

    private void UpdatePositions()
    {
        float sx = W * voxelSizeXmm * 0.001f;
        float sy = H * voxelSizeYmm * 0.001f;
        float sz = D * voxelSizeZmm * 0.001f;

        // 各スライスの座標計算
        float px = (indexX * voxelSizeXmm * 0.001f) - (sx * 0.5f) + (voxelSizeXmm * 0.001f * 0.5f);
        float py = (indexY * voxelSizeYmm * 0.001f) - (sy * 0.5f) + (voxelSizeYmm * 0.001f * 0.5f);
        float pz = (indexZ * voxelSizeZmm * 0.001f) - (sz * 0.5f) + (voxelSizeZmm * 0.001f * 0.5f);

        _quadSagittal.transform.localPosition = new Vector3(px, 0, 0);
        _quadCoronal.transform.localPosition = new Vector3(0, py, 0);
        _quadAxial.transform.localPosition = new Vector3(0, 0, pz);
    }

    private void ApplyAxialTexture(int z)
    {
        if (_quadAxial == null || _axialSlices.Length <= z) return;
        var mat = _quadAxial.GetComponent<Renderer>().sharedMaterial;
        mat.mainTexture = _axialSlices[z];
        mat.mainTextureScale = new Vector2(flipAxialLR ? -1 : 1, flipAxialUD ? -1 : 1);
        mat.mainTextureOffset = new Vector2(flipAxialLR ? 1 : 0, flipAxialUD ? 1 : 0);
    }

    private void RebuildCoronal(int y0)
    {
        if (_texCoronal == null || _axialPix == null) return;
        Color32[] buf = new Color32[W * D];
        for (int z = 0; z < D; z++) {
            int targetZ = flipCoronalIS ? (D - 1 - z) : z;
            for (int x = 0; x < W; x++) {
                int targetX = flipCoronalLR ? (W - 1 - x) : x;
                buf[z * W + x] = _axialPix[targetZ][y0 * W + targetX];
            }
        }
        _texCoronal.SetPixels32(buf); _texCoronal.Apply();
    }

    private void RebuildSagittal(int x0)
    {
        if (_texSagittal == null || _axialPix == null) return;
        Color32[] buf = new Color32[D * H];
        for (int y = 0; y < H; y++) {
            int targetY = flipSagittalAP ? (H - 1 - y) : y;
            for (int z = 0; z < D; z++) {
                int targetZ = flipSagittalIS ? (D - 1 - z) : z;
                buf[y * D + z] = _axialPix[targetZ][targetY * W + x0];
            }
        }
        _texSagittal.SetPixels32(buf); _texSagittal.Apply();
    }

    private void StepX(int d) { indexX += d; ApplyAll(true); }
    private void StepY(int d) { indexY += d; ApplyAll(true); }
    private void StepZ(int d) { indexZ += d; ApplyAll(true); }

    // GUI関連（Playモード時のみ表示される）
    private void OnGUI()
    {
        if (!Application.isPlaying || D == 0) return;
        if (showToolsWindow) toolsWindowRect = GUI.Window(1001, toolsWindowRect, (id) => { /* Tools UI */ }, "TriPlanar Tools");
        if (showSlidersWindow) slidersWindowRect = GUI.Window(1002, slidersWindowRect, (id) => { /* Sliders UI */ }, "Slices");
    }
}