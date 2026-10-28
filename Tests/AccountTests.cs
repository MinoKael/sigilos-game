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
	/// A conta do lado do jogo (GameEntry/Account), sem rede: a conversa com um servidor falso (tokens, sessão
	/// tomada, download do save, ações e troca de nome).
	/// </summary>
	internal static class AccountTests
	{
		private static readonly Guid User = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

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
		private static void CloudSaveOnlyDownloadsAndAnEmptyCloudIsNotAnError()
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
			server.On("save", _ => (200, new { revision = 3, savedAt = DateTimeOffset.UtcNow, data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)) }));
			var copy = Run(cloud.Download()).Copy;
			Assert.Equal(json, copy?.Json, "volta igual");
			Assert.Equal(3L, copy?.Revision, "com a revisão");

			server.On("save", _ => (200, new { revision = 4, savedAt = DateTimeOffset.UtcNow, gz = Convert.ToBase64String(Core.Player.SaveCodec.Compress(Encoding.UTF8.GetBytes("{\"a\":1}"))) }));
			Assert.Equal("{\"a\":1}", Run(cloud.Download()).Copy?.Json, "o download em gzip também lê");
		}

		[Test]
		private static void CloudActionSendsTheRequestAsTextAndReadsTheDelta()
		{
			var request = new Core.Progression.ActionRequest { Kind = Core.Progression.ServerActionKind.GrindRune, RuneId = 4, Index = 1, Tool = new Core.Runes.RuneTool(Core.Runes.RuneToolKind.Grindstone, Core.Runes.RuneStat.Speed, Core.Content.RuneRarity.Rare) };
			var delta = new Core.Progression.ActionDelta { Kind = request.Kind, Count = 7 };
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
			Assert.Equal(7, Core.Progression.ActionDelta.FromJson(response.Text("delta")!)!.Count, "o resultado volta do texto");
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
