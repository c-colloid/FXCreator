using System.Linq;
using colloid.FXCreator.AnimatorGraph;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// パラメータ改名が Controller 内の<b>すべての</b>参照に追随することの検証（§8）。
	///
	/// 取りこぼすと、名前だけ変わって挙動が壊れた Controller ができる。
	/// 条件式だけ直して満足しやすいので、State の speed / mirror などや
	/// BlendTree の軸まで含めて固定する。
	/// </summary>
	public class ParameterRenameTests
	{
		private const string TempFolder = "Assets/FXCreatorParameterRenameTests";

		private AnimatorController _controller;

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TempFolder))
			{
				AssetDatabase.CreateFolder("Assets", "FXCreatorParameterRenameTests");
			}
			_controller = AnimatorController.CreateAnimatorControllerAtPath(TempFolder + "/Rename.controller");
		}

		[TearDown]
		public void TearDown()
		{
			_controller = null;
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TempFolder);
			AssetDatabase.Refresh();
		}

		private AnimatorStateMachine Root => _controller.layers[0].stateMachine;

		private static AnimatorCondition[] ConditionsOf(AnimatorTransitionBase t) => t.conditions;

		#region Conditions

		[Test]
		public void RenameFollowsConditionsOnEveryKindOfTransition()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateMachine sub = Root.AddStateMachine("Sub");

			AnimatorStateTransition fromState = a.AddTransition(b);
			fromState.AddCondition(AnimatorConditionMode.If, 0f, "Old");
			AnimatorStateTransition fromAny = Root.AddAnyStateTransition(b);
			fromAny.AddCondition(AnimatorConditionMode.IfNot, 0f, "Old");
			AnimatorTransition fromEntry = Root.AddEntryTransition(b);
			fromEntry.AddCondition(AnimatorConditionMode.If, 0f, "Old");
			AnimatorTransition fromSub = Root.AddStateMachineTransition(sub, b);
			fromSub.AddCondition(AnimatorConditionMode.If, 0f, "Old");

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Assert.That(_controller.parameters.Any(p => p.name == "New"), Is.True);
			Assert.That(_controller.parameters.Any(p => p.name == "Old"), Is.False);
			Assert.That(ConditionsOf(fromState)[0].parameter, Is.EqualTo("New"), "State の遷移");
			Assert.That(ConditionsOf(fromAny)[0].parameter, Is.EqualTo("New"), "Any の遷移");
			Assert.That(ConditionsOf(fromEntry)[0].parameter, Is.EqualTo("New"), "Entry の遷移");
			Assert.That(ConditionsOf(fromSub)[0].parameter, Is.EqualTo("New"), "SubSM の遷移");
		}

		[Test]
		public void RenameReachesNestedStateMachines()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			AnimatorStateMachine sub = Root.AddStateMachine("Sub");
			AnimatorStateMachine deep = sub.AddStateMachine("Deep");
			AnimatorState x = deep.AddState("X");
			AnimatorState y = deep.AddState("Y");
			AnimatorStateTransition transition = x.AddTransition(y);
			transition.AddCondition(AnimatorConditionMode.If, 0f, "Old");

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Assert.That(transition.conditions[0].parameter, Is.EqualTo("New"));
		}

		[Test]
		public void RenameLeavesOtherParametersAlone()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			_controller.AddParameter("Keep", AnimatorControllerParameterType.Bool);
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateTransition transition = a.AddTransition(b);
			transition.AddCondition(AnimatorConditionMode.If, 0f, "Old");
			transition.AddCondition(AnimatorConditionMode.If, 0f, "Keep");

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Assert.That(transition.conditions[0].parameter, Is.EqualTo("New"));
			Assert.That(transition.conditions[1].parameter, Is.EqualTo("Keep"), "巻き添えで書き換えている");
		}

		#endregion

		#region The references that are easy to forget

		[Test]
		public void RenameFollowsStateSpeedAndMirrorParameters()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Float);
			AnimatorState state = Root.AddState("A");
			state.speedParameterActive = true;
			state.speedParameter = "Old";
			state.cycleOffsetParameterActive = true;
			state.cycleOffsetParameter = "Old";
			state.mirrorParameterActive = true;
			state.mirrorParameter = "Old";
			state.timeParameterActive = true;
			state.timeParameter = "Old";

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Assert.That(state.speedParameter, Is.EqualTo("New"), "Speed");
			Assert.That(state.cycleOffsetParameter, Is.EqualTo("New"), "Cycle Offset");
			Assert.That(state.mirrorParameter, Is.EqualTo("New"), "Mirror");
			Assert.That(state.timeParameter, Is.EqualTo("New"), "Motion Time");
		}

		[Test]
		public void RenameFollowsBlendTreeAxesIncludingNestedTrees()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Float);
			AnimatorState state = Root.AddState("A");

			var outer = new BlendTree { name = "Outer", blendType = BlendTreeType.FreeformCartesian2D };
			var inner = new BlendTree { name = "Inner", blendType = BlendTreeType.Simple1D };
			AssetDatabase.AddObjectToAsset(outer, _controller);
			AssetDatabase.AddObjectToAsset(inner, _controller);
			outer.blendParameter = "Old";
			outer.blendParameterY = "Old";
			inner.blendParameter = "Old";
			outer.AddChild(inner);
			state.motion = outer;

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Assert.That(outer.blendParameter, Is.EqualTo("New"), "外側 X");
			Assert.That(outer.blendParameterY, Is.EqualTo("New"), "外側 Y");
			Assert.That(inner.blendParameter, Is.EqualTo("New"), "入れ子の BlendTree");
		}

		[Test]
		public void RenameFollowsDirectBlendParameters()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Float);
			AnimatorState state = Root.AddState("A");

			var tree = new BlendTree { name = "Direct", blendType = BlendTreeType.Direct };
			AssetDatabase.AddObjectToAsset(tree, _controller);
			tree.AddChild(null);
			ChildMotion[] children = tree.children;
			children[0].directBlendParameter = "Old";
			tree.children = children;
			state.motion = tree;

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Assert.That(tree.children[0].directBlendParameter, Is.EqualTo("New"));
		}

		#endregion

		#region Add / remove / undo

		[Test]
		public void RenamingToATakenNameGetsASuffixInsteadOfColliding()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			_controller.AddParameter("Taken", AnimatorControllerParameterType.Bool);
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateTransition transition = a.AddTransition(b);
			transition.AddCondition(AnimatorConditionMode.If, 0f, "Old");

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "Taken");
			}

			// 2つが同名になると、どちらを指しているか分からない Controller になる。
			Assert.That(_controller.parameters.Select(p => p.name).Distinct().Count(),
				Is.EqualTo(_controller.parameters.Length), "同名のパラメータができている");
			Assert.That(transition.conditions[0].parameter, Is.EqualTo("Taken 1"),
				"条件が付け替え後の名前を指していない");
		}

		[Test]
		public void UndoRestoresBothTheNameAndTheConditions()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateTransition transition = a.AddTransition(b);
			transition.AddCondition(AnimatorConditionMode.If, 0f, "Old");

			using (AcEdit e = AcEdit.Begin(_controller, "Rename"))
			{
				e.RenameParameter("Old", "New");
			}

			Undo.PerformUndo();

			Assert.That(_controller.parameters.Any(p => p.name == "Old"), Is.True, "名前が戻っていない");
			Assert.That(transition.conditions[0].parameter, Is.EqualTo("Old"), "条件が戻っていない");
		}

		[Test]
		public void AddAndRemoveGoThroughTheTransaction()
		{
			using (AcEdit e = AcEdit.Begin(_controller, "Add"))
			{
				e.AddParameter("Toggle", AnimatorControllerParameterType.Bool);
			}
			Assert.That(_controller.parameters.Any(p => p.name == "Toggle"), Is.True);

			Undo.PerformUndo();
			Assert.That(_controller.parameters.Any(p => p.name == "Toggle"), Is.False, "追加の Undo が効いていない");

			_controller.AddParameter("Gone", AnimatorControllerParameterType.Int);
			using (AcEdit e = AcEdit.Begin(_controller, "Remove"))
			{
				e.RemoveParameter("Gone");
			}
			Assert.That(_controller.parameters.Any(p => p.name == "Gone"), Is.False);
		}

		[Test]
		public void AddingADuplicateNameGetsASuffix()
		{
			using (AcEdit e = AcEdit.Begin(_controller, "Add"))
			{
				e.AddParameter("Dup", AnimatorControllerParameterType.Bool);
				e.AddParameter("Dup", AnimatorControllerParameterType.Bool);
			}

			Assert.That(_controller.parameters.Length, Is.EqualTo(2));
			Assert.That(_controller.parameters.Select(p => p.name).Distinct().Count(), Is.EqualTo(2));
		}

		[Test]
		public void ModifyingAParameterActuallyPersists()
		{
			_controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

			using (AcEdit e = AcEdit.Begin(_controller, "Set default"))
			{
				e.ModifyParameter("Speed", p => p.defaultFloat = 2.5f);
			}

			// parameters は配列のコピー。戻し忘れるとここが 0 のままになる。
			Assert.That(_controller.parameters.First(p => p.name == "Speed").defaultFloat, Is.EqualTo(2.5f));
		}

		#endregion
	}
}
