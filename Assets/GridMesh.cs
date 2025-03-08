using UnityEngine;
using System;
using Color = UnityEngine.Color;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GridMesh : MonoBehaviour
{
    [SerializeField]
    private FileLoader fileLoader;

    // original frame size and initial scale, �{���̃t���[���̃T�C�Y
    [SerializeField]
    private int originalWidth;
    [SerializeField]
    private int originalHeight;

    //Max mesh size, ���b�V���̃T�C�Y
    [SerializeField]
    private int meshWidth;
    [SerializeField]
    private int meshHeight;

    //���b�V���̌`��Œ��������ŏI�I�ȃe�N�X�`���̃T�C�Y
    [SerializeField]
    private int texWidth;
    [SerializeField]
    private int texHeight;

    //estimated depth information
    private float[] zValuesMesh;
    private float zValueMax;
    private float zValueMin;

    //image depth information
    private float[,] zValues;

    //VU coordinates matrix, VU���W
    float[,] uMatrix;
    float[,] vMatrix;

    // Set reference to Material from Inspector, Material�ւ̎Q�Ƃ��C���X�y�N�^�[����ݒ�
    [SerializeField]
    private Material materialL;

    // Set object width in m, �I�u�W�F�N�g�̉��̒����i���[�g���j��ݒ�
    private float objectSize = 1.0f;

    // Set object diameter in m
    private float diameter = 2.0f;

    //Set initial position
    [SerializeField]
    private Vector3 initPos;

    private const float CENTERZ_MAX = -0.25f;
    private const float CENTERZ_MIN = -4.0f;

    private float centerZ = CENTERZ_MIN; //�����Ȗʂ̒��S��Z���W must be 0 or below
    private float centerZOld;
    private float rad;�@//�����Ȗʂ̔��a

    private UnityEngine.Mesh mesh;             // keep reference to mesh, ���b�V���ւ̎Q�Ƃ�ێ�
    private UnityEngine.Vector3[] vertices;    // keep vrtices, ���_�z���ێ�
    private UnityEngine.Vector3[] vertexPositions; //���ʗp�����l

    //���b�V�������ꂽ�ۂ� true �ƂȂ�v���p�e�B
    private bool _isMeshCreated;
    public bool IsMeshCreated
    {
        get { return _isMeshCreated; }
    }

    private float controllerPrevPosX = 0f;
    private float controllerPrevPosY = 0f;
    private float controllerPrevPosZ = 0f;
    private float controllerRPrevPosZ = 0f;
    private float controllerRPrevPosX = 0f;

    //Z�����̊g��W��, Max, Min
    [SerializeField]
    private float _magnificationZ = 1.0f;
    public float MagnificationZ
    {
        get { return _magnificationZ; }
    }
    private float magOld;

    //�g��̍ő�E�ŏ��{��
    private const float MAG_MAX = 25.0f;
    private const float MAG_MIN = 0.0f;
    private const float POW_MAX = 25.0f;
    private const float POW_MIN = 0.01f;      

    //Z�����̊g��W���Q�iZ�̏搔�j
    private float _powerFig = 1.0f;
    public float PowerFig
    {
        get { return _powerFig; }
    }
    private float powerFigOld;

    //Z�����̌v�Z���@
    private string _linearity = "Linear";
    public string Linearity
    {
        get { return _linearity; }
    }
    private string linearOld;

    //Z �����̃I�t�Z�b�g�l
    private static float OFFSET = 0.3f;
    private float[] positionZ;

    Transform meshTransform;

    //���ʉ摜�̔z�u�Z�b�g�o�b�N��
    private const float SETBACK = 1.0f;

    /// <summary>
    /// �C�x���g�n���h���[�F�V�����摜���쐬���ꂽ�Ƃ��ɌĂяo�����
    /// </summary>
    /// <param name=leftPixels">�摜�̃s�N�Z���f�[�^</param>
    public void OnImageCreatedHandler(Color32[] leftPixels)
    {
        magOld = 0.0f;
        powerFigOld = 0.0f;
        linearOld = "Dummy";
        centerZOld = centerZ;

        originalWidth = fileLoader.OriginalWidth;
        originalHeight = fileLoader.OriginalHeight;
        meshWidth = fileLoader.meshX;
        meshHeight = fileLoader.meshY;
        zValues = fileLoader.PixelZMatrix;
        zValueMax = fileLoader.PixelZMax;
        zValueMin = fileLoader.PixelZMin; //��ԋ߂��ꏊ��0�Ƃ���
        positionZ = new float[(meshWidth + 1) * (meshHeight + 1)];

        // ���b�V���̏c����ƂȂ�ׂ���v����e�N�X�`�����쐬���A�I���W�i���摜��\��t���鍶���̏ꏊ���v�Z
        if ((float)((float)originalHeight / (float)originalWidth) <= (float)((float)meshHeight / (float)meshWidth))
        {
            texWidth = originalWidth;
            texHeight = (int)((float)meshHeight * (float)originalWidth / (float)meshWidth);
        }
        else
        {
            texWidth = (int)((float)meshWidth * (float)originalHeight / (float)meshHeight);
            texHeight = originalHeight;
        }
        Texture2D newTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);

        // fill everything black, �S�̂�^�����ɓh��Ԃ�
        Color fillColor = new Color(0f, 0f, 0f, 0f);

        // Initialize all pixels with fill color, �S�s�N�Z����h��Ԃ��J���[�ŏ�����
        Color[] fillPixels = new Color[texWidth * texHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        // Paste cropped pixels into new texture, �N���b�v���ꂽ�s�N�Z����V�����e�N�X�`���ɓ\��t��
        newTexture.SetPixels32(0, 0, originalWidth, originalHeight, leftPixels);
        newTexture.Apply();

        // Material�Ƀe�N�X�`�������蓖�Ă�
        Destroy(materialL.mainTexture);
        AssignTextureToMaterial(newTexture, materialL);

        //���S�ɓ�����
        meshTransform = transform;
        UnityEngine.Vector3 pos;
        if(fileLoader.is360)
        {
            pos = new Vector3(0, 0, 0);
        }
        else
        {
            pos = new Vector3(0, 0, 0);
        }
        meshTransform.position = pos;
        initPos = pos;

        if (fileLoader.is360)
        {
            //Mesh���쐬����(360) Linearity = Linear
            _linearity = "Linear";
            GenerateInvertedSphere(meshWidth, meshHeight);
        }
        else
        {
            //Mesh���쐬���� Linearity = Linear
            _linearity = "Linear";
            CreateMesh(meshWidth, meshHeight, centerZ);

            meshTransform = transform;
            Vector3 tfPos = meshTransform.position;
            meshTransform.position = tfPos + new Vector3(0, 0, centerZ + SETBACK);
        }

        //���b�V���̊e���_��Z�l���v�Z
        zValuesMesh = CalculateZValues();
        _isMeshCreated = true;

        fileLoader.IsReadyToLoad = true;
    }

    /// <summary>
    /// �e�N�X�`�����w�肳�ꂽ�}�e���A���Ɋ��蓖�Ă郁�\�b�h
    /// </summary>
    /// <param name="texture">���蓖�Ă�e�N�X�`��</param>
    /// <param name="material">�Ώۂ̃}�e���A��</param>
    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null)
        {
            //Debug.LogWarning("Material�����蓖�Ă��Ă��܂���B�C���X�y�N�^�[�Őݒ肵�Ă��������B");
            return;
        }

        if (texture == null)
        {
            //Debug.LogWarning("���蓖�Ă�Texture��null�ł��B");
            return;
        }

        // Set texture to material's main texture, �}�e���A���̃��C���e�N�X�`���ɐݒ�
        material.mainTexture = texture;
    }

    //�摜���璊�o���� Z �����̏������b�V���ɓK�p�B���b�V�����쐬���Ă���Ăяo�����ƁI
    private float[] CalculateZValues()
    {
        float[] z = new float[(meshWidth + 1) * (meshHeight + 1)];
        float meshScale;
        int matrixRow;
        int matrixColumn;
        // Calculate image size and mesh ratio, �摜�T�C�Y�ƃ��b�V���̔䗦���v�Z
        if (meshWidth > meshHeight)
        {
            meshScale = (float)meshWidth / (float)texWidth;
        }
        else
        {
            meshScale = (float)meshHeight / (float)texHeight;
        }
       
        //���b�V���f�v�X����U�摜�̍Ő[�l�Ńt�B��
        Array.Fill(z, zValueMax);

        //���b�V���ɉ摜�̃f�v�X�����蓖��
        for (int j = 0; j < meshHeight; j++)
        {
            for (int i = 0; i < meshWidth; i++)
            {
                if (fileLoader.is360)
                {
                    matrixColumn = Mathf.Min((int)(i / meshScale), texWidth);
                    matrixRow = Mathf.Min((int)(j / meshScale), texHeight);
                    z[j * (meshWidth + 1) + i] = zValues[matrixRow, matrixColumn] - zValueMin + OFFSET; //��ԋ߂��Ƃ��� - offset�l������ offset ����B
                }
                else
                {
                    matrixColumn = Mathf.Min((int)(uMatrix[j, i] * texWidth), texWidth);
                    matrixRow = Mathf.Min((int)(vMatrix[j, i] * texHeight), texHeight);
                    z[j * (meshWidth + 1) + i] = zValues[matrixRow, matrixColumn] - zValueMin + OFFSET; //��ԋ߂��Ƃ��� - offset�l������ offset ����B
                }
            }

            // ��ԉE�͍��ׂ̒l���R�s�[
            z[j * (meshWidth + 1) + meshWidth] = z[j * (meshWidth + 1) + meshWidth - 1];

            // ��ԉE�͈�ԍ��̒l���R�s�[�i�ւ��Ȃ���悤�Ɂj
            if (fileLoader.is360) { z[j * (meshWidth + 1) + meshWidth] = z[j * (meshWidth + 1)]; };
        }

        //�ŏ��͒����̒l���R�s�[
        for (int i =(meshWidth + 1) * meshHeight; i < (meshWidth + 1) * (meshHeight + 1); i++)
        {
            z[i] = z[i - (meshWidth + 1)];
        }

        //pure sphere
        //if (fileLoader.is360)
        //{
        //    for (int i = 0; i < (meshWidth + 1) * (meshHeight + 1); i++)
        //    {
        //        z[i] = 2f;
        //    }
        //}

        return z;
    }

    //�Ȗʂɕ��ʉ摜��\��t����
    private void CreateMesh(int longitudeSegments, int latitudeSegments, float centerOffset)
    {
        _isMeshCreated = false;
        float objectHalfWidth = objectSize / 2f;
        float objectHalfHeight = objectHalfWidth * (float)latitudeSegments / (float)longitudeSegments;
        float objectDistance = initPos.z - centerOffset;
        float deltaTheta;
        float deltaPhi;
        if (objectDistance <= 0f)
        {
            deltaTheta = Mathf.PI;
            deltaPhi = Mathf.PI;
        }
        else
        {
            deltaTheta = Mathf.Atan(objectHalfWidth / objectDistance) * 2f; //��ԉ������܂ł̊p�x
            deltaPhi = Mathf.Atan(objectHalfHeight / objectDistance) * 2f; //��ԍ�����E�܂ł̊p�x

        }
        rad = Mathf.Sqrt((objectDistance * objectDistance) + (objectHalfWidth * objectHalfWidth) + (objectHalfHeight * objectHalfHeight));
        float startTheta = (Mathf.PI - deltaTheta) / 2f;
        float startPhi = (Mathf.PI - deltaPhi) / 2f;
        float startThetaCos = Mathf.Cos(startTheta);
        float startPhiCos = Mathf.Cos(startPhi);

        mesh = new UnityEngine.Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.name = "InvertedPartialSphere";

        int vertCount = (latitudeSegments + 1) * (longitudeSegments + 1);

        // vertexPositions �z����m��
        vertexPositions = new Vector3[vertCount];
        vertices = new Vector3[vertCount];

        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        int index = 0;
        uMatrix = new float[latitudeSegments + 1, longitudeSegments + 1];
        vMatrix = new float[latitudeSegments + 1, longitudeSegments + 1];

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float latNormalized = (float)lat / latitudeSegments;
            float phi = deltaPhi * (1f - latNormalized) + startPhi;
            // (lat=0 �ŏ��0�ɂ������ꍇ�Ȃǂ�
            //   float phi = Mathf.PI * latNormalized; 
            // �Ƃ���Ώ㉺���t�ɂȂ�܂�)

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float lonNormalized = (float)lon / longitudeSegments;
                float theta = deltaTheta * (1f - lonNormalized) + startTheta;
                //��������݂�̂ŁA�t���ɂ���

                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);

                // ���_���W
                Vector3 pos = new Vector3(x, y, z) * rad;
                vertices[index] = pos;
                vertexPositions[index] = pos;

                // ���������@��
                normals[index] = -new Vector3(x, y, z);

                float u = Mathf.Cos(theta) / startThetaCos / 2 + 0.5f;
                float v = Mathf.Cos(phi) / startPhiCos / 2 + 0.5f;
                uvs[index] = new Vector2(u, v);
                uMatrix[lat, lon] = u;
                vMatrix[lat, lon] = v;
                index++;
            }
        }

        // �O�p�`�C���f�b�N�X�𐶐�
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

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        // Set mesh to MeshFilter, MeshFilter�Ƀ��b�V����ݒ�
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
    }

    //Equirectangular 360 sphere image�iRicoh Theta �Ŋm�F�j�p
    private void GenerateInvertedSphere(int longitudeSegments, int latitudeSegments)
    {
        mesh = new UnityEngine.Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.name = "InvertedSphere";

        int vertCount = (latitudeSegments + 1) * (longitudeSegments + 1);

        // vertexPositions �z����m��
        vertexPositions = new Vector3[vertCount];
        vertices = new Vector3[vertCount];

        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        float radius = diameter / 2f;
        int index = 0;

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float latNormalized = (float)lat / latitudeSegments;
            float phi = Mathf.PI * (1f - latNormalized);
            // (lat=0 �ŏ��0�ɂ������ꍇ�Ȃǂ�
            //   float phi = Mathf.PI * latNormalized; 
            // �Ƃ���Ώ㉺���t�ɂȂ�܂�)

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float lonNormalized = (float)lon / longitudeSegments;
                float theta = 2f * Mathf.PI * (1f - lonNormalized);
                //��������݂�̂ŁA�t���ɂ���

                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);

                // ���_���W
                Vector3 pos = new Vector3(x, y, z) * radius;
                vertices[index] = pos;
                vertexPositions[index] = pos;

                // ���������@��
                normals[index] = -new Vector3(x, y, z);

                float u = lonNormalized;
                float v = latNormalized;
                uvs[index] = new Vector2(u, v);

                index++;
            }
        }

        // �O�p�`�C���f�b�N�X�𐶐�
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

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        // Set mesh to MeshFilter, MeshFilter�Ƀ��b�V����ݒ�
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _isMeshCreated = false;
    }

    // Update is called once per frame
    void Update()
    {
        bool passUpdateZpositions = true;

        if (!_isMeshCreated)
            return;

        meshTransform = transform;

        if (fileLoader.IsReadyToControl)
        {
            ObjectManipulation();
        };

        //�R���g���[���[R�̃n���h�g���K�[�����E�Ƀh���b�O���ꂽ���A�Ȗʂ�ύX���邽�߂Ƀ��b�V�����č쐬����
        if (centerZ != centerZOld)
        {
            centerZOld = centerZ;
            CreateMesh(meshWidth, meshHeight, centerZ);
            zValuesMesh = CalculateZValues();
            _isMeshCreated = true;
            passUpdateZpositions = false;
        }

        //�I�u�W�F�N�g�̕ό`�̃`�F�b�N
        if (_magnificationZ != magOld || _powerFig != powerFigOld || _linearity != linearOld)
        {
            passUpdateZpositions = false;
        }

        //�I�u�W�F�N�g�̕ό`���Ȃ��Ƃ��́A�������o�C�p�X����
        if (passUpdateZpositions)
            return;
        magOld = _magnificationZ;
        powerFigOld = _powerFig;
        linearOld = _linearity;

        // Update z coordinates, Z���W���X�V
        float magOffset = _magnificationZ * OFFSET;
        UpdateVertexZPositions(i =>
        {
            float zValue = zValuesMesh[i];
            
            zValue += OFFSET;

            if (_linearity == "Log")
            {
                zValue = Mathf.Log(1 + (float)Math.Pow((double)zValue, (double)_powerFig));
            }
            zValue = _magnificationZ * zValue;

            zValue -= magOffset; //Log �̕��͖���

            return zValue;
        });
    }

    // Update Vertex Z Position, ���_��Z���W��ύX���郁�\�b�h
    public void UpdateVertexZPositions(System.Func<int, float> zPositionFunc)
    {
        if (fileLoader.is360)
        {
            // Update each vertices, �e���_��Z���W���X�V
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = zPositionFunc(i) * vertexPositions[i];
            }
        }
        else
        {
            // Update each vertices, �e���_��Z���W���X�V
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertexDirection = vertexPositions[i].normalized;
                vertices[i] = vertexPositions[i] + zPositionFunc(i) * vertexDirection;
            }
        }

        // Set vertices to mesh, ���b�V���ɒ��_�z����Đݒ�
        mesh.vertices = vertices;

        // Recalculate normals and bounding volumes, �K�v�ɉ����Ė@���ƃo�E���f�B���O�{�����[�����Čv�Z
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    //GameObject �̑���܂Ƃ�
    void ObjectManipulation()
    {
        //triggerR ���������܂�Ă��鎞�ɃR���g���[���[��Z�����ɓ�������magnificationZ���ω�����B
        //triggerR2 ���������܂�Ă��鎞�ɃR���g���[���[�����E�ɓ�������centerZ���ω�����B
        float triggerR = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger);
        float triggerR2 = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger);
        UnityEngine.Vector3 localPosR = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        float diffRZ = localPosR.z - controllerRPrevPosZ;
        float diffRX = localPosR.x - controllerRPrevPosX;
        float factor = (_magnificationZ + 0.01f) / MAG_MAX;
        controllerRPrevPosZ = localPosR.z;
        controllerRPrevPosX = localPosR.x;
        Vector3 meshPos = meshTransform.position;
        if (triggerR > 0.7f)
        {
            _magnificationZ = Mathf.Max(Mathf.Min(_magnificationZ + factor * diffRZ * 100.0f, MAG_MAX), MAG_MIN);
        }
        if (triggerR2 > 0.7f)
        {
            centerZ = Mathf.Max(Mathf.Min(centerZ + diffRX * 1.0f, CENTERZ_MAX), CENTERZ_MIN);
        }

        //Y, X �{�^���� Z �����̊g��W����ύX
        float diffPower = 0.01f;
        if (OVRInput.Get(OVRInput.RawButton.Y)) { _powerFig = Mathf.Min(_powerFig + diffPower, POW_MAX); }
        if (OVRInput.Get(OVRInput.RawButton.X)) { _powerFig = Mathf.Max(_powerFig - diffPower, POW_MIN); }

        //A, B �{�^���� Z �����̌v�Z���@�� Log �܂��� Linear �ɐ؂�ւ���
        _linearity = LinearitySelect();

        //triggerL ���������܂�Ă��鎞�ɃR���g���[���[�𓮂����� Mesh ���ړ�����
        float triggerL = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
        UnityEngine.Vector3 localPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
        float diffX = localPos.x - controllerPrevPosX;
        float diffY = localPos.y - controllerPrevPosY;
        float diffZ = localPos.z - controllerPrevPosZ;
        controllerPrevPosX = localPos.x;
        controllerPrevPosY = localPos.y;
        controllerPrevPosZ = localPos.z;
        //UnityEngine.Vector3 meshPos = meshTransform.position;
        if (triggerL > 0.7f)
        {
            meshPos.x += diffX * 1.0f;
            meshPos.y += diffY * 1.0f;
            meshPos.z += diffZ * 1.0f;
        }

        //stickL �̑���� Mesh �� X �����y�� Z �����Ɉړ�
        UnityEngine.Vector2 stickPositionL = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        float transformX = stickPositionL.x * 0.01f;
        float transformY = stickPositionL.y * 0.01f;
        meshPos.x = meshPos.x + transformX;
        meshPos.z = meshPos.z + transformY;

        //stickR�̍��E��Mesh�̊g��k��
        UnityEngine.Vector2 stickPositionR = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        float _mag = stickPositionR.x * 0.01f;
        UnityEngine.Vector3 objectMagnification = new UnityEngine.Vector3(_mag, _mag, _mag);
        meshTransform.localScale += objectMagnification;
        meshPos.z += _mag * centerZ; //�܂�������

        meshTransform.position = meshPos;

        //�t�@�C�����[�h�̍ۂ� GameObject �� Z �ʒu�������� File Browser �ɔ��Ȃ��悤�ɂ���
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            meshTransform.position = initPos + new Vector3(0, 0, centerZ + SETBACK);
            meshPos = initPos + new Vector3(0, 0, centerZ + SETBACK);
            meshTransform.localScale = new UnityEngine.Vector3(1f, 1f, 1f);
            _magnificationZ = 1f;
            _powerFig = 1f;
            _linearity = "Linear";
        }

    }

    //Z �����̌v�Z���@�̐؂�ւ��p���\�b�h
    private string LinearitySelect()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B)) { _linearity = "Linear"; }
        if (OVRInput.GetDown(OVRInput.RawButton.A)) { _linearity = "Log"; }

        return _linearity;
    }
}
