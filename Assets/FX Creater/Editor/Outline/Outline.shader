// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/Outline"
{
    Properties
    {
        [HideInInspector]_MainTex ("Texture", 2D) = "white" {}

        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half2 _MainTex_TexelSize;

            half4 _OutlineColor;
            half _OutlineWidth;

            half4 frag (v2f_img i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);

                float2 destUV = _MainTex_TexelSize * _OutlineWidth;

				float center = tex2D(_MainTex, i.uv).a * 8.0;
                float left   = tex2D(_MainTex, i.uv + float2(destUV.x, 0)).a;
                float right  = tex2D(_MainTex, i.uv + float2(-destUV.x, 0)).a;
                float bottom = tex2D(_MainTex, i.uv + float2(0, destUV.y)).a;
                float top    = tex2D(_MainTex, i.uv + float2(0, - destUV.y)).a;
                float topLeft = tex2D(_MainTex, i.uv + float2(destUV.x, destUV.y)).a;
                half topRight = tex2D(_MainTex, i.uv + float2(-destUV.x, destUV.y)).a;
                half bottomLeft = tex2D(_MainTex, i.uv + float2(destUV.x, -destUV.y)).a;
                half bottomRight = tex2D(_MainTex, i.uv + float2(-destUV.x, -destUV.y)).a;

                // あるピクセルの近傍が不透明であれば 1
                half result = saturate(left + right + bottom + top + topLeft + topRight + bottomLeft + bottomRight);
                //float result = center - (left + right + bottom + top + topLeft + topRight + bottomLeft + bottomRight) == 0 ? 0.5 : 1;

                // 透過じゃないところはそのまま
                clip( 0.9 - col.a);

                float4 outline = result * _OutlineColor;

                return outline;
            }
            ENDCG
        }
    }
}
