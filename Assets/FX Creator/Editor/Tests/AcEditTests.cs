using System;
using System.Collections.Generic;
using System.Linq;
using colloid.FXCreator.AnimatorGraph;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// <see cref="AcEdit"/> の回帰テスト（Docs/FXCreator-Design.md §8）。
	/// R1（Undo とサブアセット管理が想定より厄介）の対策として、Phase 3 は
	/// ここをテストファーストで固めてから UI を繋ぐ。
	///
	/// Controller は<b>実アセットとして</b>作る。サブアセットの迷子は
	/// アセット化されていないと再現しないので、ここだけはディスクを使う。
	/// </summary>
	public class AcEditTests
	{
		private const string TempFolder = "Assets/FXCreatorAcEditTests";

		private AnimatorController _controller;
		private string _path;

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TempFolder))
			{
				AssetDatabase.CreateFolder("Assets", "FXCreatorAcEditTests");
			}
			_path = TempFolder + "/AcEdit.controller";
			_controller = AnimatorController.CreateAnimatorControllerAtPath(_path);
		}

		[TearDown]
		public void TearDown()
		{
			// Undo スタックにテスト間の持ち越しを残さない（別テストの PerformUndo が
			// 前のテストの操作を巻き戻すと、原因の分からない失敗になる）。
			Undo.ClearAll();
			_controller = null;
			AssetDatabase.DeleteAsset(TempFolder);
			AssetDatabase.Refresh();
		}

		private AnimatorStateMachine Root => _controller.layers[0].stateMachine;

		/// <summary>アセットに実在するサブアセット（破棄済みを除く）。</summary>
		private List<UnityEngine.Object> SubAssets()
		{
			return AssetDatabase.LoadAllAssetsAtPath(_path)
				.Where(o => o != null && !AssetDatabase.IsMainAsset(o))
				.ToList();
		}

		private static int CountStates(AnimatorStateMachine sm)
		{
			int n = sm.states.Length;
			foreach (ChildAnimatorStateMachine c in sm.stateMachines)
			{
				n += CountStates(c.stateMachine);
			}
			return n;
		}

		#region Undo

		[Test]
		public void UndoRestoresStateCountAfterAdd()
		{
			int before = Root.states.Length;

			using (AcEdit e = AcEdit.Begin(_controller, "Add State"))
			{
				e.AddState(Root, "Added", new Vector2(40f, 60f));
			}
			Assert.That(Root.states.Length, Is.EqualTo(before + 1));

			Undo.PerformUndo();

			Assert.That(Root.states.Length, Is.EqualTo(before), "Undo で State 数が戻っていない");
		}

		[Test]
		public void UndoLeavesNoOrphanSubAssetAfterAdd()
		{
			int before = SubAssets().Count;

			AnimatorState added;
			using (AcEdit e = AcEdit.Begin(_controller, "Add State"))
			{
				added = e.AddState(Root, "Added", Vector2.zero);
			}

			Undo.PerformUndo();

			// 落とし穴2: RegisterCreatedObjectUndo を忘れると、Undo で配列からは
			// 消えるのにサブアセットだけ残る。
			Assert.That(added == null, Is.True, "Undo 後も State オブジェクトが生きている");
			Assert.That(SubAssets().Count, Is.EqualTo(before), "迷子のサブアセットが残っている");
		}

		[Test]
		public void UndoRestoresRemovedStateAndItsTransition()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			a.AddTransition(b);
			AssetDatabase.SaveAssets();

			int states = Root.states.Length;
			int subAssets = SubAssets().Count;

			using (AcEdit e = AcEdit.Begin(_controller, "Remove State"))
			{
				e.RemoveState(Root, b);
			}
			Assert.That(Root.states.Length, Is.EqualTo(states - 1));

			Undo.PerformUndo();

			Assert.That(Root.states.Length, Is.EqualTo(states), "Undo で State が戻っていない");
			Assert.That(SubAssets().Count, Is.EqualTo(subAssets), "Undo でサブアセット数が戻っていない");
		}

		[Test]
		public void OneEditIsOneUndoStepEvenWithManyApiCalls()
		{
			int before = Root.states.Length;

			using (AcEdit e = AcEdit.Begin(_controller, "Add Three"))
			{
				e.AddState(Root, "A", Vector2.zero);
				e.AddState(Root, "B", Vector2.zero);
				e.AddState(Root, "C", Vector2.zero);
			}
			Assert.That(Root.states.Length, Is.EqualTo(before + 3));

			// 落とし穴4: グループを畳んでいないと、Undo 1回では1つしか戻らない。
			Undo.PerformUndo();

			Assert.That(Root.states.Length, Is.EqualTo(before), "Undo 1回で3つとも戻っていない");
		}

		[Test]
		public void NestedBeginStillCollapsesToOneUndoStep()
		{
			int before = Root.states.Length;

			using (AcEdit outer = AcEdit.Begin(_controller, "Outer"))
			{
				outer.AddState(Root, "A", Vector2.zero);
				using (AcEdit inner = AcEdit.Begin(_controller, "Inner"))
				{
					inner.AddState(Root, "B", Vector2.zero);
				}
			}
			Assert.That(Root.states.Length, Is.EqualTo(before + 2));

			Undo.PerformUndo();

			Assert.That(Root.states.Length, Is.EqualTo(before), "入れ子のスコープが別の Undo 段になっている");
		}

		[Test]
		public void ExceptionRollsBackTheWholeEdit()
		{
			int before = Root.states.Length;

			Assert.Throws<NullReferenceException>(() =>
			{
				using (AcEdit e = AcEdit.Begin(_controller, "Boom"))
				{
					e.AddState(Root, "Survivor?", Vector2.zero);
					e.AddState(null, "Boom", Vector2.zero);
				}
			});

			Assert.That(Root.states.Length, Is.EqualTo(before),
				"例外時に巻き戻らず、途中まで適用された Controller が残っている");
		}

		#endregion

		#region Sub-asset hygiene

		[Test]
		public void RemoveStateLeavesNoOrphanSubAssets()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			a.AddTransition(b);
			b.AddTransition(a);
			Root.AddAnyStateTransition(b);
			AssetDatabase.SaveAssets();

			using (AcEdit e = AcEdit.Begin(_controller, "Remove State"))
			{
				e.RemoveState(Root, b);
			}

			// b 本体・b への遷移2本・b から出る遷移1本がすべて畳まれ、a だけが残る。
			List<UnityEngine.Object> remaining = SubAssets();
			Assert.That(remaining.Any(o => o is AnimatorState && o.name == "B"), Is.False,
				"消したはずの State がサブアセットに残っている");
			Assert.That(remaining.OfType<AnimatorStateTransition>().Count(), Is.EqualTo(0),
				"宛先を失った遷移がサブアセットに残っている");
		}

		[Test]
		public void RemoveStateAlsoRemovesTransitionsPointingAtIt()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			a.AddTransition(b);
			Root.AddAnyStateTransition(b);
			Root.AddEntryTransition(b);

			using (AcEdit e = AcEdit.Begin(_controller, "Remove State"))
			{
				e.RemoveState(Root, b);
			}

			Assert.That(a.transitions.Length, Is.EqualTo(0), "State からの遷移が残っている");
			Assert.That(Root.anyStateTransitions.Length, Is.EqualTo(0), "Any からの遷移が残っている");
			Assert.That(Root.entryTransitions.Length, Is.EqualTo(0), "Entry からの遷移が残っている");
		}

		[Test]
		public void RemoveStateMachineRemovesItsWholeSubtree()
		{
			AnimatorStateMachine sub = Root.AddStateMachine("Sub");
			sub.AddState("Inner1");
			sub.AddState("Inner2");
			AnimatorStateMachine deep = sub.AddStateMachine("Deep");
			deep.AddState("Deeper");
			AnimatorState outside = Root.AddState("Outside");
			outside.AddTransition(sub);
			AssetDatabase.SaveAssets();

			using (AcEdit e = AcEdit.Begin(_controller, "Remove Sub-State Machine"))
			{
				e.RemoveStateMachine(Root, sub);
			}

			Assert.That(Root.stateMachines.Length, Is.EqualTo(0));
			Assert.That(outside.transitions.Length, Is.EqualTo(0), "消したSMへの遷移が残っている");

			List<UnityEngine.Object> remaining = SubAssets();
			Assert.That(remaining.Any(o => o.name == "Inner1" || o.name == "Deeper" || o.name == "Deep"),
				Is.False, "サブステートマシンの中身がサブアセットに残っている");
		}

		[Test]
		public void RemoveTransitionFindsItsOwnerWhereverItLives()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateMachine sub = Root.AddStateMachine("Sub");

			AnimatorStateTransition fromState = a.AddTransition(b);
			AnimatorStateTransition fromAny = Root.AddAnyStateTransition(b);
			AnimatorTransition fromEntry = Root.AddEntryTransition(b);
			AnimatorTransition fromSubMachine = Root.AddStateMachineTransition(sub, b);

			using (AcEdit e = AcEdit.Begin(_controller, "Remove Transitions"))
			{
				e.RemoveTransition(fromState);
				e.RemoveTransition(fromAny);
				e.RemoveTransition(fromEntry);
				e.RemoveTransition(fromSubMachine);
			}

			Assert.That(a.transitions.Length, Is.EqualTo(0));
			Assert.That(Root.anyStateTransitions.Length, Is.EqualTo(0));
			Assert.That(Root.entryTransitions.Length, Is.EqualTo(0));
			Assert.That(Root.GetStateMachineTransitions(sub).Length, Is.EqualTo(0));
			Assert.That(SubAssets().OfType<AnimatorTransitionBase>().Count(), Is.EqualTo(0),
				"外した遷移がサブアセットに残っている");
		}

		#endregion

		#region Array copy trap

		[Test]
		public void SetStatePositionActuallyPersists()
		{
			AnimatorState state = Root.AddState("A", new Vector3(10f, 10f, 0f));

			using (AcEdit e = AcEdit.Begin(_controller, "Move"))
			{
				e.SetStatePosition(Root, state, new Vector2(300f, 120f));
			}

			// 落とし穴1: states は配列のコピー。要素だけ書き換えて戻し忘れると、
			// ここが元の (10,10) のままになる。
			ChildAnimatorState child = Root.states.First(c => c.state == state);
			Assert.That((Vector2)child.position, Is.EqualTo(new Vector2(300f, 120f)));
		}

		[Test]
		public void SetStateMachinePositionActuallyPersists()
		{
			AnimatorStateMachine sub = Root.AddStateMachine("Sub", new Vector3(0f, 0f, 0f));

			using (AcEdit e = AcEdit.Begin(_controller, "Move"))
			{
				e.SetStateMachinePosition(Root, sub, new Vector2(140f, 260f));
			}

			ChildAnimatorStateMachine child = Root.stateMachines.First(c => c.stateMachine == sub);
			Assert.That((Vector2)child.position, Is.EqualTo(new Vector2(140f, 260f)));
		}

		[Test]
		public void SetSpecialPositionPersistsForEachKind()
		{
			using (AcEdit e = AcEdit.Begin(_controller, "Move Specials"))
			{
				e.SetSpecialPosition(Root, AcNodeKind.Entry, new Vector2(1f, 2f));
				e.SetSpecialPosition(Root, AcNodeKind.Exit, new Vector2(3f, 4f));
				e.SetSpecialPosition(Root, AcNodeKind.Any, new Vector2(5f, 6f));
			}

			Assert.That((Vector2)Root.entryPosition, Is.EqualTo(new Vector2(1f, 2f)));
			Assert.That((Vector2)Root.exitPosition, Is.EqualTo(new Vector2(3f, 4f)));
			Assert.That((Vector2)Root.anyStatePosition, Is.EqualTo(new Vector2(5f, 6f)));
		}

		[Test]
		public void UndoRestoresStatePosition()
		{
			AnimatorState state = Root.AddState("A", new Vector3(10f, 10f, 0f));

			using (AcEdit e = AcEdit.Begin(_controller, "Move"))
			{
				e.SetStatePosition(Root, state, new Vector2(300f, 120f));
			}

			Undo.PerformUndo();

			ChildAnimatorState child = Root.states.First(c => c.state == state);
			Assert.That((Vector2)child.position, Is.EqualTo(new Vector2(10f, 10f)), "位置の Undo が効いていない");
		}

		#endregion

		#region Report

		[Test]
		public void ReportDescribesWhatChanged()
		{
			AcEditReport captured = null;
			Action<AcEditReport> handler = r => captured = r;
			AcEdit.AfterEdit += handler;
			try
			{
				using (AcEdit e = AcEdit.Begin(_controller, "Add State"))
				{
					e.AddState(Root, "A", Vector2.zero);
				}
			}
			finally
			{
				AcEdit.AfterEdit -= handler;
			}

			Assert.That(captured, Is.Not.Null, "AfterEdit が飛んでいない");
			Assert.That(captured.OperationName, Is.EqualTo("Add State"));
			Assert.That(captured.Controller, Is.EqualTo(_controller));
			Assert.That(captured.StructureChanged, Is.True);
			Assert.That(captured.Created.Count, Is.EqualTo(1));
		}

		[Test]
		public void FailedEditDoesNotFireAfterEdit()
		{
			int fired = 0;
			Action<AcEditReport> handler = r => fired++;
			AcEdit.AfterEdit += handler;
			try
			{
				Assert.Throws<NullReferenceException>(() =>
				{
					using (AcEdit e = AcEdit.Begin(_controller, "Boom"))
					{
						e.AddState(null, "Boom", Vector2.zero);
					}
				});
			}
			finally
			{
				AcEdit.AfterEdit -= handler;
			}

			Assert.That(fired, Is.EqualTo(0), "巻き戻した編集で AfterEdit を飛ばしている");
		}

		#endregion

		#region Plain property edits

		[Test]
		public void MotionSpeedAndWriteDefaultsRoundTripThroughUndo()
		{
			AnimatorState state = Root.AddState("A");
			state.speed = 1f;
			state.writeDefaultValues = true;

			using (AcEdit e = AcEdit.Begin(_controller, "Edit State"))
			{
				e.SetSpeed(state, 2.5f);
				e.SetWriteDefaults(state, false);
			}
			Assert.That(state.speed, Is.EqualTo(2.5f));
			Assert.That(state.writeDefaultValues, Is.False);

			Undo.PerformUndo();

			Assert.That(state.speed, Is.EqualTo(1f));
			Assert.That(state.writeDefaultValues, Is.True);
		}

		[Test]
		public void AddedTransitionIsReachableFromItsSource()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");

			AnimatorStateTransition transition;
			using (AcEdit e = AcEdit.Begin(_controller, "Add Transition"))
			{
				transition = e.AddTransition(a, b);
			}

			Assert.That(a.transitions.Length, Is.EqualTo(1));
			Assert.That(a.transitions[0], Is.EqualTo(transition));
			Assert.That(transition.destinationState, Is.EqualTo(b));

			Undo.PerformUndo();

			Assert.That(a.transitions.Length, Is.EqualTo(0), "遷移追加の Undo が効いていない");
		}

		#endregion
	}
}
