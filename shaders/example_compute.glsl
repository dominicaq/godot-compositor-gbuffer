#[compute]
#version 450
layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;
layout(set = 0, binding = 0, rgba16f) uniform image2D color_image;
layout(set = 0, binding = 1) uniform sampler2D normal_roughness_texture;
layout(set = 0, binding = 2) uniform sampler2D depth_texture;

layout(push_constant) uniform PushConstants {
    mat4 inv_projection_matrix;
    mat4 inv_view_matrix;
} pc;

// If you wish to sample the normal buffer, you need this from Godot's official docs
vec4 normal_roughness_compatibility(vec4 p_normal_roughness) {
    float roughness = p_normal_roughness.w;
    if (roughness > 0.5) {
        roughness = 1.0 - roughness;
    }
    roughness /= (127.0 / 255.0);
    return vec4(normalize(p_normal_roughness.xyz * 2.0 - 1.0) * 0.5 + 0.5, roughness);
}

vec3 reconstructWorldPosition(vec2 uv, float depth) {
    vec2 ndc = uv * 2.0 - 1.0;
    vec4 ndcPos = vec4(ndc, depth * 2.0 - 1.0, 1.0);
    vec4 viewPos = pc.inv_projection_matrix * ndcPos;
    viewPos.xyz /= viewPos.w;
    vec4 worldPos = pc.inv_view_matrix * vec4(viewPos.xyz, 1.0);
    return worldPos.xyz;
}

void main() {
    ivec2 coords = ivec2(gl_GlobalInvocationID.xy);
    ivec2 size = imageSize(color_image);
    if (coords.x >= size.x || coords.y >= size.y) return;

    vec2 uv = (vec2(coords) + 0.5) / vec2(size);

    // Get the original color from the color buffer
    vec3 originalColor = imageLoad(color_image, coords).rgb;

    float depth = texture(depth_texture, uv).r;
    if (depth >= 0.9999) {
        imageStore(color_image, coords, vec4(originalColor, 1.0));
        return;
    }

    vec3 worldPos = reconstructWorldPosition(uv, depth);
    vec3 normal = texture(normal_roughness_texture, uv).xyz * 2.0 - 1.0;

    vec3 lightPos = vec3(0, 5, 0);
    vec3 lightDir = normalize(lightPos - worldPos);
    float NdotL = max(dot(normal, lightDir), 0.0);

    vec3 finalColor = originalColor * NdotL;
    imageStore(color_image, coords, vec4(finalColor, 1.0));
}
