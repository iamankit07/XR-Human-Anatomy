Shader "Anatomy/Ghost"
{
    // Part ki asli jagah ka X-ray outline: kinaare (rim) chamakte hain, beech halka.
    // ZTest Always = muscles/body ke peeche bhi dikhta hai.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.25)
        _RimPower ("Rim Power", Range(0.5, 6)) = 2.0
        _Fill ("Fill", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 n : TEXCOORD0;
                float3 v : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            half _RimPower;
            half _Fill;

            v2f vert (appdata i)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex);
                float3 wp = mul(unity_ObjectToWorld, i.vertex).xyz;
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half ndv = abs(dot(normalize(i.n), normalize(i.v)));
                half rim = pow(1.0h - saturate(ndv), _RimPower);
                half a = saturate(_Fill + rim) * _Color.a;
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
