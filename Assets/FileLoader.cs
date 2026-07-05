using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Text;
using SimpleFileBrowser;
using UnityEngine.Events;

[System.Serializable]
public class ImageCreatedEvent : UnityEvent<Color32[]> { }

public class FileLoader : MonoBehaviour
{
    private const string DepthMetadataKeyword = "LookingGlassGoDepthMetadata";
    private const int TargetMesh = 250000;
    private const int MeshDiff = 2000;

    [SerializeField]
    private bool _isReadyToLoad = true;
    public bool IsReadyToLoad
    {
        get { return _isReadyToLoad; }
        set { _isReadyToLoad = value; }
    }

    private bool _isReadyToControl;
    public bool IsReadyToControl
    {
        get { return _isReadyToControl; }
    }

    [SerializeField]
    private int _originalWidth = 0;
    public int OriginalWidth
    {
        get { return _originalWidth; }
    }

    [SerializeField]
    private int _originalHeight = 0;
    public int OriginalHeight
    {
        get { return _originalHeight; }
    }

    private float[] _pixelZData;
    public float[] PixelZData
    {
        get { return _pixelZData; }
    }

    private float[,] _pixelZMatrix;
    public float[,] PixelZMatrix
    {
        get { return _pixelZMatrix; }
    }

    private Texture2D _depthTexture;
    public Texture2D DepthTexture
    {
        get { return _depthTexture; }
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
    public int MeshX
    {
        get { return _meshX; }
    }

    [SerializeField]
    private int _meshY;
    public int MeshY
    {
        get { return _meshY; }
    }

    [SerializeField]
    private bool _is360;
    public bool Is360
    {
        get { return _is360; }
    }

    private bool _hasSourceVerticalFov;
    public bool HasSourceVerticalFov
    {
        get { return _hasSourceVerticalFov; }
    }

    private float _sourceVerticalFov;
    public float SourceVerticalFov
    {
        get { return _sourceVerticalFov; }
    }

    private float _sourceHorizontalFov;
    public float SourceHorizontalFov
    {
        get { return _sourceHorizontalFov; }
    }

    private float _sourceFocalLengthPx;
    public float SourceFocalLengthPx
    {
        get { return _sourceFocalLengthPx; }
    }

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

    private void OnDestroy()
    {
        if (_depthTexture != null)
        {
            Destroy(_depthTexture);
            _depthTexture = null;
        }
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
        if (!texture.LoadImage(pngData))
        {
            Destroy(texture);
            _isReadyToLoad = true;
            return;
        }

        StartCoroutine(SplitImage(texture, pngData, filePath));
    }

    void OnFileNotSelected()
    {
        _isReadyToLoad = true;
    }

    private IEnumerator SplitImage(Texture2D originalTexture, byte[] pngData, string filePath)
    {
        if (originalTexture == null)
        {
            yield break;
        }

        _originalWidth = originalTexture.width / 2;
        _originalHeight = originalTexture.height;
        LoadDepthMetadata(pngData, filePath);

        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] leftPixels = new Color32[_originalWidth * _originalHeight];

        _pixelZData = new float[_originalWidth * _originalHeight];
        _pixelZMatrix = new float[_originalHeight, _originalWidth];
        _pixelZMax = float.MinValue;
        _pixelZMin = float.MaxValue;

        for (int y = 0; y < _originalHeight; y++)
        {
            int sourceRow = y * originalTexture.width;
            int targetRow = y * _originalWidth;
            Array.Copy(originalPixels, sourceRow, leftPixels, targetRow, _originalWidth);

            int depthRow = sourceRow + _originalWidth;
            for (int x = 0; x < _originalWidth; x++)
            {
                float z = DecodeDepth(originalPixels[depthRow + x]);
                int targetIndex = targetRow + x;
                _pixelZData[targetIndex] = z;
                _pixelZMatrix[y, x] = z;
                if (z > _pixelZMax)
                {
                    _pixelZMax = z;
                }
                if (z < _pixelZMin)
                {
                    _pixelZMin = z;
                }
            }
        }

        if (_pixelZData.Length == 0)
        {
            _pixelZMax = 0f;
            _pixelZMin = 0f;
        }

        CreateDepthTexture(_pixelZData, _originalWidth, _originalHeight);

        var bestMesh = FindBestMeshSize(TargetMesh, MeshDiff);
        _meshX = bestMesh.meshX;
        _meshY = bestMesh.meshY;

        OnImageCreated?.Invoke(leftPixels);
        Destroy(originalTexture);

        yield return null;
    }

    private static float DecodeDepth(Color32 pixel)
    {
        uint rawDepth = (uint)pixel.r
            | ((uint)pixel.g << 8)
            | ((uint)pixel.b << 16)
            | ((uint)pixel.a << 24);
        return rawDepth / 10000f;
    }

