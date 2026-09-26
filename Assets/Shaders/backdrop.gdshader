shader_type canvas_item;

// Fundo de tela: pedra quente com uma luz de vela no centro, bordas escuras e grão fino.
// Um anel de sigilo muito apagado gira devagar atrás de tudo: dá vida sem roubar a atenção.
// O centro (em UV) é o do foco da tela — a constelação, o portal — ou o meio da tela.

uniform vec4 edge_color : source_color = vec4(0.078, 0.059, 0.043, 1.0);
uniform vec4 glow_color : source_color = vec4(0.204, 0.145, 0.094, 1.0);
uniform vec4 rune_color : source_color = vec4(0.84, 0.68, 0.38, 1.0);
uniform float aspect = 1.7778;
uniform vec2 center = vec2(0.5, 0.5);
uniform float grain = 0.025;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 p = (UV - center) * vec2(aspect, 1.0);
	float light = 1.0 - smoothstep(0.0, 1.05, length(p));
	vec3 color = mix(edge_color.rgb, glow_color.rgb, light);

	// O anel: duas circunferências finas e marcas de runa girando.
	float r = length(p);
	float angle = atan(p.y, p.x) + TIME * 0.02;
	float ring = smoothstep(0.004, 0.0, abs(r - 0.46)) + smoothstep(0.003, 0.0, abs(r - 0.43));
	float ticks = step(0.94, fract(angle * 24.0 / 6.2832)) * step(abs(r - 0.445), 0.012);
	color = mix(color, rune_color.rgb, (ring + ticks) * 0.05);

	color += (hash(floor(UV * vec2(900.0, 520.0))) - 0.5) * grain;
	COLOR = vec4(color, 1.0);
}
