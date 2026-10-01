using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Fundir cópias num monstro, separado da seleção de vários da tela de Monstros (que só libera): a
	/// janela mostra só as cópias da mesma família, de qualquer elemento (as do Baú também). Um botão
	/// marca as do mesmo elemento; as outras se marcam tocando. Embaixo, a lista do que vai sumir; Fundir
	/// pergunta de novo, com a lista e os avisos, e só então funde. Cada cópia sobe uma habilidade
	/// sorteada em um nível, então não se marca mais do que ainda cabe. Bloqueadas aparecem apagadas,
	/// com o cadeado, e não se marcam.
	/// </summary>
	public sealed class FusionDialog
	{
		private const float Width = 700;
		private const float CardWidth = 92;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly OwnedSummon _target;
		private readonly SummonDefinition _summon;
		private readonly Action<IReadOnlyList<int>> _fuse;
		private readonly Dialog _dialog;
		private readonly List<OwnedSummon> _copies;
		private readonly List<int> _marked = new();
		private readonly int _room;
		private readonly TileGrid _grid = new(8) { Name = "Copies" };
		private readonly RichTextLabel _summary;
		private readonly Label _message = new() { Name = "Message", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		private readonly HBoxContainer _tools = Layout.Row(10).Named("Tools");

		private FusionDialog(Control from, GameDatabase database, PlayerState player, OwnedSummon target, Action<IReadOnlyList<int>> fuse)
		{
			_database = database;
			_player = player;
			_target = target;
			_summon = database.Summon(target.SummonId);
			_fuse = fuse;
			_room = Fusion.SkillUpsLeft(database, target);
			_copies = Candidates(database, player, target);

			var name = _summon.NameFor(target.Awakened);
			_dialog = Dialog.Open(from, T("monsters.fusion_title", name), Width, null, "FusionDialog");
			_dialog.Body.AddChild(RichText.Label(T("monsters.fusion_text", name, _room), Width - 40).Named("Text"));
			_dialog.Body.AddChild(_tools);

			var scroll = Layout.Scroll(_grid);
			scroll.CustomMinimumSize = new Vector2(0, CardWidth * 1.25f * 2 + 16);
			_dialog.Body.AddChild(scroll);

			_summary = RichText.Label("", Width - 40);
			_summary.Name = "Summary";
			_dialog.Body.AddChild(_summary);
			_message.CustomMinimumSize = new Vector2(Width - 40, 0);
			_message.AddThemeColorOverride("font_color", Palette.Negative);
			_dialog.Body.AddChild(_message);
			Refresh();
		}

		public static FusionDialog Open(Control from, GameDatabase database, PlayerState player, OwnedSummon target, Action<IReadOnlyList<int>> fuse) =>
			new(from, database, player, target, fuse);

		/// <summary>As cópias da família: as da mesma variante primeiro, depois as mais fracas (as que menos custam perder); bloqueadas no fim.</summary>
		public static List<OwnedSummon> Candidates(GameDatabase database, PlayerState player, OwnedSummon target)
		{
			var family = database.Summon(target.SummonId).FamilyId;
			return player.Monsters
				.Where(m => m.Id != target.Id && database.HasSummon(m.SummonId) && database.Summon(m.SummonId).FamilyId == family)
				.OrderBy(m => m.Locked)
				.ThenByDescending(m => m.SummonId == target.SummonId)
				.ThenBy(m => database.Summon(m.SummonId).Element)
				.ThenBy(m => m.Awakened)
				.ThenBy(m => m.Stars)
				.ThenBy(m => m.Level)
				.ThenBy(m => m.Id)
				.ToList();
		}

		private void Refresh()
		{
			var marked = Marked();
			RefreshTools();
			RefreshGrid();
			_summary.Text = marked.Count == 0
				? T(_copies.Count == 0 ? "monsters.fusion_none" : "monsters.fusion_pick")
				: T("monsters.fusion_consumed", marked.Count, string.Join(", ", marked.Select(Describe)));

			_dialog.ClearActions();
			_dialog.AddAction(T("common.cancel"), null).Named("Cancel");
			var fuse = _dialog.AddAction(T("monsters.fusion_button", marked.Count), Confirm, ButtonKind.Primary, closes: false, icon: "fuse").Named("Fuse");
			fuse.Disabled = marked.Count == 0;
		}

		private void RefreshTools()
		{
			Layout.Clear(_tools);
			var element = Texts.Name(_summon.Element);
			var same = _copies.Count(c => c.SummonId == _target.SummonId && !c.Locked);
			var auto = GameButton.Of(T("monsters.fusion_same_element", element), MarkSameElement, ButtonKind.Secondary, "copies", 48).Named("SameElement");
			auto.Disabled = same == 0 || _marked.Count >= _room;
			_tools.AddChild(auto);
			var clear = GameButton.Of(T("monsters.unmark"), () =>
			{
				_marked.Clear();
				Say(null);
				Refresh();
			}, ButtonKind.Secondary, null, 48).Named("Unmark");
			clear.Disabled = _marked.Count == 0;
			_tools.AddChild(clear);
			var room = new Label { Name = "Room", Text = T("monsters.fusion_room", _marked.Count, _room), VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = GameTheme.Faded };
			_tools.AddChild(room);
		}

		private void RefreshGrid()
		{
			Layout.Clear(_grid);
			foreach (var copy in _copies)
			{
				var summon = _database.Summon(copy.SummonId);
				var tag = copy.Stored ? T("monsters.vault") : copy.SummonId == _target.SummonId ? null : Texts.Name(summon.Element);
				var card = new CreatureCard(summon, copy, CardWidth, tag: tag) { Name = $"Copy{copy.Id}" };
				card.SetMarked(_marked.Contains(copy.Id));
				if (copy.Locked)
					card.Modulate = new Color(1, 1, 1, 0.5f);
				card.Pressed += c => Toggle(c, copy);
				_grid.AddChild(card);
			}

			if (_copies.Count == 0)
				_grid.AddChild(Layout.Text(T("monsters.fusion_none"), GameTheme.Faded, Width - 60).Named("Empty"));
		}

		private void Toggle(CreatureCard card, OwnedSummon copy)
		{
			if (copy.Locked)
			{
				Dialog.Info(card, T("monsters.locked"), T("monsters.fusion_locked"));
				return;
			}

			if (_marked.Remove(copy.Id))
				Say(null);
			else if (_marked.Count >= _room)
				Say(T("monsters.fusion_full", _room));
			else
				_marked.Add(copy.Id);
			Refresh();
		}

		/// <summary>Marca as cópias da mesma variante (as mais fracas primeiro) até onde cabe.</summary>
		private void MarkSameElement()
		{
			foreach (var copy in _copies.Where(c => c.SummonId == _target.SummonId && !c.Locked && !_marked.Contains(c.Id)))
			{
				if (_marked.Count >= _room)
					break;
				_marked.Add(copy.Id);
			}

			Say(null);
			Refresh();
		}

		private void Confirm()
		{
			var marked = Marked();
			if (marked.Count == 0)
				return;

			var name = _summon.NameFor(_target.Awakened);
			var text = T("monsters.fusion_confirm", marked.Count, name, string.Join(", ", marked.Select(Describe)))
				+ MonsterNotes.Warning(_database, _player, marked);
			Dialog.Confirm(_dialog, T("monsters.fuse_title"), text, T("monsters.fuse_button"), () =>
			{
				_dialog.Close();
				_fuse(marked.Select(m => m.Id).ToList());
			}, ButtonKind.Danger);
		}

		private List<OwnedSummon> Marked() => _marked.Select(_player.Monster).OfType<OwnedSummon>().ToList();

		/// <summary>"Goblin de Fogo ★★★ Nv 1", com o nome em dourado.</summary>
		private string Describe(OwnedSummon monster)
		{
			var summon = _database.Summon(monster.SummonId);
			return $"[color=#{Palette.Gold.ToHtml(false)}]{summon.NameFor(monster.Awakened)}[/color] {Texts.Stars(monster.Stars)} {T("monsters.fusion_level", monster.Level)}";
		}

		private void Say(string? text)
		{
			_message.Text = text ?? "";
			_message.Visible = text != null;
		}
	}
}
