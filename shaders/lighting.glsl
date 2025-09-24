#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba8) restrict uniform image2D color_image;
layout(set = 0, binding = 1) uniform sampler2D depth_texture;

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    vec2 uv = (vec2(coord) + 0.5) / imageSize(color_image);

    // Read original color
    vec4 original_color = imageLoad(color_image, coord);

    // Sample depth at center and neighbors
    float center_depth = texture(depth_texture, uv).r;

    vec2 texel_size = 1.0 / imageSize(color_image);

    // Sample neighboring depths
    float left   = texture(depth_texture, uv + vec2(-texel_size.x, 0)).r;
    float right  = texture(depth_texture, uv + vec2( texel_size.x, 0)).r;
    float up     = texture(depth_texture, uv + vec2(0, -texel_size.y)).r;
    float down   = texture(depth_texture, uv + vec2(0,  texel_size.y)).r;

    // Calculate depth differences
    float diff_x = abs(center_depth - left) + abs(center_depth - right);
    float diff_y = abs(center_depth - up) + abs(center_depth - down);

    // Edge detection threshold
    float edge_threshold = 0.001; // Adjust this to control sensitivity
    float edge_strength = diff_x + diff_y;

    // If we're on an edge, draw white outline
    if (edge_strength > edge_threshold) {
        imageStore(color_image, coord, vec4(1.0, 0.0, 0.0, 1.0)); // White outline
    } else {
        imageStore(color_image, coord, original_color); // Keep original color
    }
}
