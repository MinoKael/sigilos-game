using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O que aconteceu na luta, em ordem. O combate não sabe que existe tela: ele só produz esta lista,
	/// e a tela de batalha anima cada item. O simulador dos testes ignora a lista.
	///
	/// Os tipos ficam juntos neste arquivo de propósito: são as variantes de uma mesma coisa, e quem lê
	/// a tela de batalha quer ver todas de uma vez.
	/// </summary>
	public abstract record BattleEvent;

	public sealed record WaveStarted(int Wave, int WaveCount, IReadOnlyList<BattleUnit> Enemies) : BattleEvent;

	public sealed record TurnStarted(BattleUnit Actor, int Round) : BattleEvent;

	public sealed record SkillUsed(BattleUnit Actor, SkillDefinition Skill, bool Enhanced) : BattleEvent;

	/// <summary><paramref name="Absorbed"/> é a parte que o escudo segurou.</summary>
	public sealed record Damaged(BattleUnit Target, int Amount, int Absorbed, bool Crit, double ElementMultiplier) : BattleEvent;

	public sealed record Missed(BattleUnit Target) : BattleEvent;

	/// <summary>A Égide anulou o golpe.</summary>
	public sealed record Warded(BattleUnit Target) : BattleEvent;

	public sealed record Healed(BattleUnit Target, int Amount) : BattleEvent;

	public sealed record StatusApplied(BattleUnit Target, StatusKind Status, int Turns) : BattleEvent;

	/// <summary>A Resistência do alvo barrou um efeito negativo.</summary>
	public sealed record Resisted(BattleUnit Target) : BattleEvent;

	public sealed record StatusRemoved(BattleUnit Target, StatusKind Status) : BattleEvent;

	public sealed record ImpetoChanged(BattleUnit Target, double Amount) : BattleEvent;

	/// <summary>A unidade perdeu o turno atordoada.</summary>
	public sealed record TurnSkipped(BattleUnit Unit) : BattleEvent;

	/// <summary>O conjunto de runas da Porta deu mais um turno.</summary>
	public sealed record ExtraTurn(BattleUnit Unit) : BattleEvent;

	public sealed record Died(BattleUnit Unit) : BattleEvent;

	public sealed record Revived(BattleUnit Unit) : BattleEvent;

	public sealed record EtherChanged(int Ether, int Delta) : BattleEvent;

	public sealed record BattleEnded(bool Victory) : BattleEvent;
}
