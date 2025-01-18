using UnityEngine;
using System;
using Color = UnityEngine.Color;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GridMesh : MonoBehaviour
{
    [SerializeField]
    private FileLoader fileLoader;

    // original frame size and initial scale, 本来のフレームのサイズ
    [SerializeField]
    private int originalWidth;
    [SerializeField]
    private int originalHeight;

    //Max mesh size, メッシュのサイズ
    [SerializeField]
    private int meshWidth;
    [SerializeField]
    private int meshHeight;

    //メッシュの形状で調整した最終的なテクスチャのサイズ
    [SerializeField]
    private int texWidth;
    [SerializeField]
    private int texHeight;

    //texture position to paste, テクスチャのペースト位置(UV)に関する情報
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

    // Set reference to Material from Inspector, Materialへの参照をインスペクターから設定
    [SerializeField]
    private Material materialL;

    // Set object width in m, オブジェクトの横の長さ（メートル）を設定
    private float objectSize = 1.0f;

    //Set initial position
    private Vector3 initPos;

    private UnityEngine.Mesh mesh;             // keep reference to mesh, メッシュへの参照を保持
    private UnityEngine.Vector3[] vertices;    // keep vrtices, 頂点配列を保持

    //メッシュが作られた際に true となるプロパティ
    private bool _isMeshCreated;
    public bool IsMeshCreated
    {
        get { return _isMeshCreated; }
    }

    private float controllerPrevPosX = 0f;
    private float controllerPrevPosY = 0f;
    private float controllerPrevPosZ = 0f;
    private float controllerRPrevPosZ = 0f;

    //Z方向の拡大係数, Max, Min
    [SerializeField]
    private float _magnificationZ = 1.0f;
    public float MagnificationZ
    {
        get { return _magnificationZ; }
    }
    private float magOld;

    //拡大の最大・最小倍率
    private const float MAG_MAX = 25.0f;
    private const float MAG_MIN = 0.0f;

    //Z方向の拡大係数２（Zの乗数）
    private float _powerFig = 1.0f;
    public float PowerFig
    {
        get { return _powerFig; }
    }
    private float powerFigOld;

    //Z方向の計算方法
    private string _linearity = "Log";
    public string Linearity
    {
        get { return _linearity; }
    }
    private string linearOld;

    //Z 方向のオフセット値
    private static float OFFSET = 0.3f;

    Transform meshTransform;

    /// <summary>
    /// イベントハンドラー：新しい画像が作成されたときに呼び出される
    /// </summary>
    /// <param name=leftPixels">画像のピクセルデータ</param>
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
        zValueMin = fileLoader.PixelZMin; //一番近い場所を0とする

        // メッシュの縦横比となるべく一致するテクスチャを作成し、オリジナル画像を貼り付ける左下の場所を計算
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

        // fill everything black, 全体を真っ黒に塗りつぶし
        Color fillColor = new Color(0f, 0f, 0f, 0f);

        // Initialize all pixels with fill color, 全ピクセルを塗りつぶしカラーで初期化
        Color[] fillPixels = new Color[texWidth * texHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        // Paste cropped pixels into new texture, クロップされたピクセルを新しいテクスチャに貼り付け
        newTexture.SetPixels32(pasteX, pasteY, originalWidth, originalHeight, leftPixels);
        newTexture.Apply();

        // Materialにテクスチャを割り当てる
        Destroy(materialL.mainTexture);
        AssignTextureToMaterial(newTexture, materialL);

        //中心に動かす
        Transform myTransform = this.transform;
        UnityEngine.Vector3 pos = myTransform.position;
        pos.x = objectSize * (-0.5f);
        pos.y = (float)meshHeight / (float)meshWidth * objectSize * (-0.5f);
        myTransform.position = pos;
        initPos = pos;

        //Meshを作成する
        CreateMesh(meshWidth, meshHeight);

        //メッシュの各頂点のZ値を計算
        zValuesMesh = CalculateZValues();
        _isMeshCreated = true;

        fileLoader.IsReadyToLoad = true;
    }

    /// <summary>
    /// テクスチャを指定されたマテリアルに割り当てるメソッド
    /// </summary>
    /// <param name="texture">割り当てるテクスチャ</param>
    /// <param name="material">対象のマテリアル</param>
    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null)
        {
            //Debug.LogWarning("Materialが割り当てられていません。インスペクターで設定してください。");
            return;
        }

        if (texture == null)
        {
            //Debug.LogWarning("割り当てるTextureがnullです。");
            return;
        }

        // Set texture to material's main texture, マテリアルのメインテクスチャに設定
        material.mainTexture = texture;
    }

    //画像から抽出した Z 方向の情報をメッシュに適用
    private float[] CalculateZValues()
    {
        float[] z = new float[(meshWidth + 1) * (meshHeight + 1)];
        float meshScale;
        int matrixRow;
        int matrixColumn;
        // Calculate image size and mesh ratio, 画像サイズとメッシュの比率を計算
        if (meshWidth > meshHeight)
        {
            meshScale = (float)meshWidth / (float)texWidth;
        }
        else
        {
            meshScale = (float)meshHeight / (float)texHeight;
        }
       
        //メッシュデプスを一旦画像の最深値でフィル
        Array.Fill(z, zValueMax);

        //メッシュに画像のデプスを割り当て
        for (int j = 0; j < meshHeight; j++)
        {
            for (int i = 0; i < meshWidth + 1; i++)
            {
                if (i < meshWidth)
                {
                    matrixColumn = Mathf.Min((int)((i - pasteX * meshScale) / meshScale), texWidth);
                    matrixRow = Mathf.Min((int)((j - pasteY * meshScale) / meshScale), texHeight);
                    z[j * (meshWidth + 1) + i] = zValues[matrixRow, matrixColumn] - zValueMin; //一番近いところ - offset値分だけ offset する。
                }
                //一番右は左隣の値をコピー（メッシュの幅いっぱいに画像がある場合に限る）
                else if (((int)(pasteX + originalWidth) * meshScale) >= meshWidth)
                {
                    z[j * meshWidth + i] = z[j * meshWidth + i - 1];
                }
            }
        }
        //最上列は直下の値をコピー（メッシュの高さいっぱいに画像がある場合に限る）
        if ((int)((pasteY + originalHeight) * meshScale) >= meshHeight)
        {
            for (int i = (int)(pasteX * meshScale) + (meshWidth + 1) * meshHeight; i < meshWidth + 1; i++)
            {
                z[i] = z[i - (meshWidth + 1)];
            }
        }
        return z;
    }

    //メッシュ作成用
    public void CreateMesh(int meshWidth, int meshHeight)
    {

        ////Start to create Mesh, Mesh の作成開始
        mesh = new UnityEngine.Mesh();
        // Using UInt32 index, 頂点数が65535を超える場合は32ビットインデックスを使用
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

        // Create vertices and UV in XY plane, 頂点とUVの生成（XY平面上に配置）
        for (int y = 0; y < verticesPerColumn; y++)
        {
            for (int x = 0; x < verticesPerRow; x++)
            {
                int index = y * verticesPerRow + x;
                vertices[index] = new UnityEngine.Vector3(x * cellSize, y * cellSize, 0); // Zを0に設定
                uvs[index] = new UnityEngine.Vector2((float)x / meshWidth, (float)y / meshHeight);
            }
        }

        // Create triangles, 三角形の生成
        int triangleIndex = 0;
        for (int y = 0; y < meshHeight; y++)
        {
            for (int x = 0; x < meshWidth; x++)
            {
                int bottomLeft = y * verticesPerRow + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + verticesPerRow;
                int topRight = topLeft + 1;

                // First Triangle, 1つ目の三角形
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;

                // Second Triangle, 2つ目の三角形
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        // Apply data to mesh, メッシュにデータを適用
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Recalculate normals, 法線の再計算
        mesh.RecalculateNormals();

        // Set mesh to MeshFilter, MeshFilterにメッシュを設定
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;

        //// Finish, Mesh の作成終了
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

        //オブジェクトの変形がないときは、処理をバイパスする
        if (_magnificationZ == magOld && _powerFig == powerFigOld && _linearity == linearOld)
            return;
        magOld = _magnificationZ;
        powerFigOld = _powerFig;
        linearOld = _linearity;

        // Update z coordinates, Z座標を更新
        UpdateVertexZPositions(i =>
        {
            float zValue = zValuesMesh[i];

            zValue += OFFSET;

            if(_linearity == "Log")
            {
                zValue = Mathf.Log(1 + (float)Math.Pow((double)zValue, (double)_powerFig));
            }
            zValue = _magnificationZ * zValue;

            zValue -= _magnificationZ * OFFSET; //Log の分は無視
            
            return zValue;
        });
    }

    // Update Vertex Z Position, 頂点のZ座標を変更するメソッド
    public void UpdateVertexZPositions(System.Func<int, float> zPositionFunc)
    {
        // Update each vertices, 各頂点のZ座標を更新
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].z = zPositionFunc(i);
        }

        // Set vertices to mesh, メッシュに頂点配列を再設定
        mesh.vertices = vertices;

        // Recalculate normals and bounding volumes, 必要に応じて法線とバウンディングボリュームを再計算
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    //GameObject の操作まとめ
    void ObjectManipulation()
    {
        //triggerR が押し込まれている時にコントローラーをZ方向に動かすとmagnificationZが変化する。
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

        //A, B ボタンで Z 方向の計算方法を Log または Linear に切り替える
        _linearity = LinearitySelect();

        //triggerL が押し込まれている時にコントローラーを動かすと Mesh を移動する
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

        //stickL の操作で Mesh を X 方向及び Z 方向に移動
        UnityEngine.Vector2 stickPositionL = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        float transformX = stickPositionL.x * 0.01f;
        float transformY = stickPositionL.y * 0.01f;
        meshPos.x = meshPos.x + transformX;
        meshPos.z = meshPos.z + transformY;
        meshTransform.position = meshPos;

        //stickRの左右でMeshの拡大縮小
        UnityEngine.Vector2 stickPositionR = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        float _mag = stickPositionR.x * 0.01f;
        UnityEngine.Vector3 objectMagnification = new UnityEngine.Vector3(_mag, _mag, _mag);
        meshTransform.localScale += objectMagnification;

        //ファイルロードの際に GameObject の Z 位置を下げて File Browser に被らないようにする
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

    //Z 方向の計算方法の切り替え用メソッド
    private string LinearitySelect()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B)) { _linearity = "Linear"; }
        if (OVRInput.GetDown(OVRInput.RawButton.A)) { _linearity = "Log"; }

        return _linearity;
    }

}
