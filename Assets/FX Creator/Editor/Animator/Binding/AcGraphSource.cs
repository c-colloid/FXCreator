using System;
using System.Collections.Generic;
using colloid.FXCreator.Graph;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>
	/// <see cref="AcGraphSource"/> が吐くノード。<see cref="IFXCGraphNode"/> に
	/// Animator 固有の情報（実体への参照、3行目のテキスト）を足しただけの入れ物。
	/// ビュー側（<c>Animator/View/</c>）はこの型にキャストして追加情報を読む。
	/// </summary>
	public sealed class AcGraphNode : IFXCGraphNode
	{
		private static readonly IFXCGraphPort[] NoPorts = new IFXCGraphPort[0];

		public string Id { get; set; }
		public string Title { get; set; }
		public string Subtitle { get; set; }
		public Rect GraphRect { get; set; }
		public Color AccentColor { get; set; }

		/// <summary>Phase 2 はポートを使わない（エッジはノードの縁に繋がる）。Phase 4 で toggle/switch が使う。</summary>
		public IReadOnlyList<IFXCGraphPort> Ports => NoPorts;

		/// <summary>指している Unity 側の実体。</summary>
		public AcNodeRef Ref { get; set; }

		/// <summary>3行目。State なら "Speed 1 · WD Off" のような補足。無ければ null。</summary>
		public string Info { get; set; }

		/// <summary>このステートマシンの既定ステートか。</summary>
		public bool IsDefault { get; set; }
	}

	public sealed class AcGraphEdge : IFXCGraphEdge
	{
		public string Id { get; set; }
		public string FromNodeId { get; set; }
		public string FromPortId { get; set; }
		public string ToNodeId { get; set; }
		public string ToPortId { get; set; }
		public Color Color { get; set; }
	}

	/// <summary>
	/// <see cref="AnimatorStateMachine"/> をグラフ要素へ射影する（Docs/FXCreator-Design.md §4.1）。
	///
	/// 中間データモデルは持たない（D5/D6）。ここが持つのは
	/// 「いまどのレイヤーのどのステートマシンを見ているか」という<b>表示状態</b>だけで、
	/// ノード・エッジは <see cref="Refresh"/> のたびに Controller から作り直す。
	/// 位置も Controller がネイティブに持つ値をそのまま使うので、
	/// 標準 Animator ウィンドウと配置が一致する。
	///
	/// Phase 2 は読み取り専用（<see cref="CanEdit"/> / <see cref="CanMoveNodes"/> が false）。
	/// 書き込みは Phase 3 の <c>AcEdit</c> が入ってから開ける。
	/// </summary>
	public sealed class AcGraphSource : IFXCGraphSource
	{
		#region Layout constants

		/// <summary>
		/// ノードの大きさ（グラフ単位）。Controller の position は左上隅を指すので、
		/// 標準 Animator ウィンドウのノード幅に合わせておくと見た目も揃う。
		/// </summary>
		public static readonly Vector2 StateSize = new Vector2(200f, 54f);

		/// <summary>Any / Entry / Exit / (Up) の大きさ。丸いピル型で描く。</summary>
		public static readonly Vector2 SpecialSize = new Vector2(200f, 40f);

		#endregion

		#region Colors

		private static readonly Color StateAccent = new Color(0.45f, 0.50f, 0.58f);
		private static readonly Color DefaultStateAccent = new Color(0.90f, 0.62f, 0.22f);
		private static readonly Color SubMachineAccent = new Color(0.38f, 0.55f, 0.72f);

		private static readonly Color EntryFill = new Color(0.33f, 0.60f, 0.35f);
		private static readonly Color ExitFill = new Color(0.68f, 0.34f, 0.34f);
		private static readonly Color AnyFill = new Color(0.28f, 0.55f, 0.64f);
		private static readonly Color ParentFill = new Color(0.40f, 0.40f, 0.45f);

		private static readonly Color TransitionEdge = new Color(0.62f, 0.64f, 0.72f, 1f);
		private static readonly Color AnyEdge = new Color(0.45f, 0.68f, 0.78f, 1f);
		private static readonly Color DefaultEdge = new Color(0.55f, 0.78f, 0.55f, 1f);
		private static readonly Color MutedEdge = new Color(0.50f, 0.34f, 0.34f, 0.55f);
		private static readonly Color SoloEdge = new Color(0.90f, 0.78f, 0.30f, 1f);

		#endregion

		private readonly List<IFXCGraphNode> _nodes = new List<IFXCGraphNode>();
		private readonly List<IFXCGraphEdge> _edges = new List<IFXCGraphEdge>();
		private readonly Dictionary<string, AcGraphNode> _nodeById = new Dictionary<string, AcGraphNode>();
		private readonly Dictionary<string, AnimatorTransitionBase> _transitionByEdgeId =
			new Dictionary<string, AnimatorTransitionBase>();

		/// <summary>
		/// 表示中ステートマシンの配下（孫以降も含む）にある State / StateMachine の
		/// インスタンスID → それを代表する<b>直接の子ノード</b>のID。
		/// 遷移先がサブステートマシンの奥にある場合、標準 Animator ウィンドウと同じく
		/// サブステートマシンのノードへ繋ぐために使う。
		/// </summary>
		private readonly Dictionary<int, string> _representativeNodeId = new Dictionary<int, string>();

		/// <summary>レイヤールートから表示中ステートマシンまでのパンくず。[0] が必ずレイヤールート。</summary>
		private readonly List<AnimatorStateMachine> _path = new List<AnimatorStateMachine>();

		private string _entryNodeId;
		private string _exitNodeId;
		private string _anyNodeId;
		private string _parentNodeId;

		public event Action Changed;

		public AnimatorController Controller { get; private set; }

		public int LayerIndex { get; private set; }

		/// <summary>レイヤールート → … → 表示中ステートマシン。パンくず表示に使う。</summary>
		public IReadOnlyList<AnimatorStateMachine> Path => _path;

		/// <summary>いま描いているステートマシン。対象が無ければ null。</summary>
		public AnimatorStateMachine Current => _path.Count > 0 ? _path[_path.Count - 1] : null;

		public IReadOnlyList<IFXCGraphNode> Nodes => _nodes;

		public IReadOnlyList<IFXCGraphEdge> Edges => _edges;

		// Phase 2 は読み取り専用。書き込みは Phase 3（AcEdit）で開ける。
		public bool CanMoveNodes => false;
		public bool CanEdit => false;

		#region Target selection

		public void SetController(AnimatorController controller)
		{
			Controller = controller;
			LayerIndex = 0;
			_path.Clear();
			Refresh();
		}

		/// <summary>レイヤーを切り替える。潜っていたサブステートマシンからは出る。</summary>
		public void SetLayer(int layerIndex)
		{
			LayerIndex = layerIndex;
			_path.Clear();
			Refresh();
		}

		/// <summary>サブステートマシンへ潜る。表示中ステートマシンの直接の子でなければ何もしない。</summary>
		public void EnterStateMachine(AnimatorStateMachine stateMachine)
		{
			if (stateMachine == null || !IsDirectChild(Current, stateMachine))
			{
				return;
			}
			_path.Add(stateMachine);
			Refresh();
		}

		/// <summary>パンくずの <paramref name="depth"/> 番目まで戻る（0 = レイヤールート）。</summary>
		public void GoToDepth(int depth)
		{
			if (depth < 0 || depth >= _path.Count - 1)
			{
				return;
			}
			_path.RemoveRange(depth + 1, _path.Count - depth - 1);
			Refresh();
		}

		/// <summary>親ステートマシンへ1段戻る。</summary>
		public void GoUp()
		{
			GoToDepth(_path.Count - 2);
		}

		#endregion

		#region Lookup

		public bool TryGetNode(string nodeId, out AcGraphNode node)
		{
			node = null;
			return nodeId != null && _nodeById.TryGetValue(nodeId, out node);
		}

		/// <summary>エッジIDから元の遷移を引く（選択時に Inspector へ流すため）。</summary>
		public bool TryGetTransition(string edgeId, out AnimatorTransitionBase transition)
		{
			transition = null;
			return edgeId != null && _transitionByEdgeId.TryGetValue(edgeId, out transition);
		}

		#endregion

		#region Rebuild

		/// <summary>
		/// Controller を読み直してノード・エッジを作り直し、<see cref="Changed"/> を出す。
		/// 差分同期はしない（§4.5）。ビュー側は選択とビューポートを保持したまま作り直す。
		/// </summary>
		public void Refresh()
		{
			_nodes.Clear();
			_edges.Clear();
			_nodeById.Clear();
			_transitionByEdgeId.Clear();
			_representativeNodeId.Clear();
			_entryNodeId = _exitNodeId = _anyNodeId = _parentNodeId = null;

			ValidatePath();

			AnimatorStateMachine sm = Current;
			if (sm != null)
			{
				BuildNodes(sm);
				BuildEdges(sm);
			}

			Changed?.Invoke();
		}

		/// <summary>
		/// パンくずが外部の変更（サブステートマシンの削除など）で切れていないか確かめ、
		/// 切れていたらそこで打ち切る。壊れたパスのまま描こうとして例外を出さないため。
		/// </summary>
		private void ValidatePath()
		{
			AnimatorStateMachine root = ResolveLayerStateMachine();
			if (root == null)
			{
				_path.Clear();
				return;
			}

			if (_path.Count == 0 || _path[0] != root)
			{
				_path.Clear();
				_path.Add(root);
				return;
			}

			for (int i = 1; i < _path.Count; i++)
			{
				if (_path[i] == null || !IsDirectChild(_path[i - 1], _path[i]))
				{
					_path.RemoveRange(i, _path.Count - i);
					return;
				}
			}
		}

		private AnimatorStateMachine ResolveLayerStateMachine()
		{
			if (Controller == null)
			{
				return null;
			}
			AnimatorControllerLayer[] layers = Controller.layers;
			if (layers == null || LayerIndex < 0 || LayerIndex >= layers.Length)
			{
				return null;
			}
			return layers[LayerIndex].stateMachine;
		}

		private static bool IsDirectChild(AnimatorStateMachine parent, AnimatorStateMachine child)
		{
			if (parent == null || child == null)
			{
				return false;
			}
			ChildAnimatorStateMachine[] children = parent.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				if (children[i].stateMachine == child)
				{
					return true;
				}
			}
			return false;
		}

		#endregion

		#region Nodes

		private void BuildNodes(AnimatorStateMachine sm)
		{
			// 特殊ノード。位置はステートマシンのフィールドがそのまま持っている（§4.1）。
			_entryNodeId = AddSpecial(AcNodeKind.Entry, sm, "Entry", sm.entryPosition, EntryFill);
			_exitNodeId = AddSpecial(AcNodeKind.Exit, sm, "Exit", sm.exitPosition, ExitFill);
			_anyNodeId = AddSpecial(AcNodeKind.Any, sm, "Any State", sm.anyStatePosition, AnyFill);

			if (_path.Count > 1)
			{
				AnimatorStateMachine parent = _path[_path.Count - 2];
				_parentNodeId = AddSpecial(
					AcNodeKind.Parent, sm, "(Up) " + parent.name, sm.parentStateMachinePosition, ParentFill);
			}

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}

				bool isDefault = state == sm.defaultState;
				var node = new AcGraphNode
				{
					Id = AcNodeRef.MakeId(AcNodeKind.State, state),
					Ref = new AcNodeRef(AcNodeKind.State, state),
					Title = state.name,
					Subtitle = DescribeMotion(state.motion),
					Info = DescribeState(state),
					IsDefault = isDefault,
					AccentColor = isDefault ? DefaultStateAccent : StateAccent,
					GraphRect = new Rect(states[i].position.x, states[i].position.y, StateSize.x, StateSize.y)
				};
				AddNode(node);
				_representativeNodeId[state.GetInstanceID()] = node.Id;
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}

				var node = new AcGraphNode
				{
					Id = AcNodeRef.MakeId(AcNodeKind.StateMachine, child),
					Ref = new AcNodeRef(AcNodeKind.StateMachine, child),
					Title = child.name,
					Subtitle = DescribeStateMachine(child),
					Info = null,
					AccentColor = SubMachineAccent,
					GraphRect = new Rect(children[i].position.x, children[i].position.y, StateSize.x, StateSize.y)
				};
				AddNode(node);
				// 配下の要素は、すべてこのサブステートマシンのノードが代表する。
				MapSubtree(child, node.Id);
			}
		}

		private string AddSpecial(AcNodeKind kind, AnimatorStateMachine owner, string title, Vector3 position, Color fill)
		{
			var node = new AcGraphNode
			{
				Id = AcNodeRef.MakeId(kind, owner),
				Ref = new AcNodeRef(kind, owner),
				Title = title,
				Subtitle = null,
				Info = null,
				AccentColor = fill,
				GraphRect = new Rect(position.x, position.y, SpecialSize.x, SpecialSize.y)
			};
			AddNode(node);
			return node.Id;
		}

		private void AddNode(AcGraphNode node)
		{
			if (node.Id == null || _nodeById.ContainsKey(node.Id))
			{
				return;
			}
			_nodes.Add(node);
			_nodeById.Add(node.Id, node);
		}

		/// <summary><paramref name="sm"/> 配下のすべての State / StateMachine を <paramref name="nodeId"/> に紐づける。</summary>
		private void MapSubtree(AnimatorStateMachine sm, string nodeId)
		{
			_representativeNodeId[sm.GetInstanceID()] = nodeId;

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				if (states[i].state != null)
				{
					_representativeNodeId[states[i].state.GetInstanceID()] = nodeId;
				}
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				if (children[i].stateMachine != null)
				{
					MapSubtree(children[i].stateMachine, nodeId);
				}
			}
		}

		#endregion

		#region Edges

		private void BuildEdges(AnimatorStateMachine sm)
		{
			// Entry → 既定ステート。Unity 側に遷移オブジェクトが無い暗黙の線なので、
			// ステートマシンのIDから作った固有IDを振る。
			if (sm.defaultState != null)
			{
				string destination = ResolveRepresentative(sm.defaultState);
				if (destination != null)
				{
					AddEdge("default:" + sm.GetInstanceID(), _entryNodeId, destination, DefaultEdge, null);
				}
			}

			AnimatorTransition[] entryTransitions = sm.entryTransitions;
			for (int i = 0; i < entryTransitions.Length; i++)
			{
				AddTransitionEdge(_entryNodeId, entryTransitions[i], DefaultEdge);
			}

			AnimatorStateTransition[] anyTransitions = sm.anyStateTransitions;
			for (int i = 0; i < anyTransitions.Length; i++)
			{
				AddTransitionEdge(_anyNodeId, anyTransitions[i], AnyEdge);
			}

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}
				string from = AcNodeRef.MakeId(AcNodeKind.State, state);
				AnimatorStateTransition[] transitions = state.transitions;
				for (int t = 0; t < transitions.Length; t++)
				{
					AddTransitionEdge(from, transitions[t], TransitionEdge);
				}
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				string from = AcNodeRef.MakeId(AcNodeKind.StateMachine, child);
				// サブステートマシンから<b>外</b>への遷移は、親（= 表示中）の側が持っている。
				AnimatorTransition[] transitions = sm.GetStateMachineTransitions(child);
				for (int t = 0; t < transitions.Length; t++)
				{
					AddTransitionEdge(from, transitions[t], TransitionEdge);
				}
			}
		}

		private void AddTransitionEdge(string fromNodeId, AnimatorTransitionBase transition, Color baseColor)
		{
			if (fromNodeId == null || transition == null)
			{
				return;
			}

			string toNodeId = ResolveDestination(transition);
			if (toNodeId == null)
			{
				return;
			}

			Color color = baseColor;
			if (transition.mute)
			{
				color = MutedEdge;
			}
			else if (transition.solo)
			{
				color = SoloEdge;
			}

			AddEdge("t:" + transition.GetInstanceID(), fromNodeId, toNodeId, color, transition);
		}

		private void AddEdge(string id, string fromNodeId, string toNodeId, Color color, AnimatorTransitionBase transition)
		{
			if (id == null || fromNodeId == null || toNodeId == null)
			{
				return;
			}
			_edges.Add(new AcGraphEdge
			{
				Id = id,
				FromNodeId = fromNodeId,
				FromPortId = null,
				ToNodeId = toNodeId,
				ToPortId = null,
				Color = color
			});
			if (transition != null)
			{
				_transitionByEdgeId[id] = transition;
			}
		}

		/// <summary>
		/// 遷移先を、いま描いているノードのどれかに落とす。
		/// 奥のサブステートマシン内が行き先ならそのサブステートマシンのノードへ、
		/// このステートマシンの外が行き先なら「(Up)」ノードへ寄せる
		/// （標準 Animator ウィンドウと同じ見え方）。
		/// </summary>
		private string ResolveDestination(AnimatorTransitionBase transition)
		{
			if (transition.isExit)
			{
				return _exitNodeId;
			}
			if (transition.destinationState != null)
			{
				return ResolveRepresentative(transition.destinationState) ?? _parentNodeId;
			}
			if (transition.destinationStateMachine != null)
			{
				return ResolveRepresentative(transition.destinationStateMachine) ?? _parentNodeId;
			}
			// 行き先の無い遷移（編集途中の Controller に稀にある）は描かない。
			return null;
		}

		private string ResolveRepresentative(UnityEngine.Object target)
		{
			string id;
			return target != null && _representativeNodeId.TryGetValue(target.GetInstanceID(), out id) ? id : null;
		}

		#endregion

		#region Descriptions

		/// <summary>ノードの2行目。Motion 名（BlendTree は種別も出す）。</summary>
		public static string DescribeMotion(Motion motion)
		{
			if (motion == null)
			{
				return "None";
			}
			var tree = motion as BlendTree;
			if (tree != null)
			{
				return tree.name + "  (" + tree.blendType + ")";
			}
			return motion.name;
		}

		/// <summary>ノードの3行目。v0.1 の表示範囲は Speed と WriteDefaults（§9 Phase 2）。</summary>
		public static string DescribeState(AnimatorState state)
		{
			if (state == null)
			{
				return null;
			}
			return string.Format(
				"Speed {0:0.##}  ·  WD {1}",
				state.speed,
				state.writeDefaultValues ? "On" : "Off");
		}

		private static string DescribeStateMachine(AnimatorStateMachine sm)
		{
			int states = sm.states.Length;
			int machines = sm.stateMachines.Length;
			if (machines > 0)
			{
				return states + " states, " + machines + " sub";
			}
			return states + (states == 1 ? " state" : " states");
		}

		#endregion

		#region IFXCGraphSource (read-only in Phase 2)

		public void MoveNodes(IReadOnlyList<string> nodeIds, Vector2 graphDelta)
		{
			// Phase 3（AcEdit）で位置の書き戻しを入れるまでは動かせない。
		}

		public bool CanConnect(FXCPortRef from, FXCPortRef to) => false;

		public void Connect(FXCPortRef from, FXCPortRef to)
		{
		}

		public void DeleteNodes(IReadOnlyList<string> nodeIds)
		{
		}

		public void DeleteEdges(IReadOnlyList<string> edgeIds)
		{
		}

		public void PopulateContextMenu(GenericMenu menu, FXCGraphContext context)
		{
		}

		#endregion
	}
}
