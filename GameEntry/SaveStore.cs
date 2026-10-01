using System;
using Godot;
using Sigilos.Core.Player;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// O arquivo do save em user://. O texto vem e vai pelo <see cref="PlayerSave"/>; esta classe só
	/// abre e grava. Rodando com <c>-- --save=nome</c>, o arquivo ganha esse nome: uma partida de
	/// teste não apaga a de verdade. Cada conta tem o seu arquivo (<c>nome.account-id.json</c>, ver
	/// <see cref="Account.AccountSession"/>); o do jogo sem conta é o <c>nome.json</c>.
	///
	/// Nada se perde: um save que não dá para ler (formato antigo ou arquivo quebrado), o progresso local
	/// trocado pelo da nuvem e o da nuvem trocado pelo local viram <c>nome.old-data.json</c>.
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

		/// <summary>Quando o arquivo foi gravado pela última vez; nulo se não existe.</summary>
		public DateTimeOffset? SavedAt => FileAccess.FileExists(_path) ? DateTimeOffset.FromUnixTimeSeconds((long)FileAccess.GetModifiedTime(_path)) : null;

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

			if (player == null && Backup() is { } backup)
				GD.PushWarning($"Save de formato antigo guardado em {backup}; começando uma conta nova.");

			return player;
		}

		/// <summary>O texto do save como está no arquivo; nulo se não existe.</summary>
		public string? Read() => FileAccess.FileExists(_path) ? FileAccess.GetFileAsString(_path) : null;

		/// <summary>Grava e devolve o texto gravado (é ele que sobe para a nuvem).</summary>
		public string Save(PlayerState player)
		{
			var json = PlayerSave.ToJson(player);
			Write(json);
			return json;
		}

		public void Write(string json)
		{
			using var file = FileAccess.Open(_path, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PushError($"Não deu para gravar {_path}: {FileAccess.GetOpenError()}");
				return;
			}

			file.StoreString(json);
		}

		/// <summary>Tira o arquivo do caminho, guardado como backup. Devolve onde ficou; nulo se não deu (ele será sobrescrito).</summary>
		public string? Backup()
		{
			if (!FileAccess.FileExists(_path))
				return null;

			var backup = BackupPath("");
			if (DirAccess.RenameAbsolute(_path, backup) == Error.Ok)
				return backup;

			GD.PushError($"Não deu para guardar {_path} em {backup}; ele será sobrescrito.");
			return null;
		}

		/// <summary>Guarda um save que não está em arquivo nenhum (o da nuvem que vai ser substituído) como backup.</summary>
		public void Backup(string json, string tag)
		{
			using var file = FileAccess.Open(BackupPath(tag + "-"), FileAccess.ModeFlags.Write);
			file?.StoreString(json);
		}

		/// <summary>Passa o arquivo para outro lugar (o save sem conta que sobe para uma conta nova).</summary>
		public bool MoveTo(SaveStore other) => DirAccess.RenameAbsolute(_path, other._path) == Error.Ok;

		// Com data no nome: um backup nunca apaga outro mais velho.
		private string BackupPath(string tag) => $"user://{_slot}.old-{tag}{DateTime.Now:yyyyMMdd-HHmmss}.json";
	}
}
