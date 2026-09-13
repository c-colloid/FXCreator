using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// ノード1個のビュー。<see cref="FXCGraphView.ContentLayer"/> の子として
	/// <b>グラフ座標</b>を <c>style.left/top</c> に入れる（パン/ズームは親の transform 担当）。
	///
	/// スパイク §3.3.1 で <c>style.left/top</c> と <c>transform.position</c> に
	/// 有意差がないことを確認したので、二段構えにせず style を単一の真実として使う。
	///
	/// 構築（コンストラクタ）と内容の反映（<see cref="Bind"/>）を分けてある。
	/// 派生クラスが自分の要素を作り終えてから Bind が呼ばれるので、
	/// 「基底のコンストラクタから仮想メソッド経由で未初期化のフィールドを触る」
	/// という事故が起きない。生成は <see cref="FXCGraphView.NodeViewFactory"/> 経由。
	/// </summary>
	public class FXCNodeView : VisualElement
	{
		/// <summary>ドラッグ時のスナップ幅（グラフ単位）。グリッドの細線と揃えている。</summary>
		public const float GridSnap = FXCGridBackground.MinorSpacing;

		/// <summary>このノードを載せているグラフビュー。</summary>
		public FXCGraphView Owner => _owner;

		public string NodeId { get; private set; }

		/// <summary>グラフ空間の位置。ドラッグ中はここが即時に更新される。</summary>
		public Vector2 GraphPosition { get; private set; }

		/// <summary>グラフ空間のサイズ。</summary>
		public Vector2 GraphSize { get; private set; }

		public Rect GraphRect => new Rect(GraphPosition, GraphSize);

		/// <summary>左端のアクセント帯。派生クラスが隠したり作り替えたりしてよい。</summary>
		protected VisualElement Accent => _accent;

		/// <summary>タイトルと副題を載せている縦並びの箱。派生クラスはここに行を足す。</summary>
		protected VisualElement Body => _body;

		protected Label TitleLabel => _title;

		protected Label SubtitleLabel => _subtitle;

		private readonly FXCGraphView _owner;
		private readonly List<FXCPortView> _ports = new List<FXCPortView>();
		private readonly VisualElement _accent;
		private readonly VisualElement _body;
		private readonly Label _title;
		private readonly Label _subtitle;

		private bool _selected;
		private bool _culled;
		private int _dragPointerId = -1;
		private Vector2 _dragStartLocalInGraphView;

		public FXCNodeView(FXCGraphView owner)
		{
			_owner = owner;

			style.position = Position.Absolute;
			style.overflow = Overflow.Hidden;
			style.flexDirection = FlexDirection.Row;
			SetBorderRadius(4f);
			SetBorderWidth(1f);

			_accent = new VisualElement
			{
				pickingMode = PickingMode.Ignore,
				style = { width = 4f, flexShrink = 0 }
			};
			Add(_accent);

			_body = new VisualElement
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					flexGrow = 1,
					paddingLeft = 6f,
					paddingRight = 4f,
					paddingTop = 1f,
					overflow = Overflow.Hidden
				}
			};
			Add(_body);

			_title = new Label { pickingMode = PickingMode.Ignore };
			_title.style.overflow = Overflow.Hidden;
			// ノードは Controller の座標に合わせた高さしか無いので、行を詰める。
			_title.style.fontSize = 10f;
			_title.style.whiteSpace = WhiteSpace.NoWrap;
			_body.Add(_title);

			_subtitle = new Label { pickingMode = PickingMode.Ignore };
			_subtitle.style.fontSize = 8f;
			_subtitle.style.overflow = Overflow.Hidden;
			_body.Add(_subtitle);

			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
		}

		/// <summary>
		/// モデルの内容をビューに流し込む（再構築せず使い回すため）。
		/// 派生クラスは base を呼んでから自分の行を更新する。
		/// </summary>
		public virtual void Bind(IFXCGraphNode node)
		{
			NodeId = node.Id;
			_title.text = node.Title ?? string.Empty;

			string sub = node.Subtitle;
			bool hasSub = !string.IsNullOrEmpty(sub);
			_subtitle.text = hasSub ? sub : string.Empty;
			_subtitle.style.display = hasSub ? DisplayStyle.Flex : DisplayStyle.None;

			_accent.style.backgroundColor = node.AccentColor;

			RebuildPorts(node.Ports);
			SetGraphRect(node.GraphRect);
			ApplySelectionStyle();
		}

		public void SetGraphRect(Rect graphRect)
		{
			GraphPosition = graphRect.position;
			GraphSize = graphRect.size;
			style.left = graphRect.x;
			style.top = graphRect.y;
			style.width = graphRect.width;
			style.height = graphRect.height;
			PlacePorts();
		}

		#region Ports

		public IReadOnlyList<FXCPortView> Ports => _ports;

		private void RebuildPorts(IReadOnlyList<IFXCGraphPort> ports)
		{
			for (int i = 0; i < _ports.Count; i++)
			{
				_ports[i].RemoveFromHierarchy();
			}
			_ports.Clear();

			if (ports == null)
			{
				return;
			}

			for (int i = 0; i < ports.Count; i++)
			{
				IFXCGraphPort port = ports[i];
				if (port == null || string.IsNullOrEmpty(port.Id))
				{
					continue;
				}
				var view = new FXCPortView(this, port);
				_ports.Add(view);
				Add(view);
			}
		}

		/// <summary>
		/// ポートをノードの左右の縁に等間隔で置く。レイアウトエンジンに頼らず
		/// 明示座標で置くので、アンカーが初回フレームから確定している。
		/// </summary>
		private void PlacePorts()
		{
			int inputs = 0;
			int outputs = 0;
			for (int i = 0; i < _ports.Count; i++)
			{
				if (_ports[i].Direction == FXCPortDirection.Input)
				{
					inputs++;
				}
				else
				{
					outputs++;
				}
			}

			int seenIn = 0;
			int seenOut = 0;
			for (int i = 0; i < _ports.Count; i++)
			{
				FXCPortView port = _ports[i];
				bool isInput = port.Direction == FXCPortDirection.Input;
				int index = isInput ? seenIn++ : seenOut++;
				int count = isInput ? inputs : outputs;
				float y = GraphSize.y * (index + 1) / (count + 1);
				port.PlaceAt(new Vector2(isInput ? 0f : GraphSize.x, y));
			}
		}

		/// <summary>
		/// ポートのアンカーをグラフ座標で返す。ポートが無い／見つからない場合は false
		/// （呼び出し側はノードの縁にフォールバックする）。
		/// </summary>
		public bool TryGetPortAnchor(string portId, out Vector2 graphAnchor)
		{
			graphAnchor = Vector2.zero;
			if (string.IsNullOrEmpty(portId))
			{
				return false;
			}
			for (int i = 0; i < _ports.Count; i++)
			{
				if (string.Equals(_ports[i].PortId, portId, System.StringComparison.Ordinal))
				{
					graphAnchor = GraphPosition + _ports[i].LocalAnchor;
					return true;
				}
			}
			return false;
		}

		public FXCPortView FindPort(string portId)
		{
			if (string.IsNullOrEmpty(portId))
			{
				return null;
			}
			for (int i = 0; i < _ports.Count; i++)
			{
				if (string.Equals(_ports[i].PortId, portId, System.StringComparison.Ordinal))
				{
					return _ports[i];
				}
			}
			return null;
		}

		#endregion

		/// <summary>ビューポート外として描画を止めているか（カリング）。</summary>
		public bool Culled
		{
			get => _culled;
			set
			{
				if (_culled == value)
				{
					return;
				}
				_culled = value;
				style.display = value ? DisplayStyle.None : DisplayStyle.Flex;
			}
		}

		/// <summary>ドラッグ中の位置更新（モデルには書き戻さない）。</summary>
		public void SetGraphPosition(Vector2 graphPosition)
		{
			GraphPosition = graphPosition;
			style.left = graphPosition.x;
			style.top = graphPosition.y;
		}

		public bool Selected
		{
			get => _selected;
			set
			{
				if (_selected == value)
				{
					return;
				}
				_selected = value;
				ApplySelectionStyle();
			}
		}

		public static Vector2 SnapToGrid(Vector2 graphPosition)
		{
			return new Vector2(
				Mathf.Round(graphPosition.x / GridSnap) * GridSnap,
				Mathf.Round(graphPosition.y / GridSnap) * GridSnap);
		}

		#region Style

		protected virtual void ApplySelectionStyle()
		{
			bool pro = EditorGUIUtility.isProSkin;
			style.backgroundColor = pro
				? new Color(0.24f, 0.24f, 0.26f, 1f)
				: new Color(0.85f, 0.85f, 0.87f, 1f);
			_title.style.color = pro ? new Color(0.88f, 0.88f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
			_subtitle.style.color = pro ? new Color(0.6f, 0.6f, 0.65f) : new Color(0.35f, 0.35f, 0.38f);

			Color border = _selected
				? new Color(0.24f, 0.58f, 0.94f, 1f)
				: (pro ? new Color(0.1f, 0.1f, 0.1f, 1f) : new Color(0.45f, 0.45f, 0.48f, 1f));
			SetBorderColor(border);
			SetBorderWidth(_selected ? 2f : 1f);
		}

		protected void SetBorderRadius(float r)
		{
			style.borderTopLeftRadius = r;
			style.borderTopRightRadius = r;
			style.borderBottomLeftRadius = r;
			style.borderBottomRightRadius = r;
		}

		protected void SetBorderWidth(float w)
		{
			style.borderLeftWidth = w;
			style.borderRightWidth = w;
			style.borderTopWidth = w;
			style.borderBottomWidth = w;
		}

		protected void SetBorderColor(Color c)
		{
			style.borderLeftColor = c;
			style.borderRightColor = c;
			style.borderTopColor = c;
			style.borderBottomColor = c;
		}

		#endregion

		#region Drag

		private void OnPointerDown(PointerDownEvent evt)
		{
			// Alt+左はビューのパンに譲る。
			if (evt.button != (int)MouseButton.LeftMouse || evt.altKey)
			{
				return;
			}

			bool additive = evt.ctrlKey || evt.commandKey || evt.shiftKey;
			_owner.OnNodePressed(this, additive);

			// ダブルクリックは「開く」操作。ドラッグには入らない
			// （2打目の押下でノードを掴むと、勢いで数ピクセル動いてしまう）。
			if (evt.clickCount >= 2)
			{
				_owner.OnNodeActivated(this);
				evt.StopPropagation();
				return;
			}

			if (!_owner.BeginNodeDrag(this))
			{
				evt.StopPropagation();
				return;
			}

			_dragPointerId = evt.pointerId;
			_dragStartLocalInGraphView = this.ChangeCoordinatesTo(_owner, evt.localPosition);
			this.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
			{
				return;
			}

			Vector2 nowInGraphView = this.ChangeCoordinatesTo(_owner, evt.localPosition);
			Vector2 viewDelta = nowInGraphView - _dragStartLocalInGraphView;
			_owner.UpdateNodeDrag(viewDelta / _owner.Viewport.Zoom);
			evt.StopPropagation();
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
			{
				return;
			}

			EndDrag(true);
			evt.StopPropagation();
		}

		private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
		{
			if (_dragPointerId >= 0 && evt.pointerId == _dragPointerId)
			{
				// キャプチャを失ったらその時点の位置で確定する（中途半端な状態を残さない）。
				EndDrag(false);
			}
		}

		private void EndDrag(bool releaseCapture)
		{
			int pointerId = _dragPointerId;
			_dragPointerId = -1;
			_owner.EndNodeDrag();

			if (releaseCapture && pointerId >= 0 && this.HasPointerCapture(pointerId))
			{
				this.ReleasePointer(pointerId);
			}
		}

		#endregion
	}
}
