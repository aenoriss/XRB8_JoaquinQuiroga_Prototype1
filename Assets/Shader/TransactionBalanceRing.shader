Shader "Custom/TransactionBalanceRing"
{
    Properties
    {
        _BuyRatio ("Buy Ratio", Range(0, 1)) = 0.5
        _RingWidth ("Ring Width", Range(0, 0.5)) = 0.1
        _GreenColor ("Green Color", Color) = (0,1,0,0.1)
        _RedColor ("Red Color", Color) = (1,0,0,0.1)
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_MULTIVIEW_ON STEREO_INSTANCING_ON
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _BuyRatio;
            float _RingWidth;
            float4 _GreenColor;
            float4 _RedColor;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Convert UV to polar coordinates
                float2 center = float2(0.5, 0.5);
                float2 delta = i.uv - center;
                float radius = length(delta);
                
                // Calculate angle (0 to 1)
                float angle = atan2(delta.y, delta.x) / (2.0 * 3.14159) + 0.5;
                
                // Define ring
                float inner = 0.5 - _RingWidth;
                float outer = 0.5;
                
                // Check if we're in the ring
                if (radius < outer && radius > inner)
                {
                    // Green fills from left middle (0.25) up and down
                    // Red fills from right middle (0.75) up and down
                    float greenDist = abs(angle - 0.25);
                    float redDist = abs(angle - 0.75);
                    
                    // Normalize distances to 0-0.5 range
                    greenDist = min(greenDist, 1.0 - greenDist) * 2.0;
                    redDist = min(redDist, 1.0 - redDist) * 2.0;
                    
                    // Compare against buy ratio
                    if (greenDist < _BuyRatio)
                    {
                        return _GreenColor;
                    }
                    else if (redDist < (1.0 - _BuyRatio))
                    {
                        return _RedColor;
                    }
                }
                
                // Outside the ring or unfilled portion
                return float4(0,0,0,0);
            }
            ENDCG
        }
    }
}