Shader "Game/2D/Skill Ground Flow"
{
    Properties
    {
        [PerRendererData] _MainTex("White Shape Mask", 2D) = "white" {}
        [HDR] _Color("Body Color", Color) = (0.35,0.7,0.9,0.5)
        [HDR] _FlowColor("Moving Edge Color", Color) = (0.7,1.2,1.5,0.8)
        _Mode("0 Frost / 1 Cloud / 2 Earth", Float) = 0
        _Speed("Flow Speed", Range(0,2)) = 0.4
        _UvRect("Sprite UV Rectangle", Vector) = (0,0,1,1)
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
                half4 _Color, _FlowColor;
                float4 _UvRect;
                float _Mode, _Speed;
            CBUFFER_END
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34,456.21));
                p += dot(p,p+45.32);
                return frac(p.x*p.y);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                    lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonUnlitVertex(input);
                o.color=input.color*unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=(input.uv-_UvRect.xy)/_UvRect.zw;
                float2 p=(uv-.5)*2;
                float r=length(p), t=_Time.y*_Speed;
                float n=Noise(p*3+float2(t*.35,-t*.25));
                float mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
                float edge=1-smoothstep(.91,1.0,r);
                half alpha, glow;
                if (_Mode < .5)
                {
                    // Keep the drawn crystal rim, with sparse moving frost inside the actual radius.
                    float sweep=pow(saturate(.5+.5*sin(r*19-t*2.5+n*2)),7);
                    glow=sweep*smoothstep(.48,.85,n)*edge;
                    alpha=max(mask*.78,edge*(.07+glow*.22));
                }
                else if (_Mode < 1.5)
                {
                    // Rounded overlapping puffs make the silhouette read as a cloud.
                    float2 q=p+float2(sin(t+p.y*4),cos(t*.8+p.x*3))*.018;
                    float d0=length(q-float2(0,.12))-.48;
                    float d1=length(q-float2(-.40,-.04))-.40;
                    float d2=length(q-float2(.40,-.05))-.41;
                    float d3=length(q-float2(.04,-.40))-.38;
                    float d4=length(q-float2(-.26,.39))-.31;
                    float d5=length(q-float2(.34,.34))-.32;
                    float d=min(min(min(d0,d1),min(d2,d3)),min(d4,d5));
                    float silhouette=1-smoothstep(-.012,.014,d);
                    float rim=smoothstep(-.065,-.025,d);
                    float fold=max(max(1-smoothstep(.015,.075,abs(d1)),
                                       1-smoothstep(.015,.075,abs(d2))),
                                       1-smoothstep(.015,.065,abs(d4)));
                    float light=saturate(.48+q.y*.35+(n-.5)*.30);
                    half3 cloudColor=lerp(_Color.rgb,_FlowColor.rgb,light);
                    cloudColor=lerp(cloudColor,_Color.rgb*.48,saturate(rim+fold*.28));
                    return half4(cloudColor*input.color.rgb,silhouette*_Color.a*input.color.a);
                }
                else
                {
                    float ridge=1-smoothstep(.04,.14,abs(r-(.72+n*.12)));
                    glow=ridge;
                    alpha=edge*(.16+ridge*.4);
                }
                half3 color=lerp(_Color.rgb,_FlowColor.rgb,saturate(glow));
                return half4(color*input.color.rgb,alpha*_Color.a*input.color.a);
            }
            ENDHLSL
        }
    }
}
