Shader "Custom/TriangleTexArray"
{
    Properties
    {
        _MainTex("Tex Array", 2DArray) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            float4 rectangles[512];

            struct Triangle
            {
                float4 v0, v1, v2;
                float3 uv0, uv1, uv2;
                float3 n0, n1, n2;
                uint rectStartIndex;
                uint rectCount;
            };

            StructuredBuffer<Triangle> outputTriangleBuffer;

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float index : TEXCOORD1;
                float4 clip : TEXCOORD2;
                uint start : TEXCOORD3;
                uint count : TEXCOORD4;
            };

            v2f vert(uint id : SV_VertexID)
            {
                uint triangleIndex = id / 3;
                uint triangleVertex = id % 3;

                Triangle tri = outputTriangleBuffer[triangleIndex];

                float4 clipTriangle;
                float3 uvTriangle;

                if (triangleVertex == 0)
                {
                    clipTriangle = tri.v0;
                    uvTriangle = tri.uv0;
                }
                else if (triangleVertex == 1)
                {
                    clipTriangle = tri.v1;
                    uvTriangle = tri.uv1;
                }
                else
                {
                    clipTriangle = tri.v2;
                    uvTriangle = tri.uv2; 
                }

                v2f o;
                o.pos = clipTriangle;
                o.uv = uvTriangle.xy;
                o.index = uvTriangle.z;
                o.clip = clipTriangle;
                o.start = tri.rectStartIndex;
                o.count = tri.rectCount;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target 
            {
                float3 ndc = i.clip.xyz / i.clip.w;

                float2 screen;

                screen.x = (ndc.x * 0.5 + 0.5) * _ScreenParams.x;
                screen.y = (ndc.y * 0.5 + 0.5) * _ScreenParams.y;

                float epsilon = 0.5;

                bool insideAny = false;

                for (uint a = 0; a < i.count; a++)
                {
                    float4 rect = rectangles[i.start + a];

                    float xmin = rect.x - epsilon;
                    float ymin = rect.y - epsilon;
                    float xmax = rect.z + epsilon;
                    float ymax = rect.w + epsilon;

                    bool inside = screen.x >= xmin && screen.x <= xmax && screen.y >= ymin && screen.y <= ymax;

                    if (inside)
                    {
                        insideAny = true;
                        break;
                    }
                }

                if (!insideAny)
                {
                    discard;
                }

                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.uv, i.index));
            }
            ENDCG
        }
    }
}
