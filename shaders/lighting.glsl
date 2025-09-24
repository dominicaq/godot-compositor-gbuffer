#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba16f) uniform image2D world_position_image;
layout(set = 0, binding = 1) uniform sampler2D sign_bits_texture;
layout(set = 0, binding = 2) uniform sampler2D normal_roughness_texture;
layout(set = 0, binding = 3) uniform sampler2D depth_texture;

// Godot's normal decoder
vec4 normal_roughness_compatibility(vec4 p_normal_roughness) {
    float roughness = p_normal_roughness.w;
    if (roughness > 0.5) {
        roughness = 1.0 - roughness;
    }
    roughness /= (127.0 / 255.0);
    return vec4(normalize(p_normal_roughness.xyz * 2.0 - 1.0) * 0.5 + 0.5, roughness);
}

// Your world position decoder
vec3 decodeWorldPos(vec3 abs_world_pos, float sign_bits) {
    float sign_mask = sign_bits * 7.0;
    vec3 world_pos = abs_world_pos;

    if (sign_mask >= 4.0) {
        world_pos.z = -world_pos.z;
        sign_mask -= 4.0;
    }
    if (sign_mask >= 2.0) {
        world_pos.y = -world_pos.y;
        sign_mask -= 2.0;
    }
    if (sign_mask >= 1.0) {
        world_pos.x = -world_pos.x;
    }

    return world_pos;
}

// Basic diffuse lighting
vec3 calculateDiffuseLighting(vec3 worldPos, vec3 normal, vec3 albedo) {
    // Light at (0, 5, 0)
    vec3 lightPos = vec3(0.0, 5.0, 0.0);
    vec3 lightColor = vec3(1.0, 1.0, 1.0);
    float lightIntensity = 10.0;

    vec3 lightDir = normalize(lightPos - worldPos);
    float NdotL = max(dot(normal, lightDir), 0.0);

    // Distance attenuation
    float distance = length(lightPos - worldPos);
    float attenuation = 1.0 / (1.0 + 0.1 * distance + 0.01 * distance * distance);

    vec3 diffuse = albedo * lightColor * lightIntensity * NdotL * attenuation;
    vec3 ambient = albedo * 0.1;

    return ambient + diffuse;
}

void main() {
    ivec2 coords = ivec2(gl_GlobalInvocationID.xy);
    ivec2 size = imageSize(world_position_image);

    // Bounds check
    if (coords.x >= size.x || coords.y >= size.y) {
        return;
    }

    // Calculate UV coordinates
    vec2 uv = (vec2(coords) + 0.5) / vec2(size);

    // Sample the buffers
    vec4 world_pos_data = imageLoad(world_position_image, coords);
    vec4 sign_bits_data = texture(sign_bits_texture, uv);
    vec4 normal_roughness_raw = texture(normal_roughness_texture, uv);

    // Decode world position
    vec3 abs_world_pos = world_pos_data.rgb;
    float sign_bits = sign_bits_data.r;
    vec3 world_pos = decodeWorldPos(abs_world_pos, sign_bits);

    // Decode normal
    vec4 normal_roughness = normal_roughness_compatibility(normal_roughness_raw);
    vec3 normal = normalize(normal_roughness.xyz * 2.0 - 1.0);

    // White albedo
    vec3 albedo = vec3(1.0, 1.0, 1.0);

    // Calculate lighting
    vec3 litColor = calculateDiffuseLighting(world_pos, normal, albedo);

    // Output lit result
    imageStore(world_position_image, coords, vec4(litColor, world_pos_data.a));
}
