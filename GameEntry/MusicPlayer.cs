using Godot;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// A música do jogo (Assets/Music): o Plano Celestial em tudo que não é luta e a Batalha na tela de luta.
	/// Cada faixa tem o seu tocador e toca em laço; trocar de faixa é um cruzamento, a que sai baixa e pausa
	/// e a que entra sobe. Entrar em luta tem uma transição própria: o Plano Celestial afunda (cai o volume e
	/// o tom, como quem desce para a arena) e a Batalha entra por cima, desde o começo; ao sair da luta, o
	/// Plano Celestial volta de onde tinha parado. Lutas seguidas ("Continuar") não reiniciam a Batalha.
	///
	/// Quem escolhe a faixa é a troca de tela (GameRoot.Swap): a Batalha automática roda fora das telas
	/// (<see cref="AutoBattleRunner"/>) e não mexe na música. As faixas tocam no barramento "Music", que este
	/// nó cria se o projeto não tiver.
	/// </summary>
	public partial class MusicPlayer : Node
	{
		public enum Track
		{
			Celestial,
			Battle,
		}

		public const string Bus = "Music";

		/// <summary>O volume das faixas, em dB.</summary>
		private const float Volume = -14f;
		private const float VolumeBattle = -28f;

        /// <summary>Quieto o bastante para pausar sem estalo.</summary>
        private const float Silent = -40f;

		private readonly AudioStreamPlayer _celestial = Player("Celestial", "res://Assets/Music/Plano Celestial.ogg");
		private readonly AudioStreamPlayer _battle = Player("Battle", "res://Assets/Music/Battle.ogg");
		private Track? _track;
		private Tween? _tween;

		public MusicPlayer()
		{
			Name = "Music";
		}

		public override void _Ready()
		{
			if (AudioServer.GetBusIndex(Bus) < 0)
			{
				AudioServer.BusCount++;
				AudioServer.SetBusName(AudioServer.BusCount - 1, Bus);
				AudioServer.SetBusSend(AudioServer.BusCount - 1, "Master");
			}

			AddChild(_celestial);
			AddChild(_battle);
		}

		/// <summary>Toca a faixa pedida, com a transição dela; se já é a que toca, nada muda.</summary>
		public void Play(Track track)
		{
			if (_track == track)
				return;

			var from = _track switch
			{
				Track.Celestial => _celestial,
				Track.Battle => _battle,
				_ => null,
			};
			var to = track == Track.Battle ? _battle : _celestial;
			_track = track;
			_tween?.Kill();
			_tween = CreateTween().SetParallel();

			if (track == Track.Battle)
			{
				// A descida para a arena: o Plano Celestial afunda e a Batalha começa do início, logo depois.
				if (from != null)
				{
					_tween.TweenProperty(from, "volume_db", Silent, 0.8).SetEase(Tween.EaseType.In);
					_tween.TweenProperty(from, "pitch_scale", 0.75, 0.8).SetEase(Tween.EaseType.In);
				}

				Start(to, restart: true);
				_tween.TweenProperty(to, "volume_db", VolumeBattle, 0.9).SetDelay(from != null ? 0.35 : 0)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			}
			else
			{
				// De volta: a Batalha some devagar e o Plano Celestial continua de onde parou.
				if (from != null)
					_tween.TweenProperty(from, "volume_db", Silent, 1.0);
				Start(to, restart: false);
				_tween.TweenProperty(to, "volume_db", Volume, 1.6).SetDelay(from != null ? 0.2 : 0)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			}

			_tween.TweenProperty(to, "pitch_scale", 1.0, 0.4);
			if (from != null)
			{
				_tween.Chain().TweenCallback(Callable.From(() =>
				{
					from.StreamPaused = true;
					from.PitchScale = 1;
				}));
			}
		}

		/// <summary>
		/// Põe o tocador para tocar: do começo, ou de onde pausou (pausado, o Godot não o conta como tocando).
		/// Entra em silêncio para subir no cruzamento, a não ser que ainda esteja soando (uma volta rápida).
		/// </summary>
		private static void Start(AudioStreamPlayer player, bool restart)
		{
			if (player.Stream == null)
				return;

			if (restart || player.StreamPaused || !player.Playing)
				player.VolumeDb = Silent;

			if (restart)
			{
				player.StreamPaused = false;
				player.Play();
			}
			else if (player.StreamPaused)
			{
				player.StreamPaused = false;
			}
			else if (!player.Playing)
			{
				player.Play();
			}
		}

		private static AudioStreamPlayer Player(string name, string path)
		{
			var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
			if (stream is AudioStreamOggVorbis ogg)
				ogg.Loop = true;
			return new AudioStreamPlayer { Name = name, Stream = stream, Bus = Bus, VolumeDb = Silent };
		}
	}
}
