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
	/// O Santuário: a tela de abertura de toda sessão (GDD, seção 11), só de símbolos.
	///
	/// - No canto de cima, o retrato da conta (a Líder da Campanha) com o anel de experiência e o nível;
	///   do outro lado, o cabeçalho de recursos.
	/// - No centro, a constelação: o sigilo da canalização (coleta a ociosidade; o anel mostra o quanto
	///   encheu), a canalização rápida presa nele e os atalhos em volta. A pena liga o modo de editar:
	///   tocar numa estrela abre o carrossel de destinos.
	/// - Embaixo, a engrenagem da Configuração à esquerda; Loja, Mapa e Bolsa à direita.
	///
	/// Quem ainda não invocou vê o atalho de invocar pulsar; quem não venceu a primeira fase, o da Campanha.
	/// Só mostra e avisa: quem muda o <see cref="PlayerState"/> e salva é o GameRoot.
	/// </summary>
	public partial class HubScreen : Control
	{
		private const float StarSize = 76;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly Control _account = new() { Position = new Vector2(Layout.ScreenMargin, Layout.ScreenMargin) };
		private readonly Constellation _constellation = new();
		private readonly SigilButton _channel = new(Art.Icon("collect"), "", 128);
		private readonly SigilButton _quickChannel = new(Art.Icon("quick_channel"), "", 46);
		private readonly HBoxContainer _pending = Layout.Row(6, true);
		private readonly SigilButton _edit = new(Art.Icon("edit"), "", 52, SigilShape.Square) { ToggleMode = true };
		private List<Destination?> _shortcuts;

		public HubScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
			_shortcuts = Destinations.Shortcuts(player).ToList();
		}

		public event Action<Destination>? Requested;
		public event Action? ConfigRequested;
		public event Action? CollectRequested;
		public event Action? QuickChannelRequested;

		/// <summary>O jogador trocou um atalho: a lista inteira, vaga a vaga.</summary>
		public event Action<IReadOnlyList<Destination?>>? ShortcutsChanged;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background(_channel));

			_constellation.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_constellation.OffsetTop = 110;
			_constellation.OffsetBottom = -90;
			AddChild(_constellation);

			AddChild(_account);

			_currencies.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
			_currencies.GrowHorizontal = GrowDirection.Begin;
			_currencies.OffsetRight = -Layout.ScreenMargin;
			_currencies.OffsetTop = Layout.ScreenMargin;
			AddChild(_currencies);

			var settings = Corner(LayoutPreset.BottomLeft, GrowDirection.End);
			settings.AddChild(SigilButton.Of("config", T("destination.Config"), () => ConfigRequested?.Invoke(), 64, SigilShape.Square));
			_edit.TooltipText = T("hub.edit_shortcuts");
			_edit.Toggled += _ => RefreshConstellation();
			settings.AddChild(_edit);

			var doors = Corner(LayoutPreset.BottomRight, GrowDirection.Begin);
			foreach (var destination in new[] { Destination.Shop, Destination.Map, Destination.Bag })
				doors.AddChild(SigilButton.Of(Destinations.Icon(destination), Destinations.Name(destination), () => Requested?.Invoke(destination), 64, SigilShape.Square));

			_channel.Pressed += () => CollectRequested?.Invoke();
			_quickChannel.Pressed += () => QuickChannelRequested?.Invoke();

			// A ociosidade anda com a tela aberta: o anel e as recompensas acompanham a cada segundo.
			var timer = new Timer { WaitTime = 1, Autostart = true };
			timer.Timeout += () => RefreshIdle(DateTime.Now);
			AddChild(timer);

			Refresh(DateTime.Now);
		}

		public void Refresh(DateTime now)
		{
			_currencies.Refresh(_player);
			RefreshAccount();
			RefreshConstellation();
			RefreshIdle(now);
		}

		private void RefreshAccount()
		{
			Layout.Clear(_account);
			var toNext = Account.ExperienceToNext(_player.AccountLevel);
			var maxed = _player.AccountLevel >= Account.MaxLevel;
			var leader = Teams.Of(_player, Teams.Campaign).Select(_player.Monster).OfType<OwnedSummon>().FirstOrDefault();
			var summon = leader != null && _database.HasSummon(leader.SummonId) ? _database.Summon(leader.SummonId) : null;
			var portrait = summon != null ? Art.Creature(summon.ImageFor(leader!.Awakened)) : Art.Icon("avatar");
			var ink = summon != null ? Palette.Of(summon.Element) : Palette.Gold;
			var tooltip = maxed
				? T("hub.account_max", _player.AccountLevel)
				: T("hub.account", _player.AccountLevel, _player.AccountExperience, toNext);
			_account.AddChild(new AccountSigil(portrait, ink, _player.AccountLevel, maxed ? 1 : _player.AccountExperience / (float)toNext, tooltip));
		}

		private void RefreshConstellation()
		{
			var editing = _edit.ButtonPressed;
			var guide = Guide();
			var stars = new List<Control?>();
			for (var slot = 0; slot < Destinations.Slots; slot++)
			{
				var index = slot;
				var destination = _shortcuts[slot];
				if (destination == null && !editing)
				{
					stars.Add(null);
					continue;
				}

				var star = destination is { } d
					? new SigilButton(Art.Icon(Destinations.Icon(d)), Destinations.Name(d), StarSize)
					: new SigilButton(null, T("hub.empty_shortcut"), StarSize) { Letters = "+" };
				star.Highlight = !editing && destination == guide;
				if (editing)
				{
					star.Accent = Palette.Arcane;
					star.Pressed += () => ChooseShortcut(index, star);
				}
				else if (destination is { } go)
				{
					star.Pressed += () => Requested?.Invoke(go);
				}

				stars.Add(star);
			}

			// O sigilo do centro e o que fica preso nele saem da árvore junto com as estrelas: tira antes.
			foreach (var node in new Control[] { _channel, _quickChannel, _pending })
				node.GetParent()?.RemoveChild(node);
			_constellation.Set(_channel, stars);
			_constellation.Attach(_quickChannel, new Vector2(62, 58));
			_constellation.Attach(_pending, new Vector2(0, 104));
		}

		/// <summary>O atalho que pulsa para guiar quem está começando; nulo depois disso.</summary>
		private Destination? Guide() =>
			_player.TotalPulls == 0 ? Destination.Summon
			: _player.HighestStage == 0 ? Destination.Campaign
			: null;

		private void ChooseShortcut(int slot, Control near)
		{
			var options = Enum.GetValues<Destination>().Select(d => (Destination?)d).Append(null).ToList();
			var items = options
				.Select(d => d is { } destination
					? new ArcItem(Art.Icon(Destinations.Icon(destination)), Destinations.Name(destination), Palette.Gold)
					: new ArcItem(Art.Icon("cancel"), T("hub.empty_shortcut"), Palette.TextFaded))
				.ToList();
			var current = options.IndexOf(_shortcuts[slot]);
			ArcPicker.Open(this, items, Math.Max(0, current), near.GetGlobalRect().GetCenter(), index =>
			{
				_shortcuts[slot] = options[index];
				ShortcutsChanged?.Invoke(_shortcuts);
				RefreshConstellation();
			});
		}

		private void RefreshIdle(DateTime now)
		{
			var preview = Idle.Preview(_player, now);
			var pendingHours = Idle.PendingHours(_player, now);
			var hours = TimeSpan.FromHours(pendingHours);
			_constellation.Progress = (float)(pendingHours / Idle.CapHours);

			_channel.Disabled = preview.IsEmpty;
			_channel.Highlight = !preview.IsEmpty;
			_channel.TooltipText = T("hub.channeling", (int)hours.TotalHours, hours.Minutes.ToString("00"), Idle.CapHours);

			_quickChannel.Disabled = !Idle.CanQuickChannel(_player, now);
			_quickChannel.TooltipText = T("hub.quick_channel", Idle.QuickChannelHours);

			Layout.Clear(_pending);
			if (preview.IsEmpty)
				return;
			_pending.AddChild(Layout.Chip("essence", Texts.Short(preview.Essence), T("currency.essence")));
			if (preview.Gold > 0)
				_pending.AddChild(Layout.Chip("gold", Texts.Short(preview.Gold), T("currency.gold")));
			if (preview.Mana > 0)
				_pending.AddChild(Layout.Chip("mana", Texts.Short(preview.Mana), T("currency.mana")));
		}

		/// <summary>Uma fileira de sigilos presa num canto de baixo da tela.</summary>
		private HBoxContainer Corner(LayoutPreset preset, GrowDirection grow)
		{
			var row = Layout.Row(12);
			row.SetAnchorsAndOffsetsPreset(preset);
			row.GrowHorizontal = grow;
			row.GrowVertical = GrowDirection.Begin;
			var margin = Layout.ScreenMargin;
			if (grow == GrowDirection.End)
				row.OffsetLeft = margin;
			else
				row.OffsetRight = -margin;
			row.OffsetBottom = -margin;
			AddChild(row);
			return row;
		}
	}
}
