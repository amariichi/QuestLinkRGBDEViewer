using UnityEngine;
using System;
using Color = UnityEngine.Color;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GridMesh : MonoBehaviour
{
    [SerializeField] private FileLoader _fileLoader;
    [SerializeField] private Material _meshMaterial;

    [SerializeField] private int _originalWidth;
    [SerializeField] private int _originalHeight;

    [SerializeField] private int _meshWidth;
    [SerializeField] private int _meshHeight;

    [SerializeField] private int _texWidth;
    [SerializeField] private int _texHeight;

    private float[] _zValuesMesh;
    private float _zValueMax;
    private float _zValueMin;

    private float[,] _zValues;
    private float[,] _filteredDepth;

    private float[,] _uMatrix;
    private float[,] _vMatrix;

    private Vector3[] _vertices;
    private Vector3[] _initialVertices;
    private Vector3[] _rayDirections;
    private float[] _pixelCoordX;
    private float[] _pixelCoordY;

    private float _objectSize = 1.0f;
    private float _diameter = 2.0f;
    [SerializeField] private Vector3 _initialPosition;

    private const float CENTER_Z_MAX = -0.25f;
    private const float CENTER_Z_MIN = -4.0f;
    private float _currentCenterZ = CENTER_Z_MIN;
    private float _previousCenterZ;

    private float _radius;

    private Mesh _mesh;

    private bool _isMeshCreated;
    public bool IsMeshCreated { get { return _isMeshCreated; } }

    private float _prevControllerPosX = 0f;
    private float _prevControllerPosY = 0f;
    private float _prevControllerPosZ = 0f;
    private float _prevControllerRPosZ = 0f;
    private float _prevControllerRPosX = 0f;

    [SerializeField] private float _magnificationZ = 1.0f;
    public float MagnificationZ { get { return _magnificationZ; } }
    private float _prevMagnificationZ;

    private const float MAG_MAX = 25.0f;
    private const float MAG_MIN = 0.0f;
    private const float POW_MAX = 25.0f;
    private const float POW_MIN = 0.01f;

    private float _powerFactor = 1.0f;
    public float PowerFactor { get { return _powerFactor; } }
    private float _prevPowerFactor;

    private string _linearity = "Linear";
    public string Linearity { get { return _linearity; } }
    private string _prevLinearity;

    private const float OFFSET = 0.3f;

    private Transform _meshTransform;

    private const float SETBACK = 1.0f;
    private const float UI_CLEARANCE = 0.4f;

    private const float TRIGGER_THRESHOLD = 0.7f;
    private const float CONTROLLER_DIFF_MULTIPLIER = 100.0f;
    private const float STICK_MOVE_FACTOR = 0.01f;
    private const float POWER_DIFF = 0.01f;

    private const float MIN_FOV_Y = 30f;
    private const float MAX_FOV_Y = 110f;
    private const float MIN_DEPTH_CLAMP = 0.15f;

    private const float DEPTH_SPIKE_THRESHOLD = 0.45f;
    private const float DEPTH_STABLE_TOLERANCE = 0.12f;
    private const float COLOR_EDGE_THRESHOLD = 0.10f;
    private const float SMOOTH_BLEND = 0.30f;
    private const float BILATERAL_DEPTH_SIGMA = 0.35f;
    private const float BILATERAL_COLOR_SIGMA = 0.08f;
    private const float SCALE_MIN = 0.05f;
    private const float SCALE_MAX = 10.0f;

    private static readonly float[,] SpatialKernel = new float[3, 3]
    {
        {0.075f, 0.124f, 0.075f},
        {0.124f, 0.204f, 0.124f},
        {0.075f, 0.124f, 0.075f}
    };

    private float _baseDepth;
    private float _targetNearDistance;

    public void OnImageCreatedHandler(Color32[] leftPixels)
    {
        _prevMagnificationZ = 0.0f;
        _prevPowerFactor = 0.0f;
        _prevLinearity = "Dummy";
        _previousCenterZ = _currentCenterZ;

        _originalWidth = _fileLoader.OriginalWidth;
        _originalHeight = _fileLoader.OriginalHeight;
        _meshWidth = _fileLoader.MeshX;
        _meshHeight = _fileLoader.MeshY;
        _zValues = _fileLoader.PixelZMatrix;
        _filteredDepth = PreprocessDepth(_zValues, leftPixels);
        var extents = FindDepthExtents(_filteredDepth);
        _zValueMin = Mathf.Max(extents.min, MIN_DEPTH_CLAMP);
        _zValueMax = Mathf.Max(_zValueMin + 0.001f, extents.max);
        _baseDepth = _zValueMin;

        if ((float)_originalHeight / _originalWidth <= (float)_meshHeight / _meshWidth)
        {
            _texWidth = _originalWidth;
            _texHeight = (int)(_meshHeight * (float)_originalWidth / _meshWidth);
        }
        else
        {
            _texWidth = (int)(_meshWidth * (float)_originalHeight / _meshHeight);
            _texHeight = _originalHeight;
        }
        Texture2D newTexture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false);

        Color fillColor = new Color(0f, 0f, 0f, 0f);
        Color[] fillPixels = new Color[_texWidth * _texHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        newTexture.SetPixels32(0, 0, _originalWidth, _originalHeight, leftPixels);
        newTexture.Apply();

        Destroy(_meshMaterial.mainTexture);
        AssignTextureToMaterial(newTexture, _meshMaterial);

        _meshTransform = transform;
        Vector3 pos = Vector3.zero;
        _meshTransform.position = pos;
        _initialPosition = pos;

        int vertCount = (_meshWidth + 1) * (_meshHeight + 1);
        _zValuesMesh = new float[vertCount];
        _initialVertices = new Vector3[vertCount];
        _vertices = new Vector3[vertCount];
        _rayDirections = new Vector3[vertCount];
        _pixelCoordX = new float[vertCount];
        _pixelCoordY = new float[vertCount];

        if (_fileLoader.Is360)
        {
            _linearity = "Linear";
            GenerateInvertedSphere(_meshWidth, _meshHeight);
            _zValuesMesh = CalculateZValuesFor360();
        }
        else
        {
            _linearity = "Linear";
            CreatePerspectiveMesh(_meshWidth, _meshHeight, _currentCenterZ);
            Vector3 tfPos = _meshTransform.position;
            _meshTransform.position = tfPos + new Vector3(0, 0, ComputeViewTranslation());
        }

        _targetNearDistance = _meshTransform.position.z + _meshTransform.localScale.x * _baseDepth;

        _isMeshCreated = true;
        _fileLoader.IsReadyToLoad = true;
    }

    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null || texture == null)
            return;
        material.mainTexture = texture;
    }

    private void CreatePerspectiveMesh(int longitudeSegments, int latitudeSegments, float centerOffset)
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.name = "PerspectiveGrid";

        Vector3[] normals = new Vector3[_zValuesMesh.Length];
        Vector2[] uvs = new Vector2[_zValuesMesh.Length];

        float fovY = ComputeVerticalFov(centerOffset) * Mathf.Deg2Rad;
        float aspect = (float)_originalWidth / _originalHeight;
        float fovX = 2f * Mathf.Atan(Mathf.Tan(fovY / 2f) * aspect);
        float tanHalfX = Mathf.Tan(fovX / 2f);
        float tanHalfY = Mathf.Tan(fovY / 2f);

        float widthMinusOne = Mathf.Max(1, _originalWidth - 1);
        float heightMinusOne = Mathf.Max(1, _originalHeight - 1);
        float invHeightMinusOne = 1f / heightMinusOne;
        float uScale = (float)_originalWidth / _texWidth;
        float vScale = (float)_originalHeight / _texHeight;

        int index = 0;
        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float meshV = (float)lat / latitudeSegments;
            float pixelY = (1f - meshV) * heightMinusOne;
            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float u = (float)lon / longitudeSegments;
                float pixelX = u * widthMinusOne;

                float screenX = (u - 0.5f) * 2f * tanHalfX;
                float screenY = (0.5f - meshV) * 2f * tanHalfY;
                Vector3 dir = new Vector3(screenX, screenY, 1f).normalized;

                float depth = SampleDepth(pixelX, pixelY);

                _rayDirections[index] = dir;
                _pixelCoordX[index] = pixelX;
                _pixelCoordY[index] = pixelY;
                _zValuesMesh[index] = depth;
                _initialVertices[index] = dir * depth;
                _vertices[index] = _initialVertices[index];
                normals[index] = -dir;
                uvs[index] = new Vector2(u * uScale, (1f - meshV) * vScale);
                index++;
            }
        }

        int[] triangles = GenerateTriangles(longitudeSegments, latitudeSegments);
        InvertTriangleWinding(triangles);
        _mesh.vertices = _vertices;
        _mesh.normals = normals;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;

        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = _mesh;
    }

    private void GenerateInvertedSphere(int longitudeSegments, int latitudeSegments)
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.name = "InvertedSphere";

        int vertCount = (latitudeSegments + 1) * (longitudeSegments + 1);
        _initialVertices = new Vector3[vertCount];
        _vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        float radius = _diameter / 2f;
        int index = 0;

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float latNormalized = (float)lat / latitudeSegments;
            float phi = Mathf.PI * (1f - latNormalized);
            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float lonNormalized = (float)lon / longitudeSegments;
                float theta = 2f * Mathf.PI * (1f - lonNormalized);
                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                Vector3 pos = new Vector3(x, y, z) * radius;
                _vertices[index] = pos;
                _initialVertices[index] = pos;
                normals[index] = -new Vector3(x, y, z);
                uvs[index] = new Vector2(lonNormalized, latNormalized);
                index++;
            }
        }

        int[] triangles = GenerateTriangles(longitudeSegments, latitudeSegments);
        _mesh.vertices = _vertices;
        _mesh.normals = normals;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;

        _mesh.RecalculateNormals();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = _mesh;
    }

    private int[] GenerateTriangles(int longitudeSegments, int latitudeSegments)
    {
        int[] triangles = new int[latitudeSegments * longitudeSegments * 6];
        int triIndex = 0;
        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int current = lat * (longitudeSegments + 1) + lon;
                int next = current + longitudeSegments + 1;
                triangles[triIndex++] = current;
                triangles[triIndex++] = next;
                triangles[triIndex++] = current + 1;
                triangles[triIndex++] = current + 1;
                triangles[triIndex++] = next;
                triangles[triIndex++] = next + 1;
            }
        }
        return triangles;
    }

    private void InvertTriangleWinding(int[] triangles)
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int tmp = triangles[i];
            triangles[i] = triangles[i + 1];
            triangles[i + 1] = tmp;
        }
    }

    private float[] CalculateZValuesFor360()
    {
        float[] zValuesArray = new float[(_meshWidth + 1) * (_meshHeight + 1)];
        float meshScale = _meshWidth > _meshHeight ? (float)_meshWidth / _texWidth : (float)_meshHeight / _texHeight;
        Array.Fill(zValuesArray, _zValueMax);

        for (int j = 0; j < _meshHeight; j++)
        {
            for (int i = 0; i < _meshWidth; i++)
            {
                int matrixColumn = Mathf.Min((int)(i / meshScale), _originalWidth - 1);
                int matrixRow = Mathf.Min((int)(j / meshScale), _originalHeight - 1);
                float depth = _filteredDepth[matrixRow, matrixColumn];
                if (depth <= 0f)
                {
                    depth = _baseDepth;
                }
                zValuesArray[j * (_meshWidth + 1) + i] = Mathf.Max(depth, _baseDepth) + OFFSET;
            }
            int rightIndex = j * (_meshWidth + 1) + _meshWidth;
            zValuesArray[rightIndex] = _fileLoader.Is360
                ? zValuesArray[j * (_meshWidth + 1)]
                : zValuesArray[rightIndex - 1];
        }
        for (int i = (_meshWidth + 1) * _meshHeight; i < (_meshWidth + 1) * (_meshHeight + 1); i++)
        {
            zValuesArray[i] = zValuesArray[i - (_meshWidth + 1)];
        }
        return zValuesArray;
    }

    void Start()
    {
        _isMeshCreated = false;
    }

    void Update()
    {
        if (!_isMeshCreated)
        {
            return;
        }

        _meshTransform = transform;

        if (_fileLoader.IsReadyToControl)
        {
            HandleObjectManipulation();
        }

        bool needsRefresh = false;

        if (!_fileLoader.Is360 && _currentCenterZ != _previousCenterZ)
        {
            _previousCenterZ = _currentCenterZ;
            CreatePerspectiveMesh(_meshWidth, _meshHeight, _currentCenterZ);
            needsRefresh = true;
        }

        if (_magnificationZ != _prevMagnificationZ || _powerFactor != _prevPowerFactor || _linearity != _prevLinearity)
        {
            needsRefresh = true;
        }

        if (!needsRefresh)
        {
            return;
        }

        _prevMagnificationZ = _magnificationZ;
        _prevPowerFactor = _powerFactor;
        _prevLinearity = _linearity;

        if (_fileLoader.Is360)
        {
            UpdateVertexZPositions(i =>
            {
                float depth = _zValuesMesh[i];
                if (_linearity == "Log")
                {
                    float relative = Mathf.Max(depth - _baseDepth + OFFSET, 0.001f);
                    depth = _baseDepth + Mathf.Log(1f + Mathf.Pow(relative, _powerFactor));
                }
                float scaledDepth = _baseDepth + _magnificationZ * (depth - _baseDepth + OFFSET);
                return Mathf.Max(scaledDepth, _baseDepth + OFFSET);
            });
        }
        else
        {
            UpdateVertexZPositions(i =>
            {
                float depth = _zValuesMesh[i];
                if (_linearity == "Log")
                {
                    float relative = Mathf.Max(depth - _baseDepth + OFFSET, 0.001f);
                    depth = _baseDepth + Mathf.Log(1f + Mathf.Pow(relative, _powerFactor));
                }
                float scaledDepth = _baseDepth + _magnificationZ * (depth - _baseDepth);
                return Mathf.Max(scaledDepth, _baseDepth + 0.001f);
            });
        }
    }

    public void UpdateVertexZPositions(Func<int, float> depthFunc)
    {
        if (_fileLoader.Is360)
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                Vector3 dir = _initialVertices[i].normalized;
                float scale = depthFunc(i);
                _vertices[i] = dir * scale;
            }
        }
        else
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                float depth = depthFunc(i);
                _vertices[i] = _rayDirections[i] * depth;
            }
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    private void HandleObjectManipulation()
    {
        float triggerR = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger);
        float triggerR2 = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger);
        Vector3 localPosR = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        float diffRZ = localPosR.z - _prevControllerRPosZ;
        float diffRX = localPosR.x - _prevControllerRPosX;
        float factor = (_magnificationZ + 0.01f) / MAG_MAX;
        _prevControllerRPosZ = localPosR.z;
        _prevControllerRPosX = localPosR.x;
        Vector3 meshPos = _meshTransform.position;
        if (triggerR > TRIGGER_THRESHOLD)
        {
            _magnificationZ = Mathf.Max(Mathf.Min(_magnificationZ + factor * diffRZ * CONTROLLER_DIFF_MULTIPLIER, MAG_MAX), MAG_MIN);
        }
        if (triggerR2 > TRIGGER_THRESHOLD)
        {
            _currentCenterZ = Mathf.Max(Mathf.Min(_currentCenterZ + diffRX, CENTER_Z_MAX), CENTER_Z_MIN);
        }

        if (OVRInput.Get(OVRInput.RawButton.Y))
            _powerFactor = Mathf.Min(_powerFactor + POWER_DIFF, POW_MAX);
        if (OVRInput.Get(OVRInput.RawButton.X))
            _powerFactor = Mathf.Max(_powerFactor - POWER_DIFF, POW_MIN);
        _linearity = SelectLinearity();

        float triggerL = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
        Vector3 localPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
        float diffX = localPos.x - _prevControllerPosX;
        float diffY = localPos.y - _prevControllerPosY;
        float diffZ = localPos.z - _prevControllerPosZ;
        _prevControllerPosX = localPos.x;
        _prevControllerPosY = localPos.y;
        _prevControllerPosZ = localPos.z;
        bool translationAdjusted = false;
        if (triggerL > TRIGGER_THRESHOLD)
        {
            meshPos.x += diffX;
            meshPos.y += diffY;
            meshPos.z += diffZ;
            translationAdjusted = true;
        }

        Vector2 stickPositionL = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        if (!Mathf.Approximately(stickPositionL.x, 0f) || !Mathf.Approximately(stickPositionL.y, 0f))
        {
            meshPos.x += stickPositionL.x * STICK_MOVE_FACTOR;
            meshPos.z += stickPositionL.y * STICK_MOVE_FACTOR;
            translationAdjusted = true;
        }

        if (!_fileLoader.Is360 && translationAdjusted)
        {
            _targetNearDistance = meshPos.z + _meshTransform.localScale.x * _baseDepth;
        }

        Vector2 stickPositionR = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        float scaleDelta = stickPositionR.x * STICK_MOVE_FACTOR;
        if (!Mathf.Approximately(scaleDelta, 0f))
        {
            float currentScale = _meshTransform.localScale.x;
            float targetScale = Mathf.Clamp(currentScale + scaleDelta, SCALE_MIN, SCALE_MAX);
            if (!Mathf.Approximately(targetScale, currentScale))
            {
                _meshTransform.localScale = new Vector3(targetScale, targetScale, targetScale);
                if (_fileLoader.Is360)
                {
                    meshPos.z += (targetScale - currentScale) * _currentCenterZ;
                }
                else if (_baseDepth > 0f)
                {
                    meshPos.z = _targetNearDistance - targetScale * _baseDepth;
                }
            }
        }
        _meshTransform.position = meshPos;

        if (!_fileLoader.Is360 && _baseDepth > 0f)
        {
            _targetNearDistance = _meshTransform.position.z + _meshTransform.localScale.x * _baseDepth;
        }

        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            _meshTransform.position = _initialPosition + new Vector3(0, 0, ComputeViewTranslation());
            _meshTransform.localScale = Vector3.one;
            _magnificationZ = 1f;
            _powerFactor = 1f;
            _linearity = "Linear";
            if (!_fileLoader.Is360 && _baseDepth > 0f)
            {
                _targetNearDistance = _meshTransform.position.z + _meshTransform.localScale.x * _baseDepth;
            }
        }
    }

    private string SelectLinearity()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B)) _linearity = "Linear";
        if (OVRInput.GetDown(OVRInput.RawButton.A)) _linearity = "Log";
        return _linearity;
    }

    private float[,] PreprocessDepth(float[,] source, Color32[] colors)
    {
        if (source == null || colors == null || colors.Length == 0)
        {
            return source;
        }

        int height = source.GetLength(0);
        int width = source.GetLength(1);

        float[,] working = (float[,])source.Clone();
        float[,] spikeReduced = ReduceDepthSpikes(working, colors, width, height);
        float[,] smoothed = ApplyEdgeAwareSmooth(spikeReduced, colors, width, height);
        return smoothed;
    }

    private float[,] ReduceDepthSpikes(float[,] depth, Color32[] colors, int width, int height)
    {
        float[,] result = (float[,])depth.Clone();
        float[] window = new float[9];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = 0;
                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int nx = ClampIndex(x + kx, width);
                        int ny = ClampIndex(y + ky, height);
                        window[idx++] = depth[ny, nx];
                    }
                }

                Array.Sort(window);
                float median = window[window.Length / 2];
                float center = depth[y, x];
                if (center <= 0f)
                {
                    continue;
                }

                int stableCount = 0;
                float colorDiffAccum = 0f;
                int neighborCount = 0;
                Color32 centerColor = colors[y * width + x];

                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        if (kx == 0 && ky == 0)
                        {
                            continue;
                        }
                        int nx = ClampIndex(x + kx, width);
                        int ny = ClampIndex(y + ky, height);
                        float neighbor = depth[ny, nx];
                        if (Mathf.Abs(neighbor - median) < DEPTH_STABLE_TOLERANCE)
                        {
                            stableCount++;
                        }
                        Color32 neighborColor = colors[ny * width + nx];
                        colorDiffAccum += ComputeColorDistance(centerColor, neighborColor);
                        neighborCount++;
                    }
                }

                float avgColorDiff = neighborCount > 0 ? colorDiffAccum / neighborCount : 0f;
                if (Mathf.Abs(center - median) > DEPTH_SPIKE_THRESHOLD &&
                    stableCount >= 5 &&
                    avgColorDiff < COLOR_EDGE_THRESHOLD)
                {
                    result[y, x] = median;
                }
            }
        }

        return result;
    }

    private float[,] ApplyEdgeAwareSmooth(float[,] depth, Color32[] colors, int width, int height)
    {
        float[,] result = new float[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float center = depth[y, x];
                if (center <= 0f)
                {
                    result[y, x] = center;
                    continue;
                }

                float accum = 0f;
                float weightSum = 0f;
                Color32 centerColor = colors[y * width + x];

                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int nx = ClampIndex(x + kx, width);
                        int ny = ClampIndex(y + ky, height);
                        float neighbor = depth[ny, nx];
                        float spatial = SpatialKernel[ky + 1, kx + 1];
                        float depthDiff = neighbor - center;
                        float depthWeight = Mathf.Exp(-(depthDiff * depthDiff) / (2f * BILATERAL_DEPTH_SIGMA * BILATERAL_DEPTH_SIGMA));
                        float colorDiff = ComputeColorDistance(centerColor, colors[ny * width + nx]);
                        float colorWeight = Mathf.Exp(-(colorDiff * colorDiff) / (2f * BILATERAL_COLOR_SIGMA * BILATERAL_COLOR_SIGMA));
                        float weight = spatial * depthWeight * colorWeight;
                        accum += neighbor * weight;
                        weightSum += weight;
                    }
                }

                float smoothed = weightSum > 0f ? accum / weightSum : center;
                result[y, x] = Mathf.Lerp(center, smoothed, SMOOTH_BLEND);
            }
        }

        return result;
    }

    private (float min, float max) FindDepthExtents(float[,] depth)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        int height = depth.GetLength(0);
        int width = depth.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = depth[y, x];
                if (value <= 0f)
                {
                    continue;
                }
                if (value < min)
                {
                    min = value;
                }
                if (value > max)
                {
                    max = value;
                }
            }
        }

        if (min == float.MaxValue)
        {
            min = MIN_DEPTH_CLAMP;
        }
        if (max == float.MinValue)
        {
            max = min;
        }

        return (min, max);
    }

    private float SampleDepth(float pixelX, float pixelY)
    {
        int ix = Mathf.Clamp(Mathf.RoundToInt(pixelX), 0, _originalWidth - 1);
        int iy = Mathf.Clamp(Mathf.RoundToInt(pixelY), 0, _originalHeight - 1);
        float depth = _filteredDepth[iy, ix];
        if (depth <= 0f)
        {
            depth = _baseDepth;
        }
        return Mathf.Max(depth, _baseDepth);
    }

    private float ComputeVerticalFov(float centerOffset)
    {
        float t = Mathf.InverseLerp(CENTER_Z_MIN, CENTER_Z_MAX, Mathf.Clamp(centerOffset, CENTER_Z_MIN, CENTER_Z_MAX));
        return Mathf.Lerp(MIN_FOV_Y, MAX_FOV_Y, t);
    }

    private float ComputeViewTranslation()
    {
        float clamped = Mathf.Clamp(_currentCenterZ, CENTER_Z_MIN, CENTER_Z_MAX);
        float offset = Mathf.Max(_baseDepth + clamped + SETBACK, 0.1f);
        return offset + UI_CLEARANCE;
    }

    private int ClampIndex(int value, int size)
    {
        if (value < 0)
            return 0;
        if (value >= size)
            return size - 1;
        return value;
    }

    private float ComputeColorDistance(Color32 a, Color32 b)
    {
        float dr = (a.r - b.r) / 255f;
        float dg = (a.g - b.g) / 255f;
        float db = (a.b - b.b) / 255f;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }
}
