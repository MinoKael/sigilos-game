shader_type canvas_item;

// Desenho a caneta: pinta a silhueta com a cor da tinta e faz o "boiling line" do GDD
// (3 variações do mesmo traço trocando a 8 fps). Dá vida sem animar.

uniform vec4 ink_color : source_color = vec4(0.12, 0.16, 0.27, 1.0);
uniform float boil_strength = 0.006;
uniform float boil_fps = 8.0;
uniform float seed = 0.0;

varying vec4 v_modulate;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	v_modulate = COLOR;
}

void fragment() {
	float frame = mod(floor(TIME * boil_fps), 3.0);
	vec2 wobble = vec2(
		sin(UV.y * 18.0 + frame * 2.1 + seed),
		cos(UV.x * 18.0 + frame * 1.7 + seed)) * boil_strength;
	vec2 jitter = (vec2(hash(vec2(frame, seed)), hash(vec2(seed, frame))) - 0.5) * boil_strength;
	float alpha = texture(TEXTURE, UV + wobble + jitter).a;
	COLOR = vec4(ink_color.rgb, alpha * ink_color.a) * v_modulate;
}
