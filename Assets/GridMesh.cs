using UnityEngine;
using System;
using Color = UnityEngine.Color;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GridMesh : MonoBehaviour
{
    // Inspector-assigned fields
    [SerializeField] private FileLoader _fileLoader;
    [SerializeField] private Material _meshMaterial;

    // Original frame size and initial scale
    [SerializeField] private int _originalWidth;
    [SerializeField] private int _originalHeight;

    // Maximum mesh dimensions
    [SerializeField] private int _meshWidth;
    [SerializeField] private int _meshHeight;

    // Final texture size adjusted to the mesh shape
    [SerializeField] private int _texWidth;
    [SerializeField] private int _texHeight;

    // Estimated depth information
    private float[] _zValuesMesh;
    private float _zValueMax;
    private float _zValueMin;

    // Image depth information
    private float[,] _zValues;

    // VU coordinate matrices
    private float[,] _uMatrix;
    private float[,] _vMatrix;

    // Object dimensions and initial position
    private float _objectSize = 1.0f;
    private float _diameter = 2.0f;
    [SerializeField] private Vector3 _initialPosition;

    // Constants for center Z limits
    private const float CENTER_Z_MAX = -0.25f;
    private const float CENTER_Z_MIN = -4.0f;
    private float _currentCenterZ = CENTER_Z_MIN;
    private float _previousCenterZ;

    // Partial sphere radius (or object radius)
    private float _radius;

    // Mesh and vertex arrays
    private Mesh _mesh;
    private Vector3[] _vertices;
    private Vector3[] _vertexPositions;

    // Mesh creation flag
    private bool _isMeshCreated;
    public bool IsMeshCreated { get { return _isMeshCreated; } }

    // Controller previous positions for input handling
    private float _prevControllerPosX = 0f;
    private float _prevControllerPosY = 0f;
    private float _prevControllerPosZ = 0f;
    private float _prevControllerRPosZ = 0f;
    private float _prevControllerRPosX = 0f;

    // Z-axis magnification and power factors
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

    // Calculation method for Z-axis ("Linear" or "Log")
    private string _linearity = "Linear";
    public string Linearity { get { return _linearity; } }
    private string _prevLinearity;

    // Z offset value and position array (if needed later)
    private const float OFFSET = 0.3f;
    private float[] _positionZ;

    // Transform for the mesh
    private Transform _meshTransform;

    // Setback for the plane image placement
    private const float SETBACK = 1.0f;

    // Input thresholds and factors (extracted magic numbers)
    private const float TRIGGER_THRESHOLD = 0.7f;
    private const float CONTROLLER_DIFF_MULTIPLIER = 100.0f;
    private const float STICK_MOVE_FACTOR = 0.01f;
    private const float POWER_DIFF = 0.01f;

    /// <summary>
    /// Called when a new image is created; sets up texture and mesh.
    /// </summary>
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
        _zValueMax = _fileLoader.PixelZMax;
        _zValueMin = _fileLoader.PixelZMin; // nearest point set to 0
        _positionZ = new float[(_meshWidth + 1) * (_meshHeight + 1)];

        // Create texture with an aspect ratio matching the mesh
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

        // Fill texture with transparent black
        Color fillColor = new Color(0f, 0f, 0f, 0f);
        Color[] fillPixels = new Color[_texWidth * _texHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        // Copy cropped pixels into new texture and apply
        newTexture.SetPixels32(0, 0, _originalWidth, _originalHeight, leftPixels);
        newTexture.Apply();

        // Assign texture to material
        Destroy(_meshMaterial.mainTexture);
        AssignTextureToMaterial(newTexture, _meshMaterial);

        // Center the mesh in the scene
        _meshTransform = transform;
        Vector3 pos = Vector3.zero;
        _meshTransform.position = pos;
        _initialPosition = pos;

        if (_fileLoader.Is360)
        {
            _linearity = "Linear";
            GenerateInvertedSphere(_meshWidth, _meshHeight);
        }
        else
        {
            _linearity = "Linear";
            CreateMesh(_meshWidth, _meshHeight, _currentCenterZ);
            Vector3 tfPos = _meshTransform.position;
            _meshTransform.position = tfPos + new Vector3(0, 0, _currentCenterZ + SETBACK);
        }

        _zValuesMesh = CalculateZValues();
        _isMeshCreated = true;
        _fileLoader.IsReadyToLoad = true;
    }

    /// <summary>
    /// Assigns the given texture to the specified material.
    /// </summary>
    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null || texture == null)
            return;
        material.mainTexture = texture;
    }

    /// <summary>
    /// Creates a partial sphere mesh using image data.
    /// </summary>
    private void CreateMesh(int longitudeSegments, int latitudeSegments, float centerOffset)
    {
        _isMeshCreated = false;
        float objectHalfWidth = _objectSize / 2f;
        float objectHalfHeight = objectHalfWidth * (float)latitudeSegments / longitudeSegments;
        float objectDistance = _initialPosition.z - centerOffset;
        float deltaTheta, deltaPhi;

        if (objectDistance <= 0f)
        {
            deltaTheta = Mathf.PI;
            deltaPhi = Mathf.PI;
        }
        else
        {
            deltaTheta = Mathf.Atan(objectHalfWidth / objectDistance) * 2f;
            deltaPhi = Mathf.Atan(objectHalfHeight / objectDistance) * 2f;
        }

        _radius = Mathf.Sqrt(objectDistance * objectDistance + objectHalfWidth * objectHalfWidth + objectHalfHeight * objectHalfHeight);
        float startTheta = (Mathf.PI - deltaTheta) / 2f;
        float startPhi = (Mathf.PI - deltaPhi) / 2f;
        float startThetaCos = Mathf.Cos(startTheta);
        float startPhiCos = Mathf.Cos(startPhi);

        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.name = "InvertedPartialSphere";

        int vertCount = (latitudeSegments + 1) * (longitudeSegments + 1);
        _vertexPositions = new Vector3[vertCount];
        _vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        int index = 0;
        _uMatrix = new float[latitudeSegments + 1, longitudeSegments + 1];
        _vMatrix = new float[latitudeSegments + 1, longitudeSegments + 1];

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float latNormalized = (float)lat / latitudeSegments;
            float phi = deltaPhi * (1f - latNormalized) + startPhi;
            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float lonNormalized = (float)lon / longitudeSegments;
                float theta = deltaTheta * (1f - lonNormalized) + startTheta;

                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                Vector3 pos = new Vector3(x, y, z) * _radius;
                _vertices[index] = pos;
                _vertexPositions[index] = pos;
                normals[index] = -new Vector3(x, y, z);
                float u = Mathf.Cos(theta) / startThetaCos / 2f + 0.5f;
                float v = Mathf.Cos(phi) / startPhiCos / 2f + 0.5f;
                uvs[index] = new Vector2(u, v);
                _uMatrix[lat, lon] = u;
                _vMatrix[lat, lon] = v;
                index++;
            }
        }

        // Generate triangles using a shared method.
        int[] triangles = GenerateTriangles(longitudeSegments, latitudeSegments);
        _mesh.vertices = _vertices;
        _mesh.normals = normals;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;

        _mesh.RecalculateNormals();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = _mesh;
    }

    /// <summary>
    /// Generates a full inverted sphere mesh (for 360Åã images).
    /// </summary>
    private void GenerateInvertedSphere(int longitudeSegments, int latitudeSegments)
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.name = "InvertedSphere";

        int vertCount = (latitudeSegments + 1) * (longitudeSegments + 1);
        _vertexPositions = new Vector3[vertCount];
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
                _vertexPositions[index] = pos;
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

    /// <summary>
    /// Generates triangle indices for a grid mesh.
    /// </summary>
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

    /// <summary>
    /// Calculates Z-values for each mesh vertex based on image depth.
    /// </summary>
    private float[] CalculateZValues()
    {
        float[] zValuesArray = new float[(_meshWidth + 1) * (_meshHeight + 1)];
        float meshScale = _meshWidth > _meshHeight ? (float)_meshWidth / _texWidth : (float)_meshHeight / _texHeight;
        Array.Fill(zValuesArray, _zValueMax);

        for (int j = 0; j < _meshHeight; j++)
        {
            for (int i = 0; i < _meshWidth; i++)
            {
                int matrixColumn, matrixRow;
                if (_fileLoader.Is360)
                {
                    matrixColumn = Mathf.Min((int)(i / meshScale), _texWidth);
                    matrixRow = Mathf.Min((int)(j / meshScale), _texHeight);
                    zValuesArray[j * (_meshWidth + 1) + i] = _zValues[matrixRow, matrixColumn] - _zValueMin + OFFSET;
                }
                else
                {
                    matrixColumn = Mathf.Min((int)(_uMatrix[j, i] * _texWidth), _texWidth);
                    matrixRow = Mathf.Min((int)(_vMatrix[j, i] * _texHeight), _texHeight);
                    zValuesArray[j * (_meshWidth + 1) + i] = _zValues[matrixRow, matrixColumn] - _zValueMin + OFFSET;
                }
            }
            // For right edge: duplicate neighbor or wrap for 360Åã images
            int rightIndex = j * (_meshWidth + 1) + _meshWidth;
            zValuesArray[rightIndex] = _fileLoader.Is360
                ? zValuesArray[j * (_meshWidth + 1)]
                : zValuesArray[rightIndex - 1];
        }
        // Copy bottom row from the row above
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
        bool passUpdateZpositions = true;

        if (!_isMeshCreated)
            return;

        _meshTransform = transform;

        if (_fileLoader.IsReadyToControl)
        {
            HandleObjectManipulation();
        }

        // Recreate mesh if center Z has changed
        if (_currentCenterZ != _previousCenterZ)
        {
            _previousCenterZ = _currentCenterZ;
            CreateMesh(_meshWidth, _meshHeight, _currentCenterZ);
            _zValuesMesh = CalculateZValues();
            _isMeshCreated = true;
            passUpdateZpositions = false;
        }

        // Update mesh only when transformations occur
        if (_magnificationZ != _prevMagnificationZ || _powerFactor != _prevPowerFactor || _linearity != _prevLinearity)
            passUpdateZpositions = false;

        if (passUpdateZpositions)
            return;

        _prevMagnificationZ = _magnificationZ;
        _prevPowerFactor = _powerFactor;
        _prevLinearity = _linearity;

        float magOffset = _magnificationZ * OFFSET;
        UpdateVertexZPositions(i =>
        {
            float zValue = _zValuesMesh[i] + OFFSET;
            if (_linearity == "Log")
            {
                zValue = Mathf.Log(1 + Mathf.Pow(zValue, _powerFactor));
            }
            return _magnificationZ * zValue - magOffset;
        });
    }

    /// <summary>
    /// Updates the Z coordinate of each vertex using a provided function.
    /// </summary>
    public void UpdateVertexZPositions(Func<int, float> zPositionFunc)
    {
        if (_fileLoader.Is360)
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                _vertices[i] = zPositionFunc(i) * _vertexPositions[i];
            }
        }
        else
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                Vector3 vertexDirection = _vertexPositions[i].normalized;
                _vertices[i] = _vertexPositions[i] + zPositionFunc(i) * vertexDirection;
            }
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    /// <summary>
    /// Handles object manipulation using controller input.
    /// </summary>
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
        if (triggerL > TRIGGER_THRESHOLD)
        {
            meshPos.x += diffX;
            meshPos.y += diffY;
            meshPos.z += diffZ;
        }

        Vector2 stickPositionL = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        meshPos.x += stickPositionL.x * STICK_MOVE_FACTOR;
        meshPos.z += stickPositionL.y * STICK_MOVE_FACTOR;

        Vector2 stickPositionR = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        float scaleDelta = stickPositionR.x * STICK_MOVE_FACTOR;
        _meshTransform.localScale += new Vector3(scaleDelta, scaleDelta, scaleDelta);
        meshPos.z += scaleDelta * _currentCenterZ;
        _meshTransform.position = meshPos;

        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            _meshTransform.position = _initialPosition + new Vector3(0, 0, _currentCenterZ + SETBACK);
            _meshTransform.localScale = Vector3.one;
            _magnificationZ = 1f;
            _powerFactor = 1f;
            _linearity = "Linear";
        }
    }

    /// <summary>
    /// Selects the linearity mode ("Linear" or "Log") based on button input.
    /// </summary>
    private string SelectLinearity()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B)) _linearity = "Linear";
        if (OVRInput.GetDown(OVRInput.RawButton.A)) _linearity = "Log";
        return _linearity;
    }
}
