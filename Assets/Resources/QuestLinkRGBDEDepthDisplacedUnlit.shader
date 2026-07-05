Shader "QuestLinkRGBDE/DepthDisplacedUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DepthTex ("Depth", 2D) = "black" {}
        _UseGpuDepthDisplacement ("Use GPU Depth Displacement", Float) = 0
        _DepthUVScale ("Depth UV Scale", Vector) = (1, 1, 0, 0)
        _BaseDepth ("Base Depth", Float) = 0
        _MagnificationZ ("Depth Magnification", Float) = 1
        _PowerFactor ("Power Factor", Float) = 1
        _LinearityMode ("Linearity Mode", Float) = 0
        _TanHalfVertical ("Tan Half Vertical", Float) = 0
        _TanHalfHorizontal ("Tan Half Horizontal", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _DepthTex;
            float _UseGpuDepthDisplacement;
            float4 _DepthUVScale;
            float _BaseDepth;
            float _MagnificationZ;
            float _PowerFactor;
            float _LinearityMode;
            float _TanHalfVertical;
            float _TanHalfHorizontal;

            float ApplyDepthCurve(float rawDepth)
            {
                float depth = max(rawDepth, _BaseDepth);
                if (_LinearityMode > 0.5)
                {
                    float relative = max(depth - _BaseDepth + 0.3, 0.001);
                    depth = _BaseDepth + log(1.0 + pow(relative, _PowerFactor));
                }

                float scaledDepth = _BaseDepth + _MagnificationZ * (depth - _BaseDepth);
                return max(scaledDepth, _BaseDepth + 0.001);
            }

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 displaced = v.vertex;

                if (_UseGpuDepthDisplacement > 0.5)
                {
                    float2 depthScale = max(_DepthUVScale.xy, float2(1e-6, 1e-6));
                    float2 depthUV = saturate(v.uv / depthScale);
                    float rawDepth = tex2Dlod(_DepthTex, float4(depthUV, 0, 0)).r;
                    float depth = ApplyDepthCurve(rawDepth);

                    float screenX = (depthUV.x - 0.5) * 2.0 * _TanHalfHorizontal;
                    float screenY = (depthUV.y - 0.5) * 2.0 * _TanHalfVertical;
                    float3 dir = normalize(float3(screenX, screenY, 1.0));
                    displaced = float4(dir * depth, 1.0);
                }

                o.vertex = UnityObjectToClipPos(displaced);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 color = tex2D(_MainTex, i.uv);
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
