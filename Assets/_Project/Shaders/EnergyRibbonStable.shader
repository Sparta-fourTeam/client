Shader "Game/2D/Stable Energy Ribbon"
{
    Properties
    {
        [PerRendererData] _MainTex("Animated Ribbon", 2D) = "white" {}
        _Bounds("Sprite Bounds", Vector) = (-0.5,-0.25,1,0.5)
        _UvSize("Frame UV Size", Vector) = (0.25,0.25,0,0)
        _CapLength("World Cap Length", Float) = 0.22
        _TextureLength("World Texture Length", Float) = 0.8
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
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float2 ribbonUV : TEXCOORD1; float worldLength : TEXCOORD2; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Bounds, _UvSize;
                float _CapLength, _TextureLength;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                float2 ribbonUV=(input.positionOS.xy-_Bounds.xy)/_Bounds.zw;
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonUnlitVertex(input);
                o.color=input.color*unity_SpriteColor;
                o.ribbonUV=ribbonUV;
                o.worldLength=length(float3(unity_ObjectToWorld._m00,unity_ObjectToWorld._m10,unity_ObjectToWorld._m20))*_Bounds.z;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float u=saturate(input.ribbonUV.x);
                float len=max(input.worldLength,.001);
                float cap=min(.45,_CapLength/len);
                // World-length sampling keeps the texture pace stable as the target moves.
                float phase=(u-cap)*len/max(_TextureLength,.01);
                float mirrored=1-abs(frac(phase*.5)*2-1);
                float sourceX=.25+mirrored*.5;
                if(u<cap) sourceX=u/max(cap,.001)*.25;
                if(u>1-cap)
                {
                    float progress=(u-1+cap)/max(cap,.001);
                    sourceX=lerp(sourceX,.75+progress*.25,smoothstep(0,.25,progress));
                }
                float2 sampleUV=input.uv;
                sampleUV.x+=(sourceX-u)*_UvSize.x;
                half4 shape=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,sampleUV);
                return shape*input.color;
            }
            ENDHLSL
        }
    }
}
