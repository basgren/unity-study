// Pixel-art friendly iris/vignette for the hero death sequence.
// Renders a stylized dark ring around a circular "window" centered on the hero.
//
// Pixel perfection: the Image is placed on a ScreenSpaceCamera canvas targeting the
// PixelPerfect main camera, so the fragment shader runs at the game's virtual
// resolution (e.g. 480x270). The shader derives its virtual-pixel coordinate from
// uv * _Resolution and uses that to index a Bayer 4x4 ordered-dither matrix, so the
// iris edge falls on the same pixel grid as the sprites underneath.
//
// Outputs pure black with animated alpha:
//   inside the circle  -> _FadeAll  (0 transparent .. 1 full black)
//   outside the circle -> _Darkness (typically 1 = full black)
Shader "Game/UI/DeathScreenIris" {
    Properties {
        // Required by Unity UI (Graphic writes _MainTex into the material each frame).
        // We do not sample it; it is declared only to silence the runtime warning.
        [HideInInspector] _MainTex ("Sprite Texture (unused)", 2D) = "white" {}
        _Center     ("Center (UV)",               Vector)      = (0.5, 0.5, 0, 0)
        _Radius     ("Radius",                    Float)       = 0.45
        _EdgeWidth  ("Edge Dither Band (UV)",     Range(0, 0.2)) = 0.04
        _Aspect     ("Aspect W/H",                Float)       = 1.7777778
        _Darkness   ("Outside Darkness",          Range(0, 1)) = 1.0
        _FadeAll    ("Inside Fade (final fade)",  Range(0, 1)) = 0.0
        _Resolution ("Virtual Resolution (px)",   Vector)      = (480, 270, 0, 0)
        _UseDither  ("Use Dither (0/1)",          Float)       = 1.0
    }

    SubShader {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float2 _Center;
            float  _Radius;
            float  _EdgeWidth;
            float  _Aspect;
            float  _Darkness;
            float  _FadeAll;
            float2 _Resolution;
            float  _UseDither;

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // Standard Bayer 4x4 ordered-dither matrix, returned in [0, 15/16].
            float Bayer4(int2 px) {
                int x = px.x & 3;
                int y = px.y & 3;
                int i = y * 4 + x;
                // Flattened matrix values — stored as direct branch so no array access is needed
                // (keeps the shader portable across graphics backends that dislike const arrays).
                float v = 0.0;
                if      (i ==  0) v =  0.0;
                else if (i ==  1) v =  8.0;
                else if (i ==  2) v =  2.0;
                else if (i ==  3) v = 10.0;
                else if (i ==  4) v = 12.0;
                else if (i ==  5) v =  4.0;
                else if (i ==  6) v = 14.0;
                else if (i ==  7) v =  6.0;
                else if (i ==  8) v =  3.0;
                else if (i ==  9) v = 11.0;
                else if (i == 10) v =  1.0;
                else if (i == 11) v =  9.0;
                else if (i == 12) v = 15.0;
                else if (i == 13) v =  7.0;
                else if (i == 14) v = 13.0;
                else              v =  5.0;
                return v / 16.0;
            }

            float4 frag (v2f i) : SV_Target {
                // Snap uv and center to the virtual pixel grid so the circle sits exactly
                // on the same pixels as the sprites beneath, regardless of how the Canvas
                // was scaled.
                float2 resolution = max(_Resolution, float2(1, 1));
                float2 pixel      = floor(i.uv * resolution);
                float2 snappedUv  = (pixel + 0.5) / resolution;
                float2 snappedC   = (floor(_Center * resolution) + 0.5) / resolution;

                float2 d2 = snappedUv - snappedC;
                // Aspect-correct so the circle stays circular on non-square viewports.
                d2.x *= _Aspect;
                float d = length(d2);

                float band = max(_EdgeWidth, 1e-5);
                // Position within the transition band: 0 = fully inside, 1 = fully outside.
                float t = saturate((d - (_Radius - band * 0.5)) / band);

                float outside;
                if (_UseDither > 0.5) {
                    // Compare band position against Bayer threshold for a dithered edge.
                    // Slight bias ensures t=0 never flips and t=1 always flips.
                    float threshold = Bayer4(int2(pixel));
                    outside = step(threshold + 0.5 / 16.0, t);
                } else {
                    outside = smoothstep(0.0, 1.0, t);
                }

                float alpha = lerp(_FadeAll, _Darkness, outside);
                return float4(0.0, 0.0, 0.0, saturate(alpha));
            }
            ENDCG
        }
    }
}
