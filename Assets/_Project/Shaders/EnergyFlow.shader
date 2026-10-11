Shader "Game/2D/Energy Flow"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        [HDR] _Tint("Outer Stroke", Color) = (0.2,0.4,1,1)
        [HDR] _CoreColor("Bright Core", Color) = (0.7,1.2,2,1)
        _Speed("Flow Speed", Float) = 1
        _Ripple("Stroke Bend", Range(0,0.3)) = 0.02
        _CoreWidth("Core Width", Range(0.02,0.3)) = 0.12
        _UseMeshUV("Line Renderer", Float) = 0
        [HideInInspector] _AfterglowStart("Afterglow Start", Float) = 0
        [HideInInspector] _AfterglowDuration("Afterglow Duration", Float) = 0
        _Bounds("Sprite Bounds", Vector) = (-0.5,-0.25,1,0.5)
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
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float2 flowUV : TEXCOORD1; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _CoreColor;
                float4 _Bounds;
                float _Speed, _Ripple, _CoreWidth, _UseMeshUV;
                float _AfterglowStart, _AfterglowDuration;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                float2 flowUV=input.uv;
                half4 color=input.color;
                if (_UseMeshUV < .5)
                {
                    SetUpSpriteInstanceProperties();
                    flowUV=(input.positionOS.xy-_Bounds.xy)/_Bounds.zw;
                    input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                    color*=unity_SpriteColor;
                }
                Varyings o=CommonUnlitVertex(input);
                o.flowUV=flowUV;
                o.color=color;
                return o;
            }
            float BoltHash(float v) { return frac(sin(v*127.1)*43758.5453); }
            float BoltBend(float u, float seed)
            {
                float x=u*13;
                return lerp(BoltHash(floor(x)+seed),BoltHash(floor(x)+1+seed),frac(x))*2-1;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=input.flowUV;
                float t=_Time.y*_Speed;
                float bend;
                if (_UseMeshUV > .5)
                    bend=BoltBend(uv.x,floor(t*8)*19)*_Ripple;
                else
                    bend=(sin(uv.x*26-t*3)+sin(uv.x*47+t*2)*.35)*_Ripple;
                float d=abs(uv.y-.5-bend);
                float outer=1-smoothstep(.25,.46,d);
                float core=1-smoothstep(_CoreWidth,_CoreWidth+.055,d);
                float ends=smoothstep(0,.025,uv.x)*(1-smoothstep(.975,1,uv.x));
                float flow=.85+.15*sin(uv.x*35-t*6);
                half3 rgb=lerp(_Tint.rgb,_CoreColor.rgb,core)*flow;
                if (_UseMeshUV > .5)
                {
                    // The wider mesh gives angular bends and forks room around the bright stroke.
                    outer=1-smoothstep(.08,.145,d);
                    core=1-smoothstep(_CoreWidth,_CoreWidth+.025,d);
                    float cell=frac(uv.x*7);
                    float forkSeed=floor(uv.x*7)+floor(t*8)*19;
                    float side=BoltHash(forkSeed)>.5 ? 1 : -1;
                    float forkBend=bend+side*cell*.34;
                    float fork=(1-smoothstep(.025,.055,abs(uv.y-.5-forkBend)))
                        *(1-smoothstep(.4,.65,cell))*step(.65,BoltHash(forkSeed+11));
                    rgb=lerp(_Tint.rgb,_CoreColor.rgb,core);
                    outer=max(outer,fork*.7);
                }
                float fade=1;
                if (_AfterglowDuration > 0)
                    fade=1-smoothstep(.18,1,saturate((_Time.y-_AfterglowStart)/_AfterglowDuration));
                return half4(rgb*input.color.rgb,outer*ends*_Tint.a*input.color.a*fade);
            }
            ENDHLSL
        }
    }
}
