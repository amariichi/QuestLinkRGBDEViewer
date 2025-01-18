using UnityEngine;
using System.Collections;
using System.IO;
using SimpleFileBrowser;
using UnityEngine.Events;

// Definition of custom UnityEvent, UnityEvent のカスタム定義
[System.Serializable]
public class ImageCreatedEvent : UnityEvent<Color32[]> { }


public class FileLoader : MonoBehaviour
{
    // Target mesh size (in pixels, in total), 目標のメッシュサイズ（ピクセル単位、全体）
    private const int TARGET_MESH = 250000;
    private const int DIFF = 2000;

    // Variable that holds a reference to the last displayed image, 前回表示した画像の参照を保持する変数
    private GameObject previousImageObject;

    // IsReadyToLoadの内部フィールド
    [SerializeField]
    private bool _isReadyToLoad = true;

    /// <summary>
    /// 他のスクリプトから参照可能なOriginalWidthプロパティ（読み取り専用）。
    /// Width after separated, 分割後のleftImage.pngの幅を示します。
    /// </summary>
    public bool IsReadyToLoad
    {
        get { return _isReadyToLoad; }
        set { _isReadyToLoad = value; }
    }

    private bool _isReadyToControl;
    /// <summary>
    /// 他のスクリプトから参照可能なコントローラー使用可能プロパティ（読み取り専用）。
    /// </summary>
    public bool IsReadyToControl
    {
        get { return _isReadyToControl; }
    }


    // originalWidthの内部フィールド
    [SerializeField]
    private int _originalWidth = 0;

    /// <summary>
    /// 他のスクリプトから参照可能なOriginalWidthプロパティ（読み取り専用）。
    /// Width after separated, 分割後のleftImage.pngの幅を示します。
    /// </summary>
    public int OriginalWidth
    {
        get { return _originalWidth; }
    }

    // originalHeightの内部フィールド
    [SerializeField]
    private int _originalHeight = 0;

    /// <summary>
    /// 他のスクリプトから参照可能なOriginalHeightプロパティ（読み取り専用）。
    /// Height after separeted, 分割後のleftImage.pngの高さを示します。
    /// </summary>
    public int OriginalHeight
    {
        get { return _originalHeight; }
    }

    //Right side depth data (UInt32 stored in RGBA32) float[]
    private float[] pixelZData;

    // pixelZMatrixの内部フィールド
    private float[,] _pixelZMatrix;

    /// <summary>
    /// Right side depth data (UInt32 stored in RGBA32) float[y,x] (read-only) that can be referenced from other scripts.
    /// 他のスクリプトから参照可能な右側デプスデータ（RGBA32にUInt32が格納）float[y,x]（読み取り専用）。
    /// 奥行き参照の元データ
    /// </summary>
    public float[,] PixelZMatrix
    {
        get { return _pixelZMatrix; }
    }

    // pixelZdataの最大値の内部フィールド
    private float _pixelZMax;
    public float PixelZMax
    {
        get { return _pixelZMax; }
    }

    // pixelZdataの最小値の内部フィールド
    private float _pixelZMin;
    public float PixelZMin
    {
        get { return _pixelZMin; }
    }

    // Mesh Width の内部フィールド
    [SerializeField]
    private int _meshX;

    /// <summary>
    /// Choosed mesh width from dropdown list, ドロップダウンの選択結果によるメッシュの幅の数
    /// </summary>
    public int meshX
    {
        get { return _meshX; }
    }

    // Mesh Height の内部フィールド
    [SerializeField]
    private int _meshY;

    /// <summary>
    /// Choosed mesh height from dropdown list, ドロップダウンの選択結果によるメッシュの縦の数
    /// </summary>
    public int meshY
    {
        get { return _meshY; }
    }

