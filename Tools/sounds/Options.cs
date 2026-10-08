using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sigilos.Sounds
{
	/// <summary>As opções da linha de comando (<c>--only=ui,combat/</c> ou <c>--only ui,combat/</c>); ver <see cref="Program"/>.</summary>
	internal sealed class Options
	{
		/// <summary>A semente da biblioteca. Trocar muda todos os arquivos; a mesma semente dá sempre os mesmos bytes.</summary>
		public const ulong DefaultSeed = 7451;

		public List<string> Only { get; } = new();
		public ulong Seed { get; private set; } = DefaultSeed;
		public string? Out { get; private set; }
		public int Rate { get; private set; } = 44100;
		public bool List { get; private set; }
		public bool Report { get; private set; }
		public bool Help { get; private set; }

		public static Options Parse(string[] args)
		{
			var options = new Options();
			for (var i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				var equals = arg.IndexOf('=');
				var name = equals > 0 ? arg[..equals] : arg;
				string Value()
				{
					if (equals > 0)
						return arg[(equals + 1)..];
					if (i + 1 >= args.Length)
						throw new ArgumentException($"{name} precisa de um valor.");
					return args[++i];
				}
				switch (name)
				{
					case "--only":
						foreach (var token in Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
							options.Only.Add(token);
						break;
					case "--seed":
						options.Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture);
						break;
					case "--out":
						options.Out = Value();
						break;
					case "--rate":
						options.Rate = int.Parse(Value(), CultureInfo.InvariantCulture);
						if (options.Rate is < 22050 or > 96000)
							throw new ArgumentException("--rate fica entre 22050 e 96000.");
						break;
					case "--list":
						options.List = true;
						break;
					case "--report":
						options.Report = true;
						break;
					case "--help" or "-h":
						options.Help = true;
						break;
					default:
						throw new ArgumentException($"Opção desconhecida: {arg}");
				}
			}
			return options;
		}
	}
}
