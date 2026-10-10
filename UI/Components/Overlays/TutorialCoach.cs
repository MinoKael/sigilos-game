using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O Mestre da luta de treino (<see cref="Tutorial"/>): uma placa no alto do campo, que ensina
	/// enquanto o jogador luta, uma coisa de cada vez, quando ela aparece na luta. As explicações esperam
	/// o Continuar (a luta espera junto); as instruções ("toque no básico") ficam na placa até o jogador
	/// agir, e prendem as escolhas ao que está sendo ensinado.
	///
	/// A ordem: o campo e a ordem de turno; o básico e o alvo; a recarga; os elementos (na segunda onda,
	/// com um inimigo de Fogo e um de Vento); o efeito (o atordoar) e o símbolo dele; o suporte (a cura);
	/// combinar efeitos; e por fim o Automático, que só então liga.
	///
	/// A mesma placa ensina depois a primeira invocação, na tela de Invocação (<see cref="Screens.SummonScreen"/>),
	/// com <see cref="Hint"/>, <see cref="Clear"/> e <see cref="Say"/>.
	/// </summary>
	public partial class TutorialCoach : PanelContainer
	{
		private readonly RichTextLabel _text;
		private readonly GameButton _continue;
		private readonly Queue<string> _queue = new();
		private TaskCompletionSource? _idle;
		private bool _showing;

		private bool _basic;
		private bool _cooldown;
		private bool _elements;
		private bool _elementPick;
		private bool _stun;
		private bool _support;
		private bool _combo;
		private bool _statusSeen;
		private bool _waveTwo;

		/// <summary>O que vale nesta vez: só esta habilidade, ou só alvos deste elemento (nulos: tudo).</summary>
		private int? _onlySkill;
		private Element? _onlyElement;

		/// <param name="textWidth">A largura do texto da placa: menor onde o espaço é estreito.</param>
		public TutorialCoach(float textWidth = 440)
		{
			Name = "TutorialCoach";
			MouseFilter = MouseFilterEnum.Stop;
			_text = RichText.Label("", textWidth).Named("Text");
			AddThemeStyleboxOverride("panel", Ornament.Panel(new Color(Palette.Panel, 0.96f), Palette.Gold, 10));
			_continue = GameButton.Of(T("tutorial.continue"), Next, ButtonKind.Primary, "confirm", 48).Named("Continue");

			var row = Layout.Row(14).Named("Row");
			var portrait = Doodle.Icon(Art.Icon("compendium"), 56, Palette.Gold).Named("Portrait");
			portrait.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			row.AddChild(portrait);
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 8);
			var title = new Label { Name = "Title", Text = T("tutorial.title"), ThemeTypeVariation = GameTheme.Heading };
			title.AddThemeColorOverride("font_color", Palette.Gold);
			column.AddChild(title);
			column.AddChild(_text);
			_continue.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			column.AddChild(_continue);
			row.AddChild(column);
			AddChild(row);
			Visible = false;
		}

		/// <summary>O Automático só liga depois da última lição.</summary>
		public bool AutoAllowed { get; private set; }

		/// <summary>A luta começou e a primeira onda está no campo.</summary>
		public Task Begin()
		{
			Say(T("tutorial.intro"));
			Say(T("tutorial.order"));
			return Idle();
		}

		/// <summary>
		/// A vez de um aliado, antes de aparecerem as habilidades: no máximo uma lição nova (a recarga ainda
		/// deixa vir uma instrução junto), que pode prender a escolha. Espera as explicações que estavam na fila.
		/// </summary>
		public async Task BeforeDecision(BattleSession session, BattleUnit ally)
		{
			await Idle();
			_onlySkill = null;
			_onlyElement = null;
			var enemies = session.Enemies.Where(e => e.IsAlive).ToList();

			if (!_basic)
			{
				_onlySkill = 0;
				Hint(T("tutorial.basic", ally.Name));
				return;
			}

			// A recarga é só explicação: na mesma vez ainda pode vir uma instrução (o atordoar já pronto).
			if (!_cooldown)
			{
				_cooldown = true;
				Say(T("tutorial.cooldown"));
				await Idle();
			}

			var elementChoice = enemies.Any(e => e.Element == Element.Fire) && enemies.Any(e => e.Element != Element.Fire);
			if (_waveTwo && !_elementPick && elementChoice)
			{
				_onlyElement = Element.Fire;
				Hint(T("tutorial.element_pick"));
				return;
			}

			if (!_stun && ally.DefinitionId == Tutorial.Stunner && ally.IsReady(Tutorial.StunSkill))
			{
				_onlySkill = Tutorial.StunSkill;
				Hint(T("tutorial.stun", ally.Skill(Tutorial.StunSkill).Name, ally.Name));
				return;
			}

			if (!_support && ally.DefinitionId == Tutorial.Healer)
			{
				_support = true;
				Say(T("tutorial.support", ally.Name, ally.Skills.Count > 1 ? ally.Skill(1).Name : ally.Skill(0).Name));
				await Idle();
				return;
			}

			if (_stun && !_combo && enemies.Any(e => e.Has(StatusKind.Stun)))
			{
				_combo = true;
				Say(T("tutorial.combo"));
				await Idle();
				return;
			}

			// O Automático vem por último: depois dos elementos, que só aparecem na segunda onda.
			if (!AutoAllowed && _stun && _support && _waveTwo && (_elementPick || !elementChoice))
			{
				AutoAllowed = true;
				Say(T("tutorial.auto"));
				await Idle();
			}
		}

		public bool CanUse(int skill) => _onlySkill is not { } only || only == skill;

		public bool CanTarget(BattleUnit target) => _onlyElement is not { } element || target.Element == element;

		/// <summary>O jogador agiu: a instrução sai da placa e a lição dela conta como aprendida.</summary>
		public void Acted(int skill)
		{
			if (_onlySkill == 0)
				_basic = true;
			if (_onlyElement != null)
				_elementPick = true;
			if (_onlySkill == Tutorial.StunSkill && skill == Tutorial.StunSkill)
				_stun = true;
			_onlySkill = null;
			_onlyElement = null;
			if (!_showing)
				Visible = false;
		}

		/// <summary>O que a tela mostrou: a segunda onda ensina os elementos; o primeiro efeito num inimigo, o símbolo.</summary>
		public void Saw(BattleEvent battleEvent)
		{
			switch (battleEvent)
			{
				case WaveStarted { Wave: 2 } when !_elements:
					_elements = true;
					_waveTwo = true;
					Say(T("tutorial.elements"));
					break;
				case StatusApplied applied when !_statusSeen && applied.Target.Side == Core.Battle.Side.Enemies:
					_statusSeen = true;
					Say(T("tutorial.status_seen"));
					break;
			}
		}

		/// <summary>A luta acabou: a última fala, com o botão que leva adiante (<paramref name="button"/>: à Invocação ou ao Santuário).</summary>
		public Task End(string button)
		{
			_queue.Clear();
			_continue.Text = button;
			Say(T("tutorial.end"));
			return Idle();
		}

		/// <summary>A instrução foi seguida fora da luta (a primeira invocação): ela sai da placa.</summary>
		public void Clear()
		{
			if (!_showing)
				Visible = false;
		}

		/// <summary>Uma explicação: entra na fila e espera o Continuar.</summary>
		public void Say(string text)
		{
			_queue.Enqueue(text);
			if (!_showing)
				Next();
		}

		/// <summary>Uma instrução: fica na placa, sem botão, até o jogador agir.</summary>
		public void Hint(string text)
		{
			_text.Text = text;
			_continue.Visible = false;
			Visible = true;
		}

		/// <summary>Espera a fila de explicações acabar.</summary>
		private Task Idle()
		{
			if (!_showing)
				return Task.CompletedTask;
			_idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			return _idle.Task;
		}

		private void Next()
		{
			if (_queue.Count == 0)
			{
				_showing = false;
				Visible = false;
				var idle = _idle;
				_idle = null;
				idle?.TrySetResult();
				return;
			}

			_showing = true;
			_text.Text = _queue.Dequeue();
			_continue.Visible = true;
			Visible = true;
		}
	}
}
