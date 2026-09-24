using System;
using Godot;
using Sigilos.Core.Player;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// O arquivo do save em user://. O texto vem e vai pelo <see cref="PlayerSave"/>; esta classe só
	/// abre e grava. Rodando com <c>-- --save=nome</c>, o arquivo ganha esse nome: uma partida de
	/// teste não apaga a de verdade.
	/// </summary>
	public sealed class SaveStore
	{
		private readonly string _path;

		public SaveStore(string slot)
		{
			_path = $"user://{slot}.json";
		}

		/// <summary>Nulo quando não há save (primeira vez) ou ele não pôde ser lido.</summary>
		public PlayerState? Load()
		{
			if (!FileAccess.FileExists(_path))
				return null;

			try
			{
				return PlayerSave.FromJson(FileAccess.GetFileAsString(_path));
			}
			catch (Exception exception)
			{
				GD.PushError($"Save ilegível em {_path}: {exception.Message}");
				return null;
			}
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
