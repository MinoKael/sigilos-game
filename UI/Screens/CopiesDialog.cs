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
	/// As cópias de um cartão do Baú que junta várias (<see cref="MonsterStack"/>), uma a uma. Marca-se
	/// uma por uma ou todas e, só com as marcadas: Tirar do Baú (até onde a coleção tiver vaga), Fundir em
	/// um monstro (a escolha do alvo, só os que aceitam estas cópias e ainda sobem habilidade, e depois a
	/// janela da fusão com elas já marcadas, <see cref="FusionDialog"/>) ou Soltar (pergunta antes, com os
	/// avisos). Bloqueadas não se fundem nem se soltam. Cada ação vira um evento da tela de Monstros; em
	/// seguida a janela relê o Baú e mostra as que sobraram, e fecha quando sobra uma só (a grade volta a
	/// mostrar o cartão normal).
	/// </summary>
	public sealed class CopiesDialog
	{
		private const float Width = 700;
		private const float CardWidth = 92;
		private const float Gap = 8;

		/// <summary>Quantos cartões cabem numa linha da grade.</summary>
		private const int PerRow = (int)((Width - 40 + Gap) / (CardWidth + Gap));

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly IReadOnlyList<int> _ids;
		private readonly HashSet<int> _marked = new();
		private readonly Dictionary<int, CreatureCard> _cards = new();
		private readonly Action<IReadOnlyList<int>> _retrieve;
		private readonly Action<int, IReadOnlyList<int>> _fuse;
		private readonly Action<IReadOnlyList<int>> _release;
		private readonly Dialog _dialog;
		private readonly HBoxContainer _tools = Layout.Row(10).Named("Tools");
		private readonly TileGrid _grid = new(Gap) { Name = "Copies" };
		private readonly ScrollContainer _scroll;
		private readonly Label _note = new() { Name = "Note", ThemeTypeVariation = GameTheme.Faded, AutowrapMode = TextServer.AutowrapMode.WordSmart };

		private CopiesDialog(Control from, GameDatabase database, PlayerState player, MonsterStack stack,
			Action<IReadOnlyList<int>> retrieve, Action<int, IReadOnlyList<int>> fuse, Action<IReadOnlyList<int>> release)
		{
			_database = database;
			_player = player;
			_ids = stack.Copies.Select(c => c.Id).ToList();
			_retrieve = retrieve;
			_fuse = fuse;
			_release = release;

			var first = stack.First;
			_dialog = Dialog.Open(from, T("monsters.copies_title", database.Summon(first.SummonId).NameFor(first.Awakened)), Width, null, "CopiesDialog");
			_dialog.Body.AddChild(Layout.Text(T("monsters.copies_hint"), GameTheme.Faded, Width - 40).Named("Hint"));
			_dialog.Body.AddChild(_tools);
			_scroll = Layout.Scroll(_grid);
			_dialog.Body.AddChild(_scroll);
			_note.CustomMinimumSize = new Vector2(Width - 40, 0);
			_dialog.Body.AddChild(_note);
			Rebuild();
		}

		public static CopiesDialog Open(Control from, GameDatabase database, PlayerState player, MonsterStack stack,
			Action<IReadOnlyList<int>> retrieve, Action<int, IReadOnlyList<int>> fuse, Action<IReadOnlyList<int>> release) =>
			new(from, database, player, stack, retrieve, fuse, release);

		/// <summary>As cópias que ainda estão no Baú, na ordem do cartão.</summary>
		private List<OwnedSummon> Copies() => _ids.Select(_player.Monster).OfType<OwnedSummon>().Where(m => m.Stored).ToList();

		/// <summary>Remonta a grade com as cópias que sobraram; com uma só (ou nenhuma), fecha.</summary>
		private void Rebuild()
		{
			var copies = Copies();
			if (copies.Count <= 1)
			{
				_dialog.Close();
				return;
			}

			_marked.IntersectWith(copies.Select(c => c.Id));
			Layout.Clear(_grid);
			_cards.Clear();
			foreach (var copy in copies)
			{
				var card = new CreatureCard(_database.Summon(copy.SummonId), copy, CardWidth) { Name = $"Copy{copy.Id}" };
				card.SetMarked(_marked.Contains(copy.Id));
				card.Pressed += _ => Toggle(copy.Id);
				_cards[copy.Id] = card;
				_grid.AddChild(card);
			}

			var rows = Math.Min(2, (copies.Count + PerRow - 1) / PerRow);
			_scroll.CustomMinimumSize = new Vector2(0, rows * CardWidth * 1.25f + (rows - 1) * Gap + 8);
			Refresh(copies);
		}

		private void Toggle(int id)
		{
			if (!_marked.Remove(id))
				_marked.Add(id);
			_cards[id].SetMarked(_marked.Contains(id));
			Refresh(Copies());
		}

		private void Mark(IEnumerable<OwnedSummon> copies, bool marked)
		{
			foreach (var copy in copies)
			{
				if (marked)
					_marked.Add(copy.Id);
				else
					_marked.Remove(copy.Id);
				_cards[copy.Id].SetMarked(marked);
			}

			Refresh(Copies());
		}

		/// <summary>A linha de cima (Marcar todas, Desmarcar, quantas), o aviso e os botões de baixo, com o que está marcado.</summary>
		private void Refresh(IReadOnlyList<OwnedSummon> copies)
		{
			var marked = copies.Where(c => _marked.Contains(c.Id)).ToList();

			Layout.Clear(_tools);
			var all = GameButton.Of(T("monsters.mark_all"), () => Mark(copies, true), ButtonKind.Secondary, null, 48).Named("MarkAll");
			all.Disabled = marked.Count == copies.Count;
			_tools.AddChild(all);
			var none = GameButton.Of(T("monsters.unmark"), () => Mark(copies, false), ButtonKind.Secondary, null, 48).Named("Unmark");
			none.Disabled = marked.Count == 0;
			_tools.AddChild(none);
			_tools.AddChild(new Label { Name = "Count", Text = T("monsters.copies_marked", marked.Count, copies.Count), VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = GameTheme.Faded });

			var free = Roster.FreeSlots(_player);
			var targets = Targets(copies[0], marked);
			var notes = new List<string>();
			if (copies[0].Locked)
				notes.Add(T("monsters.locked_note"));
			if (marked.Count > free)
				notes.Add(free == 0 ? T("monsters.collection_full") : T("monsters.copies_room", free));
			if (!copies[0].Locked && marked.Count > 0 && targets.Count == 0)
				notes.Add(T("monsters.fuse_target_none"));
			_note.Text = string.Join(" ", notes);
			_note.Visible = notes.Count > 0;

			_dialog.ClearActions();
			var retrieved = marked.Select(m => m.Id).Take(free).ToList();
			var retrieve = _dialog.AddAction(T("monsters.retrieve_marked", retrieved.Count), () => Act(() => _retrieve(retrieved)), ButtonKind.Secondary, closes: false, icon: "storage").Named("Retrieve");
			retrieve.Disabled = retrieved.Count == 0;

			var materials = marked.Where(m => !m.Locked).ToList();
			var fuse = _dialog.AddAction(T("monsters.copies_fuse", materials.Count), () => PickTarget(materials, targets), ButtonKind.Primary, closes: false, icon: "fuse").Named("Fuse");
			fuse.Disabled = materials.Count == 0 || targets.Count == 0;

			// O Núcleo de Infusão não se solta: só se funde.
			var released = marked.Where(m => !m.Locked && !m.IsInfusionCore).ToList();
			var fragments = released.Sum(m => Fusion.FragmentsFor(_database.Summon(m.SummonId).Rarity));
			var release = _dialog.AddAction(T("monsters.release_marked", released.Count), () => ConfirmRelease(released, fragments), ButtonKind.Danger, closes: false, icon: "release").Named("Release");
			if (released.Count > 0)
				release.WithCost("fragments", $"+{fragments}");
			release.Disabled = released.Count == 0;
		}

		/// <summary>
		/// Quem pode receber estas cópias: da mesma família (qualquer monstro, se forem Núcleos), fora das
		/// marcadas, com habilidade para subir; a coleção primeiro, os favoritos e os mais fortes na frente.
		/// </summary>
		private List<OwnedSummon> Targets(OwnedSummon copy, IReadOnlyList<OwnedSummon> marked)
		{
			var family = _database.Summon(copy.SummonId).FamilyId;
			var materials = marked.Select(m => m.Id).ToHashSet();
			return _player.Monsters
				.Where(m => !materials.Contains(m.Id) && !m.IsInfusionCore && _database.HasSummon(m.SummonId)
					&& (copy.IsInfusionCore || _database.Summon(m.SummonId).FamilyId == family)
					&& Fusion.SkillUpsLeft(_database, m) > 0)
				.OrderBy(m => m.Stored)
				.ThenByDescending(m => m.Favorite)
				.ThenByDescending(m => m.Stars)
				.ThenByDescending(m => m.Level)
				.ThenBy(m => m.Id)
				.ToList();
		}

		/// <summary>Escolhe o alvo e abre a fusão nele com as marcadas já escolhidas.</summary>
		private void PickTarget(IReadOnlyList<OwnedSummon> materials, IReadOnlyList<OwnedSummon> targets) =>
			MonsterPicker.Open(_dialog, _database, T("monsters.fuse_target_title"), T("monsters.fuse_target_hint"), targets, null, id =>
			{
				var target = _player.Monster(id)!;
				FusionDialog.Open(_dialog, _database, _player, target, ids => Act(() => _fuse(target.Id, ids)), materials.Select(m => m.Id));
			});

		private void ConfirmRelease(IReadOnlyList<OwnedSummon> released, int fragments) => Dialog.Confirm(_dialog,
			T("monsters.release_title"),
			T("monsters.release_many_confirm", released.Count, fragments) + MonsterNotes.Warning(_database, _player, released),
			T("monsters.release_button"),
			() => Act(() => _release(released.Select(m => m.Id).ToList())), ButtonKind.Danger);

		/// <summary>Aplica a ação (o GameRoot já atualiza a tela de Monstros) e mostra o que sobrou.</summary>
		private void Act(Action action)
		{
			action();
			Rebuild();
		}
	}
}
