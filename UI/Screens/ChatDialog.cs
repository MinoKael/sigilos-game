using System;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;
using Sigilos.Core.Social;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A janela do Chat global, cobrindo a tela toda (<see cref="Dialog.Full"/>): as linhas que chegaram desde
	/// que a conta conectou, a mais nova embaixo, e o campo de escrever com o Enviar. No cabeçalho, a conexão
	/// (ao vivo, conectando, sem conexão).
	///
	/// - Fala: a hora apagada, o nome em ouro (o desta conta em azul arcano) e o texto, como texto puro (o que
	///   o jogador escreve não vira formatação).
	/// - Feito (monstro 5★, runa +15): a linha toda é um botão que abre o resumo do monstro do catálogo ou a
	///   runa (só o conjunto, o espaço, as estrelas e o principal: o que o feito conta).
	/// - Recusa do servidor (rápido demais, conta sem nome) vira uma linha apagada só nesta janela.
	///
	/// Fechar não desliga nada: o chat segue ao vivo e guarda o que chegar.
	/// </summary>
	public sealed class ChatDialog
	{
		private const float RuneWidth = 420;

		private const float SendWidth = 160;

		private readonly ChatFeed _feed;
		private readonly Dialog _dialog;
		private readonly VBoxContainer _lines = new() { Name = "Lines" };
		private readonly Label _empty;
		private readonly LineEdit _field;
		private readonly GameButton _send;

		private ChatDialog(Control from, ChatFeed feed)
		{
			_feed = feed;
			_dialog = Dialog.Full(from, T("chat.title"), "ChatDialog");
			_dialog.UseBackButton();

			_empty = Layout.Text(T("chat.empty"), GameTheme.Faded).Named("Empty");
			_dialog.Body.AddChild(_empty);
			_lines.AddThemeConstantOverride("separation", 6);
			_dialog.Body.AddChild(_lines);
			foreach (var line in feed.Lines)
				_lines.AddChild(Row(line));

			_field = new LineEdit
			{
				Name = "Field",
				PlaceholderText = T("chat.placeholder"),
				MaxLength = ChatLine.MaxText,
				CustomMinimumSize = new Vector2(0, GameTheme.Touch),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CaretBlink = true,
			};
			_field.TextSubmitted += _ => Submit();
			_dialog.AddFooter(_field);
			_send = new GameButton(T("chat.send"), ButtonKind.Primary)
			{
				Name = "Send",
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
				CustomMinimumSize = new Vector2(SendWidth, GameTheme.Touch),
			};
			_send.Pressed += Submit;
			_dialog.AddFooter(_send);

			feed.Added += Add;
			feed.Changed += Refresh;
			feed.Refused += Refuse;
			_dialog.Closed += () =>
			{
				feed.Added -= Add;
				feed.Changed -= Refresh;
				feed.Refused -= Refuse;
			};
			Refresh();
			if (_lines.GetChildCount() > 0)
				_dialog.Reveal(_lines.GetChild<Control>(-1));
		}

		public static ChatDialog Open(Control from, ChatFeed feed) => new(from, feed);

		private void Refresh()
		{
			var (status, color) = _feed.Live ? ("chat.live", Palette.Spirit) : _feed.Connecting ? ("chat.connecting", Palette.TextFaded) : ("chat.offline", Palette.Negative);
			_dialog.SetCaption(T(status), color);
			_send.Disabled = !_feed.Live;
			_empty.Visible = _lines.GetChildCount() == 0;
		}

		private async void Submit()
		{
			var text = _field.Text.Trim();
			if (text.Length == 0 || !_feed.Live || _feed.Say is not { } say)
				return;

			_field.Clear();
			if (!await say(text) && GodotObject.IsInstanceValid(_field))
			{
				_field.Text = text;
				Notice(T("chat.error_send"));
			}
		}

		/// <summary>Uma linha nova: entra embaixo e, se o jogador estava vendo o fim (ou é dele), a rolagem acompanha.</summary>
		private void Add(ChatLine line)
		{
			var follow = _dialog.AtEnd || line.From == _feed.Me;
			var row = Row(line);
			Append(row);
			if (follow)
				_dialog.Reveal(row);
		}

		private void Refuse(string error) => Notice(T(Has($"chat.error_{error}") ? $"chat.error_{error}" : "chat.error_send"));

		/// <summary>Um aviso só desta janela (o que o servidor recusou), apagado, no fim das linhas.</summary>
		private void Notice(string text)
		{
			var label = Layout.Text(text).Named($"Notice{_lines.GetChildCount() + 1}");
			label.AddThemeColorOverride("font_color", new Color(Palette.Negative, 0.85f));
			Append(label);
			_dialog.Reveal(label);
		}

		private void Append(Control row)
		{
			_lines.AddChild(row);
			// A janela guarda o mesmo tanto que o chat: a mais velha sai.
			while (_lines.GetChildCount() > ChatFeed.MaxLines)
				Layout.Discard(_lines.GetChild(0));
			_empty.Visible = false;
		}

		private Control Row(ChatLine line)
		{
			var name = $"Line{_lines.GetChildCount() + 1}";
			return line.Feat is { } feat ? FeatRow(line, feat).Named(name) : SayRow(line).Named(name);
		}

		/// <summary>"14:05 Nome: texto". Tudo entra como texto puro: colchetes do jogador não viram BBCode.</summary>
		private RichTextLabel SayRow(ChatLine line)
		{
			var label = Text();
			Time(label, line);
			label.PushColor(line.From == _feed.Me ? Palette.Arcane : Palette.Gold);
			label.AddText(line.From);
			label.Pop();
			label.AddText($": {line.Text}");
			return label;
		}

		/// <summary>O feito numa faixa que se toca: o símbolo (o retrato do monstro, o Glifo da runa) e a frase.</summary>
		private Control FeatRow(ChatLine line, Feat feat)
		{
			var panel = new PanelContainer { MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			panel.AddThemeStyleboxOverride("panel", GameTheme.Box(new Color(Palette.Inset, 0.85f), Palette.GoldDark, 1, 10, 6));
			var row = Layout.Row(10).Named("Row");
			row.MouseFilter = Control.MouseFilterEnum.Ignore;

			var summon = feat is SummonFeat { SummonId: var id } && UiSession.Database is { } database && database.HasSummon(id) ? database.Summon(id) : null;
			Control? icon = feat switch
			{
				SummonFeat when summon != null => Layout.Medal(Art.Creature(summon.Image), Palette.Of(summon.Element), 36),
				RuneFeat rune => new RuneGlyph(RuneSets.For(rune.Set).Glyph, 28, Palette.Of(RuneFeatRarity), outline: true) { Name = "Glyph", MouseFilter = Control.MouseFilterEnum.Ignore },
				_ => null,
			};
			if (icon != null)
				row.AddChild(icon);

			var label = Text();
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			Time(label, line);
			label.PushColor(Palette.Gold);
			label.AddText(T(feat is RuneFeat ? "chat.feat_rune" : "chat.feat_summon", line.From));
			label.Pop();
			row.AddChild(label);
			panel.AddChild(row);

			Action? open = feat switch
			{
				SummonFeat when summon != null => () => MonsterSummary.Open(panel, summon, null),
				RuneFeat rune => () => ShowRune(panel, rune, line.From),
				_ => null,
			};
			if (open != null)
				Press.On(panel, open, open);
			return panel;
		}

		/// <summary>Uma runa em +15 tem sempre os quatro subatributos: é sempre da raridade mais alta.</summary>
		private static RuneRarity RuneFeatRarity => (RuneRarity)RuneRules.MaxSubstats;

		/// <summary>A runa do feito: o Glifo, as estrelas, o +15, o principal e o bônus do conjunto.</summary>
		private static void ShowRune(Control from, RuneFeat feat, string by)
		{
			var rune = feat.ToRune();
			var color = Palette.Of(RuneFeatRarity);
			var dialog = Dialog.Open(from, Texts.Title(rune), RuneWidth, from, "RuneFeatDialog");

			var row = Layout.Row(14).Named("Rune");
			row.AddChild(new RuneGlyph(RuneSets.For(rune.Set).Glyph, 48, color, outline: true) { Name = "Glyph" });
			var stats = new VBoxContainer { Name = "Stats", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			var grade = new Label { Name = "Grade", Text = $"{Texts.Stars(rune.Grade)}  +{rune.Level}" };
			grade.AddThemeColorOverride("font_color", color);
			stats.AddChild(grade);
			var main = new Label { Name = "Main", Text = Texts.Format(rune.Main, rune.MainValue) };
			main.AddThemeFontOverride("font", GameTheme.Serif);
			main.AddThemeFontSizeOverride("font_size", 22);
			stats.AddChild(main);
			row.AddChild(stats);
			dialog.Body.AddChild(row);

			var bonus = RichText.Label(Texts.Describe(RuneSets.For(rune.Set)), RuneWidth - 40, null, 14).Named("SetBonus");
			bonus.AddThemeColorOverride("default_color", Palette.TextFaded);
			dialog.Body.AddChild(bonus);
			dialog.Body.AddChild(Layout.Text(T("chat.rune_by", by), GameTheme.Faded).Named("By"));
		}

		private static RichTextLabel Text() => new()
		{
			Name = "Text",
			BbcodeEnabled = false,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass,
		};

		private static void Time(RichTextLabel label, ChatLine line)
		{
			label.PushColor(Palette.TextFaded);
			label.AddText($"{line.At.ToLocalTime():HH:mm}  ");
			label.Pop();
		}
	}
}
