//-----------------------------------------------------------------------
// File:    Decay.hlsl
// Capa de deterioro para PSXDecay_URP.shadergraph (Custom Function "ApplyDecay").
//
// Todo lo que viene de las salas llega como globales que sube DecayShaderGlobals
// (CrimsonDraft.Rendering.Decay): no hay nada por material salvo Receive/Metal/Paint,
// asi que el SRP Batcher sigue funcionando.
//
// Cada sala tiene un id (1-255) que la Decay Tool escribe en los bits 24-31 del rendering
// layer de sus renderers. Un renderer solo toma el deterioro del volumen con su mismo id,
// asi que nunca se "derrama" a otra sala; sin id (0) sale igual que con PSXMaster_URP.
// La caja del volumen solo da la geometria de la sala: piso, techo y paredes.
//-----------------------------------------------------------------------

#ifndef CRIMSON_DECAY_INCLUDED
#define CRIMSON_DECAY_INCLUDED

#define DECAY_MAX_VOLUMES 16

int      _DecayVolumeCount;
float4x4 _DecayWorldToVolume[DECAY_MAX_VOLUMES]; // mundo -> caja unitaria [-0.5, 0.5]
float4   _DecayVolumeSize[DECAY_MAX_VOLUMES];    // xyz tamaño en metros, w id de la sala
float4   _DecayAmounts[DECAY_MAX_VOLUMES];       // x mugre, y humedad, z oxido, w pintura (ya multiplicados por el deterioro general)
float4   _DecayBaseboard[DECAY_MAX_VOLUMES];     // x altura, y suavidad, z ruido del borde, w intensidad
float4   _DecayExtra[DECAY_MAX_VOLUMES];         // x tamaño de esquinas, y largo de chorreaduras, z moho, w polvo

float4 _DecayDirtColor;
float4 _DecayDampColor;
float4 _DecayMoldColor;
float4 _DecayRustColor;
float4 _DecayDustColor;
float4 _DecayBareColor;  // lo que aparece debajo de la pintura; a = cuanto reemplaza al albedo
float  _DecayTexScale;   // repeticiones de la textura de ruido por metro
float  _DecayLowDetail;  // 1 = detalle bajo; sin setear (0) = medio

// R manchas de baja frecuencia, G grano, B chorreaduras verticales, A grietas.
TEXTURE2D(_DecayNoiseTex);
SAMPLER(sampler_DecayNoiseTex);

float4 DecaySample(float2 uv)
{
    return SAMPLE_TEXTURE2D(_DecayNoiseTex, sampler_DecayNoiseTex, uv);
}

// Medio: triplanar completo. Las proyecciones de pared usan (horizontal, y) para que el
// canal B de la textura quede siempre vertical.
float4 DecaySampleTriplanar(float3 p, float3 n)
{
    float3 w = abs(n);
    w = w * w * w * w;
    w /= max(w.x + w.y + w.z, 1e-4);
    return DecaySample(p.zy) * w.x + DecaySample(p.xz) * w.y + DecaySample(p.xy) * w.z;
}

// Bajo: una sola muestra sobre el eje dominante.
float4 DecaySampleDominant(float3 p, float3 n)
{
    float3 a = abs(n);
    float2 uv = (a.y >= a.x && a.y >= a.z) ? p.xz : (a.x >= a.z ? p.zy : p.xy);
    return DecaySample(uv);
}

