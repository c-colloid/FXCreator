using System.Collections.Generic;
using System.Linq;
using colloid.FXCreator.AnimatorGraph;
using colloid.FXCreator.Graph;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// Phase 2（読み専用グラフ）の射影の回帰テスト。
	/// ゲートは「標準 Animator ウィンドウと同じ構造・同じ配置で表示される」なので、
	/// ここでは<b>構造</b>（どのノードがどのノードへ繋がるか）と
	/// <b>配置</b>（Controller の position をそのまま使っているか）を固定する。
	///
	/// Controller はアセットにせずメモリ上で組む。Phase 2 は読むだけなので
	/// サブアセットの永続化は関係なく、テストがディスクを汚さずに済む。
	/// </summary>
	public class AcGraphSourceTests
	{
		private readonly List<AnimatorController> _controllers = new List<AnimatorController>();

		/// <summary>
		/// アセットにしていないので、ステートマシン・ステート・遷移は
		/// Controller を消しても道連れにならない。明示的に全部畳む。
		/// </summary>
		[TearDown]
		public void TearDown()
		{
			for (int i = 0; i < _sources.Count; i++)
			{
				_sources[i].Dispose();
			}
			_sources.Clear();

			var garbage = new List<Object>();
			for (int i = 0; i < _controllers.Count; i++)
			{
				AnimatorController controller = _controllers[i];
				if (controller == null)
				{
					continue;
				}
				AnimatorControllerLayer[] layers = controller.layers;
				for (int l = 0; l < layers.Length; l++)
				{
					Collect(layers[l].stateMachine, garbage);
				}
				garbage.Add(controller);
			}
			_controllers.Clear();

			for (int i = 0; i < garbage.Count; i++)
			{
				if (garbage[i] != null)
				{
					Object.DestroyImmediate(garbage[i]);
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

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				if (states[i].state == null)
				{
					continue;
				}
				sink.AddRange(states[i].state.transitions);
				sink.Add(states[i].state);
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				if (children[i].stateMachine == null)
				{
					continue;
				}
				sink.AddRange(sm.GetStateMachineTransitions(children[i].stateMachine));
				Collect(children[i].stateMachine, sink);
			}

			sink.Add(sm);
		}

		private AnimatorController NewController()
		{
			var controller = new AnimatorController();
			controller.AddLayer("Base Layer");
			_controllers.Add(controller);
			return controller;
		}

		private static AnimatorStateMachine Root(AnimatorController controller)
		{
			return controller.layers[0].stateMachine;
		}

		private readonly List<AcGraphSource> _sources = new List<AcGraphSource>();

		/// <summary>
		/// <see cref="AcGraphSource"/> は静的な <c>AcEdit.AfterEdit</c> を購読するので、
		/// テストごとに必ず畳む（放っておくとドメインリロードまで積み上がる）。
		/// </summary>
		private AcGraphSource Open(AnimatorController controller)
		{
			var source = new AcGraphSource();
			_sources.Add(source);
			source.SetController(controller);
			return source;
		}

		private static AcGraphNode Node(AcGraphSource source, string id)
		{
			AcGraphNode node;
			Assert.That(source.TryGetNode(id, out node), Is.True, "ノードが見つかりません: " + id);
			return node;
		}

		private static AcGraphNode FindByKind(AcGraphSource source, AcNodeKind kind)
		{
			return source.Nodes.Cast<AcGraphNode>().FirstOrDefault(n => n.Ref.Kind == kind);
		}

		private static bool HasEdge(AcGraphSource source, string fromId, string toId)
		{
			return source.Edges.Any(e => e.FromNodeId == fromId && e.ToNodeId == toId);
		}

		[Test]
		public void SpecialNodesExistAndParentIsAbsentAtLayerRoot()
		{
			AnimatorController controller = NewController();
			AcGraphSource source = Open(controller);

			Assert.That(FindByKind(source, AcNodeKind.Entry), Is.Not.Null);
			Assert.That(FindByKind(source, AcNodeKind.Exit), Is.Not.Null);
			Assert.That(FindByKind(source, AcNodeKind.Any), Is.Not.Null);

			// レイヤールートには戻り先が無いので (Up) は出ない。
			Assert.That(FindByKind(source, AcNodeKind.Parent), Is.Null);
		}

		[Test]
		public void SpecialNodesUseTheStateMachinePositions()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			sm.entryPosition = new Vector3(10f, 20f, 0f);
			sm.exitPosition = new Vector3(30f, 40f, 0f);
			sm.anyStatePosition = new Vector3(50f, 60f, 0f);

			AcGraphSource source = Open(controller);

			Assert.That(FindByKind(source, AcNodeKind.Entry).GraphRect.position, Is.EqualTo(new Vector2(10f, 20f)));
			Assert.That(FindByKind(source, AcNodeKind.Exit).GraphRect.position, Is.EqualTo(new Vector2(30f, 40f)));
			Assert.That(FindByKind(source, AcNodeKind.Any).GraphRect.position, Is.EqualTo(new Vector2(50f, 60f)));
		}

		[Test]
		public void StatesKeepTheirControllerPositions()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState state = sm.AddState("Idle", new Vector3(123f, 456f, 0f));

			AcGraphSource source = Open(controller);
			AcGraphNode node = Node(source, AcNodeRef.MakeId(AcNodeKind.State, state));

			// 位置をそのまま使うのが「標準 Animator ウィンドウと同じ配置」の根拠（§4.1）。
			Assert.That(node.GraphRect.position, Is.EqualTo(new Vector2(123f, 456f)));
			Assert.That(node.GraphRect.size, Is.EqualTo(AcGraphSource.StateSize));
			Assert.That(node.Title, Is.EqualTo("Idle"));
		}

		[Test]
		public void StateSubtitleAndInfoDescribeMotionSpeedAndWriteDefaults()
		{
			AnimatorController controller = NewController();
			AnimatorState state = Root(controller).AddState("Wave");
			state.writeDefaultValues = false;
			state.speed = 2f;

			AcGraphSource source = Open(controller);
			AcGraphNode node = Node(source, AcNodeRef.MakeId(AcNodeKind.State, state));

			Assert.That(node.Subtitle, Is.EqualTo("None"), "Motion 未設定は None と出す");
			Assert.That(node.Info, Does.Contain("2"));
			Assert.That(node.Info, Does.Contain("WD Off"));
		}

		[Test]
		public void EntryEdgeGoesToTheDefaultState()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState first = sm.AddState("First");
			sm.AddState("Second");

			AcGraphSource source = Open(controller);

			// 最初に足した State が既定ステートになる。
			Assert.That(sm.defaultState, Is.EqualTo(first));
			Assert.That(
				HasEdge(source, FindByKind(source, AcNodeKind.Entry).Id, AcNodeRef.MakeId(AcNodeKind.State, first)),
				Is.True);
			Assert.That(Node(source, AcNodeRef.MakeId(AcNodeKind.State, first)).IsDefault, Is.True);
		}

		[Test]
		public void StateTransitionBecomesAnEdgeBetweenTheTwoStates()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			AnimatorStateTransition transition = a.AddTransition(b);

			AcGraphSource source = Open(controller);

			string edgeId = "t:" + transition.GetInstanceID();
			IFXCGraphEdge edge = source.Edges.FirstOrDefault(e => e.Id == edgeId);
			Assert.That(edge, Is.Not.Null);
			Assert.That(edge.FromNodeId, Is.EqualTo(AcNodeRef.MakeId(AcNodeKind.State, a)));
			Assert.That(edge.ToNodeId, Is.EqualTo(AcNodeRef.MakeId(AcNodeKind.State, b)));

			AnimatorTransitionBase resolved;
			Assert.That(source.TryGetTransition(edgeId, out resolved), Is.True);
			Assert.That(resolved, Is.EqualTo(transition));
		}

		[Test]
		public void AnyStateTransitionStartsAtTheAnyNode()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState a = sm.AddState("A");
			sm.AddAnyStateTransition(a);

			AcGraphSource source = Open(controller);

			Assert.That(
				HasEdge(source, FindByKind(source, AcNodeKind.Any).Id, AcNodeRef.MakeId(AcNodeKind.State, a)),
				Is.True);
		}

		[Test]
		public void ExitTransitionEndsAtTheExitNode()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState a = sm.AddState("A");
			a.AddExitTransition();

			AcGraphSource source = Open(controller);

			Assert.That(
				HasEdge(source, AcNodeRef.MakeId(AcNodeKind.State, a), FindByKind(source, AcNodeKind.Exit).Id),
				Is.True);
		}

		[Test]
		public void TransitionIntoASubStateMachineTargetsTheSubMachineNode()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState outside = sm.AddState("Outside");
			AnimatorStateMachine sub = sm.AddStateMachine("Sub");
			AnimatorState inside = sub.AddState("Inside");
			outside.AddTransition(inside);

			AcGraphSource source = Open(controller);

			// 奥の State はこの階層には出ない。標準 Animator ウィンドウと同じく
			// サブステートマシンのノードが遷移先を代表する。
			AcGraphNode hidden;
			Assert.That(source.TryGetNode(AcNodeRef.MakeId(AcNodeKind.State, inside), out hidden), Is.False);
			Assert.That(
				HasEdge(
					source,
					AcNodeRef.MakeId(AcNodeKind.State, outside),
					AcNodeRef.MakeId(AcNodeKind.StateMachine, sub)),
				Is.True);
		}

		[Test]
		public void EnteringASubStateMachineShowsItsContentsAndTheParentNode()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorStateMachine sub = sm.AddStateMachine("Sub");
			AnimatorState inside = sub.AddState("Inside");

			AcGraphSource source = Open(controller);
			source.EnterStateMachine(sub);

			Assert.That(source.Current, Is.EqualTo(sub));
			Assert.That(source.Path.Count, Is.EqualTo(2));
			Assert.That(FindByKind(source, AcNodeKind.Parent), Is.Not.Null);

			AcGraphNode node = Node(source, AcNodeRef.MakeId(AcNodeKind.State, inside));
			Assert.That(node.Title, Is.EqualTo("Inside"));

			source.GoUp();
			Assert.That(source.Current, Is.EqualTo(sm));
			Assert.That(FindByKind(source, AcNodeKind.Parent), Is.Null);
		}

		[Test]
		public void EnteringIgnoresStateMachinesThatAreNotDirectChildren()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorStateMachine sub = sm.AddStateMachine("Sub");
			AnimatorStateMachine deep = sub.AddStateMachine("Deep");

			AcGraphSource source = Open(controller);
			source.EnterStateMachine(deep);

			// 一段飛ばしでは潜らない（パンくずが実際の親子関係と食い違わないように）。
			Assert.That(source.Current, Is.EqualTo(sm));
		}

		[Test]
		public void RefreshIsDeterministicAndKeepsNodeIds()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorState a = sm.AddState("A");
			AnimatorState b = sm.AddState("B");
			a.AddTransition(b);

			AcGraphSource source = Open(controller);
			string[] before = source.Nodes.Select(n => n.Id).ToArray();
			string[] beforeEdges = source.Edges.Select(e => e.Id).ToArray();

			source.Refresh();

			// ID がインスタンスIDから決まるので、作り直しても選択が生き残る（§3.1）。
			Assert.That(source.Nodes.Select(n => n.Id).ToArray(), Is.EqualTo(before));
			Assert.That(source.Edges.Select(e => e.Id).ToArray(), Is.EqualTo(beforeEdges));
		}

		[Test]
		public void LayerSwitchShowsTheOtherLayersStateMachine()
		{
			AnimatorController controller = NewController();
			Root(controller).AddState("OnBase");
			controller.AddLayer("Second");
			AnimatorState second = controller.layers[1].stateMachine.AddState("OnSecond");

			AcGraphSource source = Open(controller);
			source.SetLayer(1);

			Assert.That(source.LayerIndex, Is.EqualTo(1));
			Assert.That(Node(source, AcNodeRef.MakeId(AcNodeKind.State, second)).Title, Is.EqualTo("OnSecond"));
			Assert.That(source.Nodes.Count(n => ((AcGraphNode)n).Ref.Kind == AcNodeKind.State), Is.EqualTo(1));
		}

		[Test]
		public void MissingControllerYieldsAnEmptyGraphInsteadOfThrowing()
		{
			AcGraphSource source = Open(null);

			Assert.That(source.Current, Is.Null);
			Assert.That(source.Nodes, Is.Empty);
			Assert.That(source.Edges, Is.Empty);
		}

		[Test]
		public void PathFallsBackToTheLayerRootWhenTheSubStateMachineIsGone()
		{
			AnimatorController controller = NewController();
			AnimatorStateMachine sm = Root(controller);
			AnimatorStateMachine sub = sm.AddStateMachine("Sub");

			AcGraphSource source = Open(controller);
			source.EnterStateMachine(sub);
			Assert.That(source.Current, Is.EqualTo(sub));

			// 外部（標準 Animator ウィンドウなど）で消された状況を再現する。
			sm.RemoveStateMachine(sub);
			source.Refresh();

			Assert.That(source.Current, Is.EqualTo(sm));
			Assert.That(source.Path.Count, Is.EqualTo(1));
		}
	}
}
