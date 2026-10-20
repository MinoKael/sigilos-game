using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sigilos.GameEntry.Account;

namespace Sigilos.Tests
{
	/// <summary>
	/// A conta do lado do jogo (GameEntry/Account), sem rede: quem ganha entre o save do aparelho e o da
	/// nuvem, e a conversa com um servidor falso (tokens, sessão tomada, save).
	/// </summary>
	internal static class AccountTests
	{
		private static readonly Guid User = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

		[Test]
		private static void SyncKeepsWhoChangedAndAsksWhenBothDid()
		{
			var point = new SyncPoint(4, CloudSync.Hash("a"));
			CloudCopy Cloud(long revision, string json) => new(revision, DateTimeOffset.UtcNow, json);

			Assert.Equal(SyncAction.Start, CloudSync.Decide(null, null, null), "nada em lugar nenhum: jogo novo");
			Assert.Equal(SyncAction.Download, CloudSync.Decide(null, null, Cloud(1, "a")), "aparelho sem save: vem o da nuvem");
			Assert.Equal(SyncAction.Upload, CloudSync.Decide("a", null, null), "nuvem vazia: sobe o local");
			Assert.Equal(SyncAction.InSync, CloudSync.Decide("a", null, Cloud(9, "a")), "conteúdo igual não pergunta, nem sem ponto");
			Assert.Equal(SyncAction.Download, CloudSync.Decide("a", point, Cloud(5, "b")), "só a nuvem mudou");
			Assert.Equal(SyncAction.Upload, CloudSync.Decide("c", point, Cloud(4, "a")), "só o aparelho mudou");
			Assert.Equal(SyncAction.Ask, CloudSync.Decide("c", point, Cloud(5, "b")), "os dois mudaram");
			Assert.Equal(SyncAction.Ask, CloudSync.Decide("c", null, Cloud(5, "b")), "sem ponto e diferentes (o save sem conta numa conta que já tem save)");
		}

		[Test]
		private static void TakenSessionIsClaimedAgainUnlessAnotherDeviceHoldsIt()
		{
			// Aparelho que só dormiu: ninguém mais está com a conta, segue com uma sessão nova.
			var second = Guid.NewGuid();
			var server = new FakeServer();
			server.On("auth/refresh", _ => (200, Tokens("r2")));
			server.On("session/heartbeat", _ => (409, new { error = "session_taken" }));
			server.On("session/claim", _ => (200, new { sessionId = second }));
			var auth = new AuthClient(server.Client());
			auth.Restore("r1", User);
			var session = new SessionLock(auth, "device", "PC");
			Run(session.Claim(false));

			Assert.Equal(BeatOutcome.Alive, Run(session.Beat()), "tomou de novo");
			Assert.Equal(second, session.SessionId, "com a sessão nova");
			Assert.False(server.Body(server.Paths.Count - 1).GetProperty("force").GetBoolean(), "sem forçar");

			// Outro aparelho está com ela: caiu.
			server.On("session/claim", _ => (409, new { error = "session_active", deviceName = "Celular", lastSeen = DateTimeOffset.UtcNow }));
			Assert.Equal(BeatOutcome.Taken, Run(session.Beat()), "derrubado");
			Assert.Equal(null, session.SessionId, "sem sessão");
		}

		[Test]
		private static void CloudSaveTravelsAsBase64AndAnEmptyCloudIsNotAnError()
		{
			var server = new FakeServer();
			server.On("auth/refresh", _ => (200, Tokens("r2")));
			server.On("save", _ => (404, null));
			var auth = new AuthClient(server.Client());
			auth.Restore("r1", User);
			var cloud = new CloudSave(auth);

			var empty = Run(cloud.Download());
			Assert.True(empty.Ok && empty.Copy == null, "conta nova: nuvem vazia");

			var json = "{\"Version\": 7, \"Nome\": \"Dragão\"}";
			server.On("save", request => request.Method == HttpMethod.Put ? (204, null) : (200, new { revision = 3, savedAt = DateTimeOffset.UtcNow, data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)) }));
			var session = Guid.NewGuid();
			Assert.True(Run(cloud.Upload(session, 2, 3, json)).Ok, "enviou");
			var sent = server.Body(server.Paths.Count - 1);
			Assert.Equal(json, Encoding.UTF8.GetString(Convert.FromBase64String(sent.GetProperty("data").GetString()!)), "o JSON vai em Base64");
			Assert.Equal(2L, sent.GetProperty("baseRevision").GetInt64(), "com a revisão de partida");

			var copy = Run(cloud.Download()).Copy;
			Assert.Equal(json, copy?.Json, "volta igual");
			Assert.Equal(3L, copy?.Revision, "com a revisão");

