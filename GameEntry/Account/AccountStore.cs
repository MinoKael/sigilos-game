using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// O que o aparelho lembra da conta, em <c>user://{slot}.account.json</c>:
	/// - o id do aparelho, sorteado uma vez (o servidor reconhece "o mesmo aparelho" por ele);
	/// - se o jogador escolheu jogar sem conta (o jogo abre direto, sem a tela de login);
	/// - o idioma do último jogo (a tela de login sai nele, antes de haver save aberto);
	/// - o e-mail, o nome, o id e o token de renovação de quem entrou (a senha nunca);
	/// - por conta, o último ponto de sincronização (<see cref="SyncPoint"/>).
	///
	/// Com <c>-- --save=nome</c>, cada nome tem o seu arquivo e o seu id: dá para testar dois "aparelhos"
	/// na mesma máquina.
	/// </summary>
	public sealed class AccountStore
	{
		private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

		private string _path = "";

		public string DeviceId { get; set; } = "";

		public bool Offline { get; set; }

		public string? Language { get; set; }

		public string? Email { get; set; }

		/// <summary>O nome da conta (único no servidor), como veio na última entrada ou renovação.</summary>
		public string? Name { get; set; }

		public Guid? UserId { get; set; }

		public string? RefreshToken { get; set; }

		/// <summary>
		/// A chave de recuperação que o servidor acabou de mandar e o jogador ainda não confirmou ter
		/// guardado: fica aqui até o "Já guardei", para não se perder se o jogo fechar antes.
		/// </summary>
		public string? RecoveryKey { get; set; }

		/// <summary>O último ponto de sincronização, pelo id da conta.</summary>
		public Dictionary<string, SyncPoint> Synced { get; set; } = new();

		/// <summary>O ponto de sincronização da conta de agora; nulo se ela nunca sincronizou neste aparelho.</summary>
		[JsonIgnore]
		public SyncPoint? CurrentSync => UserId is { } id ? Synced.GetValueOrDefault(id.ToString("N")) : null;

		public static AccountStore Load(string slot)
		{
			var path = $"user://{slot}.account.json";
			AccountStore? store = null;
			if (FileAccess.FileExists(path))
			{
				try
				{
					store = JsonSerializer.Deserialize<AccountStore>(FileAccess.GetFileAsString(path), Json);
				}
				catch (JsonException exception)
				{
					GD.PushError($"Dados da conta ilegíveis em {path}: {exception.Message}");
				}
			}

			store ??= new AccountStore();
			store._path = path;
			if (store.DeviceId.Length == 0)
			{
				store.DeviceId = Guid.NewGuid().ToString("N");
				store.Save();
			}

			return store;
		}

		public void Save()
		{
			using var file = FileAccess.Open(_path, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PushError($"Não deu para gravar {_path}: {FileAccess.GetOpenError()}");
				return;
			}

			file.StoreString(JsonSerializer.Serialize(this, Json));
		}

		/// <summary>Anota o ponto de sincronização da conta de agora.</summary>
		public void MarkSynced(SyncPoint point)
		{
			if (UserId is not { } id)
				return;
			Synced[id.ToString("N")] = point;
			Save();
		}
	}
}
