using System.Linq;
using colloid.FXCreator.AnimatorGraph;
using colloid.FXCreator.Graph;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// グラフ操作 → Controller の書き込み（Docs/FXCreator-Design.md §9 Phase 3）。
	/// <see cref="AcEdit"/> 自体は <see cref="AcEditTests"/> が見るので、ここは
	/// <b>どの操作がどの API に落ちるか</b>の対応表を固定する。接続可否の表は
	/// 手で書くと取りこぼしやすいので、組み合わせを網羅して確かめる。
	///
	/// Controller は実アセット。<see cref="AcGraphSource.CanEdit"/> は
	/// 保存先のないアセットを読み取り専用として弾くので、メモリ上では編集を試せない。
	/// </summary>
	public class AcGraphEditingTests
	{
		private const string TempFolder = "Assets/FXCreatorAcGraphEditingTests";

		private AnimatorController _controller;
		private AcGraphSource _source;

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TempFolder))
			{
				AssetDatabase.CreateFolder("Assets", "FXCreatorAcGraphEditingTests");
			}
			_controller = AnimatorController.CreateAnimatorControllerAtPath(TempFolder + "/Graph.controller");
			_source = new AcGraphSource();
			_source.SetController(_controller);
		}

		[TearDown]
		public void TearDown()
		{
			_source.Dispose();
			_source = null;
			_controller = null;
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TempFolder);
			AssetDatabase.Refresh();
		}

		private AnimatorStateMachine Root => _controller.layers[0].stateMachine;

		private static FXCPortRef Out(string nodeId) => new FXCPortRef(nodeId, AcGraphPort.OutId);

		private static FXCPortRef In(string nodeId) => new FXCPortRef(nodeId, AcGraphPort.InId);

		private string NodeIdOf(AcNodeKind kind)
		{
			return _source.Nodes.Cast<AcGraphNode>().First(n => n.Ref.Kind == kind).Id;
		}

		private string StateNode(AnimatorState state) => AcNodeRef.MakeId(AcNodeKind.State, state);

		private string MachineNode(AnimatorStateMachine sm) => AcNodeRef.MakeId(AcNodeKind.StateMachine, sm);

		#region Editability

		[Test]
		public void AnAssetBackedControllerIsEditable()
		{
			Assert.That(_source.CanEdit, Is.True);
			Assert.That(_source.CanMoveNodes, Is.True);
			Assert.That(_source.ReadOnlyReason, Is.Null);
		}

		[Test]
		public void AnInMemoryControllerIsReadOnly()
		{
			var loose = new AnimatorController();
			loose.AddLayer("L");
			using (var source = new AcGraphSource())
			{
				source.SetController(loose);

				// 保存先が無いので、編集させても結果が残らない。
				Assert.That(source.CanEdit, Is.False);
				Assert.That(source.ReadOnlyReason, Is.Not.Null);
			}
			Object.DestroyImmediate(loose.layers[0].stateMachine);
			Object.DestroyImmediate(loose);
		}

		#endregion

		#region Connection rules

		[Test]
		public void StateToStateIsAllowedAndCreatesATransition()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			_source.Refresh();

			Assert.That(_source.CanConnect(Out(StateNode(a)), In(StateNode(b))), Is.True);
			_source.Connect(Out(StateNode(a)), In(StateNode(b)));

			Assert.That(a.transitions.Length, Is.EqualTo(1));
			Assert.That(a.transitions[0].destinationState, Is.EqualTo(b));
		}

		[Test]
		public void StateToExitBecomesAnExitTransition()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			_source.Connect(Out(StateNode(a)), In(NodeIdOf(AcNodeKind.Exit)));

			Assert.That(a.transitions.Length, Is.EqualTo(1));
			Assert.That(a.transitions[0].isExit, Is.True);
		}

		[Test]
		public void AnyStateToStateBecomesAnAnyStateTransition()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			_source.Connect(Out(NodeIdOf(AcNodeKind.Any)), In(StateNode(a)));

			Assert.That(Root.anyStateTransitions.Length, Is.EqualTo(1));
			Assert.That(Root.anyStateTransitions[0].destinationState, Is.EqualTo(a));
			Assert.That(a.transitions.Length, Is.EqualTo(0), "State 側に遷移を作ってしまっている");
		}

		[Test]
		public void EntryToStateBecomesAnEntryTransition()
		{
			Root.AddState("Default");
			AnimatorState b = Root.AddState("B");
			_source.Refresh();

			_source.Connect(Out(NodeIdOf(AcNodeKind.Entry)), In(StateNode(b)));

			Assert.That(Root.entryTransitions.Length, Is.EqualTo(1));
			Assert.That(Root.entryTransitions[0].destinationState, Is.EqualTo(b));
		}

		[Test]
		public void SubStateMachineToStateBecomesAStateMachineTransition()
		{
			AnimatorStateMachine sub = Root.AddStateMachine("Sub");
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			_source.Connect(Out(MachineNode(sub)), In(StateNode(a)));

			AnimatorTransition[] transitions = Root.GetStateMachineTransitions(sub);
			Assert.That(transitions.Length, Is.EqualTo(1));
			Assert.That(transitions[0].destinationState, Is.EqualTo(a));
		}

		[Test]
		public void StateToSubStateMachineTargetsTheMachine()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorStateMachine sub = Root.AddStateMachine("Sub");
			_source.Refresh();

			_source.Connect(Out(StateNode(a)), In(MachineNode(sub)));

			Assert.That(a.transitions.Length, Is.EqualTo(1));
			Assert.That(a.transitions[0].destinationStateMachine, Is.EqualTo(sub));
		}

		[Test]
		public void AStateMayTransitionToItself()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			Assert.That(_source.CanConnect(Out(StateNode(a)), In(StateNode(a))), Is.True);
		}

		[Test]
		public void ConnectionsTheStockAnimatorForbidsAreRejected()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			string entry = NodeIdOf(AcNodeKind.Entry);
			string exit = NodeIdOf(AcNodeKind.Exit);
			string any = NodeIdOf(AcNodeKind.Any);

			// Entry / Any は Exit へ引けない。
			Assert.That(_source.CanConnect(Out(entry), In(exit)), Is.False, "Entry → Exit");
			Assert.That(_source.CanConnect(Out(any), In(exit)), Is.False, "Any → Exit");

			// Exit からは出られず、Entry / Any へは入れない。
			Assert.That(_source.CanConnect(Out(exit), In(StateNode(a))), Is.False, "Exit → State");
			Assert.That(_source.CanConnect(Out(StateNode(a)), In(entry)), Is.False, "State → Entry");
			Assert.That(_source.CanConnect(Out(StateNode(a)), In(any)), Is.False, "State → Any");

			// 向きが逆（in → out）の組み合わせも通さない。
			Assert.That(_source.CanConnect(In(StateNode(a)), Out(StateNode(a))), Is.False, "in → out");
		}

		[Test]
		public void ConnectDoesNothingWhenTheComboIsRejected()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			_source.Connect(Out(NodeIdOf(AcNodeKind.Exit)), In(StateNode(a)));

			Assert.That(Root.anyStateTransitions.Length, Is.EqualTo(0));
			Assert.That(Root.entryTransitions.Length, Is.EqualTo(0));
			Assert.That(a.transitions.Length, Is.EqualTo(0));
		}

		#endregion

		#region Ports

		[Test]
		public void PortsMatchWhatEachNodeKindCanDo()
		{
			Root.AddState("A");
			Root.AddStateMachine("Sub");
			_source.Refresh();

			Assert.That(PortIds(AcNodeKind.State), Is.EquivalentTo(new[] { "in", "out" }));
			Assert.That(PortIds(AcNodeKind.StateMachine), Is.EquivalentTo(new[] { "in", "out" }));
			Assert.That(PortIds(AcNodeKind.Entry), Is.EquivalentTo(new[] { "out" }));
			Assert.That(PortIds(AcNodeKind.Any), Is.EquivalentTo(new[] { "out" }));
			Assert.That(PortIds(AcNodeKind.Exit), Is.EquivalentTo(new[] { "in" }));
		}

		private string[] PortIds(AcNodeKind kind)
		{
			AcGraphNode node = _source.Nodes.Cast<AcGraphNode>().First(n => n.Ref.Kind == kind);
			return node.Ports.Select(p => p.Id).ToArray();
		}

		[Test]
		public void EdgesStayAnchoredToNodeEdgesNotPorts()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			a.AddTransition(b);
			a.AddTransition(b);
			_source.Refresh();

			// ポートに寄せると同じ2ノード間の複数遷移が1本に重なって見える。
			// 端点を null のままにして FXCEdgeLayer の平行エッジオフセットに任せる。
			foreach (IFXCGraphEdge edge in _source.Edges)
			{
				Assert.That(edge.FromPortId, Is.Null);
				Assert.That(edge.ToPortId, Is.Null);
			}
		}

		#endregion

		#region Move / delete

		[Test]
		public void MovingNodesWritesThroughToTheController()
		{
			AnimatorState a = Root.AddState("A", new Vector3(100f, 100f, 0f));
			AnimatorStateMachine sub = Root.AddStateMachine("Sub", new Vector3(200f, 200f, 0f));
			Root.entryPosition = new Vector3(0f, 0f, 0f);
			_source.Refresh();

			_source.MoveNodes(
				new[] { StateNode(a), MachineNode(sub), NodeIdOf(AcNodeKind.Entry) },
				new Vector2(20f, 40f));

			Assert.That((Vector2)Root.states.First(c => c.state == a).position,
				Is.EqualTo(new Vector2(120f, 140f)));
			Assert.That((Vector2)Root.stateMachines.First(c => c.stateMachine == sub).position,
				Is.EqualTo(new Vector2(220f, 240f)));
			Assert.That((Vector2)Root.entryPosition, Is.EqualTo(new Vector2(20f, 40f)));
		}

		[Test]
		public void OneDragIsOneUndoStepAcrossAllSelectedNodes()
		{
			AnimatorState a = Root.AddState("A", new Vector3(100f, 100f, 0f));
			AnimatorState b = Root.AddState("B", new Vector3(300f, 100f, 0f));
			_source.Refresh();

			_source.MoveNodes(new[] { StateNode(a), StateNode(b) }, new Vector2(20f, 0f));
			Undo.PerformUndo();

			Assert.That((Vector2)Root.states.First(c => c.state == a).position,
				Is.EqualTo(new Vector2(100f, 100f)));
			Assert.That((Vector2)Root.states.First(c => c.state == b).position,
				Is.EqualTo(new Vector2(300f, 100f)), "Undo 1回で両方戻っていない");
		}

		[Test]
		public void DeletingNodesIgnoresTheSpecialOnes()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			_source.DeleteNodes(new[] { StateNode(a), NodeIdOf(AcNodeKind.Entry), NodeIdOf(AcNodeKind.Exit) });

			Assert.That(Root.states.Length, Is.EqualTo(0));
			// Entry / Exit は消えないし、消そうとして例外にもならない。
			_source.Refresh();
			Assert.That(_source.Nodes.Cast<AcGraphNode>().Any(n => n.Ref.Kind == AcNodeKind.Entry), Is.True);
		}

		[Test]
		public void DeletingAnEdgeRemovesTheTransition()
		{
			AnimatorState a = Root.AddState("A");
			AnimatorState b = Root.AddState("B");
			AnimatorStateTransition transition = a.AddTransition(b);
			_source.Refresh();

			_source.DeleteEdges(new[] { "t:" + transition.GetInstanceID() });

			Assert.That(a.transitions.Length, Is.EqualTo(0));
		}

		[Test]
		public void DeletingTheImplicitEntryEdgeDoesNothing()
		{
			AnimatorState a = Root.AddState("A");
			_source.Refresh();

			// Entry → 既定ステートの線は Unity 側に遷移オブジェクトが無い。
			string implicitEdge = _source.Edges.First().Id;
			Assert.That(implicitEdge, Does.StartWith("default:"));

			_source.DeleteEdges(new[] { implicitEdge });

			Assert.That(Root.defaultState, Is.EqualTo(a), "暗黙の線を消して既定ステートを壊している");
		}

		#endregion

		#region Refresh on edit

		[Test]
		public void TheGraphRebuildsItselfAfterAnEdit()
		{
			int changed = 0;
			_source.Changed += () => changed++;

			using (AcEdit e = AcEdit.Begin(_controller, "Add State"))
			{
				e.AddState(Root, "A", Vector2.zero);
			}

			Assert.That(changed, Is.GreaterThan(0), "AcEdit.AfterEdit を受けて作り直していない");
			Assert.That(_source.Nodes.Cast<AcGraphNode>().Any(n => n.Title == "A"), Is.True);
		}

		[Test]
		public void EditsToOtherControllersAreIgnored()
		{
			var other = AnimatorController.CreateAnimatorControllerAtPath(TempFolder + "/Other.controller");
			int changed = 0;
			_source.Changed += () => changed++;

			using (AcEdit e = AcEdit.Begin(other, "Add State"))
			{
				e.AddState(other.layers[0].stateMachine, "Elsewhere", Vector2.zero);
			}

			Assert.That(changed, Is.EqualTo(0), "別 Controller の編集で作り直している");
		}

		#endregion
	}
}
