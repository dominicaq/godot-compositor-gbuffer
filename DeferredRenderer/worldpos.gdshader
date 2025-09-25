shader_type spatial;
render_mode unshaded;
uniform sampler2D albedo_texture : source_color;
varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    ALBEDO = texture(albedo_texture, UV).rgb;
}
