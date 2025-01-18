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

    //texture position to paste, �e�N�X�`���̃y�[�X�g�ʒu(UV)�Ɋւ�����
    [SerializeField]
    private int pasteX;
    [SerializeField]
    private int pasteY;

    //estimated depth information
    private float[] zValuesMesh;
    private float zValueMax;
    private float zValueMin;

    //image depth information
    private float[,] zValues;

    // Set reference to Material from Inspector, Material�ւ̎Q�Ƃ��C���X�y�N�^�[����ݒ�
    [SerializeField]
    private Material materialL;

    // Set object width in m, �I�u�W�F�N�g�̉��̒����i���[�g���j��ݒ�
    private float objectSize = 1.0f;

    //Set initial position
    private Vector3 initPos;

    private UnityEngine.Mesh mesh;             // keep reference to mesh, ���b�V���ւ̎Q�Ƃ�ێ�
    private UnityEngine.Vector3[] vertices;    // keep vrtices, ���_�z���ێ�

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

    //Z�����̊g��W���Q�iZ�̏搔�j
    private float _powerFig = 1.0f;
    public float PowerFig
    {
        get { return _powerFig; }
    }
    private float powerFigOld;

    //Z�����̌v�Z���@
    private string _linearity = "Log";
    public string Linearity
    {
        get { return _linearity; }
    }
    private string linearOld;

    //Z �����̃I�t�Z�b�g�l
    private static float OFFSET = 0.3f;

    Transform meshTransform;

    /// <summary>
    /// �C�x���g�n���h���[�F�V�����摜���쐬���ꂽ�Ƃ��ɌĂяo�����
    /// </summary>
    /// <param name=leftPixels">�摜�̃s�N�Z���f�[�^</param>
    public void OnImageCreatedHandler(Color32[] leftPixels)
    {
        magOld = 0.0f;
        powerFigOld = 0.0f;
        linearOld = "Dummy";

        originalWidth = fileLoader.OriginalWidth;
        originalHeight = fileLoader.OriginalHeight;
        meshWidth = fileLoader.meshX;
        meshHeight = fileLoader.meshY;
        zValues = fileLoader.PixelZMatrix;
        zValueMax = fileLoader.PixelZMax;
        zValueMin = fileLoader.PixelZMin; //��ԋ߂��ꏊ��0�Ƃ���

        // ���b�V���̏c����ƂȂ�ׂ���v����e�N�X�`�����쐬���A�I���W�i���摜��\��t���鍶���̏ꏊ���v�Z
        if ((float)((float)originalHeight / (float)originalWidth) <= (float)((float)meshHeight / (float)meshWidth))
        {
            texWidth = originalWidth;
            texHeight = (int)((float)meshHeight * (float)originalWidth / (float)meshWidth);
            pasteX = 0;
            pasteY = (int)(((float)texHeight - (float)originalHeight) / 2f);
        }
        else
        {
            texWidth = (int)((float)meshWidth * (float)originalHeight / (float)meshHeight);
            texHeight = originalHeight;
            pasteX = (int)(((float)texWidth - (float)originalWidth) / 2f);
            pasteY = 0;
        }
        Texture2D newTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);

        // fill everything black, �S�̂�^�����ɓh��Ԃ�
        Color fillColor = new Color(0f, 0f, 0f, 0f);

        // Initialize all pixels with fill color, �S�s�N�Z����h��Ԃ��J���[�ŏ�����
        Color[] fillPixels = new Color[texWidth * texHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        // Paste cropped pixels into new texture, �N���b�v���ꂽ�s�N�Z����V�����e�N�X�`���ɓ\��t��
        newTexture.SetPixels32(pasteX, pasteY, originalWidth, originalHeight, leftPixels);
        newTexture.Apply();

        // Material�Ƀe�N�X�`�������蓖�Ă�
        Destroy(materialL.mainTexture);
        AssignTextureToMaterial(newTexture, materialL);

        //���S�ɓ�����
        Transform myTransform = this.transform;
        UnityEngine.Vector3 pos = myTransform.position;
        pos.x = objectSize * (-0.5f);
        pos.y = (float)meshHeight / (float)meshWidth * objectSize * (-0.5f);
        myTransform.position = pos;
        initPos = pos;

        //Mesh���쐬����
        CreateMesh(meshWidth, meshHeight);

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

    //�摜���璊�o���� Z �����̏������b�V���ɓK�p
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
            for (int i = 0; i < meshWidth + 1; i++)
            {
                if (i < meshWidth)
                {
                    matrixColumn = Mathf.Min((int)((i - pasteX * meshScale) / meshScale), texWidth);
                    matrixRow = Mathf.Min((int)((j - pasteY * meshScale) / meshScale), texHeight);
                    z[j * (meshWidth + 1) + i] = zValues[matrixRow, matrixColumn] - zValueMin; //��ԋ߂��Ƃ��� - offset�l������ offset ����B
                }
                //��ԉE�͍��ׂ̒l���R�s�[�i���b�V���̕������ς��ɉ摜������ꍇ�Ɍ���j
                else if (((int)(pasteX + originalWidth) * meshScale) >= meshWidth)
                {
                    z[j * meshWidth + i] = z[j * meshWidth + i - 1];
                }
            }
        }
        //�ŏ��͒����̒l���R�s�[�i���b�V���̍��������ς��ɉ摜������ꍇ�Ɍ���j
        if ((int)((pasteY + originalHeight) * meshScale) >= meshHeight)
        {
            for (int i = (int)(pasteX * meshScale) + (meshWidth + 1) * meshHeight; i < meshWidth + 1; i++)
            {
                z[i] = z[i - (meshWidth + 1)];
            }
        }
        return z;
    }

    //���b�V���쐬�p
    public void CreateMesh(int meshWidth, int meshHeight)
    {

        ////Start to create Mesh, Mesh �̍쐬�J�n
        mesh = new UnityEngine.Mesh();
        // Using UInt32 index, ���_����65535�𒴂���ꍇ��32�r�b�g�C���f�b�N�X���g�p
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int verticesPerRow = meshWidth + 1;
        int verticesPerColumn = meshHeight + 1;
        int numVertices = verticesPerRow * verticesPerColumn;
        int numSquares = meshWidth * meshHeight;
        int numTriangles = numSquares * 2;
        int numIndices = numTriangles * 3;
        float cellSize = objectSize / meshWidth;

        vertices = new UnityEngine.Vector3[numVertices];
        UnityEngine.Vector2[] uvs = new UnityEngine.Vector2[numVertices];
        int[] triangles = new int[numIndices];

        // Create vertices and UV in XY plane, ���_��UV�̐����iXY���ʏ�ɔz�u�j
        for (int y = 0; y < verticesPerColumn; y++)
        {
            for (int x = 0; x < verticesPerRow; x++)
            {
                int index = y * verticesPerRow + x;
                vertices[index] = new UnityEngine.Vector3(x * cellSize, y * cellSize, 0); // Z��0�ɐݒ�
                uvs[index] = new UnityEngine.Vector2((float)x / meshWidth, (float)y / meshHeight);
            }
        }

        // Create triangles, �O�p�`�̐���
        int triangleIndex = 0;
        for (int y = 0; y < meshHeight; y++)
        {
            for (int x = 0; x < meshWidth; x++)
            {
                int bottomLeft = y * verticesPerRow + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + verticesPerRow;
                int topRight = topLeft + 1;

                // First Triangle, 1�ڂ̎O�p�`
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;

                // Second Triangle, 2�ڂ̎O�p�`
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        // Apply data to mesh, ���b�V���Ƀf�[�^��K�p
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Recalculate normals, �@���̍Čv�Z
        mesh.RecalculateNormals();

        // Set mesh to MeshFilter, MeshFilter�Ƀ��b�V����ݒ�
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;

        //// Finish, Mesh �̍쐬�I��
        //Debug.Log("vertices.Length; " + vertices.Length);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _isMeshCreated = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isMeshCreated)
            return;

        meshTransform = gameObject.transform;

        if (fileLoader.IsReadyToControl)
        {
            ObjectManipulation();
        };

        //�I�u�W�F�N�g�̕ό`���Ȃ��Ƃ��́A�������o�C�p�X����
        if (_magnificationZ == magOld && _powerFig == powerFigOld && _linearity == linearOld)
            return;
        magOld = _magnificationZ;
        powerFigOld = _powerFig;
        linearOld = _linearity;

        // Update z coordinates, Z���W���X�V
        UpdateVertexZPositions(i =>
        {
            float zValue = zValuesMesh[i];

            zValue += OFFSET;

            if(_linearity == "Log")
            {
                zValue = Mathf.Log(1 + (float)Math.Pow((double)zValue, (double)_powerFig));
            }
            zValue = _magnificationZ * zValue;

            zValue -= _magnificationZ * OFFSET; //Log �̕��͖���
            
            return zValue;
        });
    }

    // Update Vertex Z Position, ���_��Z���W��ύX���郁�\�b�h
    public void UpdateVertexZPositions(System.Func<int, float> zPositionFunc)
    {
        // Update each vertices, �e���_��Z���W���X�V
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].z = zPositionFunc(i);
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
        float triggerR = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger);
        UnityEngine.Vector3 localPosR = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        float diffRZ = localPosR.z - controllerRPrevPosZ;
        float factor = (_magnificationZ + 0.01f) / MAG_MAX;
        controllerRPrevPosZ = localPosR.z;
        if (triggerR > 0.7f)
        {
            _magnificationZ = Mathf.Max(Mathf.Min(_magnificationZ + factor * diffRZ * 100.0f, MAG_MAX), MAG_MIN);
        }
        float diffPower = 0.01f;
        if (OVRInput.Get(OVRInput.RawButton.Y)) { _powerFig = Mathf.Min(_powerFig + diffPower, 5.0f); }
        if (OVRInput.Get(OVRInput.RawButton.X)) { _powerFig = Mathf.Max(_powerFig - diffPower, 0.01f); }

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
        UnityEngine.Vector3 meshPos = meshTransform.position;
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
        meshTransform.position = meshPos;

        //stickR�̍��E��Mesh�̊g��k��
        UnityEngine.Vector2 stickPositionR = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        float _mag = stickPositionR.x * 0.01f;
        UnityEngine.Vector3 objectMagnification = new UnityEngine.Vector3(_mag, _mag, _mag);
        meshTransform.localScale += objectMagnification;

        //�t�@�C�����[�h�̍ۂ� GameObject �� Z �ʒu�������� File Browser �ɔ��Ȃ��悤�ɂ���
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            meshTransform.position = initPos;
            meshPos = initPos;
            meshTransform.localScale = new UnityEngine.Vector3(1f, 1f, 1f);
            _magnificationZ = 1f;
            _powerFig = 1f;
            _linearity = "Log";
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
