using System;
using System.Collections.Generic;
using UnityEditor.Animations;

namespace colloid.FXCreator.AnimatorGraph
{
	public enum AcGroupKind
	{
		/// <summary>bool 1個で True / False に分かれる（資料の toggle ノード）。</summary>
		Toggle,

		/// <summary>int 1個が値ごとに分かれる（資料の switch ノード）。</summary>
		Switch
	}

	/// <summary>畳み込まれた遷移1本ぶん。同じ <see cref="PortId"/> に複数本ぶら下がることもある。</summary>
	public sealed class AcGroupBranch
	{
		public string PortId;

		/// <summary>ポートに出す表示名（"True" / "False" / threshold 値）。</summary>
		public string Label;

		public AnimatorStateTransition Transition;
	}

	/// <summary>
	/// 同一パラメータで分岐する遷移群（Docs/FXCreator-Design.md §4.2）。
	///
	/// 資料の中間ノードは「条件を持った実体」ではなく<b>遷移群の表現</b>と定義する。
	/// Controller から決定的に導出できるので、1:1 と完全ラウンドトリップを両立できる
	/// （逆変換は遷移の1フィールド書き換えに落ちる）。
	/// </summary>
	public sealed class AcTransitionGroup
	{
		public const string TruePort = "true";
		public const string FalsePort = "false";

		/// <summary>switch の出力ポートID。threshold をそのまま名前にする。</summary>
		public static string EqualsPort(float threshold)
		{
			return "eq:" + threshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
		}

		public AcGroupKind Kind { get; set; }

		public AnimatorState Source { get; set; }

		public string Parameter { get; set; }

		/// <summary>畳み込まれた遷移。元の遷移順を保つ。</summary>
		public List<AcGroupBranch> Branches { get; } = new List<AcGroupBranch>();

		/// <summary>重複を除いた出力ポート。Toggle は True→False、Switch は threshold 昇順。</summary>
		public List<string> PortIds { get; } = new List<string>();

		public List<string> PortLabels { get; } = new List<string>();

		/// <summary>
		/// グラフ上の安定ID。元ステートとパラメータ名で決まるので、
		/// 作り直しをまたいで選択が生き残る。
		/// </summary>
		public string Id
		{
			get { return "g:" + (Source != null ? Source.GetInstanceID() : 0) + ":" + Parameter; }
		}
	}

	/// <summary>
	/// 畳み込みの結果。元の遷移が<b>ちょうど1回ずつ</b>どちらかに入る
	/// （＝分割になっている）ことがラウンドトリップの担保。
	/// </summary>
	public sealed class AcGroupingResult
	{
		public readonly List<AcTransitionGroup> Groups = new List<AcTransitionGroup>();

		/// <summary>畳み込まれなかった遷移。直結エッジとして描く。</summary>
		public readonly List<AnimatorStateTransition> Ungrouped = new List<AnimatorStateTransition>();
	}

	/// <summary>
	/// <see cref="AcTransitionGroup"/> の導出（Docs/FXCreator-Design.md §4.2）。
	/// UI も Controller の書き換えも伴わない純粋な関数なので、単体でテストできる。
	/// </summary>
	public static class AcTransitionGrouping
	{
		/// <summary>
		/// 畳み込みの候補になる遷移か。条件が1つだけで、Exit Time を使わず、
		/// ミュート/ソロされていないもの。それ以外は「まとめると情報が落ちる」ので触らない。
		/// </summary>
		public static bool IsCandidate(AnimatorStateTransition transition)
		{
			if (transition == null)
			{
				return false;
			}
			if (transition.conditions == null || transition.conditions.Length != 1)
			{
				return false;
			}
			if (transition.hasExitTime)
			{
				return false;
			}
			return !transition.mute && !transition.solo;
		}

		/// <summary>
		/// <paramref name="state"/> の outgoing transitions を畳み込む。
		/// <paramref name="isExpanded"/> が true を返す (state, parameter) は
		/// 畳み込まず直結エッジのままにする（右クリックの Expand 用）。
		/// </summary>
		public static AcGroupingResult Collapse(
			AnimatorState state, Func<AnimatorState, string, bool> isExpanded = null)
		{
			var result = new AcGroupingResult();
			if (state == null)
			{
				return result;
			}

			AnimatorStateTransition[] transitions = state.transitions;

			// パラメータごとに候補を集める。出現順を覚えておくと結果の順序が決まる。
			var buckets = new Dictionary<string, List<AnimatorStateTransition>>(StringComparer.Ordinal);
			var order = new List<string>();
			var candidates = new HashSet<AnimatorStateTransition>();

			for (int i = 0; i < transitions.Length; i++)
			{
				AnimatorStateTransition transition = transitions[i];
				if (!IsCandidate(transition))
				{
					continue;
				}

				string parameter = transition.conditions[0].parameter;
				if (string.IsNullOrEmpty(parameter))
				{
					continue;
				}

				List<AnimatorStateTransition> bucket;
				if (!buckets.TryGetValue(parameter, out bucket))
				{
					bucket = new List<AnimatorStateTransition>();
					buckets.Add(parameter, bucket);
					order.Add(parameter);
				}
				bucket.Add(transition);
				candidates.Add(transition);
			}

			var grouped = new HashSet<AnimatorStateTransition>();
			for (int i = 0; i < order.Count; i++)
			{
				string parameter = order[i];
				List<AnimatorStateTransition> bucket = buckets[parameter];

				// 1本しかないなら畳み込まない（中間ノードを挟む意味がない）。
				if (bucket.Count < 2)
				{
					continue;
				}
				if (isExpanded != null && isExpanded(state, parameter))
				{
					continue;
				}

				AcTransitionGroup group = TryBuildGroup(state, parameter, bucket);
				if (group == null)
				{
					continue;
				}

				result.Groups.Add(group);
				for (int b = 0; b < bucket.Count; b++)
				{
					grouped.Add(bucket[b]);
				}
			}

			// 畳み込まれなかったものは元の順序のまま直結エッジへ。
			for (int i = 0; i < transitions.Length; i++)
			{
				if (!grouped.Contains(transitions[i]))
				{
					result.Ungrouped.Add(transitions[i]);
				}
			}

			return result;
		}

