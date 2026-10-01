shader_type canvas_item;

uniform vec4 ink_color : source_color = vec4(0.12, 0.16, 0.27, 1.0);
uniform vec4 aura_color : source_color = vec4(1.0, 0.8, 0.4, 1.0);
uniform int element = 0;
uniform float boil_strength = 0.006;
uniform float boil_fps = 8.0;
uniform float seed = 0.0;
uniform float shrink = 1;

varying vec4 v_modulate;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
        u.y
    );
}

float fbm(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;

    for (int i = 0; i < 4; i++) {
        value += amplitude * noise(p);
        p *= 2.03;
        amplitude *= 0.5;
    }

    return value;
}

float silhouette(sampler2D tex, vec2 uv) {
    vec2 s = (uv - 0.5) / shrink + 0.5;

    if (s.x < 0.0 || s.y < 0.0 || s.x > 1.0 || s.y > 1.0) {
        return 0.0;
    }

    return texture(tex, s).a;
}

void vertex() {
    v_modulate = COLOR;
}

void fragment() {
    float frame = mod(floor(TIME * boil_fps), 3.0);

    vec2 wobble = vec2(
        sin(UV.y * 18.0 + frame * 2.1 + seed),
        cos(UV.x * 18.0 + frame * 1.7 + seed)
    ) * boil_strength;

    vec2 uv = UV + wobble;
    float body = silhouette(TEXTURE, uv);

    float inner = 1.0;

    for (int i = 0; i < 8; i++) {
        float turn = float(i) * 6.28318 / 8.0;
        inner = min(
            inner,
            silhouette(TEXTURE, uv + vec2(cos(turn), sin(turn)) * 0.014)
        );
    }

    float rim = body * (1.0 - inner);

    vec2 centered = UV - 0.5;
    float angle = atan(centered.y, centered.x);
    float radius = length(centered);

    float flow;

    if (element == 0) {
        flow = fbm(vec2(
            UV.x * 7.0,
            UV.y * 5.0 + TIME * 1.8 + seed
        ));
    } else if (element == 1) {
        flow = 0.5 + 0.5 * sin(
            UV.x * 16.0 +
            TIME * 2.2 +
            fbm(UV * 5.0 + TIME * 0.3) * 4.0
        );
    } else if (element == 2) {
        flow = fbm(vec2(
            angle * 2.0 + TIME * 1.4,
            radius * 9.0 - TIME * 0.6
        ));
    } else if (element == 3) {
        flow = 0.5 + 0.5 *
            sin(angle * 14.0 + TIME * 2.0) *
            (0.6 + 0.4 * sin(TIME * 3.0 + radius * 20.0));
    } else {
        flow = fbm(vec2(
            UV.x * 5.0 + TIME * 0.25,
            UV.y * 5.0 + TIME * 0.7 + seed
        )) * 1.2;
    }

    float glow = rim * clamp(0.45 + 0.8 * flow, 0.0, 1.0);

    // Reflexo foil diagonal metalizado (prateado puro baseado em luz branca)
    float sweep = fract((UV.x + UV.y) * 0.5 - TIME * 0.50);
    float sheen = smoothstep(0.15, 0.0, abs(sweep - 0.5)) * body;

    // Cor base do material preenchida pela cor de tinta original
    vec3 base_color = ink_color.rgb;

    // Aplica a aura apenas nas bordas (rim) usando a cor da aura escolhida
    vec3 final_rgb = mix(base_color, aura_color.rgb, glow);

    // Adiciona o reflexo prateado puro (branco brilhante) de forma aditiva sobre o foil
    vec3 silver_sheen = vec3(1.0) * sheen * 0.8;
    final_rgb += silver_sheen;

    // Montagem final preservando o canal alfa original da silhueta
    vec4 color = vec4(final_rgb, body * ink_color.a);

    COLOR = color * v_modulate;
}