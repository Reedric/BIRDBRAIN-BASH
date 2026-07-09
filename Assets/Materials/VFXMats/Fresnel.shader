Shader "Custom/Fresnel"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (1,0.6,0.2,1)
        _RimPower ("Rim Power", Range(0.1, 10)) = 1.0
        _RimWidth ("Rim Width", Range(0,1)) = 0.4
        _RimThickness ("Rim Thickness", Range(0,1)) = 0.25
        _Alpha ("Alpha", Range(0,1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _RimColor;
            float _RimPower;
            float _RimWidth;
            float _RimThickness;
            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndotv = 1.0 - saturate(dot(normalize(i.normal), viewDir));
                float rim = pow(ndotv, _RimPower);
                // Use step for a hard edge, _RimWidth = where rim starts, _RimThickness = thickness of the outline
                float rimMask = step(_RimWidth, rim) - step(_RimWidth + _RimThickness, rim);
                fixed4 col = fixed4(_RimColor.rgb, _Alpha * rimMask);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
