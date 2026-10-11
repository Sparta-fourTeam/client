Shader "Game/2D/Root Wave Flex"
{
    Properties
    {
        [PerRendererData] _MainTex("Root Animation Sheet", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Bend("Branch Flex", Range(0,0.04)) = 0.012
        _Speed("Flex Speed", Range(0,5)) = 2
        _Sheet("Sheet Columns / Rows", Vector) = (4,2,0,0)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
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
                half4 _Color;
                float4 _Sheet;
                float _Bend, _Speed;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonUnlitVertex(input);
                o.color=input.color*_Color*unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 cell=floor(input.uv*_Sheet.xy);
                float2 uv=frac(input.uv*_Sheet.xy);
                // Flex the tips while the lower soil ridge remains anchored; never sample a neighbouring frame.
                float anchored=smoothstep(.22,.85,uv.y);
                uv.x+=sin(_Time.y*_Speed+uv.y*7+uv.x*4)*_Bend*anchored;
                uv.y+=sin(_Time.y*_Speed*.8+uv.x*8)*_Bend*.35*anchored;
                uv=clamp(uv,.002,.998);
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,(cell+uv)/_Sheet.xy);
                return c*input.color;
            }
            ENDHLSL
        }
    }
}
