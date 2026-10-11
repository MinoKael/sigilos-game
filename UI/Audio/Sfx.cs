using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Sigilos.UI.Audio
{
	/// <summary>
	/// Os efeitos sonoros (docs/SONS.md): um nó do GameRoot, vivo o tempo todo, com <see cref="Voices"/>
	/// tocadores no barramento "Effects" (que ele cria se o projeto não tiver; o volume é o de Efeitos, nos
	/// Ajustes). Toca pelo nome lógico do catálogo (<see cref="SfxCatalog"/>):
	///
	/// - <see cref="Play(string, double)"/>: o som da ação (<c>Sfx.Play("rewards.star_up")</c>), na hora ou
	///   depois de um atraso. Um efeito com variações sorteia uma, sem repetir a última; o mesmo som de novo
	///   em menos de <see cref="RepeatMs"/> não toca (os acertos de um golpe em área caem juntos), e um som
	///   toca no máximo <see cref="SameAtOnce"/> vezes junto: a próxima corta a mais antiga dele.
	/// - <see cref="Fallback"/>: o som de reserva dos componentes (o clique do botão, a janela abrindo, a
	///   aba trocando). Espera o fim do quadro e só toca se nenhum <c>Play</c> veio no mesmo quadro: a ação
	///   com som próprio cala o clique que a pediu. Entre as reservas do quadro, ganha a de maior prioridade
	///   (a janela que abriu ganha do clique que a abriu); no empate, a última.
	/// - <see cref="Quiet"/>: o quadro fica sem som de reserva (o toque longo que já abriu um resumo).
	///
	/// Sem o nó (testes, ferramentas), nada toca. Cada arquivo carrega na primeira vez que toca, e os da
	/// interface já ao abrir o jogo, para o primeiro clique não atrasar. Toca também com a árvore em pausa.
	/// </summary>
	public partial class Sfx : Node
	{
		public const string Bus = "Effects";

		/// <summary>Quantos sons ao mesmo tempo; o próximo corta o mais antigo.</summary>
		private const int Voices = 16;

		/// <summary>O mesmo som de novo antes disso, em milissegundos, não toca.</summary>
		private const ulong RepeatMs = 80;

		/// <summary>Quantas vezes o mesmo som toca junto; a próxima corta a mais antiga dele.</summary>
		private const int SameAtOnce = 2;

		private static Sfx? _instance;

		private readonly List<AudioStreamPlayer> _voices = new();
		private readonly string?[] _voiceNames = new string?[Voices];
		private readonly ulong[] _voiceStarted = new ulong[Voices];
		private readonly Dictionary<string, AudioStream?> _streams = new();
		private readonly Dictionary<string, ulong> _played = new();
		private readonly Dictionary<string, int> _lastFile = new();
		private readonly HashSet<string> _missing = new();
		private readonly Random _random = new();
		private SfxCatalog _catalog = SfxCatalog.Empty;
		private int _next;

		private string? _fallback;
		private int _fallbackPriority;
		private bool _claimed;
		private bool _flushQueued;

		public Sfx()
		{
			Name = "Sfx";
			ProcessMode = ProcessModeEnum.Always;
		}

		public override void _Ready()
		{
			if (AudioServer.GetBusIndex(Bus) < 0)
			{
				AudioServer.BusCount++;
				AudioServer.SetBusName(AudioServer.BusCount - 1, Bus);
				AudioServer.SetBusSend(AudioServer.BusCount - 1, "Master");
			}

			for (var i = 0; i < Voices; i++)
			{
				var voice = new AudioStreamPlayer { Name = $"Voice{i + 1}", Bus = Bus };
				AddChild(voice);
				_voices.Add(voice);
			}

			if (FileAccess.FileExists(SfxCatalog.Path))
				_catalog = SfxCatalog.Parse(FileAccess.GetFileAsString(SfxCatalog.Path));
			else
				GD.PushWarning($"Sfx: sem o catálogo {SfxCatalog.Path} (dotnet run --project Tools/sounds -c Release).");
			foreach (var file in _catalog.Names.Where(name => name.StartsWith("ui.")).SelectMany(_catalog.Files))
				Load(file);
			_instance = this;
		}

		public override void _ExitTree()
		{
			if (_instance == this)
				_instance = null;
		}

		/// <summary>Toca <paramref name="name"/> agora, ou daqui a <paramref name="delay"/> segundos. Cala o som de reserva do quadro.</summary>
		public static void Play(string name, double delay = 0)
		{
			if (_instance is not { } sfx)
				return;
			sfx.Claim();
			if (delay <= 0)
			{
				sfx.Start(name);
				return;
			}

			sfx.GetTree().CreateTimer(delay).Timeout += () =>
			{
				if (IsInstanceValid(sfx))
					sfx.Start(name);
			};
		}

		/// <summary>Toca os sons de um momento, com os atrasos divididos por <paramref name="speed"/> (a aceleração da luta).</summary>
		public static void Play(IEnumerable<Cue> cues, double speed = 1)
		{
			foreach (var cue in cues)
				Play(cue.Name, cue.Delay / speed);
		}

		/// <summary>Toca <paramref name="name"/> se a ação pegou (<paramref name="done"/>).</summary>
		public static void PlayIf(bool done, string name)
		{
			if (done)
				Play(name);
		}

		/// <summary>O som de reserva: no fim do quadro, se nenhuma ação tocou o seu (ver a classe).</summary>
		public static void Fallback(string name, int priority = 0)
		{
			if (_instance is not { } sfx)
				return;
			if (sfx._fallback == null || priority >= sfx._fallbackPriority)
			{
				sfx._fallback = name;
				sfx._fallbackPriority = priority;
			}

			sfx.QueueFlush();
		}

		/// <summary>O quadro fica sem som de reserva.</summary>
		public static void Quiet() => _instance?.Claim();

		private void Claim()
		{
			_claimed = true;
			QueueFlush();
		}

		private void QueueFlush()
		{
			if (_flushQueued)
				return;
			_flushQueued = true;
			Callable.From(Flush).CallDeferred();
		}

		private void Flush()
		{
			_flushQueued = false;
			if (!_claimed && _fallback != null)
				Start(_fallback);
			_fallback = null;
			_claimed = false;
		}

		private void Start(string name)
		{
			var files = _catalog.Files(name);
			if (files.Count == 0)
			{
				if (_missing.Add(name))
					GD.PushWarning($"Sfx: o catálogo não tem {name}.");
				return;
			}

			var now = Time.GetTicksMsec();
			if (_played.TryGetValue(name, out var last) && now - last < RepeatMs)
				return;
			_played[name] = now;

			if (Load(files[Pick(name, files.Count)]) is not { } stream)
				return;
			var index = VoiceFor(name);
			_voiceNames[index] = name;
			_voiceStarted[index] = now;
			_voices[index].Stream = stream;
			_voices[index].Play();
		}

		/// <summary>O tocador do som: o mais antigo dele se já toca <see cref="SameAtOnce"/> vezes; senão, um livre.</summary>
		private int VoiceFor(string name)
		{
			var same = 0;
			var oldest = -1;
			for (var i = 0; i < _voices.Count; i++)
			{
				if (!_voices[i].Playing || _voiceNames[i] != name)
					continue;
				same++;
				if (oldest < 0 || _voiceStarted[i] < _voiceStarted[oldest])
					oldest = i;
			}

			return same >= SameAtOnce ? oldest : FreeVoice();
		}

		/// <summary>A variação: ao acaso, sem repetir a última do mesmo som.</summary>
		private int Pick(string name, int count)
		{
			if (count == 1)
				return 0;
			var last = _lastFile.GetValueOrDefault(name, -1);
			var pick = _random.Next(last >= 0 ? count - 1 : count);
			if (last >= 0 && pick >= last)
				pick++;
			_lastFile[name] = pick;
			return pick;
		}

		/// <summary>O primeiro tocador livre, a partir do que vem depois do último usado; sem nenhum livre, o mais antigo.</summary>
		private int FreeVoice()
		{
			for (var i = 0; i < _voices.Count; i++)
			{
				var index = (_next + i) % _voices.Count;
				if (_voices[index].Playing)
					continue;
				_next = (index + 1) % _voices.Count;
				return index;
			}

			var oldest = _next;
			_next = (_next + 1) % _voices.Count;
			return oldest;
		}

		private AudioStream? Load(string file)
		{
			if (!_streams.TryGetValue(file, out var stream))
				_streams[file] = stream = ResourceLoader.Exists(file) ? GD.Load<AudioStream>(file) : null;
			return stream;
		}
	}
}
