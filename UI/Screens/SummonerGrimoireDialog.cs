using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Audio;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Grimório do Invocador: o livro pessoal da conta, aberto ao tocar no retrato do Santuário. A capa de
	/// couro com duas páginas de pergaminho abertas, os marcadores dos capítulos no alto e o número de cada
	/// página embaixo:
	///
	/// - I · Invocador: o retrato num anel de sigilo, o nome na fita, o nível com a experiência (e o que
	///   ela dá) e desde quando a conta existe; na página da frente, os registros da conta;
	/// - II · Masmorras: até que andar cada uma foi, com a equipe do melhor tempo do andar mais fundo
	///   vencido (<see cref="Records.Team"/>), e, na frente, o melhor tempo de cada andar;
	/// - III · Céu: a Exploração do mês, as três faixas e o mais longe que já chegou; na frente, o céu da
	///   faixa desenhado a tinta, com o percurso do mês riscado;
	/// - IV · Selos: os marcos da jornada e da coleção (<see cref="Seals"/>), lacrados ou só riscados.
	///
	/// As páginas e o texto delas vêm das variações do tema (<see cref="GameTheme.PagePanel"/>,
	/// <see cref="GameTheme.PageRule"/>, <see cref="GameTheme.PageText"/>...). Só lê o save. Trocar o retrato fecha o livro e pede a escolha (<see cref="AvatarRequested"/>).
	/// O livro soa ao abrir, a cada capítulo virado e ao fechar.
	/// </summary>
	public sealed partial class SummonerGrimoireDialog
	{
		private const float Width = 1120;

		/// <summary>A altura mínima das páginas: os capítulos trocam sem a janela mudar de tamanho.</summary>
		private const float PageHeight = 452;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly string? _name;
		private readonly DateTime _now;
		private readonly Dialog _dialog;
		private readonly HBoxContainer _spread = new() { Name = "Spread" };
		private readonly (VBoxContainer Content, Label Folio) _left;
		private readonly (VBoxContainer Content, Label Folio) _right;
		private readonly (string Id, Action Fill)[] _chapters;

		private SummonerGrimoireDialog(Control from, GameDatabase database, PlayerState player, string? name, DateTime now)
		{
			_database = database;
			_player = player;
			_name = name;
			_now = now;
			_chapters = [("Summoner", Summoner), ("Dungeons", DungeonsChapter), ("Sky", Sky), ("Seals", SealsChapter)];

			_dialog = Dialog.Open(from, T("book.title"), Width, null, "SummonerGrimoire");
			var tabs = new TextTabs(height: 46, compact: true) { Name = "Bookmarks", Alignment = BoxContainer.AlignmentMode.Center };
			for (var i = 0; i < _chapters.Length; i++)
				tabs.Add($"{Texts.Roman(i + 1)} · {T($"book.chapter.{_chapters[i].Id}")}").Name = _chapters[i].Id;
			tabs.Changed += index =>
			{
				Sfx.Play("grimoire.page_turn");
				Open(index);
			};
			_dialog.Body.AddChild(tabs);

			_spread.AddThemeConstantOverride("separation", 10);
			_left = Page("Left");
			_right = Page("Right");
			_dialog.Body.AddChild(_spread);

			_dialog.AddAction(T("avatar.change"), () => AvatarRequested?.Invoke(), ButtonKind.Secondary, true, "avatar").Named("Avatar");
			_dialog.Closed += () => Sfx.Play("grimoire.book_close");
			Sfx.Play("grimoire.book_open");
			Open(0);
		}

		/// <summary>O jogador quer trocar o retrato (o livro já fechou).</summary>
		public event Action? AvatarRequested;

		public static SummonerGrimoireDialog Open(Control from, GameDatabase database, PlayerState player, string? name, DateTime now) =>
			new(from, database, player, name, now);

		/// <summary>Abre o capítulo <paramref name="index"/>: as duas páginas dele, com um esmaecer de página virada.</summary>
		private void Open(int index)
		{
			Layout.Clear(_left.Content);
			Layout.Clear(_right.Content);
			_left.Folio.Text = $"— {2 * index + 1} —";
			_right.Folio.Text = $"— {2 * index + 2} —";
			_chapters[index].Fill();

			_spread.Modulate = new Color(1, 1, 1, 0.4f);
			_spread.CreateTween().TweenProperty(_spread, "modulate:a", 1f, 0.18f);
		}

		/// <summary>Uma página do livro: o conteúdo em cima e o número dela embaixo.</summary>
		private (VBoxContainer Content, Label Folio) Page(string name)
		{
			var page = new PanelContainer { Name = name, ThemeTypeVariation = GameTheme.PagePanel, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, PageHeight) };
			var column = new VBoxContainer { Name = "Column" };
			var content = new VBoxContainer { Name = "Content", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
			content.AddThemeConstantOverride("separation", 8);
			column.AddChild(content);
			var folio = new Label { Name = "Folio", ThemeTypeVariation = GameTheme.PageFaded, HorizontalAlignment = HorizontalAlignment.Center };
			column.AddChild(folio);
			page.AddChild(column);
			_spread.AddChild(page);
			return (content, folio);
		}

		// I · Invocador -------------------------------------------------------------------------------

		private void Summoner()
		{
			var page = _left.Content;
			var ring = new PortraitRing { Name = "PortraitRing" };
			var portrait = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(PortraitRing.Portrait, PortraitRing.Portrait) };
			portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Panel, Palette.Gold, 3, (int)PortraitRing.Portrait / 2, 5));
			portrait.AddChild(AvatarPicker.Portrait(_database, _player));
			ring.AddChild(portrait);
			page.AddChild(ring);

			var banner = new PanelContainer { Name = "Banner", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
			banner.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Rubric, Palette.GoldDark, 2, 4, 8));
			var name = new Label { Name = "Name", Text = _name ?? T("book.unnamed"), ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(220, 0) };
			name.AddThemeColorOverride("font_color", Palette.Parchment);
			banner.AddChild(name);
			page.AddChild(banner);

			if (_player.Started is { } started)
			{
				var days = (_now.Date - started.Date).Days + 1;
				var since = $"{T("book.since", started.ToString("d", Culture))} · {(days <= 1 ? T("book.day") : T("book.days", days))}";
				page.AddChild(Centered(since, GameTheme.PageText).Named("Since"));
			}

			page.AddChild(Centered(T("book.level", _player.AccountLevel), GameTheme.PageHeading).Named("Level"));
			var maxed = _player.AccountLevel >= Account.MaxLevel;
			if (!maxed)
			{
				var next = Account.ExperienceToNext(_player.AccountLevel);
				var bar = Layout.Energy(Palette.Violet, 12).Named("Experience");
				bar.ThemeTypeVariation = GameTheme.PageBar;
				bar.MaxValue = next;
				bar.Value = _player.AccountExperience;
				page.AddChild(bar);
				page.AddChild(Centered(T("book.experience", Texts.Number(_player.AccountExperience), Texts.Number(next)), GameTheme.PageFaded).Named("Progress"));
			}

			page.AddChild(Rule());
			var tip = maxed ? T("book.max_level", _player.AccountLevel)
				: T("book.experience_tip", Account.LevelUpGold, Account.MaxLevel, Mana.BaseMax + Mana.MaxFromLevels);
			page.AddChild(Centered(tip, GameTheme.PageFaded).Named("Tip"));

			var ledger = _right.Content;
			ledger.AddChild(Heading(T("book.records"), T("book.records_hint")));
			var monsters = Seals.Monsters(_player).ToList();
			var seals = Seals.Journey(_player, _database).Concat(Seals.Collection(_player, _database)).ToList();
			ledger.AddChild(Entry("Campaign", "campaign", T("book.campaign"), T("book.of", Math.Min(_player.HighestStage, _database.Stages.Count), _database.Stages.Count)));
			ledger.AddChild(Entry("Pulls", "summon", T("book.pulls"), Texts.Number(_player.TotalPulls)));
			ledger.AddChild(Entry("Monsters", "monster", T("book.monsters"), Texts.Number(monsters.Count)));
			ledger.AddChild(Entry("Families", "grimoire", T("book.families"), T("book.of", Seals.Families(_player, _database), _database.Families.Count)));
			ledger.AddChild(Entry("Awakened", "awaken", T("book.awakened"), Texts.Number(monsters.Count(m => m.Awakened))));
			ledger.AddChild(Entry("Runes", "rune", T("book.runes"), Texts.Number(_player.Runes.Count)));
			ledger.AddChild(Entry("Furthest", "star", T("book.furthest"), T("book.of", _player.ExplorationBest, _database.Exploration.Constellations.Count)));
			ledger.AddChild(Entry("Seals", "seal", T("book.seals"), T("book.of", seals.Count(s => s.Done), seals.Count)));
		}

		// II · Masmorras ------------------------------------------------------------------------------

		private void DungeonsChapter()
		{
			var page = _left.Content;
			page.AddChild(Heading(T("book.chapter.Dungeons"), T("book.dungeons_hint")));
			foreach (var dungeon in _database.Dungeons)
			{
				var cleared = Dungeons.Cleared(_player, dungeon);
				var open = Dungeons.IsUnlocked(_player, dungeon);
				var row = Layout.Row(12).Named(Layout.NodeName(dungeon.Id));
				var art = Doodle.Icon(Art.Creature(dungeon.Image), 46, open ? Palette.Ink : Palette.InkFaded);
				row.AddChild(art);
				var text = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
				text.AddThemeConstantOverride("separation", 0);
				text.AddChild(new Label { Name = "Name", Text = dungeon.Name, ThemeTypeVariation = GameTheme.PageText });
				var detail = open ? T("book.dungeon_floors", cleared, dungeon.Floors.Count) : T("dungeons.opens_at", dungeon.UnlockStage);
				text.AddChild(new Label { Name = "Detail", Text = detail, ThemeTypeVariation = GameTheme.PageFaded });
				if (cleared > 0 && Team(Records.FloorKey(dungeon.Id, cleared)) is { } team)
					text.AddChild(team);
				row.AddChild(text);
				row.AddChild(new FloorMarks(cleared, dungeon.Floors.Count) { Name = "Floors", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
				page.AddChild(row);
			}

			var times = _right.Content;
			times.AddChild(Heading(T("book.best_times"), T("book.best_times_hint")));
			var floors = _database.Dungeons.Count == 0 ? 0 : _database.Dungeons.Max(d => d.Floors.Count);
			var table = Layout.Grid(floors + 1, 10).Named("Times");
			table.AddThemeConstantOverride("v_separation", 14);
			table.AddChild(new Control { Name = "Corner" });
			for (var floor = 1; floor <= floors; floor++)
				table.AddChild(Cell(Texts.Roman(floor), GameTheme.PageFaded).Named($"Floor{floor}"));
			foreach (var dungeon in _database.Dungeons)
			{
				var name = Layout.Text(dungeon.Name, GameTheme.PageText, 170).Named(Layout.NodeName(dungeon.Id));
				name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				name.VerticalAlignment = VerticalAlignment.Center;
				table.AddChild(name);
				for (var floor = 1; floor <= floors; floor++)
				{
					var best = floor <= dungeon.Floors.Count ? Records.Best(_player, Records.FloorKey(dungeon.Id, floor)) : null;
					var cell = best is { } seconds ? Cell(TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"), GameTheme.PageText) : Cell("·", GameTheme.PageFaded);
					table.AddChild(cell.Named($"{Layout.NodeName(dungeon.Id)}{floor}"));
				}
			}

			times.AddChild(table);
		}

		// III · Céu -----------------------------------------------------------------------------------

		private void Sky()
		{
			var exploration = _database.Exploration;
			var page = _left.Content;
			var variation = Exploration.VariationOf(_now);
			var month = exploration.Explorations.Count > 0 ? exploration.Explorations[variation % exploration.Explorations.Count].Name : T("exploration.title");
			page.AddChild(Heading(month, ExplorationScreen.NextRotation(_now)));

			var total = exploration.Constellations.Count;
			if (!Exploration.IsOpen(_player, exploration))
			{
				page.AddChild(Layout.Text(T("exploration.closed", exploration.UnlockStage), GameTheme.PageText).Named("Closed"));
				_right.Content.AddChild(Heading(T("book.sky_notes"), ""));
				_right.Content.AddChild(Layout.Text(T("book.sky_unknown"), GameTheme.PageFaded).Named("Unknown"));
				return;
			}

			var cleared = Exploration.Cleared(_player, _now);
			page.AddChild(Layout.Text(T("exploration.progress", cleared, total), GameTheme.PageText).Named("Progress"));
			var bar = Layout.Energy(Palette.Violet, 12).Named("Month");
			bar.ThemeTypeVariation = GameTheme.PageBar;
			bar.MaxValue = Math.Max(1, total);
			bar.Value = cleared;
			page.AddChild(bar);
			foreach (var hemisphere in Enum.GetValues<Hemisphere>())
			{
				var (first, last) = exploration.Range(hemisphere);
				var done = Math.Clamp(cleared - first + 1, 0, Math.Max(0, last - first + 1));
				page.AddChild(Entry(hemisphere.ToString(), $"hemisphere_{hemisphere.ToString().ToLowerInvariant()}", Texts.Name(hemisphere), T("book.of", done, last - first + 1)));
			}

			page.AddChild(Rule());
			if (_player.ExplorationBest > 0 && _player.ExplorationBest <= total)
			{
				var furthest = exploration.Constellation(_player.ExplorationBest);
				page.AddChild(Layout.Text(T("book.sky_best", _player.ExplorationBest, furthest.Name, furthest.Latin), GameTheme.PageText).Named("Best"));
			}
			else
			{
				page.AddChild(Layout.Text(T("book.sky_first"), GameTheme.PageFaded).Named("Best"));
			}

			var next = Math.Min(cleared + 1, total);
			var shown = total == 0 ? Hemisphere.Boreal : exploration.Constellation(next).Hemisphere;
			if (cleared < total)
			{
				var constellation = exploration.Constellation(next);
				page.AddChild(Layout.Text(T("book.sky_next", next, constellation.Name, constellation.Latin), GameTheme.PageText).Named("Next"));
				page.AddChild(Layout.Text(T("exploration.influence", constellation.Influence.Name), GameTheme.PageFaded).Named("Influence"));
			}

			var notes = _right.Content;
			notes.AddChild(Heading(T("book.sky_notes"), Texts.Name(shown)));
			var chart = new SkyNotes(Exploration.Of(exploration, shown).ToList(), cleared, _player.ExplorationBest, cleared < total ? next : 0)
			{
				Name = "Chart",
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 250),
			};
			notes.AddChild(chart);
			notes.AddChild(Layout.Text(T("book.sky_legend"), GameTheme.PageFaded).Named("Legend"));
		}

		// IV · Selos ----------------------------------------------------------------------------------

		private void SealsChapter()
		{
			SealPage(_left.Content, "journey", Seals.Journey(_player, _database));
			SealPage(_right.Content, "collection", Seals.Collection(_player, _database));
		}

		private static void SealPage(VBoxContainer page, string id, IReadOnlyList<Seal> seals)
		{
			page.AddChild(Heading(T($"book.seals_{id}"), T("book.of", seals.Count(s => s.Done), seals.Count)));
			foreach (var seal in seals)
			{
				var row = Layout.Row(12).Named(Layout.NodeName(seal.Id));
				row.AddChild(new SealMark(seal.Done) { Name = "Seal" });
				var text = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
				text.AddThemeConstantOverride("separation", 0);
				var title = new Label { Name = "Title", Text = T($"book.seal.{seal.Id}"), ThemeTypeVariation = seal.Done ? GameTheme.PageHeading : GameTheme.PageText };
				if (seal.Done)
					title.AddThemeFontSizeOverride("font_size", GameTheme.BodySize + 2);
				text.AddChild(title);
				var goal = T($"book.seal_goal.{seal.Id}", seal.Goal);
				if (!seal.Done && seal.Goal > 1)
					goal = $"{goal} · {Math.Min(seal.Progress, seal.Goal)}/{seal.Goal}";
				text.AddChild(new Label { Name = "Goal", Text = goal, ThemeTypeVariation = GameTheme.PageFaded, AutowrapMode = TextServer.AutowrapMode.WordSmart });
				row.AddChild(text);
				page.AddChild(row);
			}
		}

		// Peças da página -----------------------------------------------------------------------------

		/// <summary>O título do capítulo na tinta violeta, com uma nota apagada embaixo.</summary>
		private static Control Heading(string title, string note)
		{
			var column = new VBoxContainer { Name = "Heading" };
			column.AddThemeConstantOverride("separation", 0);
			column.AddChild(new Label { Name = "Title", Text = title, ThemeTypeVariation = GameTheme.PageHeading });
			if (note.Length > 0)
				column.AddChild(new Label { Name = "Note", Text = note, ThemeTypeVariation = GameTheme.PageFaded, AutowrapMode = TextServer.AutowrapMode.WordSmart });
			return column;
		}

		/// <summary>Uma linha do registro: o ícone a tinta, o nome, a linha pontilhada e o valor.</summary>
		private static Control Entry(string name, string icon, string label, string value)
		{
			var row = Layout.Row(10).Named(name);
			row.AddChild(Doodle.Icon(Art.Icon(icon), 26, Palette.Ink));
			row.AddChild(new Label { Name = "Label", Text = label, ThemeTypeVariation = GameTheme.PageText, VerticalAlignment = VerticalAlignment.Center });
			row.AddChild(new Leader { Name = "Leader", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
			row.AddChild(new Label { Name = "Value", Text = value, ThemeTypeVariation = GameTheme.PageText, VerticalAlignment = VerticalAlignment.Center });
			return row;
		}

		private static Label Centered(string text, string variation)
		{
			var label = Layout.Text(text, variation);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			return label;
		}

		private static Label Cell(string text, string variation) =>
			new() { Text = text, ThemeTypeVariation = variation, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(52, 0) };

		/// <summary>O divisor da página.</summary>
		private static HSeparator Rule() => new() { Name = "Rule", ThemeTypeVariation = GameTheme.PageRule };

		/// <summary>
		/// A equipe do recorde <paramref name="key"/> em retratos pequenos (moldura da raridade, a aura no
		/// desperto), a Líder primeiro; null quando o tempo é de antes de a equipe ser gravada.
		/// </summary>
		private Control? Team(string key)
		{
			var members = Records.Team(_player, key).Where(m => _database.HasSummon(m.Summon)).ToList();
			if (members.Count == 0)
				return null;

			var row = Layout.Row(4).Named("Team");
			foreach (var member in members)
			{
				var summon = _database.Summon(member.Summon);
				var portrait = new PanelContainer { Name = Layout.NodeName(member.Summon), CustomMinimumSize = new Vector2(TeamPortrait, TeamPortrait) };
				portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 2, (int)(TeamPortrait / 2), 2));
				portrait.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: member.Awakened ? summon.Element : null));
				row.AddChild(portrait);
			}

			return row;
		}

		private const float TeamPortrait = 30;

		// Desenhos a tinta ----------------------------------------------------------------------------

		/// <summary>O anel de sigilo em volta do retrato: dois círculos, as marcas dos graus e quatro estrelas nos pontos cardeais.</summary>
		private partial class PortraitRing : CenterContainer
		{
			public const float Portrait = 96;
			private const float Side = 136;

			public PortraitRing() => CustomMinimumSize = new Vector2(0, Side);

			public override void _Draw()
			{
				var center = Size / 2;
				var outer = Side / 2 - 4;
				var inner = outer - 10;
				DrawArc(center, outer, 0, Mathf.Tau, 96, Palette.InkFaded, 1.5f, true);
				DrawArc(center, inner, 0, Mathf.Tau, 96, new Color(Palette.Ink, 0.55f), 1, true);
				for (var i = 0; i < 36; i++)
				{
					var angle = i * Mathf.Tau / 36;
					var direction = Vector2.FromAngle(angle);
					var length = i % 9 == 0 ? 10 : i % 3 == 0 ? 6 : 3;
					DrawLine(center + direction * inner, center + direction * (inner + length), new Color(Palette.Ink, 0.5f), 1, true);
				}

				for (var i = 0; i < 4; i++)
				{
					var direction = Vector2.FromAngle(i * Mathf.Pi / 2 - Mathf.Pi / 2);
					Starlight.Sparkle(this, center + direction * (outer + 1), 6, Palette.Rubric);
					Starlight.Sparkle(this, center + Vector2.FromAngle(i * Mathf.Pi / 2 - Mathf.Pi / 4) * outer, 2.5f, Palette.InkFaded);
				}
			}
		}

		/// <summary>Os pontos que ligam o nome ao valor numa linha do registro.</summary>
		private partial class Leader : Control
		{
			private const float Step = 7;

			public Leader() => CustomMinimumSize = new Vector2(16, 0);

			public override void _Draw()
			{
				var y = Size.Y * 0.62f;
				var color = new Color(Palette.InkFaded, 0.6f);
				for (var x = 4f; x < Size.X - 4; x += Step)
					DrawCircle(new Vector2(x, y), 1.1f, color);
			}
		}

		/// <summary>Os andares de uma Masmorra em losangos: cheios até o mais fundo vencido.</summary>
		private partial class FloorMarks : Control
		{
			private const float Cell = 15;
			private readonly int _cleared;
			private readonly int _total;

			public FloorMarks(int cleared, int total)
			{
				_cleared = cleared;
				_total = total;
				CustomMinimumSize = new Vector2(Cell * total, Cell);
			}

			public override void _Draw()
			{
				for (var i = 0; i < _total; i++)
				{
					var center = new Vector2(Cell * i + Cell / 2, Size.Y / 2);
					const float r = 5.5f;
					var points = new[] { center + new Vector2(0, -r), center + new Vector2(r, 0), center + new Vector2(0, r), center + new Vector2(-r, 0), center + new Vector2(0, -r) };
					if (i < _cleared)
						DrawColoredPolygon(points[..4], Palette.Rubric);
					DrawPolyline(points, i < _cleared ? Palette.Rubric : Palette.InkFaded, 1.2f, true);
				}
			}
		}

		/// <summary>
		/// O selo de um marco: lacrado, é um lacre de cera violeta, de borda ondulada, com a estrela de ouro
		/// no meio; ainda não, só o círculo riscado a tinta, tracejado, à espera.
		/// </summary>
		private partial class SealMark : Control
		{
			private const float Side = 46;
			private readonly bool _done;

			public SealMark(bool done)
			{
				_done = done;
				CustomMinimumSize = new Vector2(Side, Side);
				SizeFlagsVertical = SizeFlags.ShrinkCenter;
			}

			public override void _Draw()
			{
				var center = Size / 2;
				var radius = Side / 2 - 2;
				if (!_done)
				{
					var color = new Color(Palette.InkFaded, 0.75f);
					for (var i = 0; i < 16; i++)
						DrawArc(center, radius - 3, i * Mathf.Tau / 16, (i + 0.55f) * Mathf.Tau / 16, 4, color, 1.2f, true);
					Starlight.Sparkle(this, center, 6, new Color(Palette.InkFaded, 0.45f));
					return;
				}

				var wax = new Vector2[48];
				for (var i = 0; i < wax.Length; i++)
				{
					var angle = i * Mathf.Tau / wax.Length;
					wax[i] = center + Vector2.FromAngle(angle) * (radius - 1.5f + 1.5f * Mathf.Cos(angle * 12));
				}

				DrawColoredPolygon(wax, Palette.Rubric);
				DrawArc(center, radius - 7, 0, Mathf.Tau, 48, Palette.Rubric.Lightened(0.3f), 1.5f, true);
				Starlight.Sparkle(this, center, 9, Palette.Gold);
				Starlight.Sparkle(this, center, 3.5f, Palette.Gold.Lightened(0.4f));
			}
		}

		/// <summary>
		/// O céu de uma faixa desenhado a tinta: a grade de ascensão reta e declinação, as constelações onde
		/// ficam no mapa da Exploração (<see cref="ConstellationDefinition.Chart"/>, deitado quando é mais alto
		/// que largo), o percurso do mês riscado entre as vencidas, as já alcançadas em violeta e a próxima
		/// num anel de ouro.
		/// </summary>
		private partial class SkyNotes : Control
		{
			private const float Pad = 16;
			private readonly List<(int Number, Vector2 At)> _stars;
			private readonly int _cleared;
			private readonly int _best;
			private readonly int _next;
			private readonly Rect2 _bounds;

			public SkyNotes(List<(int Number, ConstellationDefinition Constellation)> constellations, int cleared, int best, int next)
			{
				_stars = constellations.Select(c => (c.Number, new Vector2((float)c.Constellation.Chart[0], (float)c.Constellation.Chart[1]))).ToList();
				// O mapa da faixa é alto (rola na tela da Exploração); a página é larga: o esboço deita o mapa.
				var tall = _stars.Count > 0 && _stars.Max(s => s.At.Y) - _stars.Min(s => s.At.Y) > _stars.Max(s => s.At.X) - _stars.Min(s => s.At.X);
				if (tall)
					_stars = _stars.Select(s => (s.Number, new Vector2(s.At.Y, -s.At.X))).ToList();
				_cleared = cleared;
				_best = best;
				_next = next;
				_bounds = _stars.Count == 0 ? new Rect2(0, 0, 1, 1) : _stars.Aggregate(new Rect2(_stars[0].At, Vector2.Zero), (box, s) => box.Expand(s.At));
				Resized += QueueRedraw;
			}

			public override void _Draw()
			{
				var area = new Rect2(Pad, Pad, Size.X - 2 * Pad, Size.Y - 2 * Pad);
				var grid = new Color(Palette.Indigo, 0.28f);
				for (var i = 0; i <= 6; i++)
				{
					var x = area.Position.X + area.Size.X * i / 6;
					DrawDashedLine(new Vector2(x, area.Position.Y), new Vector2(x, area.End.Y), grid, 1, 4);
				}

				for (var i = 0; i <= 4; i++)
				{
					var y = area.Position.Y + area.Size.Y * i / 4;
					DrawDashedLine(new Vector2(area.Position.X, y), new Vector2(area.End.X, y), grid, 1, 4);
				}

				if (_stars.Count == 0)
					return;

				var scale = Mathf.Min(area.Size.X / Mathf.Max(1, _bounds.Size.X), area.Size.Y / Mathf.Max(1, _bounds.Size.Y));
				var offset = area.Position + (area.Size - _bounds.Size * scale) / 2;
				Vector2 At(Vector2 chart) => offset + (chart - _bounds.Position) * scale;

				for (var i = 1; i < _stars.Count; i++)
				{
					var (number, at) = _stars[i];
					var from = At(_stars[i - 1].At);
					if (number <= _cleared)
						DrawLine(from, At(at), Palette.Rubric, 1.6f, true);
					else
						DrawDashedLine(from, At(at), new Color(Palette.InkFaded, 0.35f), 1, 3);
				}

				foreach (var (number, chart) in _stars)
				{
					var at = At(chart);
					if (number <= _cleared)
						Starlight.Sparkle(this, at, 7, Palette.Rubric);
					else if (number <= _best)
						Starlight.Sparkle(this, at, 5.5f, Palette.Violet);
					else
						DrawArc(at, 2.6f, 0, Mathf.Tau, 12, Palette.InkFaded, 1, true);
					if (number == _next)
						DrawArc(at, 10, 0, Mathf.Tau, 32, Palette.GoldDark, 2, true);
				}
			}
		}
	}
}
