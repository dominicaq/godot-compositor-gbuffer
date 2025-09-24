shader_type spatial;
render_mode unshaded;

varying vec3 world_normal;
varying vec3 world_position;

uniform vec3 world_bounds_min : source_color = vec3(-50.0, -50.0, -50.0);
uniform vec3 world_bounds_max : source_color = vec3(50.0, 50.0, 50.0);

// Octahedral normal encoding
vec2 encode_octahedral(vec3 n) {
    n /= (abs(n.x) + abs(n.y) + abs(n.z));
    vec2 o = n.z >= 0.0 ? n.xy : (1.0 - abs(n.yx)) * sign(n.xy);
    return o * 0.5 + 0.5;
}

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // Encode normal into RG channels
    vec3 normal = normalize(world_normal);
    vec2 encoded_normal = encode_octahedral(normal);
    
    // Encode world position Y into B channel (normalized to bounds)
    float normalized_y = (world_position.y - world_bounds_min.y) / (world_bounds_max.y - world_bounds_min.y);
    normalized_y = clamp(normalized_y, 0.0, 1.0);
    
    ALBEDO = vec3(encoded_normal.x, encoded_normal.y, normalized_y);
    ALPHA = 1.0;
}