    private void CreateDepthTexture(float[] depthData, int width, int height)
    {
        if (_depthTexture != null)
        {
            Destroy(_depthTexture);
            _depthTexture = null;
        }

        _depthTexture = new Texture2D(width, height, TextureFormat.RFloat, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _depthTexture.SetPixelData(depthData, 0);
        _depthTexture.Apply(false, false);
    }

    private void LoadDepthMetadata(byte[] pngData, string imagePath)
    {
        _hasSourceVerticalFov = false;
        _sourceVerticalFov = 0f;
        _sourceHorizontalFov = 0f;
        _sourceFocalLengthPx = 0f;

        string json = ReadPngITxt(pngData, DepthMetadataKeyword);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            DepthMetadata metadata = JsonUtility.FromJson<DepthMetadata>(json);
            if (metadata == null)
            {
                return;
            }

            _sourceFocalLengthPx = metadata.focallength_px;
            _sourceVerticalFov = metadata.vertical_fov_deg;
            _sourceHorizontalFov = metadata.horizontal_fov_deg;

            if (_sourceVerticalFov <= 0f && _sourceFocalLengthPx > 0f)
            {
                int height = metadata.height > 0 ? metadata.height : _originalHeight;
                _sourceVerticalFov = CalculateFovDegrees(height, _sourceFocalLengthPx);
            }
            if (_sourceHorizontalFov <= 0f && _sourceFocalLengthPx > 0f)
            {
                int width = metadata.width > 0 ? metadata.width : _originalWidth;
                _sourceHorizontalFov = CalculateFovDegrees(width, _sourceFocalLengthPx);
            }

            _hasSourceVerticalFov = _sourceVerticalFov > 0f;
            if (_hasSourceVerticalFov)
            {
                Debug.Log(
                    $"Loaded RGBDE metadata: focalLengthPx={_sourceFocalLengthPx:F3}, "
                    + $"verticalFov={_sourceVerticalFov:F3}, horizontalFov={_sourceHorizontalFov:F3}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read embedded depth metadata: {imagePath}\n{ex.Message}");
        }
    }

    private static string ReadPngITxt(byte[] pngData, string keyword)
    {
        if (!HasPngSignature(pngData))
        {
            return null;
        }

        int offset = 8;
        while (offset + 12 <= pngData.Length)
        {
            uint length = ReadUInt32BigEndian(pngData, offset);
            if (length > int.MaxValue)
            {
                return null;
            }

            long chunkEnd = (long)offset + 12L + length;
            if (chunkEnd > pngData.Length)
            {
                return null;
            }

            string chunkType = Encoding.ASCII.GetString(pngData, offset + 4, 4);
            int dataOffset = offset + 8;
            int dataLength = (int)length;

            if (chunkType == "iTXt")
            {
                string text = TryReadITxtChunk(pngData, dataOffset, dataLength, keyword);
                if (text != null)
                {
                    return text;
                }
            }
            else if (chunkType == "IEND")
            {
                break;
            }

            offset = (int)chunkEnd;
        }

        return null;
    }

    private static bool HasPngSignature(byte[] data)
    {
        return data != null
            && data.Length >= 8
            && data[0] == 0x89
            && data[1] == 0x50
            && data[2] == 0x4E
            && data[3] == 0x47
            && data[4] == 0x0D
            && data[5] == 0x0A
            && data[6] == 0x1A
            && data[7] == 0x0A;
    }

    private static uint ReadUInt32BigEndian(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24)
            | ((uint)data[offset + 1] << 16)
            | ((uint)data[offset + 2] << 8)
            | data[offset + 3];
    }

    private static string TryReadITxtChunk(byte[] data, int dataOffset, int dataLength, string keyword)
    {
        int end = dataOffset + dataLength;
        int keywordEnd = FindNullByte(data, dataOffset, end);
        if (keywordEnd < 0)
        {
            return null;
        }

        string chunkKeyword = Encoding.ASCII.GetString(data, dataOffset, keywordEnd - dataOffset);
        if (!string.Equals(chunkKeyword, keyword, StringComparison.Ordinal))
        {
            return null;
        }

        int cursor = keywordEnd + 1;
        if (cursor + 2 > end)
        {
            return null;
        }

        byte compressionFlag = data[cursor++];
        byte compressionMethod = data[cursor++];
        if (compressionFlag != 0 || compressionMethod != 0)
        {
            return null;
        }

        int languageEnd = FindNullByte(data, cursor, end);
        if (languageEnd < 0)
        {
            return null;
        }

        cursor = languageEnd + 1;
        int translatedKeywordEnd = FindNullByte(data, cursor, end);
        if (translatedKeywordEnd < 0)
        {
            return null;
        }

        cursor = translatedKeywordEnd + 1;
        return Encoding.UTF8.GetString(data, cursor, end - cursor);
    }

    private static int FindNullByte(byte[] data, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (data[i] == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static float CalculateFovDegrees(float sizePx, float focalLengthPx)
    {
        return Mathf.Atan(sizePx / (2f * focalLengthPx)) * Mathf.Rad2Deg * 2f;
    }

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
            if (t <= 0)
            {
                continue;
            }
            int approxX = Mathf.RoundToInt((float)Math.Sqrt(t * aspectRatio));
            if (approxX <= 0)
            {
                continue;
            }
            int approxY = Mathf.RoundToInt(t / (float)approxX);
            int product = approxX * approxY;
            if (product < minMesh || product > maxMesh)
            {
                continue;
            }
            double currentRatioError = Math.Abs((double)approxX / approxY - aspectRatio);
            if (currentRatioError < bestRatioError)
            {
                bestRatioError = currentRatioError;
                bestMeshX = approxX;
                bestMeshY = approxY;
            }
        }
        return (bestMeshX, bestMeshY);
    }

    [Serializable]
    private class DepthMetadata
    {
        public int width;
        public int height;
        public float focallength_px;
        public float vertical_fov_deg;
        public float horizontal_fov_deg;
    }
}
