using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Aprimorar um sigilo, numa janela com as abas em pé à esquerda e a ficha dele no meio:
	///
	/// - Melhorar: o nível a alcançar (−/+ e os marcos +3 a +15), o que muda no principal, quantos
	///   subatributos chegam ou sobem, o custo e o botão.
	/// - Afiar e Encantar: as pedras que servem ficam no fim da linha de cada subatributo, na ficha.
	/// - Reavaliar: o que a Gema de Reavaliação desfaz, e o botão (pergunta antes).
	///
	/// E a melhoria seletiva (Melhorar, com sigilos marcados): os marcados à esquerda, o mesmo nível a
	/// alcançar à direita, quantos sobem e o custo. Sobem os que estão abaixo do nível, todos ou nenhum.
	/// </summary>
	public partial class RuneScreen
	{
		private const float UpgradeWidth = 1000;
		private const float UpgradeCardWidth = 380;
		private const float TabsWidth = 150;

		/// <summary>A altura da página de Aprimorar: a da ficha mais alta (4 subatributos, cada um com o botão da pedra).</summary>
		private const float UpgradePageHeight = 380;

		private const float StepperWidth = 64;
		private const float ToolButtonWidth = 120;

		/// <summary>A coluna da direita da janela de Aprimorar: o que sobra das abas e da ficha.</summary>
		private const float UpgradeSideWidth = UpgradeWidth - DialogInset - TabsWidth - UpgradeCardWidth - 2 * Space.Loose;

		private const float BulkWidth = 940;

		/// <summary>A grade dos marcados: 6 pedras por linha e 5 linhas à vista (16 é a barra de rolagem).</summary>
		private const float BulkGridWidth = 6 * RuneTile.Side + 5 * Space.Small + 16;

		private const float BulkGridHeight = 5 * RuneTile.Side + 4 * Space.Small;
		private const float BulkSideWidth = BulkWidth - DialogInset - BulkGridWidth - Space.Loose;

		/// <summary>Os níveis da escolha rápida, os mesmos dos marcos.</summary>
		private static readonly int[] Milestones = { 3, 6, 9, 12, 15 };

		/// <summary>A janela de Aprimorar, se aberta, o sigilo e a aba dela, e o nível a alcançar em Melhorar.</summary>
		private Dialog? _upgrade;
		private int? _upgradeRune;
		private int _upgradeTab;
		private int _upgradeTarget;
		private VBoxContainer? _upgradeCard;
		private VBoxContainer? _upgradeSide;

		/// <summary>A ficha à vista na janela e a aba em que ela foi montada (cada aba tem os botões dela na ficha).</summary>
		private RuneCard? _upgradeShown;
		private int _upgradeShownTab = -1;

		/// <summary>A janela da melhoria seletiva, se aberta, e o nível a alcançar nela.</summary>
		private Dialog? _bulk;
		private int _bulkTarget;

		private void OpenUpgrade(Rune rune)
		{
			_upgrade?.Close();
			var dialog = Dialog.Open(this, T("runes.improve"), UpgradeWidth, null, "UpgradeDialog");
			dialog.Closed += () =>
			{
				if (_upgrade != dialog)
					return;
				_upgrade = null;
				_upgradeRune = null;
				_upgradeShown = null;
			};

			// Altura fixa: a janela não pula ao trocar de aba (a ficha com botões de pedra é a mais alta).
			var page = Layout.Row(Space.Loose).Named("Page");
			page.CustomMinimumSize = new Vector2(0, UpgradePageHeight);
			var tabs = new TextTabs(vertical: true, height: 48) { Name = "Tabs", CustomMinimumSize = new Vector2(TabsWidth, 0), SizeFlagsVertical = SizeFlags.ShrinkBegin };
			tabs.Add(T("runes.upgrade")).Name = "Upgrade";
			tabs.Add(T("runes.grind")).Name = "Grind";
			tabs.Add(T("runes.enchant")).Name = "Enchant";
			tabs.Add(T("runes.reappraise")).Name = "Reappraise";
			tabs.Select(0);
			tabs.Changed += index =>
			{
				_upgradeTab = index;
				RefreshUpgrade();
			};
			page.AddChild(tabs);

			_upgradeCard = new VBoxContainer { Name = "Card", CustomMinimumSize = new Vector2(UpgradeCardWidth, 0) };
			page.AddChild(_upgradeCard);
			_upgradeSide = new VBoxContainer { Name = "Side", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_upgradeSide.AddThemeConstantOverride("separation", Space.Large);
			page.AddChild(_upgradeSide);
			dialog.Body.AddChild(page);

			_upgrade = dialog;
			_upgradeRune = rune.Id;
			_upgradeTab = 0;
			_upgradeShown = null;
			_upgradeTarget = RuneRules.NextMilestone(rune.Level);
			RefreshUpgrade();
		}

		/// <summary>
		/// Refaz a janela de Aprimorar com o sigilo como está agora. A ficha que está mostrando a melhora termina o
		/// que começou: um refresh no meio dela não a troca por uma nova, que cortaria a animação.
		/// </summary>
		private void RefreshUpgrade()
		{
			if (_upgrade == null || _upgradeCard == null || _upgradeSide == null)
				return;
			if (_player.Runes.FirstOrDefault(r => r.Id == _upgradeRune) is not { } rune)
			{
				_upgrade.Close();
				return;
			}

			if (_upgradeTarget <= rune.Level)
				_upgradeTarget = RuneRules.NextMilestone(rune.Level);

			var running = _upgradeShown != null && IsInstanceValid(_upgradeShown) && _upgradeShownTab == _upgradeTab && _upgradeShown.Animating && _upgradeShown.Shows(rune);
			if (!running)
			{
				Layout.Clear(_upgradeCard);
				// Refeita no mesmo quadro do pedido de melhora (a tela pode se refazer mais de uma vez nele).
				int? animateFrom = _upgrading is { } upgrading && upgrading.Rune == rune.Id && Engine.GetProcessFrames() - upgrading.Frame <= 1 ? upgrading.Level : null;
				RuneToolKind? kind = _upgradeTab switch
				{
					1 => RuneToolKind.Grindstone,
					2 => RuneToolKind.Gem,
					_ => null,
				};
				_upgradeShown = new RuneCard(rune, UpgradeCardWidth, Opened(rune), kind is { } tools ? index => SubstatTools(rune, index, tools) : null, animateFrom: animateFrom) { Name = "Rune" };
				_upgradeShownTab = _upgradeTab;
				_upgradeCard.AddChild(_upgradeShown);
			}

			Layout.Clear(_upgradeSide);
			switch (_upgradeTab)
			{
				case 0:
					UpgradeSide(_upgradeSide, rune);
					break;
				case 1:
					ForgeSide(_upgradeSide, rune, RuneToolKind.Grindstone);
					break;
				case 2:
					ForgeSide(_upgradeSide, rune, RuneToolKind.Gem);
					break;
				default:
					ReappraiseSide(_upgradeSide, rune);
					break;
			}
		}

		/// <summary>Melhorar: o nível a alcançar, o que muda ao chegar nele, o custo e o botão.</summary>
		private void UpgradeSide(VBoxContainer side, Rune rune)
		{
			var maxed = rune.Level >= RuneRules.MaxLevel;
			var target = maxed ? RuneRules.MaxLevel : _upgradeTarget;
			side.AddChild(TargetControls(T("runes.upgrade"), target,
				value => maxed ? T("runes.why_max", RuneRules.MaxLevel) : value <= rune.Level ? T("runes.why_target", rune.Level) : null,
				value =>
				{
					_upgradeTarget = value;
					RefreshUpgrade();
				}));

			var preview = new PanelContainer { Name = "Preview", ThemeTypeVariation = GameTheme.InsetPanel };
			var lines = new VBoxContainer { Name = "Lines" };
			lines.AddThemeConstantOverride("separation", Space.Small);
			lines.AddChild(ChangeLine(Texts.Label(rune.Main), Texts.Amount(rune.Main, rune.MainValue), Texts.Amount(rune.Main, RuneRules.MainValue(rune.Main, rune.Grade, target))));
			var (added, rolls) = SubstatGains(rune, target);
			lines.AddChild(Layout.Text(T("runes.upgrade_substats", added, rolls), GameTheme.Faded, UpgradeSideWidth - 2 * Space.Large).Named("Substats"));
			preview.AddChild(lines);
			side.AddChild(preview);

			var cost = RuneRules.UpgradeCost(rune, target);
			side.AddChild(CostLine(cost));
			side.AddChild(GameButton.Of(T("runes.upgrade"), () => _ = Upgrade(rune, target), ButtonKind.Primary, "level_max", ActionHeight)
				.Block(maxed ? T("runes.why_max", RuneRules.MaxLevel) : _player.Essence < cost ? T("runes.why_essence") : null, T("runes.upgrade"))
				.FixedHeight().Named("Upgrade"));
		}

		/// <summary>
		/// O nível a alcançar: − e + de um em um, com o nível no meio, e os marcos numa fileira. O que não dá para
		/// escolher fica apagado, com o motivo de <paramref name="why"/>.
		/// </summary>
		private static Control TargetControls(string title, int target, Func<int, string?> why, Action<int> set)
		{
			string? Reason(int value) => value > RuneRules.MaxLevel ? T("runes.why_max", RuneRules.MaxLevel) : why(value);

			var column = new VBoxContainer { Name = "Target" };
			column.AddThemeConstantOverride("separation", Space.Medium);
			var stepper = Layout.Row(Space.Medium).Named("Stepper");
			var less = GameButton.Of("−", () => set(target - 1), ButtonKind.Secondary, null, ActionHeight).Wide(StepperWidth)
				.Block(Reason(target - 1), title).FixedHeight().Named("Less");
			less.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			stepper.AddChild(less);
			stepper.AddChild(new Label { Name = "Level", Text = $"+{target}", ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var more = GameButton.Of("+", () => set(target + 1), ButtonKind.Secondary, null, ActionHeight).Wide(StepperWidth)
				.Block(Reason(target + 1), title).FixedHeight().Named("More");
			more.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			stepper.AddChild(more);
			column.AddChild(stepper);

			var quick = Layout.Grid(Milestones.Length, Space.Small).Named("Quick");
			foreach (var level in Milestones)
			{
				quick.AddChild(GameButton.Of($"+{level}", () => set(level), level == target ? ButtonKind.Primary : ButtonKind.Secondary, null, 40)
					.Block(Reason(level), title).FixedHeight().Named($"To{level}"));
			}

			column.AddChild(quick);
			return column;
		}

		/// <summary>Uma linha de antes e depois: o nome, o valor de agora e, depois da seta, o novo em verde.</summary>
		private static Control ChangeLine(string name, string now, string next)
		{
			var row = Layout.Row(Space.Medium).Named("Main");
			row.AddChild(new Label { Name = "Name", Text = name, CustomMinimumSize = new Vector2(10, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart });
			row.AddChild(new Label { Name = "Now", Text = now, ThemeTypeVariation = GameTheme.Number });
			row.AddChild(new Label { Name = "Arrow", Text = "▸", ThemeTypeVariation = GameTheme.Faded });
			var after = new Label { Name = "Next", Text = next, ThemeTypeVariation = GameTheme.Number };
			if (next != now)
				after.AddThemeColorOverride("font_color", Palette.Positive);
			row.AddChild(after);
			return row;
		}

		/// <summary>O custo e quanto o jogador tem; em vermelho, se não dá.</summary>
		private Control CostLine(int cost)
		{
			var line = new Label { Name = "Cost", Text = T("runes.upgrade_cost", Texts.Number(cost), Texts.Number(_player.Essence)), CustomMinimumSize = new Vector2(10, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
			if (_player.Essence < cost)
				line.AddThemeColorOverride("font_color", Palette.Negative);
			return line;
		}

		/// <summary>
		/// Os subatributos que a melhora até <paramref name="target"/> traz: cada marco (+3, +6, +9, +12) dá um novo
		/// enquanto há lugar, e depois sorteia um dos que já existem para subir.
		/// </summary>
		private static (int New, int Rolls) SubstatGains(Rune rune, int target)
		{
			var count = rune.Substats.Count;
			var added = 0;
			var rolls = 0;
			for (var level = rune.Level + 1; level <= Math.Min(target, RuneRules.MaxLevel); level++)
			{
				if (!RuneRules.IsMilestone(level))
					continue;
				if (count < RuneRules.SubstatsAt(level))
				{
					count++;
					added++;
				}
				else
				{
					rolls++;
				}
			}

			return (added, rolls);
		}

		/// <summary>Afiar e Encantar: as pedras do tipo que servem em algum subatributo do sigilo (os botões ficam na ficha).</summary>
		private void ForgeSide(VBoxContainer side, Rune rune, RuneToolKind kind)
		{
			var fits = Tools(kind, tool => Enumerable.Range(0, rune.Substats.Count).Any(index => Fits(rune, index, tool)));
			if (fits.Count == 0)
			{
				side.AddChild(Layout.Text(T(kind == RuneToolKind.Grindstone ? "runes.why_no_grind" : "runes.why_no_enchant", RuneForge.EnchantLevel), GameTheme.Faded, UpgradeSideWidth).Named("Empty"));
				return;
			}

			var list = new VBoxContainer { Name = "Tools" };
			list.AddThemeConstantOverride("separation", Space.Small);
			for (var i = 0; i < fits.Count; i++)
				list.AddChild(ToolRow(fits[i].Tool, fits[i].Count).Named($"Tool{i + 1}"));
			side.AddChild(list);
		}

		private static bool Fits(Rune rune, int index, RuneTool tool) =>
			tool.Kind == RuneToolKind.Grindstone ? RuneForge.CanGrind(rune, index, tool) : RuneForge.CanEnchant(rune, index, tool);

		/// <summary>O botão das pedras do tipo que servem num subatributo, no fim da linha dele na ficha.</summary>
		private IEnumerable<Control> SubstatTools(Rune rune, int index, RuneToolKind kind)
		{
			var tools = _player.Tools.Distinct().Where(t => t.Kind == kind && Fits(rune, index, t)).OrderByDescending(t => t.Grade).ToList();
			if (tools.Count == 0)
				yield break;
			if (kind == RuneToolKind.Grindstone)
				yield return ToolButton(T("runes.grind"), "grindstone", tools, tool => GrindRequested?.Invoke(rune.Id, index, tool)).Named("Grind");
			else
				yield return ToolButton(T("runes.enchant"), "gem", tools, tool => EnchantRequested?.Invoke(rune.Id, index, tool)).Named("Enchant");
		}

		/// <summary>O botão da pedra: abre a lista das pedras que servem neste subatributo.</summary>
		private static GameButton ToolButton(string text, string icon, IReadOnlyList<RuneTool> tools, Action<RuneTool> chosen)
		{
			// Largura fixa, encostado à direita: na fileira do subatributo, todos os botões ficam alinhados.
			var button = new GameButton(text, ButtonKind.Secondary, icon, 40).Wide(ToolButtonWidth);
			button.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			button.Pressed += () => Choices.Open(button, text,
				tools.Select(t => new Choice($"{Texts.Name(t)} ({Texts.Range(t)})", Art.Icon(icon), Palette.Of(t.Grade))).ToList(),
				-1, index => chosen(tools[index]));
			return button;
		}

		/// <summary>
		/// Reavaliar: o que a gema vai desfazer e o botão, sempre no lugar. Num sigilo que não mudou desde que caiu,
		/// ou sem gema, o botão fica apagado e diz o motivo; com, pergunta antes.
		/// </summary>
		private void ReappraiseSide(VBoxContainer side, Rune rune)
		{
			var changes = RuneReappraisal.Preview(rune);
			var gems = _player.ReappraisalGems;
			side.AddChild(Layout.Text(changes.Any ? ReappraisalText(changes, gems) : T("runes.why_no_reappraise"), changes.Any ? null : GameTheme.Faded, UpgradeSideWidth).Named("Changes"));
			side.AddChild(GameButton.Of(T("runes.reappraise"), () => Dialog.Confirm(this, T("runes.reappraise_title"), ReappraisalText(changes, gems),
					T("runes.reappraise_button"), () => ReappraiseRequested?.Invoke(rune.Id), ButtonKind.Danger), ButtonKind.Danger, "gem", ActionHeight)
				.Block(!changes.Any ? T("runes.why_no_reappraise") : gems == 0 ? T("runes.reappraise_none") : null, T("runes.reappraise_title"))
				.FixedHeight().Named("Reappraise"));
		}

		/// <summary>O que a gema vai desfazer, uma linha por mudança, e o custo.</summary>
		private static string ReappraisalText(ReappraisalChanges changes, int gems)
		{
			var lines = new List<string>();
			if (changes.Level > 0)
				lines.Add(T("runes.reappraise_level", changes.Level));
			if (changes.RemovedSubstats.Count > 0)
				lines.Add(T("runes.reappraise_removed", string.Join(", ", changes.RemovedSubstats.Select(Texts.Name))));
			if (changes.ExtraRolls > 0)
				lines.Add(T("runes.reappraise_rolls", changes.ExtraRolls));
			if (changes.Grinds > 0)
				lines.Add(T("runes.reappraise_grinds", changes.Grinds));
			if (changes.Enchant is { } enchant)
				lines.Add(T("runes.reappraise_enchant", Texts.Name(enchant.To), Texts.Name(enchant.From)));
			if (changes.EnchantKept)
				lines.Add(T("runes.reappraise_enchant_kept"));
			return string.Join("\n", lines.Select(line => "• " + line)) + "\n\n" + T("runes.reappraise_cost", gems);
		}

		/// <summary>Pede a melhora. A ficha do sigilo, refeita logo em seguida, mostra os níveis subindo.</summary>
		private async System.Threading.Tasks.Task Upgrade(Rune rune, int target)
		{
			if (UpgradeRequested != null)
				await UpgradeRequested.Invoke(rune.Id, target);
		}

		/// <summary>
		/// O sigilo vai ser (ou acabou de ser) melhorado a partir de <paramref name="fromLevel"/>, por aqui ou por uma
		/// janela por cima (a Batalha automática): a ficha dele, refeita no mesmo quadro, mostra os níveis subindo.
		/// </summary>
		public void Upgraded(int runeId, int fromLevel) => _upgrading = (runeId, fromLevel, Engine.GetProcessFrames());

		// Melhoria seletiva -----------------------------------------------------------------------

		private List<Rune> MarkedRunes() => _player.Runes.Where(r => _marked.Contains(r.Id)).ToList();

		private void OpenBulk()
		{
			_bulk?.Close();
			var marked = MarkedRunes();
			if (marked.Count == 0)
				return;

			_bulkTarget = RuneRules.NextMilestone(marked.Min(r => r.Level));
			var dialog = Dialog.Open(this, T("runes.bulk_title"), BulkWidth, null, "BulkDialog");
			dialog.Closed += () =>
			{
				if (_bulk == dialog)
					_bulk = null;
			};
			_bulk = dialog;
			RefreshBulk();
		}

		/// <summary>
		/// Refaz a melhoria seletiva: os marcados à esquerda (apagados os que já estão no nível), o nível a alcançar,
		/// quantos sobem e o custo à direita. A janela fica aberta depois de melhorar, para o próximo nível.
		/// </summary>
		private void RefreshBulk()
		{
			if (_bulk == null)
				return;
			var marked = MarkedRunes();
			if (marked.Count == 0)
			{
				_bulk.Close();
				return;
			}

			// Depois de melhorar, nenhum marcado fica abaixo do alvo: ele passa ao próximo marco, como na melhora de um sigilo.
			var lowest = marked.Min(r => r.Level);
			if (lowest >= _bulkTarget && lowest < RuneRules.MaxLevel)
				_bulkTarget = RuneRules.NextMilestone(lowest);

			Layout.Clear(_bulk.Body);
			var page = Layout.Row(Space.Loose).Named("Page");
			var grid = new TileGrid(Space.Small) { Name = "Grid" };
			var scroll = Layout.Scroll(grid);
			scroll.CustomMinimumSize = new Vector2(BulkGridWidth, BulkGridHeight);
			grid.Virtualize(marked.Count, RuneTile.TileSize, index => BulkTile(marked[index]));
			page.AddChild(scroll);

			var side = new VBoxContainer { Name = "Side", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			side.AddThemeConstantOverride("separation", Space.Large);
			var target = _bulkTarget;
			side.AddChild(TargetControls(T("runes.bulk_title"), target,
				value => marked.Any(r => r.Level < value) ? null : T("runes.why_bulk_none", value),
				value =>
				{
					_bulkTarget = value;
					RefreshBulk();
				}));

			var rising = marked.Where(r => r.Level < target).ToList();
			var cost = rising.Sum(r => RuneRules.UpgradeCost(r, target));
			side.AddChild(Layout.Text(T("runes.bulk_count", rising.Count, marked.Count), null, BulkSideWidth).Named("Count"));
			side.AddChild(CostLine(cost));
			side.AddChild(GameButton.Of(T("runes.upgrade"), () => UpgradeManyRequested?.Invoke(rising.Select(r => r.Id).ToList(), target), ButtonKind.Primary, "level_max", ActionHeight)
				.Block(rising.Count == 0 ? T("runes.why_bulk_none", target) : _player.Essence < cost ? T("runes.why_essence") : null, T("runes.upgrade"))
				.FixedHeight().Named("Upgrade"));
			page.AddChild(side);
			_bulk.Body.AddChild(page);
		}

		/// <summary>Um marcado na grade da melhoria seletiva: apagado se já está no nível; o toque abre a ficha.</summary>
		private RuneTile BulkTile(Rune rune)
		{
			var tile = new RuneTile(rune, rune.Slot) { Name = $"Rune{rune.Id}", OpenedLevel = OpenedLevel };
			if (rune.Level >= _bulkTarget)
				tile.Modulate = new Color(1, 1, 1, Fade.Disabled);
			tile.Pressed += t => RuneDialog.Show(t, t.Rune!, opened: Opened(t.Rune!));
			return tile;
		}
	}
}
