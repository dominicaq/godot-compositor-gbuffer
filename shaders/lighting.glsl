#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba16f) restrict uniform image2D color_image;
layout(set = 0, binding = 1) uniform sampler2D depth_texture;
layout(set = 0, binding = 2) uniform sampler2D normal_roughness_texture;

vec4 normal_roughness_compatibility(vec4 p_normal_roughness) {
    float roughness = p_normal_roughness.w;
    if (roughness > 0.5) {
        roughness = 1.0 - roughness;
    }
    roughness /= (127.0 / 255.0);
    return vec4(normalize(p_normal_roughness.xyz * 2.0 - 1.0) * 0.5 + 0.5, roughness);
}

void main() {
    ivec2 coords = ivec2(gl_GlobalInvocationID.xy);
    ivec2 size = imageSize(color_image);

    // Bounds check
    if (coords.x >= size.x || coords.y >= size.y) {
        return;
    }

    // Calculate UV coordinates
    vec2 uv = (vec2(coords) + 0.5) / vec2(size);

    // Sample the buffers
    vec4 color = imageLoad(color_image, coords);
    float center_depth = texture(depth_texture, uv).r;
    vec4 normal_roughness_raw = texture(normal_roughness_texture, uv);

    // Convert normal/roughness to usable format
    vec4 normal_roughness = normal_roughness_compatibility(normal_roughness_raw);
    vec3 normal = normal_roughness.xyz * 2.0 - 1.0; // Convert back to [-1, 1] range
    vec3 normal_color = normal * 0.5 + 0.5; // Convert to [0, 1] for visualization

    // Sample neighboring depths for edge detection
    vec2 texel_size = 1.0 / vec2(size);
    float left_depth = texture(depth_texture, uv + vec2(-texel_size.x, 0)).r;
    float right_depth = texture(depth_texture, uv + vec2( texel_size.x, 0)).r;
    float up_depth = texture(depth_texture, uv + vec2(0, -texel_size.y)).r;
    float down_depth = texture(depth_texture, uv + vec2(0, texel_size.y)).r;

    // Calculate depth differences for edge detection
    float depth_diff_x = abs(center_depth - left_depth) + abs(center_depth - right_depth);
    float depth_diff_y = abs(center_depth - up_depth) + abs(center_depth - down_depth);

    // Edge detection threshold
    float edge_threshold = 0.001; // Adjust this to control sensitivity
    float edge_strength = depth_diff_x + depth_diff_y;

    // If we're on an edge, use normal colors, otherwise use original color
    if (edge_strength > edge_threshold) {
        imageStore(color_image, coords, vec4(normal_color, color.a));
    } else {
        imageStore(color_image, coords, color);
    }
}
