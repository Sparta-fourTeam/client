Shader "Game/2D/Lightning Stroke Warp"
{
    Properties
    {
        _MainTex("White Bolt Frames", 2D) = "white" {}
        _Columns("Frame Columns", Float) = 3
        _Distortion("Jagged Distortion", Range(0,0.1)) = 0.025
        _Softness("Stroke Softness", Range(0,0.015)) = 0.003
        _Speed("Distortion Speed", Float) = 12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Columns, _Distortion, _Softness, _Speed;
            CBUFFER_END
            float Hash(float v) { return frac(sin(v*127.1)*43758.5453); }
            Varyings Vert(Attributes input)
            {
                Varyings o=CommonUnlitVertex(input);
                o.color=input.color;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float column=min(floor(input.uv.x*_Columns),_Columns-1);
                float segment=input.uv.y*13;
                float seed=floor(_Time.y*_Speed)*19+column*7;
                float offset=lerp(Hash(floor(segment)+seed),Hash(floor(segment)+1+seed),frac(segment))-.5;
                float2 uv=input.uv;
                uv.x+=offset*_Distortion/_Columns;
                uv.x=clamp(uv.x,(column+.002)/_Columns,(column+.998)/_Columns);
                float a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a;
                float2 blur=float2(_Softness/_Columns,0);
                float soft=max(a,max(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+blur).a,
                                    SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-blur).a)*.5);
                half3 color=lerp(input.color.rgb*.6,lerp(input.color.rgb,half3(2.1,2.7,3.0),.55),a*a);
                return half4(color,soft*input.color.a);
            }
            ENDHLSL
        }
    }
}