			var paths = server.Paths.Count;
			var huge = Run(cloud.Upload(session, 3, 4, new string('x', CloudSave.MaxBytes + 1)));
			Assert.Equal("save_too_large", huge.Error, "grande demais nem sai do aparelho");
			Assert.Equal(paths, server.Paths.Count, "nenhuma pergunta ao servidor");
		}

		[Test]
		private static void CloudSaveGzipsOnlyWhenTheServerAdvertisesIt()
		{
			var server = new FakeServer();
			server.On("auth/refresh", _ => (200, Tokens("r2")));
			server.On("save", request => request.Method == HttpMethod.Put ? (204, null) : (200, new { revision = 3, savedAt = DateTimeOffset.UtcNow, gz = Convert.ToBase64String(Core.Player.SaveCodec.Compress(Encoding.UTF8.GetBytes("{\"a\":1}"))) }));
			var auth = new AuthClient(server.Client());
			auth.Restore("r1", User);
			var format = 0;
			var cloud = new CloudSave(auth, () => format);
			var json = "{\"Nome\": \"Dragão\"}";

			Run(cloud.Upload(Guid.NewGuid(), 0, 1, json));
			Assert.True(server.Body(server.Paths.Count - 1).TryGetProperty("data", out _), "servidor antigo: JSON puro");

			format = 1;
			Run(cloud.Upload(Guid.NewGuid(), 1, 2, json));
			var sent = server.Body(server.Paths.Count - 1);
			Assert.False(sent.TryGetProperty("data", out _), "sem o campo puro");
			Assert.Equal(json, Encoding.UTF8.GetString(Core.Player.SaveCodec.Decompress(Convert.FromBase64String(sent.GetProperty("gz").GetString()!))!), "gzip volta igual");
			Assert.Equal("{\"a\":1}", Run(cloud.Download()).Copy?.Json, "o download em gzip também lê");
		}

		[Test]
		private static void CloudSaveSendsADeltaOnlyWhenTheServerAcceptsIt()
		{
			var server = new FakeServer();
			server.On("auth/refresh", _ => (200, Tokens("r2")));
			server.On("save/delta", _ => (204, null));
			var auth = new AuthClient(server.Client());
			auth.Restore("r1", User);
			var format = 1;
			var cloud = new CloudSave(auth, () => format);
			Assert.False(cloud.CanDelta, "formato 1: só o save inteiro");
			format = 2;
			Assert.True(cloud.CanDelta, "formato 2: aceita delta");

			var baseline = "{\"Version\":1,\"Essence\":10,\"Monsters\":[{\"Id\":1,\"Level\":1}]}";
			var next = "{\"Version\":1,\"Essence\":7,\"Monsters\":[{\"Id\":1,\"Level\":2}]}";
			var delta = Core.Player.SaveDelta.Diff(baseline, next)!;
			Run(cloud.UploadDelta(Guid.NewGuid(), 4, 5, delta, Core.Player.SaveCodec.Hash(next)));
			var sent = server.Body(server.Paths.Count - 1);
			var opened = Encoding.UTF8.GetString(Core.Player.SaveCodec.Decompress(Convert.FromBase64String(sent.GetProperty("delta").GetString()!))!);
			Assert.Equal(next, Core.Player.SaveDelta.Apply(baseline, opened), "o delta enviado leva ao save novo");
			Assert.Equal(Core.Player.SaveCodec.Hash(next), sent.GetProperty("hash").GetString(), "com o hash do save inteiro");
			Assert.Equal(4L, sent.GetProperty("baseRevision").GetInt64(), "e a revisão de partida");
		}

		[Test]
		private static void SendUsesTheDeltaAndFallsBackToTheWholeSave()
		{
			var baseline = "{\"Version\":1,\"Essence\":10,\"Names\":\"" + new string('x', 400) + "\"}";
			var next = baseline.Replace("\"Essence\":10", "\"Essence\":7");

			int Count(FakeServer server, string path) => System.Linq.Enumerable.Count(server.Paths, p => p == path);

			CloudSave Open(FakeServer server, int format = 2)
			{
				server.On("auth/refresh", _ => (200, Tokens("r2")));
				server.On("save", _ => (204, null));
				var auth = new AuthClient(server.Client());
				auth.Restore("r1", User);
				return new CloudSave(auth, () => format);
			}

			var good = new FakeServer();
			good.On("save/delta", _ => (204, null));
			Assert.True(Run(Open(good).Send(Guid.NewGuid(), 4, next, baseline)).Ok, "delta aceito");
			Assert.Equal(1, Count(good, "save/delta"), "uma chamada de delta");
			Assert.Equal(0, Count(good, "save"), "sem enviar o save inteiro");

			var noBaseline = new FakeServer();
			Run(Open(noBaseline).Send(Guid.NewGuid(), 4, next, null));
			Assert.Equal(0, Count(noBaseline, "save/delta"), "sem ponto de partida: save inteiro");
			Assert.Equal(1, Count(noBaseline, "save"), "um save inteiro");

			var oldServer = new FakeServer();
			Run(Open(oldServer, 1).Send(Guid.NewGuid(), 4, next, baseline));
			Assert.Equal(0, Count(oldServer, "save/delta"), "formato 1: sem delta");

			var mismatch = new FakeServer();
			mismatch.On("save/delta", _ => (409, new { error = "delta_mismatch" }));
			var cloud = Open(mismatch);
			Assert.True(Run(cloud.Send(Guid.NewGuid(), 4, next, baseline)).Ok, "o mesmo envio cai para o save inteiro");
			Assert.Equal(1, Count(mismatch, "save"), "um save inteiro");
			Assert.True(cloud.CanDelta, "uma falha ainda tenta");
			Run(cloud.Send(Guid.NewGuid(), 4, next, baseline));
			Assert.False(cloud.CanDelta, "duas falhas seguidas desligam o delta");
			var before = Count(mismatch, "save/delta");
			Run(cloud.Send(Guid.NewGuid(), 4, next, baseline));
			Assert.Equal(before, Count(mismatch, "save/delta"), "desligado: nem tenta");

			var rolledBack = new FakeServer();
			rolledBack.On("save/delta", _ => (404, null));
			Assert.True(Run(Open(rolledBack).Send(Guid.NewGuid(), 4, next, baseline)).Ok, "rota que não existe: save inteiro");

			var conflict = new FakeServer();
			conflict.On("save/delta", _ => (409, new { error = "save_conflict", revision = 9 }));
			var refused = Run(Open(conflict).Send(Guid.NewGuid(), 4, next, baseline));
			Assert.True(refused.Conflict("save_conflict"), "conflito de revisão volta como está");
			Assert.Equal(0, Count(conflict, "save"), "e não manda o inteiro por cima");
		}

		[Test]
		private static void CloudActionSendsTheRequestAsTextAndReadsTheDelta()
		{
			var request = new Core.Progression.ActionRequest { Kind = Core.Progression.ServerActionKind.GrindRune, RuneId = 4, Index = 1, Tool = new Core.Runes.RuneTool(Core.Runes.RuneToolKind.Grindstone, Core.Runes.RuneStat.Speed, Core.Content.RuneRarity.Rare) };
			var delta = new Core.Progression.ActionDelta { Kind = request.Kind, Essence = 7 };
			var server = new FakeServer();
			server.On("auth/refresh", _ => (200, Tokens("r2")));
			server.On("game/action", _ => (200, new { revision = 5, delta = delta.ToJson() }));
			var auth = new AuthClient(server.Client());
			auth.Restore("r1", User);
			var response = Run(new CloudSave(auth).Action(Guid.NewGuid(), 4, request));
			Assert.True(response.Ok, "o servidor respondeu");
			var sent = server.Body(server.Paths.Count - 1);
			Assert.Equal(4L, sent.GetProperty("baseRevision").GetInt64(), "com a revisão de partida");
			Assert.Equal(4, Core.Progression.ActionRequest.FromJson(sent.GetProperty("request").GetString()!)!.RuneId, "o pedido volta do texto");
			Assert.Equal(5L, response.Number("revision"), "a revisão nova");
			Assert.Equal(7, Core.Progression.ActionDelta.FromJson(response.Text("delta")!)!.Essence, "o resultado volta do texto");
		}

		private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

		/// <summary>A resposta de login ou renovação: o JWT de acesso tem o <c>user_id</c> no miolo.</summary>
		private static object Tokens(string refresh, string? name = null)
		{
			static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
			var access = $"{Part("{\"alg\":\"HS256\"}")}.{Part($"{{\"user_id\":\"{User}\"}}")}.assinatura";
			return new { accessToken = access, expiresIn = 900, refreshToken = refresh, name };
		}

		/// <summary>Um servidor de mentira: responde por caminho e anota cada pergunta (caminho e corpo).</summary>
		private sealed class FakeServer : HttpMessageHandler
		{
			private readonly Dictionary<string, Func<HttpRequestMessage, (int Status, object? Json)>> _routes = new();
			private readonly List<string?> _bodies = new();

			public List<string> Paths { get; } = new();

			public TimeSpan Delay { get; init; }

			public bool Unreachable { get; init; }

			public void On(string path, Func<HttpRequestMessage, (int Status, object? Json)> route) => _routes[path] = route;

			public HttpClient Client() => new(this) { BaseAddress = new Uri("http://sigilos.test/") };

			public JsonElement Body(int index) => JsonDocument.Parse(_bodies[index]!).RootElement;

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				if (Unreachable)
					throw new HttpRequestException("sem rede");

				var path = request.RequestUri!.AbsolutePath.TrimStart('/');
				Paths.Add(path);
				_bodies.Add(request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null);
				if (Delay > TimeSpan.Zero)
					await Task.Delay(Delay, cancellationToken);

				var (status, json) = _routes.TryGetValue(path, out var route) ? route(request) : (500, null);
				var response = new HttpResponseMessage((HttpStatusCode)status);
				if (json != null)
					response.Content = new StringContent(JsonSerializer.Serialize(json), Encoding.UTF8, "application/json");
				return response;
			}
		}
	}
}
