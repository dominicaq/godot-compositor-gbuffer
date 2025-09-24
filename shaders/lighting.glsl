#[compute]
#version 450

// Invocations in the (x, y, z) dimension
layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

// G-Buffer textures
layout(rgba16f, set = 0, binding = 0) uniform image2D world_position_image;
layout(set = 0, binding = 1) uniform sampler2D sign_bits_texture;
layout(set = 0, binding = 2) uniform sampler2D normal_roughness_texture;
layout(set = 0, binding = 3) uniform sampler2D depth_texture;

// Push constant for additional data if needed
layout(push_constant, std430) uniform Params {
    vec2 screen_size;
    float time;
    float _padding;
} params;

// Basic lighting calculation
vec3 calculateLighting(vec3 worldPos, vec3 normal, vec3 albedo) {
    // Light properties
    vec3 lightPos = vec3(0.0, 5.0, 0.0);
    vec3 lightColor = vec3(1.0, 1.0, 1.0);
    float lightIntensity = 10.0;

    // Calculate light direction and distance
    vec3 lightDir = lightPos - worldPos;
    float lightDistance = length(lightDir);
    lightDir = normalize(lightDir);

    // Attenuation (inverse square law)
    float attenuation = 1.0 / (1.0 + 0.1 * lightDistance + 0.01 * lightDistance * lightDistance);

    // Lambertian diffuse lighting
    float NdotL = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = albedo * lightColor * lightIntensity * NdotL * attenuation;

    // Simple ambient
    vec3 ambient = albedo * 0.1;

    return ambient + diffuse;
}

// Godot's normal_roughness compatibility function from docs
vec4 normal_roughness_compatibility(vec4 p_normal_roughness) {
    float roughness = p_normal_roughness.w;
    if (roughness > 0.5) {
        roughness = 1.0 - roughness;
    }
    roughness /= (127.0 / 255.0);
    return vec4(normalize(p_normal_roughness.xyz * 2.0 - 1.0) * 0.5 + 0.5, roughness);
}

// Decode normal from normal_roughness texture
vec3 decodeNormal(vec4 normalRoughness) {
    vec4 compatible = normal_roughness_compatibility(normalRoughness);
    // Convert from [0,1] to [-1,1] and normalize
    vec3 normal = compatible.rgb * 2.0 - 1.0;
    return normalize(normal);
}

void main() {
    ivec2 pixel_coord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 screen_size = ivec2(params.screen_size);

    // Bounds check
    if (pixel_coord.x >= screen_size.x || pixel_coord.y >= screen_size.y) {
        return;
    }

    // Calculate UV coordinates (add 0.5 for pixel center)
    vec2 uv = (vec2(pixel_coord) + 0.5) / vec2(screen_size);

    // Sample G-Buffer data
    vec4 worldPosData = imageLoad(world_position_image, pixel_coord);
    vec3 worldPos = worldPosData.rgb;

    // Check if this pixel has geometry (assuming w component indicates valid geometry)
    if (worldPosData.w <= 0.0) {
        return; // Skip pixels without geometry
    }

    vec4 normalRoughnessData = texture(normal_roughness_texture, uv);
    vec3 normal = decodeNormal(normalRoughnessData);

    // Get the corrected roughness value too
    vec4 compatibleData = normal_roughness_compatibility(normalRoughnessData);
    float roughness = compatibleData.a;

    vec4 signBitsData = texture(sign_bits_texture, uv);
    float depth = texture(depth_texture, uv).r;

    // For now, use white as base albedo
    vec3 albedo = vec3(1.0, 1.0, 1.0);

    // Calculate lighting
    vec3 litColor = calculateLighting(worldPos, normal, albedo);

    // Write the lit color back to the world position image
    // (You might want to write to a different target in a real implementation)
    imageStore(world_position_image, pixel_coord, vec4(litColor, worldPosData.w));
}