		/// <summary>
		/// グループの種類を決める。Toggle にも Switch にもならない組み合わせは
		/// null を返し、呼び出し側が直結エッジに落とす。
		/// </summary>
		private static AcTransitionGroup TryBuildGroup(
			AnimatorState state, string parameter, List<AnimatorStateTransition> bucket)
		{
			bool allBool = true;
			bool hasIf = false;
			bool hasIfNot = false;
			bool allEquals = true;

			for (int i = 0; i < bucket.Count; i++)
			{
				AnimatorConditionMode mode = bucket[i].conditions[0].mode;
				if (mode == AnimatorConditionMode.If)
				{
					hasIf = true;
				}
				else if (mode == AnimatorConditionMode.IfNot)
				{
					hasIfNot = true;
				}
				else
				{
					allBool = false;
				}

				if (mode != AnimatorConditionMode.Equals)
				{
					allEquals = false;
				}
			}

			// toggle: If / IfNot だけで、両方が1本以上ある。
			if (allBool && hasIf && hasIfNot)
			{
				return BuildToggle(state, parameter, bucket);
			}

			// switch: すべて Equals で、threshold が相異なる。
			if (allEquals && HasDistinctThresholds(bucket))
			{
				return BuildSwitch(state, parameter, bucket);
			}

			return null;
		}

		private static bool HasDistinctThresholds(List<AnimatorStateTransition> bucket)
		{
			var seen = new HashSet<float>();
			for (int i = 0; i < bucket.Count; i++)
			{
				if (!seen.Add(bucket[i].conditions[0].threshold))
				{
					// 同じ値が2本あるとポートに割り振れない（どちらへ行くか決まらない）。
					return false;
				}
			}
			return true;
		}

		private static AcTransitionGroup BuildToggle(
			AnimatorState state, string parameter, List<AnimatorStateTransition> bucket)
		{
			var group = new AcTransitionGroup
			{
				Kind = AcGroupKind.Toggle,
				Source = state,
				Parameter = parameter
			};

			for (int i = 0; i < bucket.Count; i++)
			{
				bool isTrue = bucket[i].conditions[0].mode == AnimatorConditionMode.If;
				group.Branches.Add(new AcGroupBranch
				{
					PortId = isTrue ? AcTransitionGroup.TruePort : AcTransitionGroup.FalsePort,
					Label = isTrue ? "True" : "False",
					Transition = bucket[i]
				});
			}

			// 出力は常に True → False の順に出す（見た目が遷移の並び順で揺れないように）。
			group.PortIds.Add(AcTransitionGroup.TruePort);
			group.PortLabels.Add("True");
			group.PortIds.Add(AcTransitionGroup.FalsePort);
			group.PortLabels.Add("False");
			return group;
		}

		private static AcTransitionGroup BuildSwitch(
			AnimatorState state, string parameter, List<AnimatorStateTransition> bucket)
		{
			var group = new AcTransitionGroup
			{
				Kind = AcGroupKind.Switch,
				Source = state,
				Parameter = parameter
			};

			for (int i = 0; i < bucket.Count; i++)
			{
				float threshold = bucket[i].conditions[0].threshold;
				group.Branches.Add(new AcGroupBranch
				{
					PortId = AcTransitionGroup.EqualsPort(threshold),
					Label = FormatThreshold(threshold),
					Transition = bucket[i]
				});
			}

			// ポートは threshold 昇順。遷移を足した順に並ぶと 0,2,1 のような並びになる。
			var sorted = new List<AcGroupBranch>(group.Branches);
			sorted.Sort((a, b) => ThresholdOf(a).CompareTo(ThresholdOf(b)));
			for (int i = 0; i < sorted.Count; i++)
			{
				group.PortIds.Add(sorted[i].PortId);
				group.PortLabels.Add(sorted[i].Label);
			}
			return group;
		}

		private static float ThresholdOf(AcGroupBranch branch)
		{
			return branch.Transition.conditions[0].threshold;
		}

		/// <summary>int パラメータ想定なので、割り切れるなら小数点を出さない。</summary>
		private static string FormatThreshold(float threshold)
		{
			if (Math.Abs(threshold - Math.Round(threshold)) < 0.0001f)
			{
				return ((int)Math.Round(threshold)).ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
			return threshold.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
