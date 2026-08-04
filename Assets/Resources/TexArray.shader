Shader "Custom/TexArray"
{
    Properties
    {
        _MainTex("Tex Array", 2DArray) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass   
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float index : TEXCOORD1;
                float4 clip : TEXCOORD2;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            float4 rectangles[32];
            int count;

            v2f vert(appdata v)
            {
                v2f o;
                float4 clipVertex = mul(UNITY_MATRIX_VP, float4(v.vertex.xyz, 1.0));
                o.pos = clipVertex;
                o.uv = v.uv.xy;
                o.index = v.uv.z;
                o.clip = clipVertex;
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

                for (uint a = 0; a < count; a++)
                {
                    float4 ndcRect = rectangles[a];

                    float xmin = ((ndcRect.x * 0.5 + 0.5) * _ScreenParams.x) - epsilon;
                    float ymin = ((ndcRect.y * 0.5 + 0.5) * _ScreenParams.y) - epsilon;
                    float xmax = ((ndcRect.z * 0.5 + 0.5) * _ScreenParams.x) + epsilon;
                    float ymax = ((ndcRect.w * 0.5 + 0.5) * _ScreenParams.y) + epsilon;

                    bool inside = screen.x >= xmin && screen.x <= xmax && screen.y >= ymin && screen.y <= ymax;

                    if (inside)
                    {
                        insideAny = true;
                        break;
                    }
                }

                if (!insideAny && count != 0)
                {
                    discard;
                }

                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.uv, i.index)); 
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
