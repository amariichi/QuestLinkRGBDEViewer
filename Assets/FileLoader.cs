using UnityEngine;
using System.Collections;
using System.IO;
using SimpleFileBrowser;
using UnityEngine.Events;

[System.Serializable]
public class ImageCreatedEvent : UnityEvent<Color32[]> { }

public class FileLoader : MonoBehaviour
{
    // Target mesh size (in pixels, total)
    // 目標メッシュサイズ（ピクセル単位、全体）
    private const int TargetMesh = 250000;
    private const int MeshDiff = 2000;

    // Reference to the previously displayed image object
    // 前回表示された画像オブジェクトの参照
    private GameObject _previousImageObject;

    // Internal field for readiness to load
    // 読み込み準備の内部フィールド
    [SerializeField]
    private bool _isReadyToLoad = true;

    /// <summary>
    /// Public property to check or set if loading is ready.
    /// 読み込み準備完了かどうかを取得または設定するパブリックプロパティ
    /// </summary>
    public bool IsReadyToLoad
    {
        get { return _isReadyToLoad; }
        set { _isReadyToLoad = value; }
    }

    // Internal field for readiness to control
    // コントロール準備の内部フィールド
    private bool _isReadyToControl;
    /// <summary>
    /// Public property to check if control is ready.
    /// コントロール準備完了かどうかを取得するパブリックプロパティ
    /// </summary>
    public bool IsReadyToControl
    {
        get { return _isReadyToControl; }
    }

    [SerializeField]
    private int _originalWidth = 0;
    /// <summary>
    /// Public property for the original width after separation.
    /// 分割後の元画像の幅（左側）のパブリックプロパティ
    /// </summary>
    public int OriginalWidth
    {
        get { return _originalWidth; }
    }

    [SerializeField]
    private int _originalHeight = 0;
    /// <summary>
    /// Public property for the original height after separation.
    /// 分割後の元画像の高さのパブリックプロパティ
    /// </summary>
    public int OriginalHeight
    {
        get { return _originalHeight; }
    }

    // Right side depth data (UInt32 stored in RGBA32) as float array
    // 右側の深度情報（RGBA32に格納されたUInt32）をfloat配列で保持
    private float[] _pixelZData;

    // Internal field for the pixel Z matrix
    // 深度情報の2次元配列（pixel Z matrix）の内部フィールド
    private float[,] _pixelZMatrix;
    /// <summary>
    /// Public property for the depth data matrix [y, x].
    /// 深度情報の2次元配列（[y,x]）のパブリックプロパティ
    /// </summary>
    public float[,] PixelZMatrix
    {
        get { return _pixelZMatrix; }
    }

    private float _pixelZMax;
    public float PixelZMax
    {
        get { return _pixelZMax; }
    }

    private float _pixelZMin;
    public float PixelZMin
    {
        get { return _pixelZMin; }
    }

    [SerializeField]
    private int _meshX;
    /// <summary>
    /// Public property for the mesh width selected from the dropdown.
    /// ドロップダウンから選択されたメッシュの横数のパブリックプロパティ
    /// </summary>
    public int MeshX
    {
        get { return _meshX; }
    }

    [SerializeField]
    private int _meshY;
    /// <summary>
    /// Public property for the mesh height selected from the dropdown.
    /// ドロップダウンから選択されたメッシュの縦数のパブリックプロパティ
    /// </summary>
    public int MeshY
    {
        get { return _meshY; }
    }

    [SerializeField]
    private bool _is360;
    /// <summary>
    /// Public property indicating if the image is a 360 image.
    /// 360画像かどうかを示すパブリックプロパティ（trueなら360画像）
    /// </summary>
    public bool Is360
    {
        get { return _is360; }
    }

    /// <summary>
    /// UnityEvent that is triggered when the image is created.
    /// Imageが作成された際に発生するUnityEvent
    /// </summary>
    [SerializeField]
    public ImageCreatedEvent OnImageCreated;

