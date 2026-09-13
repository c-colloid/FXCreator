using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>
	/// Controller のどこでどのパラメータが使われているかを集める（§6.1 の「参照されていない
	/// パラメータに警告」用）。
	///
	/// 走査先は <see cref="AcEdit.RenameParameter"/> と<b>同じ範囲</b>でなければならない。
	/// 片方だけが知っている参照があると、「未参照」と言われたパラメータを消したのに
	/// 実は使われていた、という壊し方をする。
	/// </summary>
	public static class AcParameterUsage
	{
		public static HashSet<string> CollectReferenced(AnimatorController controller)
		{
			var used = new HashSet<string>(StringComparer.Ordinal);
			if (controller == null)
			{
				return used;
			}

			AnimatorControllerLayer[] layers = controller.layers;
			for (int i = 0; i < layers.Length; i++)
			{
				CollectInStateMachine(layers[i].stateMachine, used);
			}
			return used;
		}

		private static void CollectInStateMachine(AnimatorStateMachine sm, HashSet<string> used)
		{
			if (sm == null)
			{
				return;
			}

			CollectInTransitions(sm.anyStateTransitions, used);
			CollectInTransitions(sm.entryTransitions, used);

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}

				CollectInTransitions(state.transitions, used);

				// 有効になっているものだけ数える。無効なら名前が残っていても効いていない。
				if (state.speedParameterActive) Add(used, state.speedParameter);
				if (state.cycleOffsetParameterActive) Add(used, state.cycleOffsetParameter);
				if (state.mirrorParameterActive) Add(used, state.mirrorParameter);
				if (state.timeParameterActive) Add(used, state.timeParameter);

				CollectInMotion(state.motion, used);
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				CollectInTransitions(sm.GetStateMachineTransitions(child), used);
				CollectInStateMachine(child, used);
			}
		}

		private static void CollectInTransitions(AnimatorTransitionBase[] transitions, HashSet<string> used)
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
					Add(used, conditions[c].parameter);
				}
			}
		}

		private static void CollectInMotion(Motion motion, HashSet<string> used)
		{
			var tree = motion as BlendTree;
			if (tree == null)
			{
				return;
			}

			// ブレンド種別で「実際に効く軸」が違う。効かない軸まで数えると、
			// 未設定の BlendTree が持っている既定値 "Blend" を使用中とみなしてしまい、
			// 同名のパラメータが本当は未参照でも警告が出なくなる（実アバターで確認）。
			switch (tree.blendType)
			{
				case BlendTreeType.Direct:
					// 軸は使わず、子ごとの directBlendParameter だけが効く。
					break;
				case BlendTreeType.Simple1D:
					Add(used, tree.blendParameter);
					break;
				default:
					Add(used, tree.blendParameter);
					Add(used, tree.blendParameterY);
					break;
			}

			ChildMotion[] children = tree.children;
			for (int i = 0; i < children.Length; i++)
			{
				if (tree.blendType == BlendTreeType.Direct)
				{
					Add(used, children[i].directBlendParameter);
				}
				CollectInMotion(children[i].motion, used);
			}
		}

		private static void Add(HashSet<string> used, string name)
		{
			if (!string.IsNullOrEmpty(name))
			{
				used.Add(name);
			}
		}
	}
}
