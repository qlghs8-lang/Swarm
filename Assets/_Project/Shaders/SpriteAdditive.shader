// 2D 렌더러용 가산 합성(additive) 스프라이트 셰이더.
//
// 원래 SpriteAdditive.mat은 URP/Lit — 3D용 셰이더 — 를 SpriteRenderer에 물려 쓰고 있었다.
// Lit은 _TexelSize / _ST 같은 텍스처 부속 프로퍼티를 들고 있어서 2D SRP Batcher가 처리하지 못하고,
// 이펙트가 하나 뜰 때마다 "SRP batching will be disabled for 2D Renderers" 경고를 뱉는 동시에
// 그 머티리얼을 쓰는 스프라이트 전부가 배칭에서 빠진다.
//
// 이펙트에 필요한 것은 조명 계산이 아니라 '밝게 더해지는 그림' 하나뿐이라, 필요한 만큼만 담은
// unlit 셰이더로 바꾼다. 프로퍼티를 UnityPerMaterial 하나에 모아 두었으므로 SRP Batcher를 탄다.
Shader "Swarm/Sprite Additive"
{
    Properties
    {
        [PerRendererData][NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // 알파가 그대로 밝기가 된다: 투명한 픽셀은 아무것도 더하지 않고, 불투명한 픽셀은 원색만큼 더한다.
        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // SRP Batcher가 요구하는 형태: 머티리얼 프로퍼티는 이 한 블록 안에만 있어야 한다.
            //
            // _MainTex_ST(타일링/오프셋)와 _MainTex_TexelSize는 일부러 넣지 않는다. 2D SRP Batcher는
            // 이 둘이 선언된 머티리얼을 통째로 배칭에서 제외하면서 경고를 뱉는다 — 앞서 URP/Lit이
            // 걸렸던 바로 그 지점이다. 스프라이트는 아틀라스 UV가 메시에 이미 구워져 오므로
            // 타일링·오프셋을 쓸 일이 없다.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                // SpriteRenderer.color가 정점 색으로 들어온다. 이펙트가 페이드아웃하는 통로다.
                output.color = input.color * _Color;

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
