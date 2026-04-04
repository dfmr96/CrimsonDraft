
// Reconstruct interpolating frustum rays (in world space) using uv and linear eye depth
float3 ReconstructWorldPosFromFrustum(float2 uv, float lineaDepth, float4x4 frustumCoord)
{
    // Interpolate between frustum corner rays (in world space)
    float3 ray00 = frustumCoord[0].xyz; // Lower-left
    float3 ray01 = frustumCoord[1].xyz; // Upper-left
    float3 ray10 = frustumCoord[2].xyz; // Upper-right
    float3 ray11 = frustumCoord[3].xyz; // Lower-right

    // Correct interpolation order
    float3 leftEdge = lerp(ray00, ray01, uv.y);  // Vertical interpolation on the left side
    float3 rightEdge = lerp(ray11, ray10, uv.y); // Vertical interpolation on the right side
    float3 farPlaneWorldPos = lerp(leftEdge, rightEdge, uv.x); // Final horizontal interpolation

    // Transform the ray into world space
    float3 worldDirection = normalize(farPlaneWorldPos - _WorldSpaceCameraPos);
    
    // Retrieve near and far plane distances
    float nearPlane = _ProjectionParams.y;
    float farPlane = _ProjectionParams.z;
    float3 camForward = normalize(unity_CameraWorldClipPlanes[5].xyz);

    // Correct near and far plane distances based on view ray angle
    float cosTheta = dot(worldDirection, camForward);
    float cosThetaInv = rcp(max(cosTheta, 0.0001));
    float correctedNear = nearPlane * cosThetaInv;
    float correctedFar = farPlane * cosThetaInv;

    // Convert Linear01Depth to real-world depth with corrected near/far planes
    float realDepth = lerp(correctedNear, correctedFar, lineaDepth);

    return _WorldSpaceCameraPos + worldDirection * realDepth;
}

float3 ReconstructWorldPos(float2 screenPos, float nlDepth, float4x4 invViewMatrix)
{
    float4 clipSpaceLocation;
    clipSpaceLocation.xy = screenPos * 2.0f - 1.0f;
    clipSpaceLocation.z = nlDepth;
    clipSpaceLocation.w = 1.0f;
    float4 homogenousLocation = mul(invViewMatrix, clipSpaceLocation);
    return homogenousLocation.xyz / homogenousLocation.w;
}

float3 GetGridFromWorldPos(float3 worldPos) 
{
    float gridX = 1 - step(sin(worldPos.x), 0.99);
    float gridY = 1 - step(sin(worldPos.y), 0.99);
    float gridZ = 1 - step(sin(worldPos.z), 0.99);
    return float4(gridX, gridY, gridZ, 1);
}