void ApplyDecay_float(float4 Albedo, float3 PositionWS, float3 NormalWS, float Receive, float Metal, float Paint, out float4 Out)
{
    Out = Albedo;

    uint roomId = (asuint(unity_RenderingLayer.x) >> 24) & 0xFF;

    [branch] if (roomId == 0 || _DecayVolumeCount <= 0 || Receive <= 0.0)
        return;

    // --- Sala de este renderer ----------------------------------------------------------
    int best = -1;
    for (int i = 0; i < _DecayVolumeCount; i++)
    {
        if ((uint)_DecayVolumeSize[i].w == roomId) { best = i; break; }
    }

    [branch] if (best < 0)
        return;

    float4 amounts = _DecayAmounts[best] * Receive;

    // --- Geometría de la sala (en metros) -----------------------------------------------
    float3 size = _DecayVolumeSize[best].xyz;
    float3 lm   = mul(_DecayWorldToVolume[best], float4(PositionWS, 1.0)).xyz * size;

    float heightFromFloor  = lm.y + size.y * 0.5;
    float heightToCeiling  = size.y * 0.5 - lm.y;
    float dx               = max(size.x * 0.5 - abs(lm.x), 0.0);
    float dz               = max(size.z * 0.5 - abs(lm.z), 0.0);
    float wallDist         = min(dx, dz);

    float3 n       = normalize(NormalWS);
    float  isFloor = saturate((n.y - 0.5) * 4.0);
    float  isCeil  = saturate((-n.y - 0.5) * 4.0);
    float  isWall  = 1.0 - saturate((abs(n.y) - 0.3) * 4.0);

    float3 p = PositionWS * _DecayTexScale;
    float4 nz;
    [branch] if (_DecayLowDetail < 0.5)
        nz = DecaySampleTriplanar(p, n);
    else
        nz = DecaySampleDominant(p, n);

    float4 bb    = _DecayBaseboard[best];
    float4 extra = _DecayExtra[best];

    // --- Zócalo: gradiente pared/suelo con borde suave y ruidoso ------------------------
    float bbHeight = max(bb.x, 1e-3);
    float bbStart  = bbHeight * (1.0 - bb.y);
    float wobble   = (nz.r - 0.5) * bb.z * bbHeight * 2.0;
    float baseWall = 1.0 - smoothstep(bbStart, bbHeight, heightFromFloor + wobble);
    float baseFloor = 1.0 - smoothstep(bbStart * 0.6, bbHeight * 0.6 + 1e-3, wallDist + wobble * 0.5);
    float baseboard = saturate(baseWall * isWall + baseFloor * isFloor * saturate(1.0 - heightFromFloor * 4.0)) * bb.w;

    // --- Esquinas y uniones -----------------------------------------------------------
    float cs        = max(extra.x, 1e-3);
    float wallNear  = saturate(1.0 - wallDist / cs);
    float cornerV   = saturate(1.0 - dx / cs) * saturate(1.0 - dz / cs);
    float cornerLow = saturate(1.0 - heightFromFloor / cs) * wallNear;
    float cornerTop = saturate(1.0 - heightToCeiling / cs) * wallNear * 0.7;
    float corner    = saturate(max(cornerV, max(cornerLow, cornerTop)));
    corner = corner * corner * (3.0 - 2.0 * corner);

    // --- Mugre y polvo -----------------------------------------------------------------
    // Manchas generales: con contraste, para que haya zonas limpias y zonas percudidas.
    float mottle = smoothstep(0.3, 0.85, nz.r);
    float grime  = baseboard + corner * 0.8 + mottle * (0.45 + isFloor * 0.25);
    float dirt  = saturate(amounts.x * grime * (0.55 + nz.g * 0.9));
    float dust  = amounts.x * extra.w * isFloor * saturate((heightFromFloor - 0.15) * 5.0) * (0.4 + nz.g * 0.6);

    // --- Humedad: chorreaduras del techo, aureolas, humedad que sube del piso -----------
    float streakLen = max(extra.y * (0.4 + nz.r * 1.2), 1e-3);
    float streak    = saturate(nz.b * 1.6 - 0.3) * (1.0 - saturate(heightToCeiling / streakLen)) * isWall;
    float ceilStain = saturate(nz.r * 2.0 - 0.7) * isCeil;
    float riseH     = bbHeight * 2.5 * (0.6 + nz.r * 0.8);
    float rise      = (1.0 - smoothstep(riseH * 0.8, riseH, heightFromFloor)) * isWall;
    float tide      = (smoothstep(riseH * 0.7, riseH * 0.88, heightFromFloor) - smoothstep(riseH * 0.88, riseH, heightFromFloor)) * isWall;
    float damp      = saturate(amounts.y * (streak + ceilStain + rise * 0.45 + tide * 0.6));
    float mold      = saturate(amounts.y * extra.z * saturate(corner * 1.3 + rise * 0.4) * saturate(nz.g * 1.8 - 0.5) * 1.5);

    // --- Óxido (solo donde el material es metal) ---------------------------------------
    float rustBlot   = saturate((nz.r + nz.g * 0.5 - 1.25 + amounts.z * 0.9) * 3.0);
    float rustStreak = saturate(nz.b * 1.5 - 0.5) * saturate(nz.r * 1.5) * isWall;
    float rust       = saturate(Metal * amounts.z * (rustBlot + rustStreak * 0.7 + corner * 0.3));

    // --- Pintura descascarada y grietas -------------------------------------------------
    float paintWear = amounts.w * Paint;
    float peelThr   = 1.0 - paintWear * 0.85;
    float peelVal   = nz.r * 0.65 + nz.g * 0.35 + corner * 0.15;
    float peel      = smoothstep(peelThr, peelThr + 0.06, peelVal) * step(1e-3, paintWear);
    float peelEdge  = smoothstep(peelThr - 0.05, peelThr, peelVal) - peel;
    float crack     = 0.0;
    [branch] if (_DecayLowDetail < 0.5)
        crack = saturate((nz.a - (1.0 - paintWear * 0.5)) * 8.0) * isWall;

    // --- Composición ---------------------------------------------------------------------
    float3 c   = Albedo.rgb;
    float  lum = dot(c, float3(0.299, 0.587, 0.114));

    c = lerp(c, lerp(lum.xxx * 0.6, _DecayBareColor.rgb, _DecayBareColor.a), peel);
    c *= 1.0 - saturate(peelEdge) * 0.35 - crack * 0.6;
    c = lerp(c, _DecayRustColor.rgb * (0.55 + nz.g * 0.6), rust);
    c = lerp(c, c * _DecayDampColor.rgb, damp);
    c = lerp(c, _DecayMoldColor.rgb * (lum + 0.25), mold);
    c = lerp(c, c * _DecayDirtColor.rgb, dirt);
    c = lerp(c, _DecayDustColor.rgb, saturate(dust) * 0.6);

    Out = float4(c, Albedo.a);
}

void ApplyDecay_half(half4 Albedo, half3 PositionWS, half3 NormalWS, half Receive, half Metal, half Paint, out half4 Out)
{
    float4 o;
    ApplyDecay_float(Albedo, PositionWS, NormalWS, Receive, Metal, Paint, o);
    Out = o;
}

#endif
