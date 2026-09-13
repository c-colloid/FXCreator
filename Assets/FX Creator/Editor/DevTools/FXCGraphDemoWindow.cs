using System;
using System.Collections.Generic;
using System.Text;
using colloid.FXCreator.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.DevTools
{
	/// <summary>
	/// <see cref="FXCGraphView"/> をインメモリのダミーデータで動かす確認用ウィンドウ。
	/// Phase 1 のゲート（100ノード / 300エッジで 16ms 以下）の確認と、
	/// 操作の目視確認に使う。Phase 2 で本物の Animator エディタウィンドウが
	/// できたら削除する。
	///
	/// 操作:
	///   ホイール           カーソル基準ズーム
	///   中ドラッグ / Alt+左 パン
	///   左クリック          ノード / エッジ選択（Ctrl・Shift で追加選択）
	///   左ドラッグ(空白)    矩形選択
	///   ノードをドラッグ    移動（20 グリッドにスナップ、複数選択はまとめて移動）
	/// </summary>
	public class FXCGraphDemoWindow : EditorWindow
	{
		private FXCGraphView _graph;
		private DemoGraphSource _source;
		private Label _readout;

		private const int GateFrameBudgetMs = 16;
		private const int MeasureFrames = 240;

		private static readonly Vector2Int[] SweepSizes =
		{
			new Vector2Int(20, 40),
			new Vector2Int(100, 300),
			new Vector2Int(300, 900)
		};

		private bool _sweeping;
		private int _sweepStage;
		private readonly List<string> _sweepRows = new List<string>();

		[MenuItem("Tools/FXCreator/Debug/Graph View Demo", priority = 1931)]
		public static void ShowWindow()
		{
			FXCGraphDemoWindow wnd = GetWindow<FXCGraphDemoWindow>();
			wnd.titleContent = new GUIContent("Graph View Demo");
			wnd.minSize = new Vector2(700, 480);
		}

		public void CreateGUI()
		{
			VisualElement root = rootVisualElement;

			var bar = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexShrink = 0,
					flexWrap = Wrap.Wrap,
					paddingTop = 2,
					paddingBottom = 2
				}
			};
			root.Add(bar);

			_source = DemoGraphSource.Create(100, 300);

			_graph = new FXCGraphView();
			_graph.ViewportChanged += UpdateReadout;
			_graph.Selection.Changed += UpdateReadout;

			bar.Add(new Button(() => Regenerate(20, 40)) { text = "20 / 40" });
			bar.Add(new Button(() => Regenerate(100, 300)) { text = "100 / 300 (gate)" });
			bar.Add(new Button(() => Regenerate(300, 900)) { text = "300 / 900" });
			bar.Add(new Button(() => _graph.FrameAll()) { text = "Frame All" });
			bar.Add(new Button(() => _graph.ResetViewport()) { text = "Reset" });
			bar.Add(new Button(StartSweep) { text = "Run sweep" });

			_readout = new Label("-")
			{
				style = { marginLeft = 10, unityTextAlign = TextAnchor.MiddleLeft, flexGrow = 1 }
			};
			bar.Add(_readout);

			root.Add(_graph);
			_graph.SetSource(_source);

			// レイアウト確定後に全体を収める。
			_graph.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
			UpdateReadout();
		}

		private void OnFirstLayout(GeometryChangedEvent evt)
		{
			_graph.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
			_graph.FrameAll();
		}

		private void OnEnable()
		{
			EditorApplication.update += OnEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			if (_graph != null)
			{
				_graph.EdgeLayer.Instrument = false;
			}
		}

		/// <summary>
		/// 3段のサイズを自動で一巡し、結果を Console に表で出す。
		/// 計測中は毎フレーム再描画させて最悪ケース（パン相当）を測る。
		/// </summary>
		private void StartSweep()
		{
			_sweepRows.Clear();
			_sweeping = true;
			_sweepStage = 0;
			EnterSweepStage();
		}

		private void EnterSweepStage()
		{
			Vector2Int size = SweepSizes[_sweepStage];
			Regenerate(size.x, size.y);
			_graph.EdgeLayer.Instrument = true;
			_graph.EdgeLayer.ResetStats();
		}

		private void OnEditorUpdate()
		{
			if (!_sweeping || _graph == null)
			{
				return;
			}

			FXCEdgeLayer layer = _graph.EdgeLayer;
			// パンし続けている状況を模して毎フレーム描き直す。
			// MarkDirtyRepaint だけではウィンドウが非フォーカスのとき描画が走らないので、
			// Repaint() も呼んで確実に1フレーム進める。
			layer.MarkDirtyRepaint();
			Repaint();

			if (layer.FrameCount < layer.WarmupFrames + MeasureFrames)
			{
				return;
			}

			_sweepRows.Add(string.Format(
				"  {0,4} / {1,4} | {2,8:0.00} | {3,9:0.00} | {4,6:0} | {5}",
				_source.Nodes.Count,
				_source.Edges.Count,
				layer.AvgFrameMs,
				layer.AvgGenerateMs,
				layer.AvgFrameMs > 0.001d ? 1000d / layer.AvgFrameMs : 0d,
				layer.AvgFrameMs <= GateFrameBudgetMs ? "PASS" : "FAIL"));

			_sweepStage++;
			if (_sweepStage < SweepSizes.Length)
			{
				EnterSweepStage();
				return;
			}

			_sweeping = false;
			layer.Instrument = false;

			var sb = new StringBuilder();
			sb.AppendLine("[FXCreator] Phase 1 gate -- real classes (FXCGraphView / FXCNodeView / FXCEdgeLayer)");
			sb.AppendLine("  nodes / edges | frame ms | painter ms |    fps | gate <= " + GateFrameBudgetMs + " ms");
			foreach (string row in _sweepRows)
			{
				sb.AppendLine(row);
			}
			Debug.Log(sb.ToString());
		}

		private void Regenerate(int nodes, int edges)
		{
			_source = DemoGraphSource.Create(nodes, edges);
			_graph.SetSource(_source);
			_graph.FrameAll();
			UpdateReadout();
		}

		private void UpdateReadout()
		{
			if (_readout == null || _graph == null)
			{
				return;
			}

			FXCGraphViewport vp = _graph.Viewport;
			_readout.text = string.Format(
				"nodes {0} / edges {1}   zoom {2:0.000}   selected {3} node(s) {4} edge(s)",
				_source.Nodes.Count,
				_source.Edges.Count,
				vp.Zoom,
				_graph.Selection.Nodes.Count,
				_graph.Selection.Edges.Count);
		}

		#region Demo model

		private sealed class DemoPort : IFXCGraphPort
		{
			public string Id { get; set; }
			public string Name { get; set; }
			public FXCPortDirection Direction { get; set; }
			public Color Color { get; set; }
		}

		private sealed class DemoNode : IFXCGraphNode
		{
			public string Id { get; set; }
			public string Title { get; set; }
			public string Subtitle { get; set; }
			public Rect GraphRect { get; set; }
			public Color AccentColor { get; set; }
			public IReadOnlyList<IFXCGraphPort> Ports { get; set; }
		}

		private sealed class DemoEdge : IFXCGraphEdge
		{
			public string Id { get; set; }
			public string FromNodeId { get; set; }
			public string FromPortId { get; set; }
			public string ToNodeId { get; set; }
			public string ToPortId { get; set; }
			public Color Color { get; set; }
		}

		private sealed class DemoGraphSource : IFXCGraphSource
		{
			private readonly List<DemoNode> _nodes = new List<DemoNode>();
			private readonly List<IFXCGraphEdge> _edges = new List<IFXCGraphEdge>();
			private readonly Dictionary<string, DemoNode> _byId = new Dictionary<string, DemoNode>();

			public IReadOnlyList<IFXCGraphNode> Nodes { get; private set; }
			public IReadOnlyList<IFXCGraphEdge> Edges => _edges;

			public event Action Changed;

			public bool CanMoveNodes => true;
			public bool CanEdit => true;

			private int _nextEdgeId = 100000;

			public bool CanConnect(FXCPortRef from, FXCPortRef to)
			{
				if (!from.IsValid || !to.IsValid || from.NodeId == to.NodeId)
				{
					return false;
				}
				// out -> in のみ。
				if (!from.PortId.StartsWith("out", StringComparison.Ordinal)
					|| !to.PortId.StartsWith("in", StringComparison.Ordinal))
				{
					return false;
				}
				// 同じポート対の重複は作らない。
				for (int i = 0; i < _edges.Count; i++)
				{
					IFXCGraphEdge e = _edges[i];
					if (e.FromNodeId == from.NodeId && e.FromPortId == from.PortId
						&& e.ToNodeId == to.NodeId && e.ToPortId == to.PortId)
					{
						return false;
					}
				}
				return true;
			}

			public void Connect(FXCPortRef from, FXCPortRef to)
			{
				_edges.Add(new DemoEdge
				{
					Id = "edge" + _nextEdgeId++,
					FromNodeId = from.NodeId,
					FromPortId = from.PortId,
					ToNodeId = to.NodeId,
					ToPortId = to.PortId,
					Color = new Color(0.45f, 0.80f, 0.50f, 1f)
				});
				RaiseChanged();
			}

			public void DeleteNodes(IReadOnlyList<string> nodeIds)
			{
				var drop = new HashSet<string>(nodeIds);
				_nodes.RemoveAll(n => drop.Contains(n.Id));
				foreach (string id in drop)
				{
					_byId.Remove(id);
				}
				_edges.RemoveAll(e => drop.Contains(e.FromNodeId) || drop.Contains(e.ToNodeId));
				RaiseChanged();
			}

			public void DeleteEdges(IReadOnlyList<string> edgeIds)
			{
				var drop = new HashSet<string>(edgeIds);
				_edges.RemoveAll(e => drop.Contains(e.Id));
				RaiseChanged();
			}

			public void PopulateContextMenu(GenericMenu menu, FXCGraphContext context)
			{
				if (context.NodeId != null)
				{
					string id = context.NodeId;
					menu.AddItem(new GUIContent("Delete Node"), false, () => DeleteNodes(new[] { id }));
				}
				else if (context.EdgeId != null)
				{
					string id = context.EdgeId;
					menu.AddItem(new GUIContent("Delete Edge"), false, () => DeleteEdges(new[] { id }));
				}
				else
				{
					Vector2 at = context.GraphPosition;
					menu.AddItem(new GUIContent("Add State Here"), false, () => AddNode(at));
				}
			}

			private void AddNode(Vector2 graphPosition)
			{
				var node = new DemoNode
				{
					Id = "node" + _nextEdgeId++,
					Title = "New State",
					Subtitle = null,
					GraphRect = new Rect(graphPosition, new Vector2(160f, 60f)),
					AccentColor = new Color(0.55f, 0.55f, 0.6f),
					Ports = MakePorts()
				};
				_nodes.Add(node);
				_byId.Add(node.Id, node);
				RaiseChanged();
			}

			private static IReadOnlyList<IFXCGraphPort> MakePorts()
			{
				return new IFXCGraphPort[]
				{
					new DemoPort
					{
						Id = "in",
						Name = "In",
						Direction = FXCPortDirection.Input,
						Color = new Color(0.35f, 0.55f, 0.9f)
					},
					new DemoPort
					{
						Id = "out",
						Name = "Out",
						Direction = FXCPortDirection.Output,
						Color = new Color(0.9f, 0.5f, 0.4f)
					}
				};
			}

			public void MoveNodes(IReadOnlyList<string> nodeIds, Vector2 graphDelta)
			{
				for (int i = 0; i < nodeIds.Count; i++)
				{
					DemoNode node;
					if (_byId.TryGetValue(nodeIds[i], out node))
					{
						Rect r = node.GraphRect;
						r.position += graphDelta;
						node.GraphRect = r;
					}
				}
				// 本実装（Phase 2 の AcGraphSource）は Changed を出してビューを作り直させるが、
				// ここではドラッグ後のビュー位置が既に正しいので通知を省く。
			}

			public static DemoGraphSource Create(int nodeCount, int edgeCount)
			{
				var source = new DemoGraphSource();
				var rng = new System.Random(12345);

				int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(nodeCount)));
				const float nodeW = 160f;
				const float nodeH = 60f;
				const float stepX = 260f;
				const float stepY = 100f;

				var accents = new[]
				{
					new Color(0.35f, 0.55f, 0.85f),
					new Color(0.45f, 0.75f, 0.45f),
					new Color(0.85f, 0.65f, 0.35f),
					new Color(0.75f, 0.45f, 0.75f)
				};

				for (int i = 0; i < nodeCount; i++)
				{
					var node = new DemoNode
					{
						Id = "node" + i,
						Title = "State " + i,
						Subtitle = "clip_" + i + ".anim",
						GraphRect = new Rect((i % columns) * stepX, (i / columns) * stepY, nodeW, nodeH),
						AccentColor = accents[i % accents.Length],
						Ports = MakePorts()
					};
					source._nodes.Add(node);
					source._byId.Add(node.Id, node);
				}
				source.Nodes = source._nodes;

				if (nodeCount >= 2)
				{
					for (int i = 0; i < edgeCount; i++)
					{
						int a = rng.Next(nodeCount);
						int b = rng.Next(nodeCount);
						if (a == b)
						{
							b = (b + 1) % nodeCount;
						}
						source._edges.Add(new DemoEdge
						{
							Id = "edge" + i,
							FromNodeId = "node" + a,
							FromPortId = "out",
							ToNodeId = "node" + b,
							ToPortId = "in",
							Color = new Color(0.62f, 0.64f, 0.72f, 1f)
						});
					}
				}

				return source;
			}

			public void RaiseChanged()
			{
				Changed?.Invoke();
			}
		}

		#endregion
	}
}
