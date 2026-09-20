using System.Collections.Generic;
using colloid.FXCreator.AnimatorGraph;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// パラメータの型変更（§6.1）。
	///
	/// 見張るのは「型を変えたあとも Controller が<b>意味として</b>壊れていないこと」。
	/// Unity は型に合わない条件モードを弾かないので、テストが無いと
	/// 「グラフ上は正常なのに遷移しない」状態が黙って作られる。
	/// </summary>
	public class AcParameterTypeChangeTests
	{
		private const string TempFolder = "Assets/FXCreatorTypeChangeTests";

		private AnimatorController _controller;
		private string _path;

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TempFolder))
			{
				AssetDatabase.CreateFolder("Assets", "FXCreatorTypeChangeTests");
			}
			_path = TempFolder + "/TypeChange.controller";
			_controller = AnimatorController.CreateAnimatorControllerAtPath(_path);
		}

		[TearDown]
		public void TearDown()
		{
			Undo.ClearAll();
			_controller = null;
			AssetDatabase.DeleteAsset(TempFolder);
			AssetDatabase.Refresh();
		}

		private AnimatorStateMachine Root { get { return _controller.layers[0].stateMachine; } }

		private AnimatorStateTransition MakeTransition(
			string parameter, AnimatorConditionMode mode, float threshold)
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateTransition t = a.AddTransition(b);
			t.hasExitTime = false;
			t.AddCondition(mode, threshold, parameter);
			return t;
		}

		private static AnimatorCondition Only(AnimatorTransitionBase transition)
		{
			AnimatorCondition[] conditions = transition.conditions;
			Assert.That(conditions.Length, Is.EqualTo(1), "条件が1本である前提のテスト");
			return conditions[0];
		}

		#region Conversion table

		private static void AssertConvert(
			AnimatorControllerParameterType from, AnimatorControllerParameterType to,
			AnimatorConditionMode mode, float threshold,
			AnimatorConditionMode expectedMode, float expectedThreshold)
		{
			AnimatorConditionMode actualMode;
			float actualThreshold;
			bool ok = AcParameterTypeChange.TryConvert(
				from, to, mode, threshold, out actualMode, out actualThreshold);

			string label = from + "/" + mode + " " + threshold + " → " + to;
			Assert.That(ok, Is.True, label + " が読み替え不可になった");
			Assert.That(actualMode, Is.EqualTo(expectedMode), label);
			Assert.That(actualThreshold, Is.EqualTo(expectedThreshold).Within(0.0001f), label);
			// 読み替えた先が、その型で本当に成立するモードであること。
			Assert.That(AcParameterTypeChange.IsValidMode(to, actualMode), Is.True,
				label + " の結果 " + actualMode + " は " + to + " では成立しない");
		}

		private static void AssertUnconvertible(
			AnimatorControllerParameterType from, AnimatorControllerParameterType to,
			AnimatorConditionMode mode, float threshold)
		{
			AnimatorConditionMode actualMode;
			float actualThreshold;
			Assert.That(
				AcParameterTypeChange.TryConvert(from, to, mode, threshold, out actualMode, out actualThreshold),
				Is.False,
				from + "/" + mode + " " + threshold + " → " + to + " は読み替えられないはず");
		}

		[Test]
		public void Convert_BoolToInt()
		{
			AssertConvert(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Int,
				AnimatorConditionMode.If, 0f, AnimatorConditionMode.Equals, 1f);
			AssertConvert(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Int,
				AnimatorConditionMode.IfNot, 0f, AnimatorConditionMode.Equals, 0f);
		}

		[Test]
		public void Convert_BoolToFloat()
		{
			AssertConvert(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float,
				AnimatorConditionMode.If, 0f, AnimatorConditionMode.Greater, 0.5f);
			AssertConvert(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float,
				AnimatorConditionMode.IfNot, 0f, AnimatorConditionMode.Less, 0.5f);
		}

		[Test]
		public void Convert_IntToBool()
		{
			// ユーザー規則: Equals 0 は IfNot、それ以外は If。
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.Equals, 0f, AnimatorConditionMode.IfNot, 0f);
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.Equals, 3f, AnimatorConditionMode.If, 0f);
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.NotEqual, 0f, AnimatorConditionMode.If, 0f);
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.Greater, 0f, AnimatorConditionMode.If, 0f);
			// Int の「1 未満」は 0 のこと。ここを If にすると意味が反転する。
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.Less, 1f, AnimatorConditionMode.IfNot, 0f);
		}

		[Test]
		public void Convert_FloatToBool()
		{
			AssertConvert(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.Greater, 0.5f, AnimatorConditionMode.If, 0f);
			AssertConvert(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Bool,
				AnimatorConditionMode.Less, 0.5f, AnimatorConditionMode.IfNot, 0f);
		}

		[Test]
		public void Convert_FloatToInt_RoundsOutward()
		{
			// 範囲を狭めない側へ丸める。Greater 2.7 を Greater 3 にすると 3 が漏れる。
			AssertConvert(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int,
				AnimatorConditionMode.Greater, 2.7f, AnimatorConditionMode.Greater, 2f);
			AssertConvert(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int,
				AnimatorConditionMode.Less, 2.2f, AnimatorConditionMode.Less, 3f);
		}

		[Test]
		public void Convert_IntToFloat_KeepsComparisons()
		{
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float,
				AnimatorConditionMode.Greater, 2f, AnimatorConditionMode.Greater, 2f);
			AssertConvert(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float,
				AnimatorConditionMode.Less, 2f, AnimatorConditionMode.Less, 2f);
		}

		[Test]
		public void Convert_IntToFloat_EqualsIsUnconvertible()
		{
			// Float に一致比較は無い。幅を持たせると条件が2本要るので読み替えない。
			AssertUnconvertible(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float,
				AnimatorConditionMode.Equals, 3f);
			AssertUnconvertible(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float,
				AnimatorConditionMode.NotEqual, 3f);
		}

		[Test]
		public void Convert_ToTrigger_OnlyKeepsPositiveConditions()
		{
			AssertConvert(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Trigger,
				AnimatorConditionMode.If, 0f, AnimatorConditionMode.If, 0f);

			// 「立っていない」は Trigger では表せない。
			AssertUnconvertible(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Trigger,
				AnimatorConditionMode.IfNot, 0f);
			AssertUnconvertible(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Trigger,
				AnimatorConditionMode.Equals, 0f);
			AssertUnconvertible(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Trigger,
				AnimatorConditionMode.Less, 0.5f);
		}

		/// <summary>
		/// 読み替えた結果は、必ずその型で成立するモードであること。
		/// ここが破れると「見た目は正常なのに遷移しない」Controller ができる。
		/// </summary>
		[Test]
		public void Convert_NeverProducesInvalidMode()
		{
			var types = new[]
			{
				AnimatorControllerParameterType.Bool,
				AnimatorControllerParameterType.Int,
				AnimatorControllerParameterType.Float,
				AnimatorControllerParameterType.Trigger
			};
			var modes = new[]
			{
				AnimatorConditionMode.If, AnimatorConditionMode.IfNot,
				AnimatorConditionMode.Greater, AnimatorConditionMode.Less,
				AnimatorConditionMode.Equals, AnimatorConditionMode.NotEqual
			};
			var thresholds = new[] { -1f, 0f, 0.5f, 1f, 3f };

			int converted = 0, refused = 0;
			foreach (AnimatorControllerParameterType from in types)
			{
				foreach (AnimatorControllerParameterType to in types)
				{
					if (from == to)
					{
						continue;
					}
					foreach (AnimatorConditionMode mode in modes)
					{
						if (!AcParameterTypeChange.IsValidMode(from, mode))
						{
							continue;   // もとの型で成立しない組み合わせは入力になりえない
						}
						foreach (float threshold in thresholds)
						{
							AnimatorConditionMode newMode;
							float newThreshold;
							if (AcParameterTypeChange.TryConvert(
									from, to, mode, threshold, out newMode, out newThreshold))
							{
								converted++;
								Assert.That(AcParameterTypeChange.IsValidMode(to, newMode), Is.True,
									from + "/" + mode + " " + threshold + " → " + to
									+ " が " + newMode + " になった（" + to + " では成立しない）");
							}
							else
							{
								refused++;
							}
						}
					}
				}
			}

			// 総当たりが「全部断った」「全部通した」で緑になっていないことの見張り
			// （Phase 4 のランダムテストで踏んだのと同じ罠）。
			Assert.That(converted, Is.GreaterThan(0), "1件も読み替えられていない");
			Assert.That(refused, Is.GreaterThan(0), "1件も断っていない（不可の判定が死んでいる）");
		}

		#endregion

		#region Apply

		[Test]
		public void Apply_RewritesConditions()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			AnimatorStateTransition t = MakeTransition("p", AnimatorConditionMode.If, 0f);

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Int);
			}

			Assert.That(_controller.parameters[0].type, Is.EqualTo(AnimatorControllerParameterType.Int));
			AnimatorCondition c = Only(t);
			Assert.That(c.mode, Is.EqualTo(AnimatorConditionMode.Equals));
			Assert.That(c.threshold, Is.EqualTo(1f).Within(0.0001f));
		}

		[Test]
		public void Apply_RewritesEveryConditionOnTheSameTransition()
		{
			// conditions は配列のコピー。1本ずつ書き戻すと最後の1本しか残らない。
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			AnimatorStateTransition t = MakeTransition("p", AnimatorConditionMode.If, 0f);
			t.AddCondition(AnimatorConditionMode.IfNot, 0f, "p");

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Int);
			}

			AnimatorCondition[] conditions = t.conditions;
			Assert.That(conditions.Length, Is.EqualTo(2));
			Assert.That(conditions[0].mode, Is.EqualTo(AnimatorConditionMode.Equals));
			Assert.That(conditions[0].threshold, Is.EqualTo(1f).Within(0.0001f));
			Assert.That(conditions[1].mode, Is.EqualTo(AnimatorConditionMode.Equals));
			Assert.That(conditions[1].threshold, Is.EqualTo(0f).Within(0.0001f));
		}

		[Test]
		public void Apply_CarriesDefaultValue()
		{
			_controller.AddParameter(
				new AnimatorControllerParameter
				{
					name = "p",
					type = AnimatorControllerParameterType.Bool,
					defaultBool = true
				});

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Int);
			}

			// true だった Bool が 0 の Int になると、既定の見た目が変わってしまう。
			Assert.That(_controller.parameters[0].defaultInt, Is.EqualTo(1));
		}

		[Test]
		public void Apply_LeavesUnconvertibleReferencesAlone()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Float);
			AnimatorState state = Root.AddState("S");
			state.speedParameterActive = true;
			state.speedParameter = "p";

			AcParameterTypeChangePlan plan =
				AcParameterTypeChange.Plan(_controller, "p", AnimatorControllerParameterType.Int);
			Assert.That(plan.HasBlockers, Is.True, "Speed は Float しか受け付けないので止まるはず");

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Int);
			}

			// 黙って消さない。型を戻せば元どおりになる。
			Assert.That(state.speedParameter, Is.EqualTo("p"));
			Assert.That(state.speedParameterActive, Is.True);
		}

		[Test]
		public void Apply_IsOneUndoStep()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			AnimatorStateTransition t = MakeTransition("p", AnimatorConditionMode.If, 0f);

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Int);
			}

			Undo.PerformUndo();

			Assert.That(_controller.parameters[0].type, Is.EqualTo(AnimatorControllerParameterType.Bool));
			Assert.That(Only(t).mode, Is.EqualTo(AnimatorConditionMode.If));
		}

		[Test]
		public void Apply_SameTypeDoesNothing()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			AnimatorStateTransition t = MakeTransition("p", AnimatorConditionMode.If, 0f);

			AcParameterTypeChangePlan plan =
				AcParameterTypeChange.Plan(_controller, "p", AnimatorControllerParameterType.Bool);
			Assert.That(plan.IsNoOp, Is.True);

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Bool);
			}

			Assert.That(Only(t).mode, Is.EqualTo(AnimatorConditionMode.If));
		}

		[Test]
		public void Plan_IgnoresOtherParameters()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			_controller.AddParameter("other", AnimatorControllerParameterType.Bool);
			AnimatorStateTransition t = MakeTransition("other", AnimatorConditionMode.If, 0f);

			AcParameterTypeChangePlan plan =
				AcParameterTypeChange.Plan(_controller, "p", AnimatorControllerParameterType.Int);

			Assert.That(plan.Rewrites, Is.Empty, "別のパラメータの条件まで巻き込んでいる");
			Assert.That(Only(t).mode, Is.EqualTo(AnimatorConditionMode.If));
		}

		[Test]
		public void Plan_FindsConditionsInNestedStateMachines()
		{
			// 走査範囲が AcParameterUsage と揃っていることの見張り。
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			AnimatorStateMachine child = Root.AddStateMachine("Child");
			AnimatorState a = child.AddState("A");
			AnimatorState b = child.AddState("B");
			AnimatorStateTransition t = a.AddTransition(b);
			t.hasExitTime = false;
			t.AddCondition(AnimatorConditionMode.If, 0f, "p");

			AcParameterTypeChangePlan plan =
				AcParameterTypeChange.Plan(_controller, "p", AnimatorControllerParameterType.Int);

			Assert.That(plan.Rewrites.Count, Is.EqualTo(1), "入れ子のステートマシンを見ていない");
		}

		[Test]
		public void Plan_ReportsBlendTreeAsBlocker()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Float);
			var tree = new BlendTree { blendType = BlendTreeType.Simple1D, blendParameter = "p", name = "Tree" };
			AssetDatabase.AddObjectToAsset(tree, _controller);
			AnimatorState state = Root.AddState("S");
			state.motion = tree;

			AcParameterTypeChangePlan plan =
				AcParameterTypeChange.Plan(_controller, "p", AnimatorControllerParameterType.Bool);

			Assert.That(plan.HasBlockers, Is.True, "BlendTree の軸は Float しか受け付けない");
			Assert.That(plan.Summary(), Does.Contain("Float"));
		}

		#endregion
	}
}
