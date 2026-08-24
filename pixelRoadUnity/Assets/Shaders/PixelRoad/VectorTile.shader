Shader "PixelRoad/Vector Tile"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }

        Cull Off
        Lighting Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 tileUv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 tileUv : TEXCOORD0;
            };

            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.tileUv = input.tileUv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Road strokes are expanded beyond tile edges. Clip in tile space so
                // adjacent tiles meet without visible overdraw outside their bounds.
                clip(input.tileUv.x);
                clip(input.tileUv.y);
                clip(1.0 - input.tileUv.x);
                clip(1.0 - input.tileUv.y);
                return input.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
