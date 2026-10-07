shader_type canvas_item;

// Fundo de tela: o céu de noite visto da mesa de estudo do invocador. Índigo quase preto nas bordas,
// uma claridade violeta no centro, uma névoa de nebulosa bem apagada, pontos de estrela que piscam
// devagar e o grão de velino por cima. Atrás de tudo, um astrolábio muito apagado gira devagar
// (dois anéis, as doze casas e as marcas dos graus): dá vida sem roubar a atenção.
// O centro (em UV) é o do foco da tela — a constelação, o portal — ou o meio da tela.

uniform vec4 edge_color : source_color = vec4(0.047, 0.039, 0.086, 1.0);
uniform vec4 glow_color : source_color = vec4(0.149, 0.114, 0.251, 1.0);
uniform vec4 rune_color : source_color = vec4(0.85, 0.70, 0.41, 1.0);
uniform vec4 star_color : source_color = vec4(0.91, 0.93, 1.0, 1.0);
uniform vec4 nebula_color : source_color = vec4(0.53, 0.38, 0.81, 1.0);
uniform float aspect = 1.7778;
uniform vec2 center = vec2(0.5, 0.5);
uniform float grain = 0.02;
uniform float ring_strength = 1.0;
uniform float star_strength = 1.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

vec2 hash2(vec2 p) {
	return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 4; i++) {
		value += amplitude * noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return value;
}

// Uma camada de estrelas: no máximo uma por célula, num ponto ao acaso, e só nas células sorteadas
// (density é a fração delas). size é o raio em fração da célula; cross acende a cruz de luz das maiores.
float stars(vec2 p, float cells, float density, float size, float cross, float time) {
	vec2 g = p * cells;
	vec2 id = floor(g);
	vec2 f = fract(g);
	float h = hash(id);
	vec2 spot = hash2(id) * 0.7 + 0.15;
	vec2 d = abs(f - spot);
	float r = size * (0.6 + 0.8 * hash(id + 3.7));
	float core = smoothstep(r, 0.0, length(d));
	float rays = cross * (smoothstep(r * 0.35, 0.0, d.y) * smoothstep(r * 3.5, 0.0, d.x) + smoothstep(r * 0.35, 0.0, d.x) * smoothstep(r * 3.5, 0.0, d.y));
	float twinkle = 0.55 + 0.45 * sin(time * (0.5 + h * 1.4) + h * 40.0);
	return step(1.0 - density, h) * (core + rays * 0.5) * twinkle;
}

void fragment() {
	vec2 p = (UV - center) * vec2(aspect, 1.0);
	float r = length(p);
	float light = 1.0 - smoothstep(0.0, 1.1, r);
	vec3 color = mix(edge_color.rgb, glow_color.rgb, light);

	// A névoa: nuvens largas, violeta, que andam quase nada.
	vec2 sky = UV * vec2(aspect, 1.0);
	float cloud = fbm(sky * 2.2 + vec2(TIME * 0.004, 0.0));
	color = mix(color, nebula_color.rgb, smoothstep(0.5, 0.85, cloud) * 0.07 * star_strength);

	// As estrelas: muitas miúdas e apagadas, poucas maiores, com a cruz de luz.
	float points = stars(sky, 46.0, 0.16, 0.07, 0.0, TIME) * 0.35 + stars(sky + 0.37, 15.0, 0.08, 0.05, 1.0, TIME) * 0.6;
	color = mix(color, star_color.rgb, clamp(points, 0.0, 1.0) * star_strength * (0.55 + 0.45 * cloud));

	// O astrolábio: os anéis, as doze casas e as marcas dos graus girando.
	float angle = atan(p.y, p.x) + TIME * 0.015;
	float turn = angle / 6.2832;
	float rings = smoothstep(0.004, 0.0, abs(r - 0.46)) + smoothstep(0.003, 0.0, abs(r - 0.43)) + smoothstep(0.0025, 0.0, abs(r - 0.30)) * 0.6;
	float degrees = step(0.9, fract(turn * 72.0)) * step(abs(r - 0.445), 0.012);
	float houses = step(0.985, fract(turn * 12.0)) * step(abs(r - 0.38), 0.085);
	float dotted = step(0.75, fract(turn * 180.0)) * smoothstep(0.003, 0.0, abs(r - 0.52)) * 0.7;
	color = mix(color, rune_color.rgb, (rings + degrees + houses * 0.6 + dotted) * 0.055 * ring_strength);

	// O grão do velino.
	color += (hash(floor(UV * vec2(900.0, 520.0))) - 0.5) * grain;
	COLOR = vec4(color, 1.0);
}
