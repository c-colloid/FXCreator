using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>パラメータの参照場所。読み替えられない理由を UI に出すために種類を持つ。</summary>
	public enum AcParameterRefKind
	{
		Condition,
		StateSpeed,
		StateCycleOffset,
		StateMirror,
		StateTime,
		BlendTreeX,
		BlendTreeY,
		BlendTreeDirect,
	}

	/// <summary>読み替える条件1本。</summary>
	public struct AcConditionRewrite
	{
		public AnimatorTransitionBase Transition;
		public int Index;
		public AnimatorConditionMode FromMode;
		public float FromThreshold;
		public AnimatorConditionMode ToMode;
		public float ToThreshold;

		public string Describe()
		{
			return Format(FromMode, FromThreshold) + " → " + Format(ToMode, ToThreshold);
		}

		private static string Format(AnimatorConditionMode mode, float threshold)
		{
			switch (mode)
			{
				case AnimatorConditionMode.If:
				case AnimatorConditionMode.IfNot:
					return mode.ToString();
				default:
					return mode + " " + threshold.ToString("0.##");
			}
		}
	}

	/// <summary>読み替えられない参照。</summary>
	public struct AcParameterBlocker
	{
		public AcParameterRefKind Kind;

		/// <summary>参照している側（遷移 / State / BlendTree）。UI から選択させるのに使う。</summary>
		public UnityEngine.Object Owner;

		public string Description;
	}

	/// <summary>
	/// 型変更の下調べ。<b>Controller を一切書き換えない純粋関数</b>なので、
	/// 単体でテストでき、適用前にそのまま確認ダイアログの材料にできる。
	/// </summary>
	public sealed class AcParameterTypeChangePlan
	{
		public string Parameter;
		public AnimatorControllerParameterType From;
		public AnimatorControllerParameterType To;

		public readonly List<AcConditionRewrite> Rewrites = new List<AcConditionRewrite>();
		public readonly List<AcParameterBlocker> Blockers = new List<AcParameterBlocker>();

		/// <summary>型が同じ、またはパラメータが無い。</summary>
		public bool IsNoOp;

		public bool HasBlockers { get { return Blockers.Count > 0; } }

		/// <summary>確認ダイアログに出す要約。</summary>
		public string Summary()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append(Parameter).Append(" : ").Append(From).Append(" → ").Append(To);

			if (Rewrites.Count > 0)
			{
				sb.Append("\n\n読み替える条件 ").Append(Rewrites.Count).Append(" 件");
				int show = Mathf.Min(6, Rewrites.Count);
				for (int i = 0; i < show; i++)
				{
					sb.Append("\n  ").Append(Rewrites[i].Describe());
				}
				if (Rewrites.Count > show)
				{
					sb.Append("\n  …");
				}
			}

			if (Blockers.Count > 0)
			{
				sb.Append("\n\n読み替えられない参照 ").Append(Blockers.Count).Append(" 件（そのまま残ります）");
				int show = Mathf.Min(6, Blockers.Count);
				for (int i = 0; i < show; i++)
				{
					sb.Append("\n  ").Append(Blockers[i].Description);
				}
				if (Blockers.Count > show)
				{
					sb.Append("\n  …");
				}
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// パラメータの型変更（§6.1）。
	///
	/// 型だけ差し替えると、そのパラメータを見ている条件が<b>新しい型では成立しない
	/// モード</b>のまま残る（Bool 用の <c>If</c> が Int のパラメータに付いている等）。
	/// Unity はそれを弾かないので、グラフ上は何事もないのに遷移が起きない
	/// Controller ができあがる。ここで対応を決めて書き換える。
	///
	/// 走査範囲は <see cref="AcParameterUsage"/> / <see cref="AcEdit.RenameParameter"/> と
	/// <b>同じ</b>でなければならない。片方だけが知っている参照があると、
	/// 「全部読み替えた」と言いながら壊れた参照が残る。
	/// </summary>
	public static class AcParameterTypeChange
	{
		#region Condition mode conversion

		/// <summary>その型で成立する条件モードか。</summary>
		public static bool IsValidMode(AnimatorControllerParameterType type, AnimatorConditionMode mode)
		{
			switch (type)
			{
				case AnimatorControllerParameterType.Bool:
					return mode == AnimatorConditionMode.If || mode == AnimatorConditionMode.IfNot;
				case AnimatorControllerParameterType.Trigger:
					// Trigger は「立った」しか見られない。IfNot は存在しない。
					return mode == AnimatorConditionMode.If;
				case AnimatorControllerParameterType.Int:
					return mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less
						|| mode == AnimatorConditionMode.Equals || mode == AnimatorConditionMode.NotEqual;
				case AnimatorControllerParameterType.Float:
					// Float に Equals / NotEqual は無い（浮動小数の一致は当てにならない）。
					return mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less;
				default:
					return false;
			}
		}

		/// <summary>
		/// 条件1本を新しい型へ読み替える。意味が移せないときは false。
		///
		/// 対応表（意味が変わらないことを優先し、変わるくらいなら読み替えない）:
		/// <list type="bullet">
		/// <item>Bool/Trigger → Int: <c>If → Equals 1</c> / <c>IfNot → Equals 0</c></item>
		/// <item>Bool/Trigger → Float: <c>If → Greater 0.5</c> / <c>IfNot → Less 0.5</c></item>
		/// <item>Int → Bool: <c>Equals 0 → IfNot</c>、<c>Less t (t≦1) → IfNot</c>（Int で「1未満」は 0 のこと）、
		/// それ以外は <c>If</c></item>
		/// <item>Float → Bool: <c>Greater → If</c> / <c>Less → IfNot</c></item>
		/// <item>Int → Float: <c>Greater/Less</c> はそのまま。<c>Equals/NotEqual</c> は<b>移せない</b>
		/// （Float に一致比較が無く、幅を持たせると条件が2本要る）</item>
		/// <item>Float → Int: <c>Greater t → Greater floor(t)</c> / <c>Less t → Less ceil(t)</c></item>
		/// <item>→ Trigger: 「立っている」に落とせるものだけ <c>If</c>。
		/// 「立っていない」を意味する条件（IfNot / Equals 0 / Less）は移せない</item>
		/// </list>
		/// </summary>
		public static bool TryConvert(
			AnimatorControllerParameterType from,
			AnimatorControllerParameterType to,
			AnimatorConditionMode mode,
			float threshold,
			out AnimatorConditionMode newMode,
			out float newThreshold)
		{
			newMode = mode;
			newThreshold = threshold;

			if (from == to)
			{
				return true;
			}

			// もとが新しい型でもそのまま成立するなら触らない（Int→Float の Greater/Less）。
			bool positive = IsPositive(from, mode, threshold);

			switch (to)
			{
				case AnimatorControllerParameterType.Bool:
					newMode = positive ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
					newThreshold = 0f;
					return true;

				case AnimatorControllerParameterType.Trigger:
					// 「立っていない」は Trigger で表せない。
					if (!positive)
					{
						return false;
					}
					newMode = AnimatorConditionMode.If;
					newThreshold = 0f;
					return true;

				case AnimatorControllerParameterType.Int:
					if (from == AnimatorControllerParameterType.Float)
					{
						if (mode == AnimatorConditionMode.Greater)
						{
							newMode = AnimatorConditionMode.Greater;
							newThreshold = Mathf.Floor(threshold);
							return true;
						}
						if (mode == AnimatorConditionMode.Less)
						{
							newMode = AnimatorConditionMode.Less;
							newThreshold = Mathf.Ceil(threshold);
							return true;
						}
						return false;
					}
					// Bool / Trigger から
					newMode = AnimatorConditionMode.Equals;
					newThreshold = positive ? 1f : 0f;
					return true;

				case AnimatorControllerParameterType.Float:
					if (from == AnimatorControllerParameterType.Int)
					{
						if (mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less)
						{
							newMode = mode;
							newThreshold = threshold;
							return true;
						}
						// Equals / NotEqual は Float に無い。
						return false;
					}
					// Bool / Trigger から
					newMode = positive ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less;
					newThreshold = 0.5f;
					return true;

				default:
					return false;
			}
		}

		/// <summary>その条件が「真」の側を見ているか（＝1 に寄っているか）。</summary>
		private static bool IsPositive(
			AnimatorControllerParameterType from, AnimatorConditionMode mode, float threshold)
		{
			switch (mode)
			{
				case AnimatorConditionMode.If:
					return true;
				case AnimatorConditionMode.IfNot:
					return false;
				case AnimatorConditionMode.Equals:
					// Equals 0 だけが「立っていない」。それ以外の値は真の側。
					return Mathf.Abs(threshold) > 0.0001f;
				case AnimatorConditionMode.NotEqual:
					// NotEqual 0 は「非ゼロ」。NotEqual n は 0 も含むが、
					// 主眼は「その値以外」なので真の側に寄せる（ユーザー規則の「それ以外は If」）。
					return true;
				case AnimatorConditionMode.Greater:
					return true;
				case AnimatorConditionMode.Less:
					// Int の「1 未満」は 0 のこと。Float の Less は 0 側を見ている。
					return from == AnimatorControllerParameterType.Int && threshold > 1f;
				default:
					return true;
			}
		}

		#endregion

		#region Plan

		/// <summary>その型でしか使えない参照のために、要求される型を返す。</summary>
		private static AnimatorControllerParameterType Required(AcParameterRefKind kind)
		{
			return kind == AcParameterRefKind.StateMirror
				? AnimatorControllerParameterType.Bool
				: AnimatorControllerParameterType.Float;
		}

		public static AcParameterTypeChangePlan Plan(
			AnimatorController controller, string parameter, AnimatorControllerParameterType to)
		{
			var plan = new AcParameterTypeChangePlan
			{
				Parameter = parameter,
				To = to,
				IsNoOp = true,
			};

			if (controller == null || string.IsNullOrEmpty(parameter))
			{
				return plan;
			}

			AnimatorControllerParameter[] parameters = controller.parameters;
			int index = -1;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i] != null && string.Equals(parameters[i].name, parameter, StringComparison.Ordinal))
				{
					index = i;
					break;
				}
			}
			if (index < 0)
			{
				return plan;
			}

			plan.From = parameters[index].type;
			if (plan.From == to)
			{
				return plan;
			}
			plan.IsNoOp = false;

			AnimatorControllerLayer[] layers = controller.layers;
			for (int i = 0; i < layers.Length; i++)
			{
				Walk(layers[i].stateMachine, plan);
			}
			return plan;
		}

		private static void Walk(AnimatorStateMachine sm, AcParameterTypeChangePlan plan)
		{
			if (sm == null)
			{
				return;
			}

			WalkTransitions(sm.anyStateTransitions, plan);
			WalkTransitions(sm.entryTransitions, plan);

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}

				WalkTransitions(state.transitions, plan);

				// 「有効になっているものだけ」の判定は AcParameterUsage と揃える。
				CheckSlot(plan, state.speedParameterActive, state.speedParameter,
					AcParameterRefKind.StateSpeed, state, state.name + " の Speed");
				CheckSlot(plan, state.cycleOffsetParameterActive, state.cycleOffsetParameter,
					AcParameterRefKind.StateCycleOffset, state, state.name + " の Cycle Offset");
				CheckSlot(plan, state.mirrorParameterActive, state.mirrorParameter,
					AcParameterRefKind.StateMirror, state, state.name + " の Mirror");
				CheckSlot(plan, state.timeParameterActive, state.timeParameter,
					AcParameterRefKind.StateTime, state, state.name + " の Motion Time");

				WalkMotion(state.motion, state, plan);
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				WalkTransitions(sm.GetStateMachineTransitions(child), plan);
				Walk(child, plan);
			}
		}

		private static void WalkTransitions(AnimatorTransitionBase[] transitions, AcParameterTypeChangePlan plan)
		{
			for (int i = 0; i < transitions.Length; i++)
			{
				AnimatorTransitionBase transition = transitions[i];
				if (transition == null)
				{
					continue;
				}

				AnimatorCondition[] conditions = transition.conditions;
				for (int c = 0; c < conditions.Length; c++)
				{
					if (!string.Equals(conditions[c].parameter, plan.Parameter, StringComparison.Ordinal))
					{
						continue;
					}

					AnimatorConditionMode newMode;
					float newThreshold;
					if (TryConvert(plan.From, plan.To, conditions[c].mode, conditions[c].threshold,
							out newMode, out newThreshold))
					{
						// もう成立しているモードなら書き換えない（差分を最小にする）。
						if (newMode == conditions[c].mode
							&& Mathf.Approximately(newThreshold, conditions[c].threshold)
							&& IsValidMode(plan.To, conditions[c].mode))
						{
							continue;
						}

						plan.Rewrites.Add(new AcConditionRewrite
						{
							Transition = transition,
							Index = c,
							FromMode = conditions[c].mode,
							FromThreshold = conditions[c].threshold,
							ToMode = newMode,
							ToThreshold = newThreshold,
						});
					}
					else
					{
						plan.Blockers.Add(new AcParameterBlocker
						{
							Kind = AcParameterRefKind.Condition,
							Owner = transition,
							Description = "条件 " + conditions[c].mode
								+ (conditions[c].mode == AnimatorConditionMode.If
									|| conditions[c].mode == AnimatorConditionMode.IfNot
									? string.Empty
									: " " + conditions[c].threshold.ToString("0.##"))
								+ " は " + plan.To + " で表せません",
						});
					}
				}
			}
		}

		private static void CheckSlot(
			AcParameterTypeChangePlan plan, bool active, string name,
			AcParameterRefKind kind, UnityEngine.Object owner, string label)
		{
			if (!active || !string.Equals(name, plan.Parameter, StringComparison.Ordinal))
			{
				return;
			}

			AnimatorControllerParameterType required = Required(kind);
			if (plan.To == required)
			{
				return;
			}

			plan.Blockers.Add(new AcParameterBlocker
			{
				Kind = kind,
				Owner = owner,
				Description = label + " は " + required + " のパラメータしか受け付けません",
			});
		}

		private static void WalkMotion(Motion motion, UnityEngine.Object state, AcParameterTypeChangePlan plan)
		{
			var tree = motion as BlendTree;
			if (tree == null)
			{
				return;
			}

			// 効く軸だけ見る（AcParameterUsage と同じ理由）。
			switch (tree.blendType)
			{
				case BlendTreeType.Direct:
					break;
				case BlendTreeType.Simple1D:
					CheckBlend(plan, tree.blendParameter, AcParameterRefKind.BlendTreeX, tree, tree.name + " の Blend");
					break;
				default:
					CheckBlend(plan, tree.blendParameter, AcParameterRefKind.BlendTreeX, tree, tree.name + " の Blend X");
					CheckBlend(plan, tree.blendParameterY, AcParameterRefKind.BlendTreeY, tree, tree.name + " の Blend Y");
					break;
			}

			ChildMotion[] children = tree.children;
			for (int i = 0; i < children.Length; i++)
			{
				if (tree.blendType == BlendTreeType.Direct)
				{
					CheckBlend(plan, children[i].directBlendParameter, AcParameterRefKind.BlendTreeDirect,
						tree, tree.name + " の Direct Blend");
				}
				WalkMotion(children[i].motion, state, plan);
			}
		}

		private static void CheckBlend(
			AcParameterTypeChangePlan plan, string name, AcParameterRefKind kind,
			UnityEngine.Object owner, string label)
		{
			if (!string.Equals(name, plan.Parameter, StringComparison.Ordinal))
			{
				return;
			}
			if (plan.To == AnimatorControllerParameterType.Float)
			{
				return;
			}
			plan.Blockers.Add(new AcParameterBlocker
			{
				Kind = kind,
				Owner = owner,
				Description = label + " は Float のパラメータしか受け付けません",
			});
		}

		#endregion
	}
}
