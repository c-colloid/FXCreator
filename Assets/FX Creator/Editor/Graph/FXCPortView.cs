using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// ノードの接続点。<see cref="FXCNodeView"/> の子として、ノード左右の縁に
	/// <b>明示的な座標で</b>置く（レイアウトエンジンの計算結果を待たずに
	/// アンカー位置が確定するので、エッジ層が初回フレームからずれない）。
	/// </summary>
	public sealed class FXCPortView : VisualElement
	{
		public const float Diameter = 9f;

		public string PortId { get; private set; }
		public FXCPortDirection Direction { get; private set; }

		/// <summary>ノードローカル座標でのポート中心。エッジの端点になる。</summary>
		public Vector2 LocalAnchor { get; private set; }

		private readonly FXCNodeView _node;
		private Color _baseColor;
		private bool _highlighted;

		public FXCPortView(FXCNodeView node, IFXCGraphPort port)
		{
			_node = node;
			PortId = port.Id;
			Direction = port.Direction;
			_baseColor = port.Color;

			tooltip = string.IsNullOrEmpty(port.Name) ? null : port.Name;

			style.position = Position.Absolute;
			style.width = Diameter;
			style.height = Diameter;
			float r = Diameter * 0.5f;
			style.borderTopLeftRadius = r;
			style.borderTopRightRadius = r;
			style.borderBottomLeftRadius = r;
			style.borderBottomRightRadius = r;
			style.borderLeftWidth = 1f;
			style.borderRightWidth = 1f;
			style.borderTopWidth = 1f;
			style.borderBottomWidth = 1f;
			SetBorderColor(new Color(0.08f, 0.08f, 0.08f, 1f));
			ApplyColor();

			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
		}

		/// <summary>ノードローカル座標での中心を指定して置く。</summary>
		public void PlaceAt(Vector2 localAnchor)
		{
			LocalAnchor = localAnchor;
			style.left = localAnchor.x - Diameter * 0.5f;
			style.top = localAnchor.y - Diameter * 0.5f;
		}

		/// <summary>接続候補としてのハイライト。</summary>
		public bool Highlighted
		{
			get => _highlighted;
			set
			{
				if (_highlighted == value)
				{
					return;
				}
				_highlighted = value;
				ApplyColor();
			}
		}

		private void ApplyColor()
		{
			if (_highlighted)
			{
				style.backgroundColor = new Color(0.30f, 0.70f, 1f, 1f);
				style.scale = new Scale(new Vector2(1.4f, 1.4f));
				style.opacity = 1f;
				return;
			}

			// ポートは<b>遷移を引くための取っ手</b>であって、線が実際に刺さる場所ではない
			// （線はノードの縁のうち相手に近い側から出る。§3.6）。
			// はっきり描くと「ここに繋がっている」と読めてしまうので、
			// 普段は控えめにして、掴もうとしたときだけ出す。
			style.backgroundColor = _baseColor;
			style.scale = new Scale(Vector2.one);
			style.opacity = IdleOpacity;
		}

		/// <summary>掴んでいないときの濃さ。接続点と見間違えない程度に落とす。</summary>
		private const float IdleOpacity = 0.35f;

		private void SetBorderColor(Color c)
		{
			style.borderLeftColor = c;
			style.borderRightColor = c;
			style.borderTopColor = c;
			style.borderBottomColor = c;
		}

		#region Connection drag

		private int _dragPointerId = -1;

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (evt.button != (int)MouseButton.LeftMouse || evt.altKey)
			{
				return;
			}
			if (!_node.Owner.BeginConnect(_node, this))
			{
				// 接続できない状況でも、ノードのドラッグに落ちないよう食い止める。
				evt.StopPropagation();
				return;
			}

			_dragPointerId = evt.pointerId;
			this.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
			{
				return;
			}
			Vector2 inGraphView = this.ChangeCoordinatesTo(_node.Owner, evt.localPosition);
			_node.Owner.UpdateConnect(inGraphView);
			evt.StopPropagation();
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (_dragPointerId < 0 || evt.pointerId != _dragPointerId)
			{
				return;
			}
			Vector2 inGraphView = this.ChangeCoordinatesTo(_node.Owner, evt.localPosition);
			int id = _dragPointerId;
			_dragPointerId = -1;
			_node.Owner.EndConnect(inGraphView);
			if (this.HasPointerCapture(id))
			{
				this.ReleasePointer(id);
			}
			evt.StopPropagation();
		}

		private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
		{
			if (_dragPointerId >= 0 && evt.pointerId == _dragPointerId)
			{
				_dragPointerId = -1;
				_node.Owner.CancelConnect();
			}
		}

		#endregion
	}
}
