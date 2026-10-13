Shader "Game/UI/Spotlight"
{
    // 화면을 Image 색(어둡게)으로 덮고, 둥근 사각형 구멍(_Hole)만 뚫는 튜토리얼 스포트라이트.
    // 정점 위치(RectTransform 로컬 좌표)를 그대로 비교하므로 Image가 구멍을 계산한 영역과 같은 RectTransform 안에서 전체를 덮어야 한다.
    // 구멍 가장자리에서 바깥으로 퍼지는 펄스 링(_RingColor, _Ring)을 함께 그린다.
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Dim Color", Color) = (0,0,0,0.7)
        _Hole("Hole (center.xy, half size.xy) in local px", Vector) = (0,0,100,100)
        _Params("Corner Radius, Edge Softness", Vector) = (36,10,0,0)
        _RingColor("Ring Color", Color) = (1,0.85,0.4,1)
        _Ring("Ring Progress 0-1, Max Spread px, Thickness px", Vector) = (0,60,6,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Pass
        {
            Name "UISpotlight"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 local : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Hole;
                float4 _Params;
                half4 _RingColor;
                float4 _Ring;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.local = input.positionOS.xy;
                o.color = input.color * _Color;
                return o;
            }

            // 둥근 사각형까지의 부호 있는 거리 (안쪽 음수)
            float RoundedRectSdf(float2 p, float2 center, float2 halfSize, float radius)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p - center) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float d = RoundedRectSdf(i.local, _Hole.xy, _Hole.zw, _Params.x);
                float soft = max(_Params.y, 0.001);
                // 구멍 안은 투명, 바깥은 어둡게. 가장자리는 부드럽게
                half dimAlpha = smoothstep(-soft * 0.5, soft * 0.5, d) * i.color.a;
                half4 result = half4(i.color.rgb, dimAlpha);

                // 펄스 링: 구멍 가장자리에서 progress만큼 바깥으로 퍼지며 사라진다
                float progress = _Ring.x;
                float ringDist = progress * _Ring.y;
                float thickness = max(_Ring.z, 0.001);
                float ring = 1.0 - smoothstep(0.0, thickness, abs(d - ringDist));
                half ringAlpha = ring * (1.0 - progress) * _RingColor.a;
                result.rgb = lerp(result.rgb, _RingColor.rgb, ringAlpha);
                result.a = max(result.a, ringAlpha);
                return result;
            }
            ENDHLSL
        }
    }
}
