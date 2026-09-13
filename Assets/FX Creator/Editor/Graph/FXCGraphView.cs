using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// 自作ノードグラフの土台（Docs/FXCreator-Design.md §3）。
	/// UnityEditor.Experimental.GraphView は使わない。
	///
	/// 要素の重なり順（奥 → 手前）:
	///   1. <see cref="FXCGridBackground"/>  背景グリッド。ビュー全体を覆いビュー座標で描く
	///   2. <see cref="FXCEdgeLayer"/>       エッジ。同じくビュー全体・ビュー座標
	///   3. <see cref="ContentLayer"/>       ノードの親。transform でパン/ズームされる
	///   4. 矩形選択のラバーバンド
	///
	/// ノードは <see cref="ContentLayer"/> の子として <b>グラフ座標</b>を
	/// <c>style.left/top</c> に入れる。パン/ズームは ContentLayer の transform 1回で済むので、
	/// ノード数に比例したコストがかからない。
	/// エッジ層はノードビューのグラフ座標 + ビューポートから自分で座標を出す
	/// （transform 後のレイアウトを読まないので、ドラッグ中も1フレームずれない）。
	/// </summary>
	public class FXCGraphView : VisualElement
	{
		/// <summary>エッジのクリック判定の許容距離（ビューpx）。</summary>
		private const float EdgePickTolerance = 6f;

		/// <summary>この距離を超えて動いたら「ドラッグ」と見なす（クリックと区別する）。</summary>
		private const float DragThreshold = 3f;

		/// <summary>接続ドラッグでポートに吸着する半径（ビューpx）。</summary>
		private const float PortSnapRadius = 18f;

		/// <summary>カリングの余白（グラフ単位）。画面外少し先まで生かしておく。</summary>
		private const float CullMargin = 200f;

		public FXCGraphViewport Viewport { get; } = new FXCGraphViewport();
		public FXCSelection Selection { get; } = new FXCSelection();

		/// <summary>ノードを入れる層。子はグラフ座標で配置する。</summary>
		public VisualElement ContentLayer { get; }

		/// <summary>エッジ層。性能計測（Instrument）を外から回すために公開している。</summary>
		public FXCEdgeLayer EdgeLayer => _edgeLayer;

		/// <summary>パンまたはズームでビューポートが変化したときに発火する。</summary>
		public event Action ViewportChanged;

		private readonly FXCGridBackground _grid;
		private readonly FXCEdgeLayer _edgeLayer;
		private readonly VisualElement _marquee;
		private readonly Dictionary<string, FXCNodeView> _nodeViews = new Dictionary<string, FXCNodeView>();

		private IFXCGraphSource _source;

		// パン
		private bool _panning;
		private Vector2 _panLastLocal;
		private int _panPointerId = -1;

		// 矩形選択
		private bool _marqueeActive;
		private int _marqueePointerId = -1;
		private Vector2 _marqueeStartLocal;
		private bool _marqueeAdditive;

		// ノードドラッグ
		private FXCNodeView _dragPrimary;
		private readonly List<string> _dragIds = new List<string>();
		private readonly List<Vector2> _dragStartPositions = new List<Vector2>();
		private Vector2 _dragApplied;

		// 接続ドラッグ
		private FXCPortRef _connectFrom;
		private FXCPortView _highlightedPort;

		public FXCGraphView()
		{
			style.overflow = Overflow.Hidden;
			style.flexGrow = 1;
			focusable = true;

			_grid = new FXCGridBackground(Viewport);
			Add(_grid);

			_edgeLayer = new FXCEdgeLayer(this);
			Add(_edgeLayer);

			ContentLayer = new VisualElement
			{
				// 自身はピッキング対象外。子（ノード）は通常どおり拾われる。
				pickingMode = PickingMode.Ignore,
				style =
				{
					position = Position.Absolute,
					left = 0,
					top = 0,
					right = 0,
					bottom = 0
				}
			};
			// transform の原点を左上にしないと view = graph * Zoom + Offset と一致しない。
			ContentLayer.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
			Add(ContentLayer);

			_marquee = new VisualElement
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					position = Position.Absolute,
					display = DisplayStyle.None,
					backgroundColor = new Color(0.24f, 0.58f, 0.94f, 0.12f),
					borderLeftWidth = 1f,
					borderRightWidth = 1f,
					borderTopWidth = 1f,
					borderBottomWidth = 1f,
					borderLeftColor = new Color(0.24f, 0.58f, 0.94f, 0.9f),
					borderRightColor = new Color(0.24f, 0.58f, 0.94f, 0.9f),
					borderTopColor = new Color(0.24f, 0.58f, 0.94f, 0.9f),
					borderBottomColor = new Color(0.24f, 0.58f, 0.94f, 0.9f)
				}
			};
			Add(_marquee);

			Selection.Changed += OnSelectionChanged;

			RegisterCallback<WheelEvent>(OnWheel);
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
		RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
			RegisterCallback<ContextClickEvent>(OnContextClick);
			RegisterCallback<KeyDownEvent>(OnKeyDown);

			ApplyViewport();
		}

		#region Source binding

		public void SetSource(IFXCGraphSource source)
		{
			if (_source != null)
			{
				_source.Changed -= Rebuild;
			}
			_source = source;
			if (_source != null)
			{
				_source.Changed += Rebuild;
			}
			Rebuild();
		}

		/// <summary>
		/// モデルからビューを作り直す。差分同期はせず全再構築する
		/// （§4.5 の方針。ノード数は通常数十で、選択とビューポートは保持する）。
		/// </summary>
		public void Rebuild()
		{
			if (_source == null)
			{
				foreach (FXCNodeView view in _nodeViews.Values)
				{
					view.RemoveFromHierarchy();
				}
				_nodeViews.Clear();
				_edgeLayer.SetEdges(Array.Empty<IFXCGraphEdge>());
				return;
			}

			IReadOnlyList<IFXCGraphNode> nodes = _source.Nodes;

			// 既存のビューは使い回し、消えたものだけ落とす。
			var live = new HashSet<string>();
			for (int i = 0; i < nodes.Count; i++)
			{
				IFXCGraphNode node = nodes[i];
				if (node == null || string.IsNullOrEmpty(node.Id))
				{
					continue;
				}
				live.Add(node.Id);

				FXCNodeView view;
				if (_nodeViews.TryGetValue(node.Id, out view))
				{
					view.Bind(node);
				}
				else
				{
					view = new FXCNodeView(this, node);
					_nodeViews.Add(node.Id, view);
					ContentLayer.Add(view);
				}
			}

			var stale = new List<string>();
			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				if (!live.Contains(kv.Key))
				{
					stale.Add(kv.Key);
				}
			}
			for (int i = 0; i < stale.Count; i++)
			{
				_nodeViews[stale[i]].RemoveFromHierarchy();
				_nodeViews.Remove(stale[i]);
			}

			_edgeLayer.SetEdges(_source.Edges);

			var liveEdges = new HashSet<string>();
			IReadOnlyList<IFXCGraphEdge> edges = _source.Edges;
			for (int i = 0; i < edges.Count; i++)
			{
				if (edges[i] != null)
				{
					liveEdges.Add(edges[i].Id);
				}
			}
			Selection.Prune(live, liveEdges);

			ApplySelectionToViews();
			ApplyCulling();
		}

		public FXCNodeView FindNodeView(string nodeId)
		{
			if (string.IsNullOrEmpty(nodeId))
			{
				return null;
			}
			FXCNodeView view;
			return _nodeViews.TryGetValue(nodeId, out view) ? view : null;
		}

		/// <summary>全ノードを含むグラフ空間の矩形。ノードが無ければ幅0の矩形。</summary>
		public Rect GetContentGraphBounds()
		{
			bool any = false;
			float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;
			foreach (FXCNodeView view in _nodeViews.Values)
			{
				Rect r = view.GraphRect;
				if (!any)
				{
					minX = r.xMin;
					minY = r.yMin;
					maxX = r.xMax;
					maxY = r.yMax;
					any = true;
					continue;
				}
				minX = Mathf.Min(minX, r.xMin);
				minY = Mathf.Min(minY, r.yMin);
				maxX = Mathf.Max(maxX, r.xMax);
				maxY = Mathf.Max(maxY, r.yMax);
			}
			return any ? new Rect(minX, minY, maxX - minX, maxY - minY) : new Rect();
		}

		#endregion

		#region Viewport

		/// <summary>ビューポートの状態を ContentLayer の transform と各層の再描画に反映する。</summary>
		public void ApplyViewport()
		{
			ContentLayer.transform.scale = new Vector3(Viewport.Zoom, Viewport.Zoom, 1f);
			ContentLayer.transform.position = new Vector3(Viewport.Offset.x, Viewport.Offset.y, 0f);
			_grid.MarkDirtyRepaint();
			_edgeLayer.MarkDirtyRepaint();
			ApplyCulling();
			ViewportChanged?.Invoke();
		}

		public void ResetViewport()
		{
			Viewport.Reset();
			ApplyViewport();
		}

		/// <summary>グラフ空間の矩形を画面に収める。</summary>
		public void FrameGraphRect(Rect graphRect)
		{
			Viewport.FrameGraphRect(graphRect, contentRect);
			ApplyViewport();
		}

		/// <summary>全ノードを画面に収める（Frame All）。</summary>
		public void FrameAll()
		{
			Rect bounds = GetContentGraphBounds();
			if (bounds.width <= 0f || bounds.height <= 0f)
			{
				return;
			}
			FrameGraphRect(bounds);
		}

		private void OnGeometryChanged(GeometryChangedEvent evt)
		{
			_grid.MarkDirtyRepaint();
			_edgeLayer.MarkDirtyRepaint();
			ApplyCulling();
		}

		/// <summary>
		/// 画面外のノードビューを <c>display:none</c> にする（§3.1 のビューポートカリング）。
		/// 状態が変わったときだけ style を触る（毎フレーム触るとレイアウトが走る）。
		/// GraphRect は残るので、エッジの端点計算と Frame All には影響しない。
		/// </summary>
		private void ApplyCulling()
		{
			Rect view = contentRect;
			if (view.width < 1f || view.height < 1f)
			{
				return;
			}

			Rect visible = Viewport.ViewToGraph(view);
			visible = new Rect(
				visible.x - CullMargin,
				visible.y - CullMargin,
				visible.width + CullMargin * 2f,
				visible.height + CullMargin * 2f);

			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				kv.Value.Culled = !visible.Overlaps(kv.Value.GraphRect, true);
			}
		}

		private void OnWheel(WheelEvent evt)
		{
			// 上方向のスクロール（delta.y < 0）で拡大。
			Viewport.ZoomByStepsAt(-evt.delta.y, evt.localMousePosition);
			ApplyViewport();
			evt.StopPropagation();
		}

		#endregion

		#region Selection

		private void OnSelectionChanged()
		{
			ApplySelectionToViews();
			_edgeLayer.MarkDirtyRepaint();
		}

		private void ApplySelectionToViews()
		{
			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				kv.Value.Selected = Selection.ContainsNode(kv.Key);
			}
		}

		/// <summary>ノードが押されたときの選択更新（<see cref="FXCNodeView"/> から呼ばれる）。</summary>
		internal void OnNodePressed(FXCNodeView node, bool additive)
		{
			if (additive)
			{
				Selection.ToggleNode(node.NodeId);
				return;
			}
			// 既に選択に含まれていれば選択を維持する（複数選択のドラッグを壊さない）。
			if (!Selection.ContainsNode(node.NodeId))
			{
				Selection.SelectOnlyNode(node.NodeId);
			}
		}

		#endregion

		#region Node drag

		/// <summary>ドラッグ開始。動かせない場合は false。</summary>
		internal bool BeginNodeDrag(FXCNodeView primary)
		{
			if (_source == null || !_source.CanMoveNodes)
			{
				return false;
			}

			_dragPrimary = primary;
			_dragApplied = Vector2.zero;
			_dragIds.Clear();
			_dragStartPositions.Clear();

			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				if (Selection.ContainsNode(kv.Key))
				{
					_dragIds.Add(kv.Key);
					_dragStartPositions.Add(kv.Value.GraphPosition);
				}
			}

			// 選択に入っていないノードを掴んだ場合はそれ1つだけ動かす。
			if (_dragIds.Count == 0)
			{
				_dragIds.Add(primary.NodeId);
				_dragStartPositions.Add(primary.GraphPosition);
			}

			return true;
		}

		/// <summary>
		/// ドラッグ中の更新。ビューだけを動かし、モデルには書き戻さない
		/// （離した時に合計移動量で1回だけ反映する＝Undo が1操作になる）。
		/// </summary>
		internal void UpdateNodeDrag(Vector2 graphDelta)
		{
			if (_dragPrimary == null || _dragIds.Count == 0)
			{
				return;
			}

			// 掴んだノードの着地点をスナップし、その差分を全選択ノードに同じだけ適用する。
			int primaryIndex = _dragIds.IndexOf(_dragPrimary.NodeId);
			Vector2 primaryStart = primaryIndex >= 0
				? _dragStartPositions[primaryIndex]
				: _dragPrimary.GraphPosition;
			Vector2 applied = FXCNodeView.SnapToGrid(primaryStart + graphDelta) - primaryStart;

			if (applied == _dragApplied)
			{
				return;
			}
			_dragApplied = applied;

			for (int i = 0; i < _dragIds.Count; i++)
			{
				FXCNodeView view = FindNodeView(_dragIds[i]);
				if (view != null)
				{
					view.SetGraphPosition(_dragStartPositions[i] + applied);
				}
			}

			_edgeLayer.MarkDirtyRepaint();
		}

		internal void EndNodeDrag()
		{
			if (_dragPrimary == null)
			{
				return;
			}

			if (_dragApplied != Vector2.zero && _source != null && _source.CanMoveNodes)
			{
				_source.MoveNodes(_dragIds.ToArray(), _dragApplied);
			}

			_dragPrimary = null;
			_dragApplied = Vector2.zero;
			_dragIds.Clear();
			_dragStartPositions.Clear();
		}

		#endregion

		#region Connect (port drag)

		/// <summary>ポートからの接続ドラッグを開始する。編集不可なら false。</summary>
		internal bool BeginConnect(FXCNodeView node, FXCPortView port)
		{
			if (_source == null || !_source.CanEdit)
			{
				return false;
			}

			Vector2 anchor;
			if (!node.TryGetPortAnchor(port.PortId, out anchor))
			{
				return false;
			}

			_connectFrom = new FXCPortRef(node.NodeId, port.PortId);
			_edgeLayer.PendingActive = true;
			_edgeLayer.PendingFromGraph = anchor;
			_edgeLayer.PendingToView = Viewport.GraphToView(anchor);
			_edgeLayer.PendingValid = false;
			_edgeLayer.MarkDirtyRepaint();
			return true;
		}

		internal void UpdateConnect(Vector2 viewPoint)
		{
			if (!_edgeLayer.PendingActive)
			{
				return;
			}

			_edgeLayer.PendingToView = viewPoint;

			FXCNodeView targetNode;
			FXCPortView target = FindPortNear(viewPoint, out targetNode);
			bool valid = target != null
				&& _source.CanConnect(_connectFrom, new FXCPortRef(targetNode.NodeId, target.PortId));

			SetHighlightedPort(valid ? target : null);
			_edgeLayer.PendingValid = valid;
			_edgeLayer.MarkDirtyRepaint();
		}

		internal void EndConnect(Vector2 viewPoint)
		{
			if (!_edgeLayer.PendingActive)
			{
				return;
			}

			FXCNodeView targetNode;
			FXCPortView target = FindPortNear(viewPoint, out targetNode);
			if (target != null)
			{
				var to = new FXCPortRef(targetNode.NodeId, target.PortId);
				if (_source.CanConnect(_connectFrom, to))
				{
					_source.Connect(_connectFrom, to);
				}
			}

			CancelConnect();
		}

		internal void CancelConnect()
		{
			SetHighlightedPort(null);
			_connectFrom = default(FXCPortRef);
			_edgeLayer.PendingActive = false;
			_edgeLayer.PendingValid = false;
			_edgeLayer.MarkDirtyRepaint();
		}

		private void SetHighlightedPort(FXCPortView port)
		{
			if (_highlightedPort == port)
			{
				return;
			}
			if (_highlightedPort != null)
			{
				_highlightedPort.Highlighted = false;
			}
			_highlightedPort = port;
			if (_highlightedPort != null)
			{
				_highlightedPort.Highlighted = true;
			}
		}

		/// <summary>
		/// ビュー座標の点に最も近いポートを返す。VisualElement のピッキングに頼らないのは、
		/// 接続ドラッグ中はドラッグ元のポートがポインタをキャプチャしていて、
		/// 他の要素にイベントが届かないため。
		/// </summary>
		private FXCPortView FindPortNear(Vector2 viewPoint, out FXCNodeView owningNode)
		{
			owningNode = null;
			FXCPortView best = null;
			float bestSqr = PortSnapRadius * PortSnapRadius;

			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				FXCNodeView node = kv.Value;
				if (node.Culled)
				{
					continue;
				}
				IReadOnlyList<FXCPortView> ports = node.Ports;
				for (int i = 0; i < ports.Count; i++)
				{
					Vector2 anchorView = Viewport.GraphToView(node.GraphPosition + ports[i].LocalAnchor);
					float sqr = (anchorView - viewPoint).sqrMagnitude;
					if (sqr < bestSqr)
					{
						bestSqr = sqr;
						best = ports[i];
						owningNode = node;
					}
				}
			}

			return best;
		}

		#endregion

		#region Context menu / delete

		private void OnContextClick(ContextClickEvent evt)
		{
			if (_source == null || !_source.CanEdit)
			{
				return;
			}

			Vector2 local = evt.localMousePosition;
			var context = new FXCGraphContext
			{
				GraphPosition = Viewport.ViewToGraph(local),
				NodeId = FindNodeIdAtGraphPoint(Viewport.ViewToGraph(local)),
				EdgeId = null
			};
			if (context.NodeId == null)
			{
				context.EdgeId = _edgeLayer.PickEdge(local, EdgePickTolerance);
			}

			// 右クリックした対象が選択外なら、その対象に選択を移してからメニューを出す
			// （見えている選択とメニューの対象を食い違わせない）。
			if (context.NodeId != null && !Selection.ContainsNode(context.NodeId))
			{
				Selection.SelectOnlyNode(context.NodeId);
			}
			else if (context.EdgeId != null && !Selection.ContainsEdge(context.EdgeId))
			{
				Selection.SelectOnlyEdge(context.EdgeId);
			}

			var menu = new GenericMenu();
			_source.PopulateContextMenu(menu, context);
			if (menu.GetItemCount() > 0)
			{
				menu.ShowAsContext();
			}
			evt.StopPropagation();
		}

		private string FindNodeIdAtGraphPoint(Vector2 graphPoint)
		{
			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				if (kv.Value.GraphRect.Contains(graphPoint))
				{
					return kv.Key;
				}
			}
			return null;
		}

		private void OnKeyDown(KeyDownEvent evt)
		{
			if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
			{
				return;
			}
			if (_source == null || !_source.CanEdit || Selection.IsEmpty)
			{
				return;
			}

			var nodeIds = new List<string>(Selection.Nodes);
			var edgeIds = new List<string>(Selection.Edges);
			Selection.Clear();

			if (edgeIds.Count > 0)
			{
				_source.DeleteEdges(edgeIds);
			}
			if (nodeIds.Count > 0)
			{
				_source.DeleteNodes(nodeIds);
			}
			evt.StopPropagation();
		}

		#endregion

		#region Pointer (pan / marquee / edge pick)

		private static bool IsPanTrigger(PointerDownEvent evt)
		{
			if (evt.button == (int)MouseButton.MiddleMouse)
			{
				return true;
			}
			// Alt + 左ドラッグ（Unity の各ビューと同じ操作）。
			return evt.button == (int)MouseButton.LeftMouse && evt.altKey;
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (IsPanTrigger(evt))
			{
				if (_panning)
				{
					return;
				}
				_panning = true;
				_panPointerId = evt.pointerId;
				_panLastLocal = evt.localPosition;
				this.CapturePointer(evt.pointerId);
				evt.StopPropagation();
				return;
			}

			if (evt.button != (int)MouseButton.LeftMouse || _marqueeActive)
			{
				return;
			}

			// ここに来たのはノードに当たらなかった場合（ノードは自分で StopPropagation する）。
			// まずエッジを拾い、当たらなければ矩形選択を始める。
			Vector2 local = evt.localPosition;
			bool additive = evt.ctrlKey || evt.commandKey || evt.shiftKey;

			string edgeId = _edgeLayer.PickEdge(local, EdgePickTolerance);
			if (edgeId != null)
			{
				if (additive)
				{
					Selection.ToggleEdge(edgeId);
				}
				else
				{
					Selection.SelectOnlyEdge(edgeId);
				}
				evt.StopPropagation();
				return;
			}

			_marqueeActive = true;
			_marqueePointerId = evt.pointerId;
			_marqueeStartLocal = local;
			_marqueeAdditive = additive;
			_marquee.style.display = DisplayStyle.None;
			this.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_panning && evt.pointerId == _panPointerId)
			{
				Vector2 local = evt.localPosition;
				Viewport.PanByView(local - _panLastLocal);
				_panLastLocal = local;
				ApplyViewport();
				evt.StopPropagation();
				return;
			}

			if (_marqueeActive && evt.pointerId == _marqueePointerId)
			{
				Rect band = RectFromCorners(_marqueeStartLocal, evt.localPosition);
				if (band.width < DragThreshold && band.height < DragThreshold)
				{
					evt.StopPropagation();
					return;
				}
				_marquee.style.display = DisplayStyle.Flex;
				_marquee.style.left = band.x;
				_marquee.style.top = band.y;
				_marquee.style.width = band.width;
				_marquee.style.height = band.height;
				evt.StopPropagation();
			}
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (_panning && evt.pointerId == _panPointerId)
			{
				EndPan();
				evt.StopPropagation();
				return;
			}

			if (_marqueeActive && evt.pointerId == _marqueePointerId)
			{
				bool dragged = _marquee.resolvedStyle.display == DisplayStyle.Flex;
				Rect band = RectFromCorners(_marqueeStartLocal, evt.localPosition);
				EndMarquee();

				if (dragged && (band.width >= DragThreshold || band.height >= DragThreshold))
				{
					CommitMarquee(band, _marqueeAdditive);
				}
				else if (!_marqueeAdditive)
				{
					// 空クリックは選択解除。
					Selection.Clear();
				}
				evt.StopPropagation();
			}
		}

		private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
		{
			if (_panning && evt.pointerId == _panPointerId)
			{
				_panning = false;
				_panPointerId = -1;
			}
			if (_marqueeActive && evt.pointerId == _marqueePointerId)
			{
				_marqueeActive = false;
				_marqueePointerId = -1;
				_marquee.style.display = DisplayStyle.None;
			}
		}

		private void CommitMarquee(Rect viewBand, bool additive)
		{
			Rect graphBand = Viewport.ViewToGraph(viewBand);
			var hits = new List<string>();
			foreach (KeyValuePair<string, FXCNodeView> kv in _nodeViews)
			{
				if (graphBand.Overlaps(kv.Value.GraphRect, true))
				{
					hits.Add(kv.Key);
				}
			}
			Selection.SetNodes(hits, additive);
		}

		private void EndPan()
		{
			if (!_panning)
			{
				return;
			}
			int id = _panPointerId;
			_panning = false;
			_panPointerId = -1;
			if (id >= 0 && this.HasPointerCapture(id))
			{
				this.ReleasePointer(id);
			}
		}

		private void EndMarquee()
		{
			int id = _marqueePointerId;
			_marqueeActive = false;
			_marqueePointerId = -1;
			_marquee.style.display = DisplayStyle.None;
			if (id >= 0 && this.HasPointerCapture(id))
			{
				this.ReleasePointer(id);
			}
		}

		private static Rect RectFromCorners(Vector2 a, Vector2 b)
		{
			return new Rect(
				Mathf.Min(a.x, b.x),
				Mathf.Min(a.y, b.y),
				Mathf.Abs(b.x - a.x),
				Mathf.Abs(b.y - a.y));
		}

		#endregion
	}
}
