shader_type spatial;
render_mode unshaded;
uniform sampler2D albedo_texture : source_color;
varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // vec4 tex_color = texture(albedo_texture, UV);

    // Store absolute world position in albedo
    ALBEDO = abs(world_pos);

    // Pack sign bits into specular
    float sign_mask = 0.0;
    sign_mask += (world_pos.x < 0.0) ? 1.0 : 0.0;
    sign_mask += (world_pos.y < 0.0) ? 2.0 : 0.0;
    sign_mask += (world_pos.z < 0.0) ? 4.0 : 0.0;

    SPECULAR = sign_mask / 7.0;
}
