using System.Collections.Generic;
using System.Linq;
using colloid.FXCreator.AnimatorGraph;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// toggle / switch 畳み込みの回帰テスト（Docs/FXCreator-Design.md §8、優先度「最高」）。
	///
	/// ラウンドトリップの中核は「畳み込み → 展開 が元と等価」。畳み込みは Controller を
	/// 書き換えないので、等価性は<b>分割になっていること</b>——元の遷移がちょうど1回ずつ
	/// グループか直結エッジのどちらかに現れること——と言い換えられる。これを
	/// 手組みのパターンとランダム生成の両方で確かめる。
	/// </summary>
	public class AcTransitionGroupTests
	{
		private readonly List<AnimatorController> _controllers = new List<AnimatorController>();

		[TearDown]
		public void TearDown()
		{
			var garbage = new List<Object>();
			foreach (AnimatorController controller in _controllers)
			{
				if (controller == null)
				{
					continue;
				}
				foreach (AnimatorControllerLayer layer in controller.layers)
				{
					Collect(layer.stateMachine, garbage);
				}
				garbage.Add(controller);
			}
			_controllers.Clear();

			foreach (Object o in garbage)
			{
				if (o != null)
				{
					Object.DestroyImmediate(o);
				}
			}
		}

		private static void Collect(AnimatorStateMachine sm, List<Object> sink)
		{
			if (sm == null)
			{
				return;
			}
			sink.AddRange(sm.anyStateTransitions);
			sink.AddRange(sm.entryTransitions);
			foreach (ChildAnimatorState cs in sm.states)
			{
				if (cs.state == null)
				{
					continue;
				}
				sink.AddRange(cs.state.transitions);
				sink.Add(cs.state);
			}
			foreach (ChildAnimatorStateMachine csm in sm.stateMachines)
			{
				if (csm.stateMachine == null)
				{
					continue;
				}
				sink.AddRange(sm.GetStateMachineTransitions(csm.stateMachine));
				Collect(csm.stateMachine, sink);
				sink.Add(csm.stateMachine);
			}
		}

		private AnimatorStateMachine NewStateMachine()
		{
			var controller = new AnimatorController();
			controller.AddLayer("Base Layer");
			_controllers.Add(controller);
			return controller.layers[0].stateMachine;
		}

		private static AnimatorStateTransition Transition(
			AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold = 0f)
		{
			AnimatorStateTransition transition = from.AddTransition(to);
			transition.hasExitTime = false;
			transition.AddCondition(mode, threshold, parameter);
			return transition;
		}

		#region The partition property

		/// <summary>
		/// ラウンドトリップの中核。どんな組み合わせでも、元の遷移は
		/// グループか直結エッジのどちらかにちょうど1回だけ現れる。
		/// </summary>
		private static void AssertIsPartitionOf(AnimatorState state, AcGroupingResult result)
		{
			var seen = new List<AnimatorStateTransition>();
			foreach (AcTransitionGroup group in result.Groups)
			{
				foreach (AcGroupBranch branch in group.Branches)
				{
					seen.Add(branch.Transition);
				}
			}
			seen.AddRange(result.Ungrouped);

			AnimatorStateTransition[] original = state.transitions;

			Assert.That(seen.Count, Is.EqualTo(original.Length),
				"遷移の総数が合わない（取りこぼしか重複がある）");
			Assert.That(seen.Distinct().Count(), Is.EqualTo(seen.Count),
				"同じ遷移が2回現れている");
			CollectionAssert.AreEquivalent(original, seen,
				"元の遷移集合と畳み込み結果の集合が一致しない");
		}

		[Test]
		public void BoolPairCollapsesIntoAToggle()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState on = sm.AddState("On");
			AnimatorState off = sm.AddState("Off");
			Transition(from, on, "Switch", AnimatorConditionMode.If);
			Transition(from, off, "Switch", AnimatorConditionMode.IfNot);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			Assert.That(result.Groups.Count, Is.EqualTo(1));
			Assert.That(result.Ungrouped, Is.Empty);

			AcTransitionGroup group = result.Groups[0];
			Assert.That(group.Kind, Is.EqualTo(AcGroupKind.Toggle));
			Assert.That(group.Parameter, Is.EqualTo("Switch"));
			Assert.That(group.PortIds, Is.EqualTo(new[] { "true", "false" }), "出力は常に True → False の順");
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void DistinctEqualsCollapseIntoASwitch()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			AnimatorState c = sm.AddState("C");
			// わざと降順で足して、ポートが threshold 昇順に並ぶことを見る。
			Transition(from, c, "Mode", AnimatorConditionMode.Equals, 2f);
			Transition(from, a, "Mode", AnimatorConditionMode.Equals, 0f);
			Transition(from, b, "Mode", AnimatorConditionMode.Equals, 1f);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			Assert.That(result.Groups.Count, Is.EqualTo(1));
			AcTransitionGroup group = result.Groups[0];
			Assert.That(group.Kind, Is.EqualTo(AcGroupKind.Switch));
			Assert.That(group.PortLabels, Is.EqualTo(new[] { "0", "1", "2" }));
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void ASingleTransitionIsNotWorthANode()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState to = sm.AddState("To");
			Transition(from, to, "Switch", AnimatorConditionMode.If);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			Assert.That(result.Groups, Is.Empty, "1本だけで中間ノードを作っている");
			Assert.That(result.Ungrouped.Count, Is.EqualTo(1));
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void AllTrueBranchesAreNotAToggle()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			Transition(from, a, "Switch", AnimatorConditionMode.If);
			Transition(from, b, "Switch", AnimatorConditionMode.If);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			// §4.2: If と IfNot が両方1本以上あって初めて toggle。
			Assert.That(result.Groups, Is.Empty);
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void RepeatedThresholdsAreNotASwitch()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			Transition(from, a, "Mode", AnimatorConditionMode.Equals, 1f);
			Transition(from, b, "Mode", AnimatorConditionMode.Equals, 1f);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			// 同じ値が2本あると、どちらのポートへ行くか決まらない。
			Assert.That(result.Groups, Is.Empty);
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void MixedModesOnOneParameterAreLeftAlone()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			Transition(from, a, "Mode", AnimatorConditionMode.Greater, 1f);
			Transition(from, b, "Mode", AnimatorConditionMode.Less, 5f);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			Assert.That(result.Groups, Is.Empty);
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void TransitionsThatWouldLoseInformationAreNeverCollapsed()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState on = sm.AddState("On");
			AnimatorState off = sm.AddState("Off");

			// exit time 付き・条件2つ・ミュートは、まとめると情報が落ちる。
			AnimatorStateTransition withExitTime = Transition(from, on, "Switch", AnimatorConditionMode.If);
			withExitTime.hasExitTime = true;
			AnimatorStateTransition twoConditions = Transition(from, off, "Switch", AnimatorConditionMode.IfNot);
			twoConditions.AddCondition(AnimatorConditionMode.If, 0f, "Other");
			AnimatorStateTransition muted = Transition(from, on, "Switch", AnimatorConditionMode.If);
			muted.mute = true;

			Assert.That(AcTransitionGrouping.IsCandidate(withExitTime), Is.False);
			Assert.That(AcTransitionGrouping.IsCandidate(twoConditions), Is.False);
			Assert.That(AcTransitionGrouping.IsCandidate(muted), Is.False);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);
			Assert.That(result.Groups, Is.Empty);
			Assert.That(result.Ungrouped.Count, Is.EqualTo(3));
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void UncollapsibleTransitionsCoexistWithAGroup()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState on = sm.AddState("On");
			AnimatorState off = sm.AddState("Off");
			AnimatorState other = sm.AddState("Other");

			Transition(from, on, "Switch", AnimatorConditionMode.If);
			Transition(from, off, "Switch", AnimatorConditionMode.IfNot);
			AnimatorStateTransition loose = Transition(from, other, "Speed", AnimatorConditionMode.Greater, 0.5f);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			Assert.That(result.Groups.Count, Is.EqualTo(1));
			Assert.That(result.Ungrouped, Is.EqualTo(new[] { loose }));
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void TwoParametersProduceTwoGroups()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");

			Transition(from, a, "First", AnimatorConditionMode.If);
			Transition(from, b, "First", AnimatorConditionMode.IfNot);
			Transition(from, a, "Second", AnimatorConditionMode.Equals, 0f);
			Transition(from, b, "Second", AnimatorConditionMode.Equals, 1f);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			Assert.That(result.Groups.Count, Is.EqualTo(2));
			Assert.That(result.Groups[0].Parameter, Is.EqualTo("First"));
			Assert.That(result.Groups[1].Parameter, Is.EqualTo("Second"));
			Assert.That(result.Groups.Select(g => g.Id).Distinct().Count(), Is.EqualTo(2), "ID が衝突している");
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void SeveralTransitionsMayShareOnePort()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			AnimatorState c = sm.AddState("C");

			Transition(from, a, "Switch", AnimatorConditionMode.If);
			Transition(from, b, "Switch", AnimatorConditionMode.If);
			Transition(from, c, "Switch", AnimatorConditionMode.IfNot);

			AcGroupingResult result = AcTransitionGrouping.Collapse(from);

			// True 側に2本ぶら下がるのは正当（先に成立した方が使われる）。
			Assert.That(result.Groups.Count, Is.EqualTo(1));
			AcTransitionGroup group = result.Groups[0];
			Assert.That(group.Branches.Count, Is.EqualTo(3));
			Assert.That(group.Branches.Count(x => x.PortId == "true"), Is.EqualTo(2));
			Assert.That(group.PortIds.Count, Is.EqualTo(2), "ポートは重複させない");
			AssertIsPartitionOf(from, result);
		}

		[Test]
		public void ExpandedGroupsFallBackToDirectEdges()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = sm.AddState("From");
			AnimatorState on = sm.AddState("On");
			AnimatorState off = sm.AddState("Off");
			Transition(from, on, "Switch", AnimatorConditionMode.If);
			Transition(from, off, "Switch", AnimatorConditionMode.IfNot);

			AcGroupingResult result = AcTransitionGrouping.Collapse(
				from, (state, parameter) => parameter == "Switch");

			// Expand は Controller を変えず、描き方だけを変える（§4.2）。
			Assert.That(result.Groups, Is.Empty);
			Assert.That(result.Ungrouped.Count, Is.EqualTo(2));
			AssertIsPartitionOf(from, result);
		}

		#endregion

		#region Determinism and randomised round-trip

		[Test]
		public void TheSameControllerAlwaysCollapsesTheSameWay()
		{
			AnimatorStateMachine sm = NewStateMachine();
			AnimatorState from = BuildRandomState(sm, seed: 4242);

			AcGroupingResult first = AcTransitionGrouping.Collapse(from);
			AcGroupingResult second = AcTransitionGrouping.Collapse(from);

			Assert.That(Describe(second), Is.EqualTo(Describe(first)));
		}

		/// <summary>
		/// §4.2 のプロパティテスト。ランダムな遷移の山を投げても、
		/// 常に分割になっていること（＝情報が落ちないこと）を確かめる。
		/// </summary>
		[Test]
		public void RandomControllersAlwaysPartitionCleanly()
		{
			int toggles = 0;
			int switches = 0;
			int loose = 0;

			for (int seed = 0; seed < 60; seed++)
			{
				AnimatorStateMachine sm = NewStateMachine();
				AnimatorState from = BuildRandomState(sm, seed);

				AcGroupingResult result = AcTransitionGrouping.Collapse(from);

				AssertIsPartitionOf(from, result);

				// グループになった以上、そのポート割り当ては一意に決まっていること。
				foreach (AcTransitionGroup group in result.Groups)
				{
					Assert.That(group.PortIds.Distinct().Count(), Is.EqualTo(group.PortIds.Count),
						"seed " + seed + ": ポートが重複している");
					foreach (AcGroupBranch branch in group.Branches)
					{
						Assert.That(group.PortIds, Contains.Item(branch.PortId),
							"seed " + seed + ": 枝が存在しないポートを指している");
					}
				}

				toggles += result.Groups.Count(g => g.Kind == AcGroupKind.Toggle);
				switches += result.Groups.Count(g => g.Kind == AcGroupKind.Switch);
				loose += result.Ungrouped.Count;
			}

			// カバレッジの見張り。生成が偏って畳み込み経路を通らなくなると、
			// このテストは「何も畳み込まなかった」だけで緑になってしまう。
			Assert.That(toggles, Is.GreaterThan(0), "ランダム生成が toggle を一度も作っていない");
			Assert.That(switches, Is.GreaterThan(0), "ランダム生成が switch を一度も作っていない");
			Assert.That(loose, Is.GreaterThan(0), "ランダム生成が直結エッジを一度も作っていない");
		}

		/// <summary>
		/// 畳み込みが効く形と効かない形を<b>意図的に混ぜて</b>作る。
		/// 条件を完全ランダムにすると、同じパラメータに互換なモードが2本揃うこと自体が稀で、
		/// switch はまず生成されない（実測で 40 seed 中 0 件）。それでは
		/// 「何も畳み込まれなかった」だけでこのテストが緑になってしまう。
		/// </summary>
		private static AnimatorState BuildRandomState(AnimatorStateMachine sm, int seed)
		{
			var rng = new System.Random(seed);
			AnimatorState from = sm.AddState("From");
			var targets = new List<AnimatorState>();
			for (int i = 0; i < 4; i++)
			{
				targets.Add(sm.AddState("T" + i));
			}

			int clusters = rng.Next(1, 4);
			for (int i = 0; i < clusters; i++)
			{
				string parameter = "P" + rng.Next(0, 3);
				AnimatorState Pick() => targets[rng.Next(targets.Count)];

				switch (rng.Next(6))
				{
					case 0: // toggle になる形
						Transition(from, Pick(), parameter, AnimatorConditionMode.If);
						Transition(from, Pick(), parameter, AnimatorConditionMode.IfNot);
						break;

					case 1: // switch になる形（threshold を相異ならせる）
						int branches = rng.Next(2, 5);
						for (int v = 0; v < branches; v++)
						{
							Transition(from, Pick(), parameter, AnimatorConditionMode.Equals, v);
						}
						break;

					case 2: // switch にならない形（threshold が重複）
						Transition(from, Pick(), parameter, AnimatorConditionMode.Equals, 1f);
						Transition(from, Pick(), parameter, AnimatorConditionMode.Equals, 1f);
						break;

					case 3: // toggle にならない形（True 側だけ）
						Transition(from, Pick(), parameter, AnimatorConditionMode.If);
						Transition(from, Pick(), parameter, AnimatorConditionMode.If);
						break;

					case 4: // 畳み込み候補にならない形
						AnimatorStateTransition odd = Transition(
							from, Pick(), parameter, AnimatorConditionMode.Greater, rng.Next(0, 3));
						if (rng.Next(2) == 0)
						{
							odd.hasExitTime = true;
						}
						else
						{
							odd.AddCondition(AnimatorConditionMode.If, 0f, "Extra");
						}
						break;

					default: // 1本だけ（ノードにしない）
						Transition(from, Pick(), parameter, AnimatorConditionMode.If);
						break;
				}
			}
			return from;
		}

		/// <summary>畳み込み結果を比較可能な文字列にする。</summary>
		private static string Describe(AcGroupingResult result)
		{
			var parts = new List<string>();
			foreach (AcTransitionGroup group in result.Groups)
			{
				parts.Add(group.Kind + "/" + group.Parameter + "/[" + string.Join(",", group.PortIds) + "]/["
					+ string.Join(",", group.Branches.Select(b => b.PortId + "->" + b.Transition.GetInstanceID())) + "]");
			}
			parts.Add("loose:" + string.Join(",", result.Ungrouped.Select(t => t.GetInstanceID().ToString())));
			return string.Join(" | ", parts);
		}

		#endregion
	}
}
