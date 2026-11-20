Shader "Custom/VertexClip"
{
    Properties
    {
         _MainTex("Tex Array", 2DArray) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float index : TEXCOORD1;
                float3 norm : TEXCOORD2;
                float4 clipDistance : SV_ClipDistance0;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            float4 rectangle;

            v2f vert(appdata v)
            {
                v2f o;

                float4 clip = mul(UNITY_MATRIX_VP, float4(v.vertex.xyz, 1.0f));

                o.pos = clip;
                o.uv = v.uv.xy;
                o.index = v.uv.z;
                o.norm = v.normal;

                float xmin = rectangle.x;
                float ymin = rectangle.y;
                float xmax = rectangle.z;
                float ymax = rectangle.w;

                o.clipDistance.x = clip.x - (xmin * clip.w); // Left
                o.clipDistance.y = (xmax * clip.w) - clip.x; // Right
                o.clipDistance.z = clip.y - (ymin * clip.w); // Bottom
                o.clipDistance.w = (ymax * clip.w) - clip.y; // Top

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.uv, i.index));

                float3 N = normalize(i.norm);

                float3 L = normalize(_WorldSpaceLightPos0.xyz);

                float NdotL = dot(N, L);

                float diffuse = NdotL * 0.5f + 0.5f;

                diffuse = diffuse * diffuse;

                col.rgb *= diffuse;

                return col;
            }
            ENDHLSL
        }
    }
}
