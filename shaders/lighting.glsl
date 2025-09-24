#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba16f) restrict uniform image2D world_pos_image;
layout(set = 0, binding = 1) uniform sampler2D normal_roughness_texture;
layout(set = 0, binding = 2) uniform sampler2D specular_texture;
layout(set = 0, binding = 3) uniform sampler2D depth_texture;

// Decode world position signs from specular channel
vec3 decode_world_position(vec3 abs_pos, float sign_data) {
    float sign_mask = sign_data * 7.0;
    vec3 signs = vec3(
        mod(sign_mask, 2.0) >= 1.0 ? -1.0 : 1.0,
        mod(sign_mask, 4.0) >= 2.0 ? -1.0 : 1.0,
        sign_mask >= 4.0 ? -1.0 : 1.0
    );
    return abs_pos * signs;
}

void main() {
    ivec2 coords = ivec2(gl_GlobalInvocationID.xy);
    ivec2 size = imageSize(world_pos_image);

    if (coords.x >= size.x || coords.y >= size.y) {
        return;
    }

    vec2 uv = (vec2(coords) + 0.5) / vec2(size);

    // Sample buffers
    vec3 abs_world_pos = imageLoad(world_pos_image, coords).rgb; // Absolute world pos from emission
    float sign_data = texture(specular_texture, uv).r;          // Sign bits from specular

    // Decode world position
    vec3 world_pos = decode_world_position(abs_world_pos, sign_data);

    // For now, just output the decoded world position as color
    imageStore(world_pos_image, coords, vec4(world_pos * 0.01, 1.0)); // Scale down for visibility
}
