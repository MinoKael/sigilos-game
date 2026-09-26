using System;
using Godot;
using Sigilos.Core.Player;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// O arquivo do save em user://. O texto vem e vai pelo <see cref="PlayerSave"/>; esta classe só
	/// abre e grava. Rodando com <c>-- --save=nome</c>, o arquivo ganha esse nome: uma partida de
	/// teste não apaga a de verdade.
	///
	/// Um save que não dá para ler (formato antigo ou arquivo quebrado) não é apagado: vira
	/// <c>nome.antigo-data.json</c> e o jogo começa uma conta nova.
	/// </summary>
	public sealed class SaveStore
	{
		private readonly string _slot;
		private readonly string _path;

		public SaveStore(string slot)
		{
			_slot = slot;
			_path = $"user://{slot}.json";
		}

		/// <summary>Nulo quando não há save (primeira vez) ou ele não pôde ser lido.</summary>
		public PlayerState? Load()
		{
			if (!FileAccess.FileExists(_path))
				return null;

			PlayerState? player = null;
			try
			{
				player = PlayerSave.FromJson(FileAccess.GetFileAsString(_path));
			}
			catch (Exception exception)
			{
				GD.PushError($"Save ilegível em {_path}: {exception.Message}");
			}

			if (player == null)
			{
				// Com data no nome: um backup nunca apaga outro mais velho.
				var backup = $"user://{_slot}.antigo-{DateTime.Now:yyyyMMdd-HHmmss}.json";
				if (DirAccess.RenameAbsolute(_path, backup) == Error.Ok)
					GD.PushWarning($"Save de formato antigo guardado em {backup}; começando uma conta nova.");
				else
					GD.PushError($"Não deu para guardar {_path} em {backup}; ele será sobrescrito.");
			}

			return player;
		}

		public void Save(PlayerState player)
		{
			using var file = FileAccess.Open(_path, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PushError($"Não deu para gravar {_path}: {FileAccess.GetOpenError()}");
				return;
			}

			file.StoreString(PlayerSave.ToJson(player));
		}
	}
}
