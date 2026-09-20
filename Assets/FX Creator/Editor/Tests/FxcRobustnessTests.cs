using System.Collections.Generic;
using colloid.FXCreator.AnimatorGraph;
using colloid.FXCreator.Graph;
using colloid.FXCreator.Targeting;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// 例外時の挙動（Docs/FXCreator-Design.md Phase 8）。
	///
	/// v0.1 のウィンドウは<b>閉じずに開きっぱなしで使う</b>ことが前提なので、
	/// 開いている間にアバターが消える・Controller が消える・レイヤーが減る、が普通に起きる。
	/// そのとき落ちると、Unity の再起動まで作業が止まる。
	///
	/// ここで見張るのは「落ちないこと」と「編集できないと正しく答えること」の2点だけ。
	/// 気の利いた復旧はしない（R4 の「触らなければ壊さない」）。
	/// </summary>
	public class FxcRobustnessTests
	{
		private const string TempFolder = "Assets/FXCreatorRobustnessTests";

		private readonly List<GameObject> _objects = new List<GameObject>();

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TempFolder))
			{
				AssetDatabase.CreateFolder("Assets", "FXCreatorRobustnessTests");
			}
		}

		[TearDown]
		public void TearDown()
		{
			for (int i = 0; i < _objects.Count; i++)
			{
				if (_objects[i] != null)
				{
					Object.DestroyImmediate(_objects[i]);
				}
			}
			_objects.Clear();

			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TempFolder);
			AssetDatabase.Refresh();
		}

		private AnimatorController NewController(string name = "R")
		{
			return AnimatorController.CreateAnimatorControllerAtPath(TempFolder + "/" + name + ".controller");
		}

		private GameObject NewAvatar(string name = "R_Avatar")
		{
			var go = new GameObject(name);
			go.AddComponent<UnityEngine.Animator>();
			_objects.Add(go);
			return go;
		}

		#region Graph source

		[Test]
		public void GraphSource_WithNoController_IsEmptyAndNotEditable()
		{
			using (var source = new AcGraphSource())
			{
				Assert.DoesNotThrow(() => source.SetController(null));
				Assert.That(source.Nodes, Is.Empty);
				Assert.That(source.Edges, Is.Empty);
				Assert.That(source.CanEdit, Is.False);
				Assert.DoesNotThrow(() => source.Refresh());
			}
		}

		/// <summary>
		/// 開いたまま Controller アセットを消された場合。Unity の <c>==</c> は
		/// 破棄済みオブジェクトを null と答えるので、そこに乗って静かに空になればよい。
		/// </summary>
		[Test]
		public void GraphSource_SurvivesControllerBeingDeleted()
		{
			AnimatorController controller = NewController();
			controller.layers[0].stateMachine.AddState("A");

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);
				Assert.That(source.Nodes, Is.Not.Empty);

				AssetDatabase.DeleteAsset(TempFolder + "/R.controller");
				AssetDatabase.Refresh();

				Assert.DoesNotThrow(() => source.Refresh());
				Assert.That(source.CanEdit, Is.False);
			}
		}

		[Test]
		public void GraphSource_LayerIndexOutOfRange_IsEmptyNotAnException()
		{
			AnimatorController controller = NewController();

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);

				Assert.DoesNotThrow(() => source.SetLayer(99));
				Assert.That(source.Current, Is.Null);
				Assert.That(source.Nodes, Is.Empty);
				Assert.That(source.CanEdit, Is.False);

				Assert.DoesNotThrow(() => source.SetLayer(-1));
				Assert.That(source.Nodes, Is.Empty);
			}
		}

		/// <summary>外部（標準 Animator ウィンドウ等）でレイヤーを消されたとき（R5）。</summary>
		[Test]
		public void GraphSource_SurvivesLayerRemovedExternally()
		{
			AnimatorController controller = NewController();
			controller.AddLayer("Second");
			controller.layers[1].stateMachine.AddState("A");

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);
				source.SetLayer(1);
				Assert.That(source.Nodes, Is.Not.Empty);

				controller.RemoveLayer(1);

				Assert.DoesNotThrow(() => source.Refresh());
				Assert.That(source.Nodes, Is.Empty);
				Assert.That(source.CanEdit, Is.False);
			}
		}

		/// <summary>編集操作は、編集できない状態で呼ばれても黙って何もしないこと。</summary>
		[Test]
		public void GraphSource_EditOperationsAreNoOpsWhenNotEditable()
		{
			using (var source = new AcGraphSource())
			{
				source.SetController(null);

				var ids = new List<string> { "nope" };
				Assert.DoesNotThrow(() => source.MoveNodes(ids, new Vector2(10f, 10f)));
				Assert.DoesNotThrow(() => source.DeleteNodes(ids));
				Assert.DoesNotThrow(() => source.DeleteEdges(ids));
				Assert.DoesNotThrow(() => source.MoveNodes(null, Vector2.zero));
				Assert.DoesNotThrow(() => source.DeleteNodes(null));
				Assert.DoesNotThrow(() => source.DeleteEdges(null));

				var from = new FXCPortRef("a", "p");
				var to = new FXCPortRef("b", "p");
				Assert.That(source.CanConnect(from, to), Is.False);
				Assert.DoesNotThrow(() => source.Connect(from, to));

				Assert.DoesNotThrow(() =>
					source.PopulateContextMenu(new GenericMenu(), new FXCGraphContext()));
			}
		}

		[Test]
		public void GraphSource_UnknownIdsResolveToNothing()
		{
			AnimatorController controller = NewController();
			using (var source = new AcGraphSource())
			{
				source.SetController(controller);

				AcGraphNode node;
				Assert.That(source.TryGetNode("missing", out node), Is.False);
				Assert.That(source.TryGetNode(null, out node), Is.False);

				AnimatorTransitionBase transition;
				Assert.That(source.TryGetTransition("missing", out transition), Is.False);
				Assert.That(source.TryGetTransition(null, out transition), Is.False);
			}
		}

		#endregion

		#region Targeting

		[Test]
		public void Resolver_WithNoAvatar_StillReturnsTargetsWithReasons()
		{
			List<IFxTarget> targets = FxTargetResolver.CreateTargets(null);

			Assert.That(targets, Is.Not.Empty);
			string reason;
			for (int i = 0; i < targets.Count; i++)
			{
				Assert.That(targets[i].IsAvailable(out reason), Is.False);
				Assert.That(reason, Is.Not.Null.And.Not.Empty, targets[i].Id + " に理由が無い");
				Assert.DoesNotThrow(() => targets[i].ResolveController());
				Assert.DoesNotThrow(() => targets[i].ResolveParameterStore());
				// ResolveMenu() はここでは呼ばない。戻り値が VRCExpressionsMenu なので、
				// 呼ぶだけでテストアセンブリに VRC SDK への参照が要る。
				// メニューは v0.1 では表示のみ（R4）で、null 安全性は
				// VrcMenuPanel.SetMenu(null) 側で担保されている。
				Assert.DoesNotThrow(() => targets[i].OnAfterEdit(null));
				Assert.DoesNotThrow(() =>
				{
					string ignored;
					targets[i].TryPrepareForEditing(false, out ignored);
				});
			}
		}

		/// <summary>ウィンドウを開いたままアバターを消された場合。</summary>
		[Test]
		public void Target_SurvivesAvatarBeingDestroyed()
		{
			GameObject avatar = NewAvatar();
			var target = new DirectAvatarTarget(avatar);

			Object.DestroyImmediate(avatar);
			_objects.Clear();

			string reason;
			Assert.That(target.IsAvailable(out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
			Assert.That(target.ResolveController(), Is.Null);
			Assert.That(target.IsAlreadySetUp, Is.False);
			Assert.DoesNotThrow(() => target.OnAfterEdit(null));

			// 支度は「できない」と答えること。ここで例外を投げると、
			// アバターを消しただけでウィンドウが操作不能になる。
			Assert.That(target.TryPrepareForEditing(false, out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
		}

		[Test]
		public void ChooseDefault_WithEmptyListIsNull()
		{
			Assert.That(FxTargetResolver.ChooseDefault(new List<IFxTarget>(), "direct"), Is.Null);
			Assert.That(FxTargetResolver.ChooseDefault(null, "direct"), Is.Null);
		}

		#endregion

		#region Edits and planning

		[Test]
		public void AcEdit_RefusesNullController()
		{
			Assert.Throws<System.ArgumentNullException>(() => AcEdit.Begin(null, "x"));
		}

		/// <summary>存在しない名前を指しても、静かに何もしないこと。</summary>
		[Test]
		public void AcEdit_ParameterOperationsOnMissingNamesAreNoOps()
		{
			AnimatorController controller = NewController();

			using (AcEdit e = AcEdit.Begin(controller, "x"))
			{
				Assert.DoesNotThrow(() => e.RemoveParameter("missing"));
				Assert.DoesNotThrow(() => e.RenameParameter("missing", "other"));
				Assert.DoesNotThrow(() => e.ChangeParameterType("missing", AnimatorControllerParameterType.Int));
				Assert.DoesNotThrow(() => e.ModifyParameter("missing", p => p.defaultInt = 1));
				Assert.DoesNotThrow(() => e.RenameParameter(null, null));
			}

			Assert.That(controller.parameters, Is.Empty);
		}

		[Test]
		public void TypeChangePlan_HandlesNullAndMissing()
		{
			AnimatorController controller = NewController();

			Assert.That(
				AcParameterTypeChange.Plan(null, "p", AnimatorControllerParameterType.Int).IsNoOp, Is.True);
			Assert.That(
				AcParameterTypeChange.Plan(controller, null, AnimatorControllerParameterType.Int).IsNoOp, Is.True);
			Assert.That(
				AcParameterTypeChange.Plan(controller, "missing", AnimatorControllerParameterType.Int).IsNoOp,
				Is.True);
		}

		[Test]
		public void ParameterSync_HandlesNulls()
		{
			AnimatorController controller = NewController();

			Assert.DoesNotThrow(() => FxParameterSync.FollowControllerEdits(null, null));
			Assert.That(FxParameterSync.MissingInStore(null, null), Is.Empty);
			Assert.That(FxParameterSync.MissingInController(null, null), Is.Empty);
			Assert.DoesNotThrow(() => FxParameterSync.DeclareInStore(null, null));
			Assert.DoesNotThrow(() => FxParameterSync.DeclareInController(new FxSyncParameter(), null));
			Assert.DoesNotThrow(() => FxParameterSync.DeclareInController(new FxSyncParameter(), controller));

			string reason;
			Assert.That(FxParameterSync.CanDeclare(null, out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
		}

		[Test]
		public void ControllerAccess_ReportsReasonForDeletedAsset()
		{
			AnimatorController controller = NewController();
			AssetDatabase.DeleteAsset(TempFolder + "/R.controller");
			AssetDatabase.Refresh();

			string reason;
			Assert.That(AcControllerAccess.IsWritable(controller, out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
		}

		#endregion

		#region Coexistence with the standard Animator window (R5)

		/// <summary>
		/// 標準 Animator ウィンドウが同じ Controller を書き換えたときに追従すること。
		///
		/// R5 の言い分は「同じアセットを見ているので原理的に安全」だが、
		/// グラフは読み込んだ時点のスナップショットを持っているので、
		/// <b>作り直さなければ古いまま</b>。ウィンドウのフォーカス取得で
		/// <c>Refresh</c> が走る経路（§4.5）が効いていることを、ここで固定する。
		/// </summary>
		[Test]
		public void GraphSource_PicksUpExternalStateAdditions()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine root = controller.layers[0].stateMachine;
			root.AddState("A");

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);
				int before = source.Nodes.Count;

				// AcEdit を通さない＝標準 Animator ウィンドウがやるのと同じ書き換え。
				root.AddState("AddedOutside");

				source.Refresh();

				Assert.That(source.Nodes.Count, Is.EqualTo(before + 1),
					"外部で足された State を拾えていない");
			}
		}

		[Test]
		public void GraphSource_PicksUpExternalDeletions()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine root = controller.layers[0].stateMachine;
			AnimatorState doomed = root.AddState("Doomed");

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);
				int before = source.Nodes.Count;

				root.RemoveState(doomed);

				Assert.DoesNotThrow(() => source.Refresh());
				Assert.That(source.Nodes.Count, Is.EqualTo(before - 1));
			}
		}

		/// <summary>
		/// 逆向き。FX Creator の編集が、生の Controller にそのまま出ていること
		/// （＝標準ウィンドウからも同じものが見える）。
		/// </summary>
		[Test]
		public void EditsAreVisibleInTheRawController()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine root = controller.layers[0].stateMachine;

			using (AcEdit e = AcEdit.Begin(controller, "x"))
			{
				e.AddState(root, "FromFxCreator", new Vector2(40f, 60f));
			}

			ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
			bool found = false;
			for (int i = 0; i < states.Length; i++)
			{
				if (states[i].state != null && states[i].state.name == "FromFxCreator")
				{
					found = true;
					// 位置も Controller ネイティブに入る（§4.1）。だから配置が共有される。
					Assert.That(states[i].position.x, Is.EqualTo(40f).Within(0.001f));
					Assert.That(states[i].position.y, Is.EqualTo(60f).Within(0.001f));
				}
			}
			Assert.That(found, Is.True, "FX Creator が作った State が Controller に無い");
		}

		/// <summary>
		/// 外部変更のあとも選択が破綻しないこと。選択は ID で持っているので、
		/// 消えた要素の ID は掃除され、生き残った選択は保たれる（§4.5）。
		/// </summary>
		[Test]
		public void Selection_SurvivesRebuild()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine root = controller.layers[0].stateMachine;
			AnimatorState keep = root.AddState("Keep");
			AnimatorState drop = root.AddState("Drop");

			var selection = new FXCSelection();
			selection.SelectOnlyNode(AcNodeRef.MakeId(AcNodeKind.State, keep));
			selection.ToggleNode(AcNodeRef.MakeId(AcNodeKind.State, drop));
			Assert.That(selection.Count, Is.EqualTo(2));

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);
				root.RemoveState(drop);
				source.Refresh();

				var liveNodes = new List<string>();
				foreach (IFXCGraphNode n in source.Nodes)
				{
					liveNodes.Add(n.Id);
				}
				selection.Prune(liveNodes, new List<string>());

				Assert.That(selection.ContainsNode(AcNodeRef.MakeId(AcNodeKind.State, keep)), Is.True,
					"生きている選択まで捨てている");
				Assert.That(selection.Count, Is.EqualTo(1), "消えた要素の選択が残っている");
			}
		}

		#endregion
	}
}
