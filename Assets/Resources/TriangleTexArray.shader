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

            struct Triangle
            {
                float4 v0, v1, v2;
                float3 uv0, uv1, uv2;
                float3 n0, n1, n2;
                float4 rect;
            };

            StructuredBuffer<Triangle> outputTriangleBuffer;

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float index : TEXCOORD1;
                float4 aabb : TEXCOORD2;
                float4 clip : TEXCOORD3; 
            };

            v2f vert(uint id : SV_VertexID)
            {
                uint triangleIndex = id / 3;
                uint triangleVertex = id % 3;

                Triangle tri = outputTriangleBuffer[triangleIndex];

                float4 vertexTriangle;
                float3 uvTriangle;

                if (triangleVertex == 0)
                {
                    vertexTriangle = tri.v0;
                    uvTriangle = tri.uv0;
                }
                else if (triangleVertex == 1)
                {
                    vertexTriangle = tri.v1;
                    uvTriangle = tri.uv1;
                }
                else
                {
                    vertexTriangle = tri.v2;
                    uvTriangle = tri.uv2;
                }

                v2f o;
                o.pos = vertexTriangle;
                o.uv = uvTriangle.xy;
                o.index = uvTriangle.z;
                o.aabb = tri.rect;
                o.clip = vertexTriangle;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target 
            {
                float x = i.clip.x;
                float y = i.clip.y;
                float z = i.clip.z;
                float w = i.clip.w;

                float clipMinX = i.aabb.x * w;
                float clipMaxX = i.aabb.y * w;
                float clipMinY = i.aabb.z * w;
                float clipMaxY = i.aabb.w * w;

                clip(x - (clipMinX - 0.1f)); // left
                clip((0.1f + clipMaxX) - x); // right
                clip(y - (clipMinY - 0.1f)); // bottom
                clip((0.1f + clipMaxY) - y); // top
                clip(z);                     // near
                clip(w - z);                 // far

                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.uv, i.index));
            }
            ENDCG
        }
    }
}
