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

    //estimated depth information
    private float[] zValuesMesh;
    private float zValueMax;
    private float zValueMin;

    //image depth information
    private float[,] zValues;

    //VU coordinates matrix, VU座標
    float[,] uMatrix;
    float[,] vMatrix;

    // Set reference to Material from Inspector, Materialへの参照をインスペクターから設定
    [SerializeField]
    private Material materialL;

    // Set object width in m, オブジェクトの横の長さ（メートル）を設定
    private float objectSize = 1.0f;

    // Set object diameter in m
    private float diameter = 2.0f;

    //Set initial position
    [SerializeField]
    private Vector3 initPos;

    private const float CENTERZ_MAX = -0.25f;
    private const float CENTERZ_MIN = -4.0f;

    private float centerZ = CENTERZ_MIN; //部分曲面の中心のZ座標 must be 0 or below
    private float centerZOld;
    private float rad;　//部分曲面の半径

    private UnityEngine.Mesh mesh;             // keep reference to mesh, メッシュへの参照を保持
    private UnityEngine.Vector3[] vertices;    // keep vrtices, 頂点配列を保持
    private UnityEngine.Vector3[] vertexPositions; //球面用初期値

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
    private float controllerRPrevPosX = 0f;

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
    private const float POW_MAX = 25.0f;
    private const float POW_MIN = 0.01f;      

    //Z方向の拡大係数２（Zの乗数）
    private float _powerFig = 1.0f;
    public float PowerFig
    {
        get { return _powerFig; }
    }
    private float powerFigOld;

    //Z方向の計算方法
    private string _linearity = "Linear";
    public string Linearity
    {
        get { return _linearity; }
    }
    private string linearOld;

    //Z 方向のオフセット値
    private static float OFFSET = 0.3f;
    private float[] positionZ;

    Transform meshTransform;

    //平面画像の配置セットバック量
    private const float SETBACK = 1.0f;

    /// <summary>
    /// イベントハンドラー：新しい画像が作成されたときに呼び出される
    /// </summary>
    /// <param name=leftPixels">画像のピクセルデータ</param>
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
        zValueMin = fileLoader.PixelZMin; //一番近い場所を0とする
        positionZ = new float[(meshWidth + 1) * (meshHeight + 1)];

        // メッシュの縦横比となるべく一致するテクスチャを作成し、オリジナル画像を貼り付ける左下の場所を計算
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

        // fill everything black, 全体を真っ黒に塗りつぶし
        Color fillColor = new Color(0f, 0f, 0f, 0f);

        // Initialize all pixels with fill color, 全ピクセルを塗りつぶしカラーで初期化
        Color[] fillPixels = new Color[texWidth * texHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        // Paste cropped pixels into new texture, クロップされたピクセルを新しいテクスチャに貼り付け
        newTexture.SetPixels32(0, 0, originalWidth, originalHeight, leftPixels);
        newTexture.Apply();

        // Materialにテクスチャを割り当てる
        Destroy(materialL.mainTexture);
        AssignTextureToMaterial(newTexture, materialL);

        //中心に動かす
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
            //Meshを作成する(360) Linearity = Linear
            _linearity = "Linear";
            GenerateInvertedSphere(meshWidth, meshHeight);
        }
        else
        {
            //Meshを作成する Linearity = Linear
            _linearity = "Linear";
            CreateMesh(meshWidth, meshHeight, centerZ);

            meshTransform = transform;
            Vector3 tfPos = meshTransform.position;
            meshTransform.position = tfPos + new Vector3(0, 0, centerZ + SETBACK);
        }

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

    //画像から抽出した Z 方向の情報をメッシュに適用。メッシュを作成してから呼び出すこと！
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
            for (int i = 0; i < meshWidth; i++)
            {
                if (fileLoader.is360)
                {
                    matrixColumn = Mathf.Min((int)(i / meshScale), texWidth);
                    matrixRow = Mathf.Min((int)(j / meshScale), texHeight);
                    z[j * (meshWidth + 1) + i] = zValues[matrixRow, matrixColumn] - zValueMin + OFFSET; //一番近いところ - offset値分だけ offset する。
                }
                else
                {
                    matrixColumn = Mathf.Min((int)(uMatrix[j, i] * texWidth), texWidth);
                    matrixRow = Mathf.Min((int)(vMatrix[j, i] * texHeight), texHeight);
                    z[j * (meshWidth + 1) + i] = zValues[matrixRow, matrixColumn] - zValueMin + OFFSET; //一番近いところ - offset値分だけ offset する。
                }
            }

            // 一番右は左隣の値をコピー
            z[j * (meshWidth + 1) + meshWidth] = z[j * (meshWidth + 1) + meshWidth - 1];

            // 一番右は一番左の値をコピー（輪がつながるように）
            if (fileLoader.is360) { z[j * (meshWidth + 1) + meshWidth] = z[j * (meshWidth + 1)]; };
        }

        //最上列は直下の値をコピー
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

    //曲面に平面画像を貼り付ける
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
            deltaTheta = Mathf.Atan(objectHalfWidth / objectDistance) * 2f; //一番下から上までの角度
            deltaPhi = Mathf.Atan(objectHalfHeight / objectDistance) * 2f; //一番左から右までの角度

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

        // vertexPositions 配列を確保
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
            // (lat=0 で上を0にしたい場合などは
            //   float phi = Mathf.PI * latNormalized; 
            // とすれば上下が逆になります)

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float lonNormalized = (float)lon / longitudeSegments;
                float theta = deltaTheta * (1f - lonNormalized) + startTheta;
                //内側からみるので、逆回りにする

                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);

                // 頂点座標
                Vector3 pos = new Vector3(x, y, z) * rad;
                vertices[index] = pos;
                vertexPositions[index] = pos;

                // 内側向き法線
                normals[index] = -new Vector3(x, y, z);

                float u = Mathf.Cos(theta) / startThetaCos / 2 + 0.5f;
                float v = Mathf.Cos(phi) / startPhiCos / 2 + 0.5f;
                uvs[index] = new Vector2(u, v);
                uMatrix[lat, lon] = u;
                vMatrix[lat, lon] = v;
                index++;
            }
        }

        // 三角形インデックスを生成
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

        // Set mesh to MeshFilter, MeshFilterにメッシュを設定
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
    }

    //Equirectangular 360 sphere image（Ricoh Theta で確認）用
    private void GenerateInvertedSphere(int longitudeSegments, int latitudeSegments)
    {
        mesh = new UnityEngine.Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.name = "InvertedSphere";

        int vertCount = (latitudeSegments + 1) * (longitudeSegments + 1);

        // vertexPositions 配列を確保
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
            // (lat=0 で上を0にしたい場合などは
            //   float phi = Mathf.PI * latNormalized; 
            // とすれば上下が逆になります)

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float lonNormalized = (float)lon / longitudeSegments;
                float theta = 2f * Mathf.PI * (1f - lonNormalized);
                //内側からみるので、逆回りにする

                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);

                // 頂点座標
                Vector3 pos = new Vector3(x, y, z) * radius;
                vertices[index] = pos;
                vertexPositions[index] = pos;

                // 内側向き法線
                normals[index] = -new Vector3(x, y, z);

                float u = lonNormalized;
                float v = latNormalized;
                uvs[index] = new Vector2(u, v);

                index++;
            }
        }

        // 三角形インデックスを生成
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

        // Set mesh to MeshFilter, MeshFilterにメッシュを設定
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

        //コントローラーRのハンドトリガーが左右にドラッグされた時、曲面を変更するためにメッシュを再作成する
        if (centerZ != centerZOld)
        {
            centerZOld = centerZ;
            CreateMesh(meshWidth, meshHeight, centerZ);
            zValuesMesh = CalculateZValues();
            _isMeshCreated = true;
            passUpdateZpositions = false;
        }

        //オブジェクトの変形のチェック
        if (_magnificationZ != magOld || _powerFig != powerFigOld || _linearity != linearOld)
        {
            passUpdateZpositions = false;
        }

        //オブジェクトの変形がないときは、処理をバイパスする
        if (passUpdateZpositions)
            return;
        magOld = _magnificationZ;
        powerFigOld = _powerFig;
        linearOld = _linearity;

        // Update z coordinates, Z座標を更新
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

            zValue -= magOffset; //Log の分は無視

            return zValue;
        });
    }

    // Update Vertex Z Position, 頂点のZ座標を変更するメソッド
    public void UpdateVertexZPositions(System.Func<int, float> zPositionFunc)
    {
        if (fileLoader.is360)
        {
            // Update each vertices, 各頂点のZ座標を更新
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = zPositionFunc(i) * vertexPositions[i];
            }
        }
        else
        {
            // Update each vertices, 各頂点のZ座標を更新
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertexDirection = vertexPositions[i].normalized;
                vertices[i] = vertexPositions[i] + zPositionFunc(i) * vertexDirection;
            }
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
        //triggerR2 が押し込まれている時にコントローラーを左右に動かすとcenterZが変化する。
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

        //Y, X ボタンで Z 方向の拡大係数を変更
        float diffPower = 0.01f;
        if (OVRInput.Get(OVRInput.RawButton.Y)) { _powerFig = Mathf.Min(_powerFig + diffPower, POW_MAX); }
        if (OVRInput.Get(OVRInput.RawButton.X)) { _powerFig = Mathf.Max(_powerFig - diffPower, POW_MIN); }

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
        //UnityEngine.Vector3 meshPos = meshTransform.position;
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

        //stickRの左右でMeshの拡大縮小
        UnityEngine.Vector2 stickPositionR = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
        float _mag = stickPositionR.x * 0.01f;
        UnityEngine.Vector3 objectMagnification = new UnityEngine.Vector3(_mag, _mag, _mag);
        meshTransform.localScale += objectMagnification;
        meshPos.z += _mag * centerZ; //まあいいや

        meshTransform.position = meshPos;

        //ファイルロードの際に GameObject の Z 位置を下げて File Browser に被らないようにする
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

    //Z 方向の計算方法の切り替え用メソッド
    private string LinearitySelect()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B)) { _linearity = "Linear"; }
        if (OVRInput.GetDown(OVRInput.RawButton.A)) { _linearity = "Log"; }

        return _linearity;
    }
}