    /// <summary>
    /// Imageが作成された際に発生するUnityEvent。
    /// 引数として新しく作成されたImageのGameObjectとSpriteを渡します。
    /// </summary>
    [SerializeField]
    public ImageCreatedEvent OnImageCreated;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("RGBDE file", ".png", ".PNG"));
        FileBrowser.SetDefaultFilter(".png");
        FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");
        _isReadyToLoad = false;
        StartCoroutine(ShowLoadDialogCoroutine());
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start) && IsReadyToLoad)
        {
            _isReadyToLoad = false;
            _isReadyToControl = false;
            StartCoroutine(ShowLoadDialogCoroutine());
        }
    }

    //UnitySimpleFileBrowser を立ち上げてファイルを読み込む
    private IEnumerator ShowLoadDialogCoroutine()
    {
        // Show a load file dialog and wait for a response from user
        // Load file/folder: file, Allow multiple selection: true
        // Initial path: default (Documents), Initial filename: empty
        // Title: "Load File", Submit button text: "Load"
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, "Select an RGBDE File", "Load");

        // Dialog is closed
        // Print whether the user has selected some files or cancelled the operation (FileBrowser.Success)
        Debug.Log(FileBrowser.Success);

        if (FileBrowser.Success)
        {
            OnFilesSelected(FileBrowser.Result); // FileBrowser.Result is null, if FileBrowser.Success is false
        }
        else
        {
            OnFileNotSelected();
        }

        _isReadyToControl = true;
    }

    //読み込むファイルが選択された場合にデータを取得して画像を分割する
    void OnFilesSelected(string[] filePaths)
    {
        // Get the file path of the first selected file
        string filePath = filePaths[0];

        // Read the bytes of the first file
        byte[] pngData = File.ReadAllBytes(filePath);

        // LoadImageを呼び出すと、実際の画像サイズに合わせて自動的に変更されます。
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        // バイト配列のPNGデータを読み込んでTexture2Dに変換
        texture.LoadImage(pngData);
        StartCoroutine(SplitImage(texture));
    }

    void OnFileNotSelected()
    {
        _isReadyToLoad = true;
    }

    /// <summary>
    /// 画像を分割し、左半分をテクスチャにして、右半分からデプス情報を保存するコルーチン。
    /// </summary>
    /// <param name="filePath">選択された画像のパス</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator SplitImage(Texture2D originalTexture)
    {
        if (originalTexture == null)
        {
            //Debug.LogError("Failed to load texture.");
            yield break;
        }

        // Set image width and height, 分割する幅と高さを設定
        _originalWidth = originalTexture.width / 2;
        _originalHeight = originalTexture.height;

        // Create left texture, 左側のテクスチャを作成
        Texture2D leftTexture = new Texture2D(_originalWidth, _originalHeight, originalTexture.format, false);
        // Create right texture, 右側のテクスチャを作成（リニアカラー空間として扱う）
        Texture2D rightTexture = new Texture2D(originalTexture.width - _originalWidth, _originalHeight, originalTexture.format, false, true); // true for linear

        // Get pixel data, ピクセルデータを取得
        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] leftPixels = new Color32[_originalWidth * _originalHeight];
        Color32[] rightPixels = new Color32[(originalTexture.width - _originalWidth) * _originalHeight];

        // Copy left image pixels, 左側のピクセルをコピー
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = 0; x < _originalWidth; x++)
            {
                leftPixels[y * _originalWidth + x] = originalPixels[y * originalTexture.width + x];
            }
        }

        // Copy right image pixels,右側のピクセルをコピー
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = _originalWidth; x < originalTexture.width; x++)
            {
                rightPixels[y * (originalTexture.width - _originalWidth) + (x - _originalWidth)] = originalPixels[y * originalTexture.width + x];
            }
        }

        // Calculate the depth information (y,x) from the right half of the image and make it into a two-dimensional arrayy.
        // 右半分の画像から深度情報(y,x)を計算し２次元配列にする
        pixelZData = new float[rightPixels.GetLength(0)];
        for (int i = 0; i < pixelZData.Length; i++)
        {
            pixelZData[i] = (rightPixels[i].a * 16777216f + rightPixels[i].b * 65536f + rightPixels[i].g * 256f + rightPixels[i].r) / 10000f;
        }

        _pixelZMatrix = new float[(int)(pixelZData.Length / _originalWidth), (int)(pixelZData.Length / _originalHeight)];
        for (int j = 0; j < _originalHeight; j++)
        {
            for (int i = 0; i < _originalWidth; i++)
            {
                _pixelZMatrix[j, i] = pixelZData[j * _originalWidth + i];
            }
        }

        // Set max depth, プロパティに最深値をセット
        _pixelZMax = Mathf.Max(pixelZData);

        // Set min depth, プロパティに最近値をセット
        _pixelZMin = Mathf.Min(pixelZData);

        //_meshX, _meshY を計算
        var bestMesh = FindBestMeshSize(TARGET_MESH, DIFF);
        _meshX = bestMesh.numX;
        _meshY = bestMesh.numY;

        // Invoke event to notify other objects, イベントを発火して他のオブジェクトに通知
        OnImageCreated?.Invoke(leftPixels);

        // Release memory, メモリ解放
        Destroy(originalTexture);
        Destroy(rightTexture);

        yield return null;
    }

    /// <summary>
    /// 画像の縦横比になるべく合致するメッシュの縦横の数を見つけるメソッド
    /// </summary>
    /// <param name="total">メッシュの数</param>
    /// <param name="diff">メッシュ数の許容される大小の幅 </param>
    /// <returns>メッシュの数(bestX, bestY)</returns>
    private (int numX, int numY) FindBestMeshSize(int total, int diff)
    {
        // 元画像のアスペクト比 (幅 / 高さ)
        double aspectRatio = (double)_originalWidth / _originalHeight;

        // 許容範囲 (e.g. 248000～252000)
        int minMesh = total - diff;
        int maxMesh = total + diff;

        // 最適解探索用
        double bestRatioError = double.MaxValue;
        int bestX = 0;
        int bestY = 0;

        // 許容範囲内の合計値について試す
        // (差分 4001 回程度のループなのでそこまで重くはありません)
        for (int t = minMesh; t <= maxMesh; t++)
        {
            if (t <= 0) continue;

            // x * y = t かつ (x / y) ≈ aspectRatio を想定した近似
            // x^2 ≈ t * aspectRatio → x ≈ sqrt(t * aspectRatio)
            int x = Mathf.RoundToInt((float)System.Math.Sqrt(t * aspectRatio));
            if (x <= 0) continue;

            // y = t / x 近似
            int y = Mathf.RoundToInt(t / (float)x);

            // product が再度 許容範囲内かどうかチェック
            int product = x * y;
            if (product < minMesh || product > maxMesh)
            {
                // 合計値が許容範囲を逸脱していればスキップ
                continue;
            }

            // アスペクト比とどれくらい離れているか
            double currentRatioError = System.Math.Abs((double)x / y - aspectRatio);

            // 今までで最もアスペクト比に近ければ更新
            if (currentRatioError < bestRatioError)
            {
                bestRatioError = currentRatioError;
                bestX = x;
                bestY = y;
            }
        }
        // 結果をメンバー変数に格納
        return (bestX, bestY);
    }
}
