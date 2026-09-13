using System;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>グラフに出るノードの種類（Docs/FXCreator-Design.md §4.1）。</summary>
	public enum AcNodeKind
	{
		/// <summary><see cref="AnimatorState"/>。</summary>
		State,

		/// <summary>子の <see cref="AnimatorStateMachine"/>（潜れる）。</summary>
		StateMachine,

		/// <summary>表示中ステートマシンの Any State。</summary>
		Any,

		/// <summary>表示中ステートマシンの Entry。</summary>
		Entry,

		/// <summary>表示中ステートマシンの Exit。</summary>
		Exit,

		/// <summary>親ステートマシンへ戻る「(Up)」ノード。</summary>
		Parent,

		/// <summary>
		/// toggle / switch の畳み込みノード（§4.2）。Unity 側に実体が無く、
		/// 遷移群の表現でしかないので <see cref="AcNodeRef.Target"/> には
		/// 分岐元の <see cref="AnimatorState"/> を入れる。
		/// ID はパラメータ名まで含めないと同じ State の複数グループが衝突するため、
		/// <see cref="AcNodeRef.MakeId"/> ではなく <c>AcTransitionGroup.Id</c> を使う。
		/// </summary>
		Group
	}

	/// <summary>
	/// ノードの同定（Docs/FXCreator-Design.md §4.1）。
	///
	/// 中間データモデルを持たない設計（D5/D6）なので、ノードは
	/// AnimatorController のサブアセットを<b>直接</b>指す。
	/// 文字列 <see cref="Id"/> はビューが再構築をまたいで選択を保つためのキーで、
	/// インスタンスIDから作る。名前ではないのでリネームで壊れない。
	///
	/// Any / Entry / Exit / Parent は Unity 側に実体が無く、
	/// 位置は所属ステートマシンのフィールド（<c>anyStatePosition</c> 等）にある。
	/// そのため <see cref="Target"/> には<b>所属ステートマシン</b>を入れる。
	/// </summary>
	public readonly struct AcNodeRef : IEquatable<AcNodeRef>
	{
		public readonly AcNodeKind Kind;

		/// <summary>
		/// <see cref="AcNodeKind.State"/> なら <see cref="AnimatorState"/>、
		/// それ以外は <see cref="AnimatorStateMachine"/>
		/// （<see cref="AcNodeKind.StateMachine"/> は当のサブステートマシン、
		/// 特殊ノードは所属ステートマシン）。
		/// </summary>
		public readonly UnityEngine.Object Target;

		public AcNodeRef(AcNodeKind kind, UnityEngine.Object target)
		{
			Kind = kind;
			Target = target;
		}

		public bool IsValid => Target != null;

		/// <summary>グラフ上の安定ID。<see cref="Kind"/> の接頭辞で衝突を防ぐ。</summary>
		public string Id => MakeId(Kind, Target);

		/// <summary>
		/// 種類ごとの接頭辞 + インスタンスID。
		/// Any / Entry / Exit / Parent は所属ステートマシンが同じでも接頭辞で区別される。
		/// </summary>
		public static string MakeId(AcNodeKind kind, UnityEngine.Object target)
		{
			if (target == null)
			{
				return null;
			}
			return Prefix(kind) + target.GetInstanceID().ToString();
		}

		private static string Prefix(AcNodeKind kind)
		{
			switch (kind)
			{
				case AcNodeKind.State: return "s:";
				case AcNodeKind.StateMachine: return "m:";
				case AcNodeKind.Any: return "any:";
				case AcNodeKind.Entry: return "entry:";
				case AcNodeKind.Exit: return "exit:";
				case AcNodeKind.Parent: return "up:";
				case AcNodeKind.Group: return "g:";
				default: return "?:";
			}
		}

		/// <summary>State として指しているものを返す（違う種類なら null）。</summary>
		public AnimatorState AsState()
		{
			return Kind == AcNodeKind.State ? Target as AnimatorState : null;
		}

		/// <summary>潜れるサブステートマシンを返す（違う種類なら null）。</summary>
		public AnimatorStateMachine AsStateMachine()
		{
			return Kind == AcNodeKind.StateMachine ? Target as AnimatorStateMachine : null;
		}

		public bool Equals(AcNodeRef other)
		{
			return Kind == other.Kind && Target == other.Target;
		}

		public override bool Equals(object obj) => obj is AcNodeRef other && Equals(other);

		public override int GetHashCode()
		{
			int h = Target != null ? Target.GetInstanceID() : 0;
			return (h * 397) ^ (int)Kind;
		}

		public override string ToString() => Id ?? "<invalid>";
	}
}