    void Start()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("RGBDE file", ".png", ".PNG"));
        FileBrowser.SetDefaultFilter(".png");
        FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");
        _isReadyToLoad = false;
        StartCoroutine(ShowLoadDialogCoroutine());
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start) && IsReadyToLoad)
        {
            _isReadyToLoad = false;
            _isReadyToControl = false;
            StartCoroutine(ShowLoadDialogCoroutine());
        }
    }

    private IEnumerator ShowLoadDialogCoroutine()
    {
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, "Select an RGBDE File", "Load");
        Debug.Log(FileBrowser.Success);
        if (FileBrowser.Success)
        {
            OnFilesSelected(FileBrowser.Result);
        }
        else
        {
            OnFileNotSelected();
        }
        _isReadyToControl = true;
    }

    void OnFilesSelected(string[] filePaths)
    {
        string filePath = filePaths[0];
        int position = filePath.IndexOf(".360.");
        _is360 = position != -1;

        byte[] pngData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(pngData);
        StartCoroutine(SplitImage(texture));
    }

    void OnFileNotSelected()
    {
        _isReadyToLoad = true;
    }

    /// <summary>
    /// Coroutine that splits the image:
    /// creates a texture from the left half and stores depth information from the right half.
    /// 画像を分割し、左側をテクスチャ化し、右側から深度情報を取得するコルーチン
    /// </summary>
    /// <param name="originalTexture">The original Texture2D loaded from file.</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator SplitImage(Texture2D originalTexture)
    {
        if (originalTexture == null)
        {
            yield break;
        }

        _originalWidth = originalTexture.width / 2;
        _originalHeight = originalTexture.height;

        Texture2D leftTexture = new Texture2D(_originalWidth, _originalHeight, originalTexture.format, false);
        Texture2D rightTexture = new Texture2D(originalTexture.width - _originalWidth, _originalHeight, originalTexture.format, false, true);

        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] leftPixels = new Color32[_originalWidth * _originalHeight];
        Color32[] rightPixels = new Color32[(originalTexture.width - _originalWidth) * _originalHeight];

        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = 0; x < _originalWidth; x++)
            {
                leftPixels[y * _originalWidth + x] = originalPixels[y * originalTexture.width + x];
            }
        }

        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = _originalWidth; x < originalTexture.width; x++)
            {
                rightPixels[y * (originalTexture.width - _originalWidth) + (x - _originalWidth)] = originalPixels[y * originalTexture.width + x];
            }
        }

        _pixelZData = new float[rightPixels.Length];
        for (int i = 0; i < _pixelZData.Length; i++)
        {
            _pixelZData[i] = (rightPixels[i].a * 16777216f + rightPixels[i].b * 65536f + rightPixels[i].g * 256f + rightPixels[i].r) / 10000f;
        }

        _pixelZMatrix = new float[_originalHeight, _originalWidth];
        for (int j = 0; j < _originalHeight; j++)
        {
            for (int i = 0; i < _originalWidth; i++)
            {
                _pixelZMatrix[j, i] = _pixelZData[j * _originalWidth + i];
            }
        }

        _pixelZMax = Mathf.Max(_pixelZData);
        _pixelZMin = Mathf.Min(_pixelZData);

        var bestMesh = FindBestMeshSize(TargetMesh, MeshDiff);
        _meshX = bestMesh.meshX;
        _meshY = bestMesh.meshY;

        OnImageCreated?.Invoke(leftPixels);
        Destroy(originalTexture);
        Destroy(rightTexture);

        yield return null;
    }

    /// <summary>
    /// Finds the best mesh size that closely matches the image's aspect ratio.
    /// 画像のアスペクト比に最も近いメッシュの横・縦数を見つけるメソッド
    /// </summary>
    /// <param name="totalMeshes">Total number of meshes</param>
    /// <param name="meshDiff">Allowed difference in mesh count</param>
    /// <returns>Tuple containing meshX and meshY</returns>
    private (int meshX, int meshY) FindBestMeshSize(int totalMeshes, int meshDiff)
    {
        double aspectRatio = (double)_originalWidth / _originalHeight;
        int minMesh = totalMeshes - meshDiff;
        int maxMesh = totalMeshes + meshDiff;
        double bestRatioError = double.MaxValue;
        int bestMeshX = 0;
        int bestMeshY = 0;

        for (int t = minMesh; t <= maxMesh; t++)
        {
            if (t <= 0) continue;
            int approxX = Mathf.RoundToInt((float)System.Math.Sqrt(t * aspectRatio));
            if (approxX <= 0) continue;
            int approxY = Mathf.RoundToInt(t / (float)approxX);
            int product = approxX * approxY;
            if (product < minMesh || product > maxMesh)
            {
                continue;
            }
            double currentRatioError = System.Math.Abs((double)approxX / approxY - aspectRatio);
            if (currentRatioError < bestRatioError)
            {
                bestRatioError = currentRatioError;
                bestMeshX = approxX;
                bestMeshY = approxY;
            }
        }
        return (bestMeshX, bestMeshY);
    }
}
