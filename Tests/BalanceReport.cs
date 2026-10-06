using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// O relatório da bancada de balanceamento (<see cref="BalanceLab"/>): um HTML só, com os dados embutidos e
	/// os gráficos em SVG desenhados no navegador. Abre direto do disco, sem servidor e sem internet.
	/// </summary>
	internal static class BalanceReport
	{
		/// <summary>Uma luta de exemplo inteira: quem lutou, a Vida de todos depois de cada turno e cada evento.</summary>
		internal sealed class FightLog
		{
			public string Title = "";
			public int Target;
			public string[] Ids = Array.Empty<string>();
			public bool Won;
			public int Seed;
			public double Time;
			public List<string> Allies = new();
			public List<object> Snaps = new();
			public List<object> Rows = new();
			public List<object> Waves = new();
			public double[] Damage = Array.Empty<double>();
			public double[] Healing = Array.Empty<double>();
			public double[] Taken = Array.Empty<double>();
			public List<object> Units = new();
		}

		private static Dictionary<StatusKind, string>? _statusNames;

		/// <summary>O nome de cada efeito no jogo (Data/texts/pt-BR.json, "effect.Nome.name").</summary>
		private static string StatusName(StatusKind status)
		{
			if (_statusNames == null)
			{
				var names = new Dictionary<StatusKind, string>();
				try
				{
					var text = File.ReadAllText(Path.Combine(Program.ProjectRoot, "Data", "texts", "pt-BR.json"));
					using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
					foreach (var kind in Enum.GetValues<StatusKind>())
					{
						if (doc.RootElement.TryGetProperty("effect", out var effects) && effects.TryGetProperty(kind.ToString(), out var entry) && entry.TryGetProperty("name", out var name))
							names[kind] = name.GetString() ?? kind.ToString();
					}
				}
				catch (IOException)
				{
				}

				_statusNames = names;
			}

			return _statusNames.TryGetValue(status, out var found) ? found : status.ToString();
		}

		private static double? Num(double value, int digits = 3) => double.IsNaN(value) || double.IsInfinity(value) ? null : Math.Round(value, digits);

		/// <summary>Luta de novo, turno a turno, guardando a Vida de todos e cada evento com quem o causou.</summary>
		internal static FightLog Record(GameDatabase database, BattleTeam team, BalanceLab.Target target, int seed, string title, int targetIndex)
		{
			var session = BattleFactory.Create(database, team, target.Encounter, seed);
			var log = new FightLog { Title = title, Target = targetIndex, Seed = seed, Ids = team.Members.Select(m => m.Summon.Id).ToArray() };
			var labels = new Dictionary<BattleUnit, string>();
			var allies = session.Allies.ToList();
			for (var i = 0; i < allies.Count; i++)
				labels[allies[i]] = team.Members[i].Summon.Name;
			log.Allies = allies.Select(a => labels[a]).ToList();
			log.Damage = new double[allies.Count];
			log.Healing = new double[allies.Count];
			log.Taken = new double[allies.Count];
			log.Units = allies.Select(a => (object)new
			{
				name = labels[a], awakenedName = a.Awakened ? a.Name : null, element = a.Element.ToString(), level = a.Level, awakened = a.Awakened,
				hp = Math.Round(a.Stats.Health), atk = Math.Round(a.Stats.Attack), def = Math.Round(a.Stats.Defense), spd = Math.Round(a.TurnSpeed),
				crit = Math.Round(a.Stats.Crit, 3), critDamage = Math.Round(a.Stats.CritDamage, 3), res = Math.Round(a.Stats.Resistance, 3), acc = Math.Round(a.Stats.Accuracy, 3),
				skills = a.Skills.Select(s => s.Name).Concat(a.Passive != null ? new[] { "(Passiva)" } : Array.Empty<string>()).ToArray(),
			}).ToList();

			BattleUnit? source = null;
			string Label(BattleUnit unit)
			{
				if (labels.TryGetValue(unit, out var label))
					return label;
				var same = session.Enemies.Where(e => e.Name == unit.Name).ToList();
				label = same.Count > 1 ? $"{unit.Name} {same.IndexOf(unit) + 1}" : unit.Name;
				labels[unit] = label;
				return label;
			}

			string Side(BattleUnit unit) => unit.Side == Core.Battle.Side.Allies ? "a" : "e";
			void Row(string kind, BattleUnit? subject, string text, double value = 0) => log.Rows.Add(new
			{
				t = Math.Round(session.Time, 2), r = session.Round, w = session.Wave, k = kind, s = subject == null ? "" : Side(subject), x = text, v = Math.Round(value),
			});
			void Credit(double[] table, BattleUnit? unit, double amount)
			{
				var index = unit == null ? -1 : allies.IndexOf(unit);
				if (index >= 0)
					table[index] += amount;
			}

			void Feed(IEnumerable<BattleEvent> events)
			{
				foreach (var e in events)
				{
					switch (e)
					{
						case WaveStarted w:
							log.Waves.Add(new { t = Math.Round(session.Time, 2), w = w.Wave, units = w.Enemies.Select(Label).ToArray(), bosses = w.Enemies.Select(u => u.IsBoss).ToArray() });
							Row("wave", null, $"Onda {w.Wave} de {w.WaveCount}: {string.Join(", ", w.Enemies.Select(Label))}");
							break;
						case TurnStarted ts:
							source = ts.Actor;
							Row("turn", ts.Actor, $"Vez de {Label(ts.Actor)}");
							break;
						case SkillUsed su:
							source = su.Actor;
							Row("skill", su.Actor, $"{Label(su.Actor)} usa {su.Skill.Name}");
							break;
						case Damaged d:
						{
							var notes = new List<string>();
							if (d.Crit)
								notes.Add("crítico");
							if (d.ElementMultiplier > 1)
								notes.Add("vantagem");
							else if (d.ElementMultiplier < 1)
								notes.Add("desvantagem");
							if (d.Absorbed > 0)
								notes.Add($"escudo absorveu {d.Absorbed:N0}");
							if (source != null && source.Side != d.Target.Side)
								Credit(log.Damage, source, d.Amount + d.Absorbed);
							Credit(log.Taken, d.Target, d.Amount + d.Absorbed);
							var by = source != null && source != d.Target ? $"{Label(source)} → " : "";
							Row("damage", d.Target, $"{by}{Label(d.Target)} −{d.Amount:N0}{(notes.Count > 0 ? $" ({string.Join(", ", notes)})" : "")}", d.Amount + d.Absorbed);
							break;
						}
						case Missed m:
							Row("miss", m.Target, $"{Label(m.Target)}: o golpe errou");
							break;
						case Protected p:
							Row("buff", p.Target, $"{Label(p.Target)}: a Égide anulou o golpe");
							break;
						case Healed h:
							if (source != null && source.Side == h.Target.Side)
								Credit(log.Healing, source, h.Amount);
							Row("heal", h.Target, $"{Label(h.Target)} +{h.Amount:N0}", h.Amount);
							break;
						case StatusApplied sa:
							Row(BattleRules.IsNegative(sa.Status) ? "debuff" : "buff", sa.Target, $"{Label(sa.Target)} recebe {StatusName(sa.Status)} ({sa.Turns} t)");
							break;
						case Resisted rs:
							Row("resist", rs.Target, $"{Label(rs.Target)} resistiu");
							break;
						case Immune im:
							Row("resist", im.Target, $"{Label(im.Target)} está imune");
							break;
						case StatusBlocked sb:
							Row("resist", sb.Target, $"{Label(sb.Target)}: {StatusName(sb.Status)} barrado");
							break;
						case StatusRemoved sr:
							Row("expire", sr.Target, $"{Label(sr.Target)} perde {StatusName(sr.Status)}");
							break;
						case DurationChanged dc:
							Row("expire", dc.Target, $"{Label(dc.Target)}: {StatusName(dc.Status)} {(dc.Turns > 0 ? "+" : "")}{dc.Turns} t");
							break;
						case ImpetoChanged ic:
							Row("impeto", ic.Target, $"{Label(ic.Target)} Ímpeto {(ic.Amount > 0 ? "+" : "")}{ic.Amount:0}");
							break;
						case TurnSkipped sk:
							Row("skip", sk.Unit, $"{Label(sk.Unit)} perde o turno{(sk.Cause is { } cause ? $" ({StatusName(cause)})" : "")}");
							break;
						case ExtraTurn et:
							Row("extra", et.Unit, $"{Label(et.Unit)} ganha um turno extra");
							break;
						case Counterattack ca:
							source = ca.Unit;
							Row("extra", ca.Unit, $"{Label(ca.Unit)} contra-ataca");
							break;
						case JointAttack ja:
							source = ja.Unit;
							Row("extra", ja.Unit, $"{Label(ja.Unit)} ataca junto");
							break;
						case HealthLeveled hl:
							Row("heal", hl.Target, $"{Label(hl.Target)} nivela a Vida ({(hl.Amount > 0 ? "+" : "")}{hl.Amount:N0})");
							break;
						case MaxHealthReduced mr:
							Row("damage", mr.Target, $"{Label(mr.Target)} perde {mr.Amount:N0} de Vida máxima");
							break;
						case Died di:
							Row("death", di.Unit, $"{Label(di.Unit)} cai");
							break;
						case Revived rv:
							Row("revive", rv.Unit, $"{Label(rv.Unit)} volta à luta");
							break;
						case BattleEnded be:
							Row("end", null, be.Victory ? "Vitória" : "Derrota");
							break;
					}
				}
			}

			void Snap()
			{
				double Fraction(BattleUnit u) => u.MaxHealth <= 0 ? 0 : Math.Round(Math.Max(0, u.Health) / u.MaxHealth, 4);
				var enemies = session.Enemies;
				var total = enemies.Sum(u => u.MaxHealth);
				log.Snaps.Add(new
				{
					t = Math.Round(session.Time, 3), r = session.Round, w = session.Wave,
					a = allies.Select(Fraction).ToArray(),
					e = Math.Round(total <= 0 ? 0 : enemies.Sum(u => Math.Max(0, u.Health)) / total, 4),
					eu = enemies.Select(Fraction).ToArray(),
				});
			}

			Feed(session.Start());
			Snap();
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				Feed(turn.Events);
				if (turn.NeedsDecision)
					Feed(session.Act(AutoPilot.For(session, turn.Actor)));
				Snap();
			}

			log.Won = session.Victory == true;
			log.Time = Math.Round(session.Time, 2);
			return log;
		}

		/// <summary>O HTML inteiro: os dados como JSON no fim, e o desenho em JavaScript.</summary>
		internal static string Html(GameDatabase database, BalanceLab.Settings settings, List<BalanceLab.Comp> comps, System.Numerics.BigInteger total,
			List<FightLog> samples, TimeSpan elapsed)
		{
			var ids = comps.SelectMany(c => c.Ids).Concat(settings.Pool.Select(s => s.Id)).Distinct().ToList();
			var data = new
			{
				meta = new
				{
					generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
					command = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Where(a => a.StartsWith("--", StringComparison.Ordinal))),
					investment = settings.Investment.Describe(),
					preset = settings.Investment.Name,
					pool = settings.PoolLabel,
					poolSize = settings.Pool.Count,
					size = settings.Size,
					must = settings.Must,
					max5 = settings.Max5,
					max4 = settings.Max4,
					min5 = settings.Min5,
					min4 = settings.Min4,
					comps = comps.Count,
					total = total.ToString(),
					fights = settings.Fights,
					seed = settings.Seed,
					seconds = Math.Round(elapsed.TotalSeconds, 1),
					targets = settings.Targets.Select(t => t.Label).ToArray(),
				},
				summons = ids.ToDictionary(id => id, id =>
				{
					var s = database.Summon(id);
					return (object)new { n = s.Name, e = s.Element.ToString(), r = s.Role.ToString(), s = s.Rarity, f = s.FamilyId, fn = s.Family.Name };
				}),
				comps = comps.Select(c => new { i = c.Ids, w = c.Wins.Select(x => Num(x)).ToArray(), r = c.Rounds.Select(x => Num(x, 1)).ToArray(), h = c.Health.Select(x => Num(x)).ToArray() }),
				samples = samples.Select(s => new
				{
					title = s.Title, target = s.Target, ids = s.Ids, won = s.Won, seed = s.Seed, time = s.Time, allies = s.Allies, units = s.Units,
					snaps = s.Snaps, rows = s.Rows, waves = s.Waves,
					damage = s.Damage.Select(x => Math.Round(x)), healing = s.Healing.Select(x => Math.Round(x)), taken = s.Taken.Select(x => Math.Round(x)),
				}),
			};
			var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
				.Replace("</", "<\\/");
			return Template.Replace("__DATA__", json);
		}

		private const string Template = """
<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Bancada de balanceamento</title>
<style>
:root{
  color-scheme:light;
  --surface:#fcfcfb;--surface-2:#f3f2ef;--line:#dedcd6;--grid:#ecebe7;
  --text:#0b0b0b;--text-2:#52514e;--text-3:#7b7a75;
  --s1:#2a78d6;--s2:#eb6834;--s3:#1baf7a;--s4:#eda100;--s5:#e87ba4;
  --pos:#2a78d6;--neg:#e34948;--mid:#f0efec;
  --good:#0ca30c;--warning:#fab219;--critical:#d03b3b;
  --fire:#e34948;--water:#2a78d6;--wind:#1baf7a;--light:#eda100;--dark:#4a3aa7;
  --enemy:#52514e;
}
@media (prefers-color-scheme: dark){
  :root:where(:not([data-theme="light"])){
    color-scheme:dark;
    --surface:#1a1a19;--surface-2:#232321;--line:#3a3936;--grid:#2c2c2a;
    --text:#ffffff;--text-2:#c3c2b7;--text-3:#8f8e86;
    --s1:#3987e5;--s2:#d95926;--s3:#199e70;--s4:#c98500;--s5:#d55181;
    --pos:#3987e5;--neg:#e66767;--mid:#383835;
    --fire:#e66767;--water:#3987e5;--wind:#199e70;--light:#c98500;--dark:#9085e9;
    --enemy:#c3c2b7;
  }
}
:root[data-theme="dark"]{
  color-scheme:dark;
  --surface:#1a1a19;--surface-2:#232321;--line:#3a3936;--grid:#2c2c2a;
  --text:#ffffff;--text-2:#c3c2b7;--text-3:#8f8e86;
  --s1:#3987e5;--s2:#d95926;--s3:#199e70;--s4:#c98500;--s5:#d55181;
  --pos:#3987e5;--neg:#e66767;--mid:#383835;
  --fire:#e66767;--water:#3987e5;--wind:#199e70;--light:#c98500;--dark:#9085e9;
  --enemy:#c3c2b7;
}
*{box-sizing:border-box}
body{margin:0;background:var(--surface);color:var(--text);font:14px/1.45 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
main{max-width:1280px;margin:0 auto;padding:20px 16px 60px}
h1{font-size:22px;margin:0 0 4px}h2{font-size:17px;margin:28px 0 10px}h3{font-size:15px;margin:18px 0 8px}
p,.muted{color:var(--text-2)}.small{font-size:12px}
code{font:12px ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;background:var(--surface-2);padding:1px 5px;border-radius:5px}
.meta{display:flex;flex-wrap:wrap;gap:6px;margin:10px 0}
.tag{border:1px solid var(--line);border-radius:999px;padding:2px 9px;font-size:12px;color:var(--text-2)}
.tabs{display:flex;flex-wrap:wrap;gap:6px;margin:16px 0 4px;position:sticky;top:0;background:var(--surface);padding:8px 0;z-index:5;border-bottom:1px solid var(--line)}
.tabs button{border:1px solid var(--line);background:var(--surface-2);color:var(--text);border-radius:8px;padding:6px 12px;cursor:pointer;font:inherit}
.tabs button.on{border-color:var(--s1);box-shadow:inset 0 0 0 1px var(--s1)}
.kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px}
.kpi{border:1px solid var(--line);border-radius:10px;padding:10px 12px;background:var(--surface-2)}
.kpi .k{font-size:12px;color:var(--text-2)}.kpi .v{font-size:24px;font-weight:600}.kpi .d{font-size:12px;color:var(--text-3)}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:16px}
@media (max-width:900px){.grid2{grid-template-columns:1fr}}
.card{border:1px solid var(--line);border-radius:12px;padding:12px 14px;background:var(--surface)}
.chart{width:100%;overflow:visible}
.chart text{fill:var(--text-2);font-size:11px}
.chart .axis{stroke:var(--line)}.chart .gridline{stroke:var(--grid)}
.tip{position:fixed;pointer-events:none;background:var(--surface);border:1px solid var(--line);border-radius:8px;padding:7px 9px;font-size:12px;box-shadow:0 6px 18px rgba(0,0,0,.18);z-index:20;display:none;max-width:320px}
.tip b{font-weight:600}.tip .row{display:flex;align-items:center;gap:6px}
.sw{display:inline-block;width:10px;height:10px;border-radius:3px;flex:none}
table{border-collapse:collapse;width:100%;font-size:13px}
th,td{padding:6px 8px;border-bottom:1px solid var(--grid);text-align:left;vertical-align:middle}
th{color:var(--text-2);font-weight:600;cursor:pointer;white-space:nowrap;user-select:none}
td.num,th.num{text-align:right;font-variant-numeric:tabular-nums}
.wrap{overflow:auto;border:1px solid var(--line);border-radius:10px}
.chip{display:inline-flex;align-items:center;gap:5px;border:1px solid var(--line);border-radius:999px;padding:1px 8px 1px 6px;margin:1px 3px 1px 0;font-size:12px;white-space:nowrap}
.chip .dot{width:8px;height:8px;border-radius:50%}
.chip .st{color:var(--text-3);font-size:11px}
.lead{border-color:var(--text-3)}
.bar{display:flex;align-items:center;gap:6px}
.bar .track{position:relative;width:140px;height:10px;background:var(--mid);border-radius:4px}
.bar .fill{position:absolute;top:0;height:10px;border-radius:4px}
.controls{display:flex;flex-wrap:wrap;gap:10px;align-items:center;margin:8px 0}
input[type=search],select{background:var(--surface-2);border:1px solid var(--line);color:var(--text);border-radius:8px;padding:6px 9px;font:inherit}
button.more{margin-top:8px;border:1px solid var(--line);background:var(--surface-2);color:var(--text);border-radius:8px;padding:6px 12px;cursor:pointer;font:inherit}
.legend{display:flex;flex-wrap:wrap;gap:12px;font-size:12px;color:var(--text-2);margin:4px 0 6px}
.legend span{display:inline-flex;align-items:center;gap:5px}
.badge{display:inline-block;border-radius:6px;padding:1px 8px;font-size:12px;font-weight:600;border:1px solid}
.badge.win{color:var(--good);border-color:var(--good)}.badge.loss{color:var(--critical);border-color:var(--critical)}
.log{max-height:520px;overflow:auto;border:1px solid var(--line);border-radius:10px;font-size:12.5px}
.log .r{display:grid;grid-template-columns:86px 22px 1fr;gap:6px;padding:2px 10px;border-bottom:1px solid var(--grid)}
.log .r .t{color:var(--text-3);font-variant-numeric:tabular-nums;white-space:nowrap;font-weight:400}
.log .r.turn{background:var(--surface-2);font-weight:600}
.log .r.wave,.log .r.end{background:var(--surface-2);font-weight:700}
.log .r.a .x{} .log .r.e .x{color:var(--text-2)}
.log .r.death .x{font-weight:600}
.filters{display:flex;flex-wrap:wrap;gap:10px;font-size:12px;color:var(--text-2);margin:6px 0}
.stats td{padding:3px 8px}
.weigh{display:grid;grid-template-columns:minmax(0,210px) 1fr;align-items:center;gap:8px;margin:4px 0}
.weigh .chip{overflow:hidden;text-overflow:ellipsis;max-width:210px}
footer{margin-top:40px;color:var(--text-3);font-size:12px}
</style>
</head>
<body>
<main>
  <h1>Bancada de balanceamento</h1>
  <div class="muted small" id="sub"></div>
  <div class="meta" id="meta"></div>
  <div class="tabs" id="tabs"></div>
  <div id="view"></div>
  <footer id="foot"></footer>
</main>
<div class="tip" id="tip"></div>
<script>
const DATA = __DATA__;
const $ = id => document.getElementById(id);
const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const pct = (x, d = 0) => x == null ? "—" : (x * 100).toLocaleString("pt-BR", {minimumFractionDigits: d, maximumFractionDigits: d}) + "%";
const fmt = (x, d = 1) => x == null ? "—" : Number(x).toLocaleString("pt-BR", {minimumFractionDigits: d, maximumFractionDigits: d});
const int = x => x == null ? "—" : Math.round(x).toLocaleString("pt-BR");
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const ELEMENT = {Fire: "Fogo", Water: "Água", Wind: "Vento", Light: "Luz", Dark: "Trevas"};
const ROLE = {Attack: "Ataque", Defense: "Defesa", HP: "Vida", Support: "Suporte"};
const SERIES = ["--s1", "--s2", "--s3", "--s4", "--s5"];
const M = DATA.meta, S = DATA.summons;
let target = 0;

// Dica flutuante ----------------------------------------------------------------------------------
const tip = $("tip");
function showTip(html, ev) {
  tip.innerHTML = html; tip.style.display = "block";
  const w = tip.offsetWidth, h = tip.offsetHeight;
  let x = ev.clientX + 14, y = ev.clientY + 14;
  if (x + w > innerWidth - 8) x = ev.clientX - w - 14;
  if (y + h > innerHeight - 8) y = ev.clientY - h - 14;
  tip.style.left = x + "px"; tip.style.top = y + "px";
}
const hideTip = () => tip.style.display = "none";

function chip(id, lead = false) {
  const s = S[id] || {n: id, e: "", s: ""};
  return `<span class="chip ${lead ? "lead" : ""}" title="${esc(ROLE[s.r] || s.r || "")}${lead ? " · líder" : ""}"><span class="dot" style="background:var(--${(s.e || "").toLowerCase()})"></span>${esc(s.n)} <span class="st">${s.s}★</span></span>`;
}
const team = ids => ids.map((id, i) => chip(id, i === 0)).join("");

// Cabeçalho ---------------------------------------------------------------------------------------
$("sub").textContent = `Gerado em ${M.generated} · ${M.seconds} s de simulação`;
$("meta").innerHTML = [
  `${M.comps.toLocaleString("pt-BR")} composições${BigInt(M.total) > BigInt(M.comps) ? ` (amostra de ${BigInt(M.total).toLocaleString("pt-BR")})` : ""}`,
  `${M.fights} lutas cada`, `pool: ${M.pool} (${M.poolSize} invocações)`, `${M.size} por time`,
  M.must.length ? `sempre: ${M.must.map(id => S[id]?.n || id).join(", ")}` : null,
  M.max5 != null ? `no máximo ${M.max5} de 5★` : null, M.max4 != null ? `no máximo ${M.max4} de 4★` : null,
  M.min5 != null ? `pelo menos ${M.min5} de 5★` : null, M.min4 != null ? `pelo menos ${M.min4} de 4★` : null,
  `investimento: ${M.preset} — ${M.investment}`, `semente ${M.seed}`,
].filter(Boolean).map(t => `<span class="tag">${esc(t)}</span>`).join("");
$("tabs").innerHTML = M.targets.map((t, i) => `<button data-t="${i}">${esc(t)}</button>`).join("");
$("tabs").addEventListener("click", e => { const b = e.target.closest("button"); if (b) { target = +b.dataset.t; render(); } });
$("foot").innerHTML = `Refazer: <code>dotnet run --project Tests -c Release -- ${esc(M.command)}</code>. As lutas são as do jogo (o automático, sem foco no chefe); com a semente 1, a luta i usa a semente i × 7919, a mesma dos testes de calibração.`;

// Estatística por encontro --------------------------------------------------------------------------
function stats(t) {
  const comps = DATA.comps.map(c => ({ids: c.i, w: c.w[t], r: c.r[t], h: c.h[t]}));
  comps.sort((a, b) => b.w - a.w || (a.r ?? 1e9) - (b.r ?? 1e9));
  const mean = comps.reduce((s, c) => s + c.w, 0) / comps.length;
  const impact = (key) => {
    const groups = new Map();
    for (const c of comps) for (const id of new Set(c.ids.map(key))) {
      if (!groups.has(id)) groups.set(id, {id, n: 0, w: 0, r: 0, rn: 0});
      const g = groups.get(id); g.n++; g.w += c.w; if (c.r != null && c.w > 0) { g.r += c.r; g.rn++; }
    }
    return [...groups.values()].map(g => {
      const without = comps.length - g.n, sumWithout = mean * comps.length - g.w;
      const withRate = g.w / g.n, withoutRate = without > 0 ? sumWithout / without : null;
      return {...g, with: withRate, without: withoutRate, lift: withoutRate == null ? null : withRate - withoutRate, rounds: g.rn ? g.r / g.rn : null};
    });
  };
  return {comps, mean, summons: impact(id => id), families: impact(id => S[id]?.f || id)};
}

// Gráficos ------------------------------------------------------------------------------------------
function histogram(comps) {
  const bins = Array.from({length: 10}, (_, i) => ({lo: i / 10, hi: (i + 1) / 10, n: 0}));
  for (const c of comps) bins[Math.min(9, Math.floor(c.w * 10 + 1e-9))].n++;
  const W = 560, H = 220, L = 44, B = 34, T = 12, R = 8, max = Math.max(1, ...bins.map(b => b.n));
  const step = (W - L - R) / bins.length, y = v => T + (H - T - B) * (1 - v / max);
  const ticks = niceTicks(max);
  let svg = `<svg class="chart" viewBox="0 0 ${W} ${H}" role="img" aria-label="Quantas composições vencem cada faixa de vitórias">`;
  for (const v of ticks) svg += `<line class="gridline" x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}"/><text x="${L - 6}" y="${y(v) + 4}" text-anchor="end">${v}</text>`;
  bins.forEach((b, i) => {
    const x = L + i * step + 1, w = step - 2, top = y(b.n), h = H - B - top;
    if (b.n > 0) svg += `<path d="${roundTop(x, top, w, h, 4)}" fill="var(--s1)"/>`;
    svg += `<rect x="${L + i * step}" y="${T}" width="${step}" height="${H - T - B}" fill="transparent" data-tip="<b>${(b.lo * 100).toFixed(0)}–${(b.hi * 100).toFixed(0)}% de vitórias</b><br>${b.n} composições"/>`;
    svg += `<text x="${L + i * step + step / 2}" y="${H - B + 14}" text-anchor="middle">${(b.lo * 100).toFixed(0)}</text>`;
  });
  svg += `<line class="axis" x1="${L}" x2="${W - R}" y1="${H - B}" y2="${H - B}"/><text x="${(W + L) / 2}" y="${H - 4}" text-anchor="middle">vitórias da composição (%)</text></svg>`;
  return svg;
}
function niceTicks(max) {
  const raw = max / 4, mag = Math.pow(10, Math.floor(Math.log10(raw))), step = [1, 2, 5, 10].map(m => m * mag).find(s => s >= raw);
  const out = []; for (let v = 0; v <= max + 1e-9; v += step) out.push(+v.toFixed(6)); return out;
}
function roundTop(x, y, w, h, r) {
  r = Math.min(r, h, w / 2);
  return `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${y + h}Z`;
}
function liftBar(lift, scale) {
  if (lift == null) return "—";
  const w = Math.min(70, Math.abs(lift) / scale * 70), left = lift >= 0 ? 70 : 70 - w;
  return `<span class="bar"><span class="track"><span class="fill" style="left:${left}px;width:${Math.max(1, w)}px;background:var(${lift >= 0 ? "--pos" : "--neg"})"></span><span style="position:absolute;left:69px;top:-2px;height:14px;width:2px;background:var(--text-3)"></span></span><span>${lift >= 0 ? "+" : "−"}${fmt(Math.abs(lift * 100), 1)} p.p.</span></span>`;
}

// Tabela que ordena pelo cabeçalho ---------------------------------------------------------------
function sortable(container, columns, rows, initial) {
  let key = initial, dir = -1, shown = 60, filter = "";
  const draw = () => {
    const list = rows.filter(r => !filter || r._search.includes(filter)).sort((a, b) => {
      const x = a[key], y = b[key];
      if (x == null && y == null) return 0; if (x == null) return 1; if (y == null) return -1;
      return (x > y ? 1 : x < y ? -1 : 0) * dir;
    });
    container.querySelector("tbody").innerHTML = list.slice(0, shown).map(r => `<tr>${columns.map(c => `<td class="${c.num ? "num" : ""}">${c.html(r)}</td>`).join("")}</tr>`).join("");
    container.querySelector(".more").style.display = list.length > shown ? "" : "none";
    container.querySelector(".count").textContent = `${Math.min(shown, list.length)} de ${list.length}`;
    container.querySelectorAll("th").forEach(th => th.textContent = th.dataset.label + (th.dataset.key === key ? (dir < 0 ? " ▾" : " ▴") : ""));
  };
  container.innerHTML = `<div class="controls"><input type="search" placeholder="filtrar por monstro ou família"><span class="muted small count"></span></div>
    <div class="wrap"><table><thead><tr>${columns.map(c => `<th class="${c.num ? "num" : ""}" data-key="${c.key || ""}" data-label="${esc(c.label)}">${esc(c.label)}</th>`).join("")}</tr></thead><tbody></tbody></table></div>
    <button class="more">mostrar mais</button>`;
  container.querySelector("input").addEventListener("input", e => { filter = e.target.value.toLowerCase().trim(); draw(); });
  container.querySelector(".more").addEventListener("click", () => { shown += 100; draw(); });
  container.querySelector("thead").addEventListener("click", e => {
    const th = e.target.closest("th"); if (!th || !th.dataset.key) return;
    if (th.dataset.key === key) dir = -dir; else { key = th.dataset.key; dir = -1; }
    draw();
  });
  draw();
}

// A luta de exemplo ---------------------------------------------------------------------------------
function hpChart(sample) {
  const W = 900, H = 300, L = 40, R = 140, T = 14, B = 30;
  const end = Math.max(1, sample.time), x = t => L + (W - L - R) * (t / end), y = v => T + (H - T - B) * (1 - v);
  const id = "c" + Math.random().toString(36).slice(2);
  let svg = `<svg class="chart" id="${id}" viewBox="0 0 ${W} ${H}" role="img" aria-label="Vida de cada aliado e da onda inimiga ao longo da luta">`;
  for (const v of [0, .25, .5, .75, 1]) svg += `<line class="gridline" x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}"/><text x="${L - 6}" y="${y(v) + 4}" text-anchor="end">${v * 100}%</text>`;
  for (const w of sample.waves.slice(1)) svg += `<line x1="${x(w.t)}" x2="${x(w.t)}" y1="${T}" y2="${H - B}" stroke="var(--text-3)" stroke-dasharray="3 4"/><text x="${x(w.t) + 4}" y="${T + 10}">onda ${w.w}</text>`;
  const roundsTick = Math.max(1, Math.ceil(end / 10));
  for (let r = 0; r <= end; r += roundsTick) svg += `<text x="${x(r)}" y="${H - B + 16}" text-anchor="middle">${r}</text>`;
  svg += `<text x="${(W - R + L) / 2}" y="${H - 2}" text-anchor="middle">tempo de luta (rodadas)</text>`;
  const path = f => sample.snaps.map((s, i) => `${i ? "L" : "M"}${x(s.t).toFixed(1)},${y(f(s)).toFixed(1)}`).join("");
  svg += `<path d="${path(s => s.e)}" fill="none" stroke="var(--enemy)" stroke-width="2" stroke-dasharray="6 4"/>`;
  sample.allies.forEach((_, i) => svg += `<path d="${path(s => s.a[i])}" fill="none" stroke="var(${SERIES[i % 5]})" stroke-width="2" stroke-linejoin="round"/>`);
  // Rótulos no fim de cada linha (sem se sobrepor) e as quedas marcadas.
  const last = sample.snaps[sample.snaps.length - 1];
  const labels = sample.allies.map((n, i) => ({n, y: y(last.a[i]), c: `var(${SERIES[i % 5]})`})).concat([{n: "Inimigos (onda)", y: y(last.e), c: "var(--enemy)"}]).sort((a, b) => a.y - b.y);
  for (let i = 1; i < labels.length; i++) labels[i].y = Math.max(labels[i].y, labels[i - 1].y + 13);
  for (const l of labels) svg += `<circle cx="${W - R + 6}" cy="${l.y - 4}" r="3.5" fill="${l.c}"/><text x="${W - R + 13}" y="${l.y}" style="fill:var(--text)">${esc(l.n)}</text>`;
  sample.rows.filter(r => r.k === "death" && r.s === "a").forEach(r => {
    const i = sample.allies.findIndex(n => r.x.startsWith(n)); if (i < 0) return;
    svg += `<circle cx="${x(r.t)}" cy="${y(0)}" r="5" fill="var(--surface)" stroke="var(${SERIES[i % 5]})" stroke-width="2"/>`;
  });
  svg += `<line class="cross" x1="0" x2="0" y1="${T}" y2="${H - B}" stroke="var(--text-3)" style="display:none"/>`;
  svg += `<rect x="${L}" y="${T}" width="${W - L - R}" height="${H - T - B}" fill="transparent" class="hit"/></svg>`;
  setTimeout(() => {
    const el = document.getElementById(id); if (!el) return;
    const cross = el.querySelector(".cross");
    el.querySelector(".hit").addEventListener("mousemove", ev => {
      const box = el.getBoundingClientRect(), t = ((ev.clientX - box.left) / box.width * W - L) / (W - L - R) * end;
      let snap = sample.snaps[0]; for (const s of sample.snaps) { if (s.t <= t) snap = s; else break; }
      cross.setAttribute("x1", x(snap.t)); cross.setAttribute("x2", x(snap.t)); cross.style.display = "";
      const wave = sample.waves.filter(w => w.w === snap.w)[0];
      showTip(`<b>Rodada ${snap.r} · onda ${snap.w}</b> <span class="muted">(t ${fmt(snap.t, 2)})</span>` +
        sample.allies.map((n, i) => `<div class="row"><span class="sw" style="background:var(${SERIES[i % 5]})"></span>${esc(n)}: ${pct(snap.a[i])}</div>`).join("") +
        `<div class="row"><span class="sw" style="background:var(--enemy)"></span>Onda: ${pct(snap.e)}</div>` +
        (wave ? wave.units.map((n, i) => `<div class="row muted small">· ${esc(n)}: ${pct(snap.eu[i])}</div>`).join("") : ""), ev);
    });
    el.querySelector(".hit").addEventListener("mouseleave", () => { cross.style.display = "none"; hideTip(); });
  });
  return svg;
}
function contribution(sample, key, label) {
  const values = sample[key], max = Math.max(1, ...values);
  return `<div class="small muted">${label}</div>` + sample.allies.map((n, i) => `<div class="bar small" style="margin:3px 0" title="${esc(n)}: ${int(values[i])}">
      <span style="width:150px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${esc(n)}</span>
      <span class="track" style="width:180px"><span class="fill" style="left:0;width:${values[i] / max * 180}px;background:var(${SERIES[i % 5]})"></span></span><span>${int(values[i])}</span></div>`).join("");
}
const KIND_ICON = {wave: "🌊", turn: "▸", skill: "✦", damage: "⚔", miss: "·", heal: "✚", buff: "▲", debuff: "▼", resist: "⛨", expire: "⌛", impeto: "»", skip: "⏸", extra: "↻", death: "☠", revive: "✧", end: "⚑"};
const KIND_GROUP = {damage: "dano", miss: "dano", heal: "cura", buff: "efeitos", debuff: "efeitos", resist: "efeitos", expire: "efeitos", impeto: "impeto", skip: "efeitos"};
function logView(sample) {
  const id = "l" + Math.random().toString(36).slice(2);
  const groups = ["dano", "cura", "efeitos", "impeto"];
  setTimeout(() => {
    const box = document.getElementById(id); if (!box) return;
    const draw = () => {
      const on = new Set([...box.querySelectorAll("input:checked")].map(i => i.value));
      box.querySelector(".log").innerHTML = sample.rows.filter(r => !KIND_GROUP[r.k] || on.has(KIND_GROUP[r.k])).map(r =>
        `<div class="r ${r.k} ${r.s}"><span class="t">r${r.r} · ${fmt(r.t, 2)}</span><span>${KIND_ICON[r.k] || ""}</span><span class="x">${esc(r.x)}</span></div>`).join("");
    };
    box.querySelectorAll("input").forEach(i => i.addEventListener("change", draw));
    draw();
  });
  return `<div id="${id}"><div class="filters">Mostrar: ${groups.map(g => `<label><input type="checkbox" value="${g}" checked> ${g}</label>`).join("")} <span class="muted">· ${sample.rows.length} eventos</span></div><div class="log"></div></div>`;
}
function unitsTable(sample) {
  return `<div class="wrap"><table class="stats"><thead><tr><th>Aliado</th><th class="num">Nível</th><th class="num">Vida</th><th class="num">Ataque</th><th class="num">Defesa</th><th class="num">Velocidade</th><th class="num">Crítico</th><th class="num">Dano crít.</th><th class="num">Resist.</th><th class="num">Precisão</th></tr></thead><tbody>` +
    sample.units.map((u, i) => `<tr><td><span class="sw" style="background:var(${SERIES[i % 5]})"></span> ${esc(u.name)}${u.awakenedName ? ` <span class='muted small'>desperto: ${esc(u.awakenedName)}</span>` : ""}</td><td class="num">${u.level}</td><td class="num">${int(u.hp)}</td><td class="num">${int(u.atk)}</td><td class="num">${int(u.def)}</td><td class="num">${int(u.spd)}</td><td class="num">${pct(u.crit)}</td><td class="num">${pct(u.critDamage)}</td><td class="num">${pct(u.res)}</td><td class="num">${pct(u.acc)}</td></tr>`).join("") +
    `</tbody></table></div>`;
}
function sampleCard(sample) {
  return `<div class="card" style="margin:12px 0">
    <div style="display:flex;justify-content:space-between;flex-wrap:wrap;gap:8px;align-items:center">
      <h3 style="margin:0">${esc(sample.title)}</h3>
      <span><span class="badge ${sample.won ? "win" : "loss"}">${sample.won ? "✓ Vitória" : "✕ Derrota"}</span> <span class="muted small">em ${fmt(sample.time, 1)} rodadas · semente ${sample.seed}</span></span>
    </div>
    <div style="margin:6px 0">${team(sample.ids)}</div>
    <div class="legend">${sample.allies.map((n, i) => `<span><span class="sw" style="background:var(${SERIES[i % 5]})"></span>${esc(n)}</span>`).join("")}<span><svg width="22" height="8"><line x1="0" x2="22" y1="4" y2="4" stroke="var(--enemy)" stroke-width="2" stroke-dasharray="6 4"/></svg>Inimigos (Vida da onda)</span><span>○ queda</span></div>
    ${hpChart(sample)}
    <div class="grid2" style="margin-top:10px"><div>${contribution(sample, "damage", "Dano causado (golpes, contragolpes e ataques juntos)")}</div><div>${contribution(sample, "healing", "Cura feita")}${contribution(sample, "taken", "Dano recebido")}</div></div>
    <h3>Atributos na luta</h3>${unitsTable(sample)}
    <h3>Log completo</h3>${logView(sample)}
  </div>`;
}

// A página de um encontro ------------------------------------------------------------------------
function render() {
  document.querySelectorAll("#tabs button").forEach((b, i) => b.classList.toggle("on", i === target));
  const st = stats(target), comps = st.comps, best = comps[0];
  const good = comps.filter(c => c.w >= 0.7).length, median = comps[Math.floor(comps.length / 2)];
  const scale = Math.max(0.01, ...st.summons.concat(st.families).map(s => Math.abs(s.lift ?? 0)));
  $("view").innerHTML = `
    <h2>${esc(M.targets[target])}</h2>
    <div class="kpis">
      <div class="kpi"><div class="k">Composições testadas</div><div class="v">${comps.length.toLocaleString("pt-BR")}</div><div class="d">${M.fights} lutas cada</div></div>
      <div class="kpi"><div class="k">Vencem 70% ou mais</div><div class="v">${pct(good / comps.length, 1)}</div><div class="d">${good} composições</div></div>
      <div class="kpi"><div class="k">Melhor</div><div class="v">${pct(best.w)}</div><div class="d">${best.r != null ? fmt(best.r) + " rodadas" : "sem vitória"}</div></div>
      <div class="kpi"><div class="k">Mediana</div><div class="v">${pct(median.w)}</div><div class="d">média ${pct(st.mean, 1)}</div></div>
    </div>
    <div class="grid2" style="margin-top:14px">
      <div class="card"><h3 style="margin-top:0">Distribuição das vitórias</h3>${histogram(comps)}</div>
      <div class="card"><h3 style="margin-top:0">Quem mais pesa</h3><p class="small">A diferença entre a vitória média das composições <b>com</b> o monstro e <b>sem</b> ele, em pontos percentuais.</p>
        ${st.summons.filter(s => s.lift != null).sort((a, b) => b.lift - a.lift).slice(0, 6).map(s => `<div class="weigh">${chip(s.id)} ${liftBar(s.lift, scale)}</div>`).join("")}
        <div class="small muted" style="margin-top:6px">E os que mais atrapalham:</div>
        ${st.summons.filter(s => s.lift != null).sort((a, b) => a.lift - b.lift).slice(0, 3).map(s => `<div class="weigh">${chip(s.id)} ${liftBar(s.lift, scale)}</div>`).join("")}
      </div>
    </div>
    <h2>Composições</h2><div id="compTable"></div>
    <h2>Peso de cada monstro</h2><div class="controls"><select id="impactBy"><option value="summons">por invocação</option><option value="families">por família</option></select></div><div id="impactTable"></div>
    <h2>Lutas de exemplo</h2><p class="small">Passe o mouse no gráfico para ver a Vida de todos naquele turno. Os aliados estão na ordem do time (o primeiro lidera).</p>
    <div id="samples">${DATA.samples.filter(s => s.target === target).map(sampleCard).join("") || "<p>Sem lutas de exemplo (--samples=0).</p>"}</div>`;

  sortable($("compTable"), [
    {label: "#", html: r => r.rank, key: "rank", num: true},
    {label: "Composição", html: r => team(r.ids)},
    {label: "Vitórias", key: "w", num: true, html: r => pct(r.w)},
    {label: "Rodadas (vitórias)", key: "r", num: true, html: r => fmt(r.r)},
    {label: "Vida que sobra", key: "h", num: true, html: r => pct(r.h)},
  ], comps.map((c, i) => ({...c, rank: i + 1, _search: c.ids.map(id => `${S[id]?.n} ${S[id]?.fn} ${id}`).join(" ").toLowerCase()})), "w");
  const impact = by => {
    const rows = (by === "families" ? st.families : st.summons).map(s => {
      const first = by === "families" ? Object.keys(S).find(id => S[id].f === s.id) : s.id;
      return {...s, name: by === "families" ? (S[first]?.fn || s.id) : S[s.id]?.n, _search: `${by === "families" ? S[first]?.fn : S[s.id]?.n} ${s.id}`.toLowerCase(), first};
    });
    sortable($("impactTable"), [
      {label: by === "families" ? "Família" : "Monstro", html: r => by === "families" ? esc(r.name) : chip(r.id)},
      {label: "Composições", key: "n", num: true, html: r => r.n},
      {label: "Vitória com", key: "with", num: true, html: r => pct(r.with, 1)},
      {label: "Vitória sem", key: "without", num: true, html: r => pct(r.without, 1)},
      {label: "Diferença", key: "lift", html: r => liftBar(r.lift, scale)},
      {label: "Rodadas (vitórias)", key: "rounds", num: true, html: r => fmt(r.rounds)},
    ], rows, "lift");
  };
  impact("summons");
  $("impactBy").addEventListener("change", e => impact(e.target.value));
}
document.addEventListener("mousemove", ev => {
  const t = ev.target.closest?.("[data-tip]");
  if (t) showTip(t.dataset.tip, ev); else if (!ev.target.closest?.(".hit")) hideTip();
});
render();
</script>
</body>
</html>
""";
	}
}
