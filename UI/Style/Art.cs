using System.Collections.Generic;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// Onde cada imagem mora em Assets/. As imagens são os PNG que <c>Tools/art/render_png.py</c> gera
	/// dos SVG (Assets/Rendered), em vários tamanhos: cada função devolve o de 128 px, e o
	/// <see cref="Components.Doodle"/> troca pelo tamanho mais próximo do que aparece na tela
	/// (<see cref="Sized"/>) — assim nada fica serrilhado nem pesado. Sem o PNG, cai para o SVG; sem os
	/// dois, devolve nulo e a tela mostra só a cor — o jogo roda mesmo antes de baixar a arte.
	/// </summary>
	/// <summary>O símbolo de uma coisa do jogo: um desenho (ícone, efeito) ou um Glifo escrito na fonte das runas.</summary>
	public readonly record struct Symbol(Texture2D? Icon, Glyph? Rune = null)
	{
		public static Symbol Of(Glyph glyph) => new(null, glyph);
	}

	public static class Art
	{
		/// <summary>Os tamanhos renderizados, em px no lado maior.</summary>
		public static readonly int[] Sizes = { 32, 64, 128, 256, 512 };

		private const int DefaultSize = 128;
		private const string Source = "art";

		private static readonly Dictionary<string, Texture2D?> Cache = new();
		private static readonly Dictionary<string, Texture2D?> Inks = new();

		public static Texture2D? Creature(string image) => Load("Creatures", image);

		public static Texture2D? Element(Element element) => Load("Elements", element.ToString().ToLowerInvariant());

		/// <summary>O símbolo de um efeito de batalha (Assets/Effects): Queimadura, Atordoar, Ataque+...</summary>
		public static Texture2D? Effect(StatusKind status) => Load("Effects", status.ToString().ToLowerInvariant());

		/// <summary>
		/// O símbolo de uma habilidade: o do efeito que mais a define (o que ela aplica, o escudo), senão o
		/// Glifo do que ela faz (cura, Ímpeto, dreno, dano). A Passiva usa o símbolo do que ela faz.
		/// </summary>
		public static Symbol Skill(SkillDefinition skill)
		{
			// As genéricas que disparam efeitos usam o símbolo dos efeitos, como as ativas.
			if (skill.Passive is { UsesEffects: false } passive)
			{
				return passive.Kind switch
				{
					PassiveKind.SpeedWhenLowest => new Symbol(Effect(StatusKind.SpeedUp)),
					PassiveKind.DamageReduction => new Symbol(Effect(StatusKind.DefenseUp)),
					PassiveKind.CleanseAllyEachTurn => new Symbol(Effect(StatusKind.Immunity)),
					PassiveKind.RebirthOnce => Symbol.Of(RuneSets.For(RuneSet.Wrath).Glyph),
					PassiveKind.ImpetoAtWaveStart => Symbol.Of(RuneSets.For(RuneSet.Bane).Glyph),
					PassiveKind.Lifesteal => Symbol.Of(RuneSets.For(RuneSet.Siphon).Glyph),
					PassiveKind.CooldownEachTurn => Symbol.Of(RuneSets.For(RuneSet.Frenzy).Glyph),
					PassiveKind.Dodge => Symbol.Of(RuneSets.For(RuneSet.Haste).Glyph),
					_ => Symbol.Of(RuneSets.For(RuneSet.Counter).Glyph),
				};
			}

			foreach (var effect in skill.Effects)
			{
				if (effect.Kind == EffectKind.Status)
					return new Symbol(Effect(effect.Status));
			}

			foreach (var effect in skill.Effects)
			{
				switch (effect.Kind)
				{
					case EffectKind.Shield:
						return new Symbol(Effect(StatusKind.Shield));
					case EffectKind.Heal:
						return Symbol.Of(Texts.GlyphOf(Stat.Health));
					case EffectKind.Impeto:
						return Symbol.Of(RuneSets.For(RuneSet.Bane).Glyph);
					case EffectKind.Cleanse:
						return new Symbol(Effect(StatusKind.Immunity));
					case EffectKind.Damage when effect.Drain > 0:
						return Symbol.Of(RuneSets.For(RuneSet.Siphon).Glyph);
					case EffectKind.HealTeam or EffectKind.EqualizeHealth:
						return Symbol.Of(Texts.GlyphOf(Stat.Health));
					case EffectKind.Revive:
						return new Symbol(Effect(StatusKind.Revive));
					case EffectKind.StealBuff:
						return Symbol.Of(RuneSets.For(RuneSet.Bane).Glyph);
					case EffectKind.ExtraTurnOnKill:
						return Symbol.Of(RuneSets.For(RuneSet.Frenzy).Glyph);
					case EffectKind.JointAttack:
						return new Symbol(Effect(StatusKind.Counter));
				}
			}

			return Symbol.Of(Texts.GlyphOf(Stat.Attack));
		}

		/// <summary>Ícones de Assets/Icons: scroll, essence, gold, summon, config, map, bag, fight...</summary>
		public static Texture2D? Icon(string name) => Load("Icons", name);

		/// <summary>O desenho de um retrato especial (<see cref="SpecialAvatars"/>): o de Assets/Avatars, ou o ícone do padrão.</summary>
		public static Texture2D? SpecialAvatar(string id) =>
			SpecialAvatars.FileOf(id) is { } file ? Load("Avatars", file) : Icon(SpecialAvatars.DefaultIcon);

		/// <summary>O nome do desenho (<c>lock</c>, <c>fire_golem</c>), para o nó que o mostra; null se a imagem não veio daqui.</summary>
		public static string? NameOf(Texture2D? texture) =>
			texture != null && texture.HasMeta(Source) ? texture.GetMeta(Source).AsString().GetFile() : null;

		/// <summary>
		/// A mesma imagem no tamanho renderizado que melhor cobre <paramref name="pixels"/> na tela (o menor
		/// que não precisa ampliar). Imagem que não veio daqui volta como está.
		/// </summary>
		public static Texture2D? Sized(Texture2D? texture, float pixels)
		{
			if (texture == null || !texture.HasMeta(Source))
				return texture;

			var key = texture.GetMeta(Source).AsString();
			var slash = key.IndexOf('/');
			var wanted = Sizes[^1];
			foreach (var size in Sizes)
			{
				if (size >= pixels)
				{
					wanted = size;
					break;
				}
			}

			return Load(key[..slash], key[(slash + 1)..], wanted) ?? texture;
		}

		/// <summary>O ícone em branco, para tingir ao desenhar direto num <c>_Draw</c> (<see cref="Ink"/>).</summary>
		public static Texture2D? IconInk(string name) => Ink("Icons", name);

		/// <summary>O símbolo do efeito em branco, para tingir no texto rico (<see cref="Ink"/>).</summary>
		public static Texture2D? EffectInk(StatusKind status) => Ink("Effects", status.ToString().ToLowerInvariant());

		/// <summary>
		/// O desenho em branco, para tingir no texto rico: o desenho é preto, e a cor da imagem no texto só
		/// escurece. Gerado uma vez por desenho, do PNG de 64 px (o texto é pequeno), com mipmaps.
		/// </summary>
		private static Texture2D? Ink(string folder, string name)
		{
			var key = $"{folder}/{name}";
			if (Inks.TryGetValue(key, out var ink))
				return ink;

			var image = Load(folder, name, 64)?.GetImage();
			if (image != null)
			{
				image.Decompress();
				image.Convert(Image.Format.Rgba8);
				for (var y = 0; y < image.GetHeight(); y++)
				{
					for (var x = 0; x < image.GetWidth(); x++)
						image.SetPixel(x, y, new Color(1, 1, 1, image.GetPixel(x, y).A));
				}

				image.GenerateMipmaps();
			}

			ink = image == null ? null : ImageTexture.CreateFromImage(image);
			Inks[key] = ink;
			return ink;
		}

		private static Texture2D? Load(string folder, string name, int size = DefaultSize)
		{
			var path = $"res://Assets/Rendered/{folder}/{name}_{size}.png";
			if (!Cache.TryGetValue(path, out var texture))
			{
				texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
				if (texture != null)
					texture.SetMeta(Source, $"{folder}/{name}");
				else
					texture = Svg(folder, name);
				Cache[path] = texture;
			}

			return texture;
		}

		/// <summary>O SVG original, quando o PNG ainda não foi gerado.</summary>
		private static Texture2D? Svg(string folder, string name)
		{
			var path = $"res://Assets/{folder}/{name}.svg";
			return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		}
	}
}
