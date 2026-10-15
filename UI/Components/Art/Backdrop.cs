using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O fundo de tela (Assets/Shaders/backdrop.gdshader): o céu de noite com a claridade violeta, a névoa,
	/// as estrelas que piscam e o astrolábio girando. O centro da claridade e do astrolábio fica no meio da
	/// tela ou, com um <c>focus</c>, no centro dele — e acompanha se ele (ou quem o contém) se mexer ou a
	/// janela mudar de tamanho.
	///
	/// O shader é pesado (névoa em camadas de ruído em cada pixel da tela, todo quadro, e é o que mais custa
	/// numa GPU de celular), então ele roda só duas vezes (<see cref="BackdropBake"/>): uma para o céu liso
	/// (fundo, névoa e grão) e outra para o astrolábio, em alfa. A animação é feita por cima, sem pixel
	/// shader: o astrolábio é uma imagem que gira (só a transformação muda), as estrelas são pontos
	/// desenhados uma vez, em quatro grupos que piscam em fases diferentes (só o alfa do grupo muda), e a
	/// claridade é uma imagem radial que se desenha onde o foco estiver. Tudo anda a 20 passos por segundo.
	/// </summary>
	public partial class Backdrop : Control
	{
		/// <summary>Passos de animação por segundo: o giro é lento e o piscar é suave, mais que isso é gasto à toa.</summary>
		private const float StepsPerSecond = 20;

		/// <summary>Radianos por segundo do astrolábio (uma volta a cada sete minutos), no sentido anti-horário.</summary>
		private const float Spin = 0.015f;

		/// <summary>Quanto o astrolábio aparece: o alfa dele sobre o céu.</summary>
		private const float RingAlpha = 0.055f;

		/// <summary>Quantas alturas de tela o astrolábio cobre (o anel mais largo chega a 0,52 do centro).</summary>
		private const float RingSpan = 1.1f;

		/// <summary>Quantas alturas de tela a claridade cobre (de ponta a ponta; o shader a apaga em 1,1 do centro).</summary>
		private const float GlowSpan = 2.2f;

		private const int Groups = 4;

		private readonly Control? _focus;
		private readonly bool _ring;
		private readonly TextureRect? _astrolabe;
		private readonly StarLayer[] _stars = new StarLayer[Groups];
		private Texture2D? _sky;
		private Vector2 _center = new(0.5f, 0.5f);
		private float _accumulated;

		/// <param name="ring">Com o astrolábio girando; a batalha tira, porque o chão dela já é o oval.</param>
		public Backdrop(Control? focus, bool ring = true)
		{
			_focus = focus;
			_ring = ring;
			MouseFilter = MouseFilterEnum.Ignore;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			Resized += Arrange;

			for (var group = 0; group < Groups; group++)
			{
				_stars[group] = new StarLayer(group) { Name = $"Stars{group}" };
				AddChild(_stars[group]);
			}

			if (ring)
			{
				_astrolabe = new TextureRect
				{
					Name = "Astrolabe",
					MouseFilter = MouseFilterEnum.Ignore,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.Scale,
					TextureFilter = TextureFilterEnum.Linear,
					Modulate = new Color(1, 1, 1, RingAlpha),
					Visible = false,
				};
				AddChild(_astrolabe);
				// Atrás das estrelas.
				MoveChild(_astrolabe, 0);
			}

			SetProcess(false);
		}

		public override void _Ready()
		{
			BackdropBake.Sky(GetTree(), texture =>
			{
				if (!IsInstanceValid(this))
					return;
				_sky = texture;
				QueueRedraw();
			});

			if (_astrolabe != null)
			{
				BackdropBake.Ring(GetTree(), texture =>
				{
					if (!IsInstanceValid(this) || _astrolabe == null)
						return;
					_astrolabe.Texture = texture;
					_astrolabe.Visible = true;
				});
			}

			Follow();
			Animate();
			SetProcess(IsVisibleInTree());
		}

		public override void _Notification(int what)
		{
			if (what == NotificationVisibilityChanged)
				SetProcess(IsVisibleInTree());
		}

		public override void _Process(double delta)
		{
			_accumulated += (float)delta;
			if (_accumulated < 1 / StepsPerSecond)
				return;

			_accumulated = 0;
			Follow();
			Animate();
		}

		public override void _Draw()
		{
			DrawRect(new Rect2(Vector2.Zero, Size), Palette.Background);
			if (_sky != null)
				DrawTextureRect(_sky, new Rect2(Vector2.Zero, Size), false);

			// A claridade violeta, centrada no foco: uma imagem radial no tamanho certo, que se desenha onde ele estiver.
			var side = GlowSpan * Size.Y;
			var middle = _center * Size;
			DrawTextureRect(BackdropBake.Glow, new Rect2(middle - new Vector2(side, side) / 2, new Vector2(side, side)), false);
		}

		/// <summary>Põe o centro no foco (em UV do fundo); sem foco, ou com ele ainda fora da tela, no meio.</summary>
		private void Follow()
		{
			var center = new Vector2(0.5f, 0.5f);
			if (_focus != null && IsInstanceValid(_focus) && _focus.IsInsideTree() && Size.X > 0 && Size.Y > 0)
				center = (_focus.GetGlobalRect().GetCenter() - GetGlobalRect().Position) / Size;

			if (center.IsEqualApprox(_center))
				return;

			_center = center;
			Arrange();
			QueueRedraw();
		}

		/// <summary>Põe o astrolábio, do tamanho de <see cref="RingSpan"/> alturas, em volta do centro.</summary>
		private void Arrange()
		{
			if (_astrolabe == null)
				return;

			var side = RingSpan * Size.Y;
			_astrolabe.Size = new Vector2(side, side);
			_astrolabe.PivotOffset = new Vector2(side, side) / 2;
			_astrolabe.Position = _center * Size - new Vector2(side, side) / 2;
		}

		/// <summary>O giro do astrolábio e o piscar de cada grupo de estrelas, pelo relógio (as telas seguem a mesma fase).</summary>
		private void Animate()
		{
			var time = Time.GetTicksMsec() / 1000.0;
			if (_astrolabe != null)
				_astrolabe.Rotation = (float)(-(time * Spin) % Mathf.Tau);

			for (var group = 0; group < Groups; group++)
			{
				// Da mais lenta (0,5 rad/s) à mais rápida (1,9 rad/s), cada uma numa fase: o céu parece vivo, não pulsa junto.
				var twinkle = 0.55 + 0.45 * Math.Sin(time * (0.5 + group * 0.47) + group * 1.9);
				_stars[group].Modulate = new Color(1, 1, 1, (float)twinkle);
			}
		}

		/// <summary>
		/// Um grupo de estrelas, desenhadas uma vez (de novo só se a tela muda de tamanho): as miúdas e, bem
		/// mais raras, as maiores com a cruz de luz. A posição é sorteada com semente fixa, então o céu é o
		/// mesmo em toda tela e em toda abertura.
		/// </summary>
		private partial class StarLayer : Control
		{
			/// <summary>Estrelas miúdas e maiores, no total (os grupos dividem).</summary>
			private const int Small = 380;
			private const int Big = 32;

			/// <summary>A altura de tela em que os raios abaixo foram pensados.</summary>
			private const float Reference = 1080;

			private static readonly List<(Vector2 Uv, float Radius, bool Big)>[] Fields = Sort();

			private readonly int _group;

			public StarLayer(int group)
			{
				_group = group;
				MouseFilter = MouseFilterEnum.Ignore;
				SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				Resized += QueueRedraw;
			}

			/// <summary>
			/// Cada estrela é um retângulo com a mesma imagem (o ponto, ou a cruz), então o grupo todo vai
			/// em duas chamadas de desenho em vez de uma por estrela.
			/// </summary>
			public override void _Draw()
			{
				var scale = Size.Y / Reference;
				var dim = new Color(Palette.Starlight, 0.5f);
				var bright = new Color(Palette.Starlight, 0.95f);
				foreach (var (uv, radius, big) in Fields[_group])
				{
					var at = uv * Size;
					var r = radius * scale;
					if (big)
					{
						var arm = Math.Max(5f, r * 4.5f);
						DrawTextureRect(BackdropBake.Cross, new Rect2(at - new Vector2(arm, arm), new Vector2(arm, arm) * 2), false, bright);
					}
					else
					{
						var half = Math.Max(1.2f, r * 1.4f);
						DrawTextureRect(BackdropBake.Dot, new Rect2(at - new Vector2(half, half), new Vector2(half, half) * 2), false, dim);
					}
				}
			}

			private static List<(Vector2, float, bool)>[] Sort()
			{
				var fields = new List<(Vector2, float, bool)>[Groups];
				for (var group = 0; group < Groups; group++)
					fields[group] = new List<(Vector2, float, bool)>();

				var random = new Random(4172);
				for (var i = 0; i < Small + Big; i++)
				{
					var big = i >= Small;
					var uv = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
					var radius = big ? 2.2f + (float)random.NextDouble() * 1.6f : 0.9f + (float)random.NextDouble() * 0.9f;
					fields[i % Groups].Add((uv, radius, big));
				}

				return fields;
			}
		}
	}

	/// <summary>
	/// Desenha o shader do fundo uma vez, em imagens, e guarda: o céu liso (1024 px de largura, na proporção
	/// de 16:9; a placa de vídeo o estica para outras telas, e ele é só degradê e névoa) e o astrolábio (um
	/// quadrado em alfa). Servem a toda tela que os pede. Mais a claridade, uma imagem radial feita no código.
	/// </summary>
	public static class BackdropBake
	{
		private const int Width = 1024;
		private const int RingSide = 1024;
		private const float Aspect = 16f / 9f;

		private static readonly Baked SkyImage = new();
		private static readonly Baked RingImage = new();
		private static Shader? _shader;
		private static Texture2D? _glow;
		private static Texture2D? _dot;
		private static Texture2D? _cross;

		/// <summary>A claridade violeta: alfa de 1 no centro a 0 em 1,1 alturas de tela, a mesma curva do shader.</summary>
		public static Texture2D Glow => _glow ??= MakeGlow();

		/// <summary>O ponto de estrela: branco, alfa de 1 no meio a 0 na borda.</summary>
		public static Texture2D Dot => _dot ??= MakeStar(16, cross: false);

		/// <summary>A estrela maior: o ponto e a cruz de luz, que afina até as pontas.</summary>
		public static Texture2D Cross => _cross ??= MakeStar(64, cross: true);

		public static void Sky(SceneTree tree, Action<Texture2D> ready) =>
			Get(tree, SkyImage, ready, layer: 1, new Vector2I(Width, (int)MathF.Round(Width / Aspect)), transparent: false);

		public static void Ring(SceneTree tree, Action<Texture2D> ready) =>
			Get(tree, RingImage, ready, layer: 2, new Vector2I(RingSide, RingSide), transparent: true);

		private static Texture2D MakeStar(int side, bool cross)
		{
			var image = Image.CreateEmpty(side, side, false, Image.Format.Rgba8);
			for (var y = 0; y < side; y++)
			{
				for (var x = 0; x < side; x++)
				{
					// Em unidades de meia imagem, de -1 a 1.
					var dx = ((x + 0.5f) / side - 0.5f) * 2;
					var dy = ((y + 0.5f) / side - 0.5f) * 2;
					var r = MathF.Sqrt(dx * dx + dy * dy);
					float alpha;
					if (cross)
					{
						var core = Math.Clamp(1f - r / 0.28f, 0f, 1f);
						var armX = MathF.Pow(Math.Clamp(1f - MathF.Abs(dx), 0f, 1f), 2.2f) * MathF.Exp(-MathF.Pow(dy / 0.045f, 2));
						var armY = MathF.Pow(Math.Clamp(1f - MathF.Abs(dy), 0f, 1f), 2.2f) * MathF.Exp(-MathF.Pow(dx / 0.045f, 2));
						alpha = Math.Clamp(core * core * (3f - 2f * core) + (armX + armY) * 0.55f, 0f, 1f);
					}
					else
					{
						var t = Math.Clamp(1f - r, 0f, 1f);
						alpha = t * t * (3f - 2f * t);
					}

					image.SetPixel(x, y, new Color(1, 1, 1, alpha));
				}
			}

			return ImageTexture.CreateFromImage(image);
		}

		private static Texture2D MakeGlow()
		{
			const int side = 128;
			var image = Image.CreateEmpty(side, side, false, Image.Format.Rgba8);
			var color = Palette.BackgroundGlow;
			for (var y = 0; y < side; y++)
			{
				for (var x = 0; x < side; x++)
				{
					var dx = (x + 0.5f) / side - 0.5f;
					var dy = (y + 0.5f) / side - 0.5f;
					var r = MathF.Sqrt(dx * dx + dy * dy) * 2.2f;
					var t = Math.Clamp(r / 1.1f, 0f, 1f);
					var light = 1f - t * t * (3f - 2f * t);
					image.SetPixel(x, y, new Color(color, light));
				}
			}

			return ImageTexture.CreateFromImage(image);
		}

		private static void Get(SceneTree tree, Baked slot, Action<Texture2D> ready, int layer, Vector2I size, bool transparent)
		{
			if (slot.Texture != null)
			{
				ready(slot.Texture);
				return;
			}

			slot.Waiting.Add(ready);
			if (slot.Waiting.Count == 1)
				Render(tree, slot, layer, size, transparent);
		}

		private static async void Render(SceneTree tree, Baked slot, int layer, Vector2I size, bool transparent)
		{
			// Pedido de dentro do _Ready de uma tela: a raiz ainda está montando os filhos, então espera o quadro.
			await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

			var viewport = new SubViewport
			{
				Name = "BackdropBake",
				Size = size,
				RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
				TransparentBg = transparent,
				Disable3D = true,
			};

			_shader ??= GD.Load<Shader>("res://Assets/Shaders/backdrop.gdshader");
			var material = new ShaderMaterial { Shader = _shader };
			material.SetShaderParameter("edge_color", Palette.Background);
			material.SetShaderParameter("glow_color", Palette.BackgroundGlow);
			material.SetShaderParameter("rune_color", Palette.Gold);
			material.SetShaderParameter("star_color", Palette.Starlight);
			material.SetShaderParameter("nebula_color", Palette.Violet);
			material.SetShaderParameter("aspect", (float)size.X / size.Y);
			material.SetShaderParameter("layer", layer);
			material.SetShaderParameter("span", 1.1f);
			var surface = new ColorRect { Color = Palette.Background, Material = material };
			surface.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			viewport.AddChild(surface);
			tree.Root.AddChild(viewport);

			// Uma vez desenhado (o desenho entra no quadro seguinte), lê a imagem e solta a janela de desenho.
			await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

			Texture2D? texture = null;
			if (GodotObject.IsInstanceValid(viewport))
			{
				var image = viewport.GetTexture().GetImage();
				if (image != null && !image.IsEmpty())
					texture = ImageTexture.CreateFromImage(image);
				viewport.QueueFree();
			}

			var waiting = slot.Waiting.ToArray();
			slot.Waiting.Clear();
			if (texture == null)
				return;

			slot.Texture = texture;
			foreach (var callback in waiting)
				callback(texture);
		}

		private sealed class Baked
		{
			public Texture2D? Texture;
			public readonly List<Action<Texture2D>> Waiting = new();
		}
	}
}
