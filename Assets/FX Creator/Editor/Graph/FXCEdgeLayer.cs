using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// 全エッジを <b>1つの VisualElement</b> が Painter2D でまとめて描く
	/// （Docs/FXCreator-Design.md §3.3）。エッジごとに要素を作らないので、
	/// レイアウト計算も要素数もエッジ数に比例しない。
	///
	/// 座標はビュー空間。端点はノードビューの <b>グラフ座標</b>から
	/// ビューポートで変換して求める（ドラッグ中のノードにも即座に追従する）。
	/// ヒットテストも VisualElement のピッキングに頼らず、この層が線分距離で行う。
	/// </summary>
	public sealed class FXCEdgeLayer : VisualElement
	{
		/// <summary>ベジェのサンプル数（ヒットテスト用）。</summary>
		private const int HitSamples = 12;

		/// <summary>同じノード対に複数のエッジがある場合の、垂直方向のずらし量（ビューpx）。</summary>
		private const float ParallelSpread = 9f;

		/// <summary>ラベルの文字サイズ（ビュー座標）。ズームに合わせて拡大はしない。</summary>
		private const float LabelFontSize = 9f;

		/// <summary>これより縮んだらラベルを出さない。読めない字に描画コストを払わない。</summary>
		private const float MinZoomForLabel = 0.75f;

		/// <summary>
		/// ラベルの下敷き。線の上に直接書くと読めないので敷く。
		/// ライトスキンで暗いままだと、明るい画面に黒い札が並んで目立ちすぎる。
		/// </summary>
		private static Color LabelBackColor
		{
			get
			{
				return EditorGUIUtility.isProSkin
					? new Color(0.16f, 0.16f, 0.18f, 0.92f)
					: new Color(0.95f, 0.95f, 0.96f, 0.92f);
			}
		}

		/// <summary>平行エッジのラベルを曲線に沿ってずらす量（t の差）。</summary>
		private const float LabelStagger = 0.3f;

		/// <summary>ラベルを線からさらに外へ逃がす量。線の間隔よりラベルの方が高いため。</summary>
		private const float LabelLift = 9f;

		/// <summary>線の根元に打つ丸の半径（ズーム1のとき）。</summary>
		private const float RootDotRadius = 3f;

		/// <summary>
		/// 注目していないエッジの不透明度。消さずに落とす。
		///
		/// ライトスキンでは同じ値だと<b>完全に消える</b>。暗い線を明るい背景へ
		/// アルファで薄めるほうが、明るい線を暗い背景へ薄めるより速く沈むため。
		/// </summary>
		private static float DimAlpha
		{
			get { return EditorGUIUtility.isProSkin ? 0.22f : 0.32f; }
		}

		/// <summary>いまポインタが乗っているエッジ。注目の対象を1本に絞る。</summary>
		private string _hoveredEdgeId;

		/// <summary>
		/// ポインタが乗っているエッジを設定する。変わったときだけ描き直す
		/// （PointerMove は毎フレーム来るので、毎回 MarkDirtyRepaint すると無駄）。
		/// </summary>
		public void SetHoveredEdge(string edgeId)
		{
			if (_hoveredEdgeId == edgeId)
			{
				return;
			}
			_hoveredEdgeId = edgeId;
			MarkDirtyRepaint();
		}

		/// <summary>いま「注目しているもの」があるか。無ければ減光しない。</summary>
		private bool HasFocus(FXCSelection selection)
		{
			return _hoveredEdgeId != null
				|| (selection != null && !selection.IsEmpty);
		}

		/// <summary>選択中のノードに繋がっているエッジか。</summary>
		private static bool IsAttachedToSelection(IFXCGraphEdge edge, FXCSelection selection)
		{
			return selection != null
				&& (selection.ContainsNode(edge.FromNodeId) || selection.ContainsNode(edge.ToNodeId));
		}

		/// <summary>ラベルの文字色。線の色をそのまま使うと背景に沈んで読めない。</summary>
		private static Color LabelTextColor
		{
			get
			{
				return EditorGUIUtility.isProSkin
					? new Color(0.86f, 0.86f, 0.90f)
					: new Color(0.13f, 0.13f, 0.16f);
			}
		}

		/// <summary>
		/// 端点が「どのノードのどの辺の何番目か」。
		///
		/// 8本の遷移が同じ Exit に集まると、辺の中央<b>一点</b>に全部刺さって
		/// どれがどれか追えなくなる。辺の上に等間隔で散らすためのスロット番号。
		/// ノードは動くので毎フレーム割り直す。
		/// </summary>
		private readonly List<int> _fromSlot = new List<int>();
		private readonly List<int> _fromSlotCount = new List<int>();
		private readonly List<int> _toSlot = new List<int>();
		private readonly List<int> _toSlotCount = new List<int>();
		private readonly Dictionary<long, List<int>> _slotBuckets = new Dictionary<long, List<int>>();

		/// <summary>ラベルは線をすべて描いてから出す。毎フレーム使い回して確保を避ける。</summary>
		private readonly List<string> _labelText = new List<string>();
		private readonly List<Vector2> _labelPos = new List<Vector2>();
		private readonly List<Color> _labelColor = new List<Color>();

		/// <summary>この倍率をズームに掛けた線幅で描く（縮小時に消えないよう下限を設ける）。</summary>
		private const float MinLineWidth = 1f;

		private readonly FXCGraphView _owner;

		private readonly List<IFXCGraphEdge> _edges = new List<IFXCGraphEdge>();
		/// <summary>同一ノード対の中での連番と総数（平行エッジをずらすため）。</summary>
		private readonly List<int> _parallelIndex = new List<int>();
		private readonly List<int> _parallelCount = new List<int>();

		public Color SelectedColor { get; set; } = EditorGUIUtility.isProSkin
			? new Color(0.30f, 0.65f, 1f, 1f)
			: new Color(0.06f, 0.36f, 0.82f, 1f);

		/// <summary>接続ドラッグ中のプレビュー線。<see cref="FXCGraphView"/> が設定する。</summary>
		public bool PendingActive { get; set; }

		/// <summary>プレビュー線の始点（グラフ座標＝ドラッグ元のポート）。</summary>
		public Vector2 PendingFromGraph { get; set; }

		/// <summary>プレビュー線の終点（ビュー座標＝現在のカーソル位置）。</summary>
		public Vector2 PendingToView { get; set; }

		/// <summary>ドロップ先が有効かどうかで色を変える。</summary>
		public bool PendingValid { get; set; }

		#region Instrumentation

		// §3.3 の性能ゲート（100ノード/300エッジで16ms以下）を測り続けるためのフック。
		// 既定オフで、オフの間はストップウォッチを触らない。Phase 5 でノード内プレビューを
		// 足すと再びここが効くので、使い捨てにせず残してある。

		/// <summary>true の間だけ計測する。</summary>
		public bool Instrument { get; set; }

		/// <summary>生成開始 → 次の生成開始 の平均（生成コストを含む真のフレーム周期、ms）。</summary>
		public double AvgFrameMs { get; private set; }

		/// <summary>generateVisualContent 内で費やした平均時間（ms）。</summary>
		public double AvgGenerateMs { get; private set; }

		/// <summary>計測開始からの描画回数。ウォームアップ判定に使う。</summary>
		public int FrameCount { get; private set; }

		/// <summary>この回数までは平均に入れない（初期レイアウトのコストを混ぜないため）。</summary>
		public int WarmupFrames { get; set; } = 60;

		private readonly Stopwatch _generateWatch = new Stopwatch();
		private readonly Stopwatch _periodWatch = new Stopwatch();

		public void ResetStats()
		{
			AvgFrameMs = 0d;
			AvgGenerateMs = 0d;
			FrameCount = 0;
			_periodWatch.Reset();
		}

		private static double Blend(double current, double sample)
		{
			return current <= 0d ? sample : current * 0.9d + sample * 0.1d;
		}

		#endregion

		public FXCEdgeLayer(FXCGraphView owner)
		{
			_owner = owner;
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;
			style.left = 0;
			style.top = 0;
			style.right = 0;
			style.bottom = 0;
			generateVisualContent += OnGenerateVisualContent;
		}

		public IReadOnlyList<IFXCGraphEdge> Edges => _edges;

		/// <summary>モデルのエッジ一覧を取り込み、平行エッジの連番を付け直す。</summary>
		public void SetEdges(IReadOnlyList<IFXCGraphEdge> edges)
		{
			_edges.Clear();
			_parallelIndex.Clear();
			_parallelCount.Clear();

			var perPair = new Dictionary<string, int>();
			for (int i = 0; i < edges.Count; i++)
			{
				IFXCGraphEdge e = edges[i];
				if (e == null)
				{
					continue;
				}
				_edges.Add(e);

				string key = MakePairKey(e.FromNodeId, e.ToNodeId);
				int seen;
				perPair.TryGetValue(key, out seen);
				perPair[key] = seen + 1;
				_parallelIndex.Add(seen);
				_parallelCount.Add(0); // 総数は後で埋める
			}

			for (int i = 0; i < _edges.Count; i++)
			{
				string key = MakePairKey(_edges[i].FromNodeId, _edges[i].ToNodeId);
				_parallelCount[i] = perPair[key];
			}

			MarkDirtyRepaint();
		}

		/// <summary>
		/// ビュー座標の点に最も近いエッジのIDを返す。見つからなければ null。
		/// クリック時にしか呼ばれないので、素直にベジェをサンプリングして距離を測る。
		/// </summary>
		public string PickEdge(Vector2 viewPoint, float tolerance = 6f)
		{
			string best = null;
			float bestSqr = tolerance * tolerance;

			for (int i = 0; i < _edges.Count; i++)
			{
				Vector2 p0, c0, c1, p1;
				if (!TryGetCurve(i, out p0, out c0, out c1, out p1))
				{
					continue;
				}

				Vector2 prev = p0;
				for (int s = 1; s <= HitSamples; s++)
				{
					Vector2 cur = Bezier(p0, c0, c1, p1, s / (float)HitSamples);
					float sqr = SqrDistanceToSegment(viewPoint, prev, cur);
					if (sqr < bestSqr)
					{
						bestSqr = sqr;
						best = _edges[i].Id;
					}
					prev = cur;
				}
			}

			return best;
		}

		#region Geometry

		/// <summary>
		/// エッジ i の制御点をビュー座標で求める。
		/// 端点はノードの右端中央 → 次のノードの左端中央。
		/// </summary>
		private bool TryGetCurve(int i, out Vector2 p0, out Vector2 c0, out Vector2 c1, out Vector2 p1)
		{
			p0 = c0 = c1 = p1 = Vector2.zero;

			IFXCGraphEdge e = _edges[i];
			FXCNodeView from = _owner.FindNodeView(e.FromNodeId);
			FXCNodeView to = _owner.FindNodeView(e.ToNodeId);
			if (from == null || to == null)
			{
				return false;
			}

			FXCGraphViewport vp = _owner.Viewport;

			// ポートがあればその位置、無ければノードの縁にフォールバックする。
			// 縁を使う場合は<b>相手のいる側</b>の辺から出す（AnchorOnRect）。
			Vector2 fromNormal = new Vector2(1f, 0f);
			Vector2 toNormal = new Vector2(-1f, 0f);

			Vector2 fromGraph;
			bool fromHasPort = from.TryGetPortAnchor(e.FromPortId, out fromGraph);
			if (!fromHasPort)
			{
				// 同じ辺に集まるエッジは辺の上に等間隔で散らす。
				// 中央一点に集中すると、8本が Exit に入るような形で追えなくなる。
				int side = SideOf(from.GraphRect, to.GraphRect.center, out fromNormal);
				fromGraph = SlotOnSide(
					from.GraphRect, side, _fromSlot[i], _fromSlotCount[i]);
			}

			Vector2 toGraph;
			bool toHasPort = to.TryGetPortAnchor(e.ToPortId, out toGraph);
			if (!toHasPort)
			{
				int side = SideOf(to.GraphRect, from.GraphRect.center, out toNormal);
				toGraph = SlotOnSide(to.GraphRect, side, _toSlot[i], _toSlotCount[i]);
			}

			p0 = vp.GraphToView(fromGraph);
			p1 = vp.GraphToView(toGraph);

			// ポートを使っている端はポート自体が分かれているのでずらさない。
			// ノードの縁に集まる端だけ、平行エッジを重ならないようにずらす。
			// 端点が辺の上で分かれているなら、さらにずらす必要はない。
			// 両端ともスロットが1つのときだけ、平行エッジの重なりを法線方向で避ける。
			int count = _parallelCount[i];
			if (count > 1 && _fromSlotCount[i] <= 1 && _toSlotCount[i] <= 1)
			{
				float spread = (_parallelIndex[i] - (count - 1) * 0.5f) * ParallelSpread;

				// ずらす向きは<b>線分の法線</b>。以前は y 固定だったので、
				// ノードを横に並べたときは重なったままだった。
				Vector2 delta = p1 - p0;
				float length = delta.magnitude;
				Vector2 perpendicular = length > 0.0001f
					? new Vector2(-delta.y / length, delta.x / length)
					: new Vector2(0f, 1f);

				// 法線は向きによって反転するので、そのまま使うと A→B と B→A が
				// <b>同じ側</b >に寄ってまた重なる。ノードIDの順で符号を決めて、
				// 往復のどちらから見ても同じ通路の左右に分かれるようにする。
				if (string.CompareOrdinal(e.FromNodeId, e.ToNodeId) > 0)
				{
					spread = -spread;
				}

				Vector2 offset = perpendicular * spread;
				if (!fromHasPort)
				{
					p0 += offset;
				}
				if (!toHasPort)
				{
					p1 += offset;
				}
			}

			// 制御点は「その辺の外向き」へ出す。x 固定だと、上下の辺から出る
			// エッジが横へ膨らんでノードに食い込む。
			float bend = Mathf.Max(24f, (p1 - p0).magnitude * 0.35f);
			c0 = p0 + fromNormal * bend;
			c1 = p1 + toNormal * bend;
			return true;
		}

		private static Vector2 Bezier(Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1, float t)
		{
			float u = 1f - t;
			float uu = u * u;
			float tt = t * t;
			return uu * u * p0
				+ 3f * uu * t * c0
				+ 3f * u * tt * c1
				+ tt * t * p1;
		}

		private static float SqrDistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float lenSqr = ab.sqrMagnitude;
			if (lenSqr < 1e-6f)
			{
				return (p - a).sqrMagnitude;
			}
			float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
			return (p - (a + ab * t)).sqrMagnitude;
		}

		#endregion

		private void OnGenerateVisualContent(MeshGenerationContext mgc)
		{
			if (Instrument)
			{
				if (_periodWatch.IsRunning)
				{
					double sample = _periodWatch.Elapsed.TotalMilliseconds;
					FrameCount++;
					if (FrameCount > WarmupFrames)
					{
						AvgFrameMs = Blend(AvgFrameMs, sample);
					}
				}
				_periodWatch.Restart();
				_generateWatch.Restart();
			}

			Rect view = contentRect;
			if (view.width < 1f || view.height < 1f)
			{
				return;
			}

			if (_edges.Count == 0 && !PendingActive)
			{
				return;
			}

			Painter2D painter = mgc.painter2D;

			_labelText.Clear();
			_labelPos.Clear();
			_labelColor.Clear();
			// FontAsset は<b>毎フレーム引き直す</b>（キャッシュ禁止。FXCTextFont の説明を参照）。
			FontAsset labelFont = FXCTextFont.Resolve(this);
			painter.lineCap = LineCap.Round;

			float zoom = _owner.Viewport.Zoom;
			// ノードは動くのでスロットは毎フレーム割り直す。
			AssignEndpointSlots();
			DrawPending(painter, zoom);
			float lineWidth = Mathf.Max(MinLineWidth, 2f * zoom);
			float arrow = Mathf.Max(5f, 9f * zoom);
			FXCSelection selection = _owner.Selection;

			for (int i = 0; i < _edges.Count; i++)
			{
				Vector2 p0, c0, c1, p1;
				if (!TryGetCurve(i, out p0, out c0, out c1, out p1))
				{
					continue;
				}

				// 画面外のエッジは描かない（両端が同じ側に外れている場合）。
				if (IsFullyOutside(view, p0, c0, c1, p1))
				{
					continue;
				}

				bool selected = selection != null && selection.ContainsEdge(_edges[i].Id);
				bool hovered = _hoveredEdgeId != null && _hoveredEdgeId == _edges[i].Id;
				// 注目しているものが何も無ければ全部そのまま。あるときだけ絞る。
				bool focused = hovered || selected || IsAttachedToSelection(_edges[i], selection);
				bool dim = HasFocus(selection) && !focused;

				Color color = selected || hovered ? SelectedColor : _edges[i].Color;
				if (dim)
				{
					// 消さずに落とす。消すと「線が無い」のか「関係ない」のか区別がつかない。
					color.a *= DimAlpha;
				}

				painter.strokeColor = color;
				painter.lineWidth = selected || hovered ? lineWidth + 1f : lineWidth;
				painter.BeginPath();
				painter.MoveTo(p0);
				painter.BezierCurveTo(c0, c1, p1);
				painter.Stroke();

				// 矢印は<b>終端の接線</b>へ向ける。以前は +X 固定だったので、
				// 右から左へ入るエッジでは矢印が逆を向いていた。
				// 3次ベジェの t=1 での微分は 3*(p1 - c1) なので、向きは p1 - c1。
				Vector2 tangent = p1 - c1;
				if (tangent.sqrMagnitude < 0.0001f)
				{
					tangent = p1 - p0;
				}
				if (tangent.sqrMagnitude < 0.0001f)
				{
					tangent = new Vector2(1f, 0f);
				}
				tangent.Normalize();
				Vector2 side = new Vector2(-tangent.y, tangent.x);

				painter.fillColor = color;
				painter.BeginPath();
				painter.MoveTo(p1);
				painter.LineTo(p1 - tangent * arrow + side * (arrow * 0.5f));
				painter.LineTo(p1 - tangent * arrow - side * (arrow * 0.5f));
				painter.ClosePath();
				painter.Fill();

				// 出どころに丸を打つ。行き先は矢印が示しているので、根元だけでよい。
				// ポート（取っ手）は固定位置なので、線が実際にどこから出ているかは
				// これでしか分からない。
				float dot = Mathf.Max(2f, RootDotRadius * zoom);
				painter.BeginPath();
				painter.Arc(p0, dot, 0f, 360f);
				painter.ClosePath();
				painter.Fill();

				// ラベルは線の中点。往復の2本は見た目が対称なので、
				// どちらが「入」でどちらが「切」かが線だけでは分からない。
				// 注目していないエッジのラベルは出さない。線を沈めてもラベルが残ると、
				// 文字だけが宙に浮いて<b>かえって読みにくくなる</b>。
				// ホバー中はズームが小さくても出す（そのために乗せている）。
				if (labelFont != null && !dim && (zoom >= MinZoomForLabel || hovered))
				{
					string label = FXCTextFont.Sanitize(_edges[i].Label);
					if (!string.IsNullOrEmpty(label))
					{
						// 中点固定にすると、往復の2本でラベルが<b>完全に重なる</b>。
						// 線はオフセットで 9px しか離れていないのに、ラベルは 40px 以上
						// あるため。曲線に沿って前後へずらして、通路の長い方向に逃がす。
						float at = 0.5f;
						int labelCount = _parallelCount[i];
						if (labelCount > 1)
						{
							float shift = (_parallelIndex[i] - (labelCount - 1) * 0.5f) * LabelStagger;

							// 往復の2本は<b>曲線の向きが逆</b>なので、同じ t のずらし方だと
							// 物理的に同じ場所へ来てしまう（t=0.35 と t=0.65 が
							// どちらも通路の同じ端になる）。オフセットの符号と同じく、
							// ノードIDの順で向きを揃えてからずらす。
							if (string.CompareOrdinal(
									_edges[i].FromNodeId, _edges[i].ToNodeId) > 0)
							{
								shift = -shift;
							}
							at = Mathf.Clamp(0.5f + shift, 0.18f, 0.82f);
						}
						Vector2 mid = Bezier(p0, c0, c1, p1, at);

						// 線どうしは 9px しか離れていないのに、ラベルは 13px 以上の高さがある。
						// 線と同じ側へさらに押しやらないと、2枚が同じ帯に重なって
						// <b>どちらの線の説明なのか</b>が分からない。
						if (labelCount > 1)
						{
							Vector2 ahead = Bezier(p0, c0, c1, p1, Mathf.Min(1f, at + 0.02f));
							Vector2 behind = Bezier(p0, c0, c1, p1, Mathf.Max(0f, at - 0.02f));
							Vector2 along = ahead - behind;
							if (along.sqrMagnitude > 0.0001f)
							{
								along.Normalize();
								Vector2 outward = new Vector2(-along.y, along.x);
								float lift = (_parallelIndex[i] - (labelCount - 1) * 0.5f) >= 0f
									? LabelLift
									: -LabelLift;
								if (string.CompareOrdinal(
										_edges[i].FromNodeId, _edges[i].ToNodeId) > 0)
								{
									lift = -lift;
								}
								mid += outward * lift;
							}
						}
						float width = label.Length * LabelFontSize * 0.58f;
						var box = new Rect(
							mid.x - width * 0.5f - 3f,
							mid.y - LabelFontSize * 0.5f - 2f,
							width + 6f,
							LabelFontSize + 4f);

						// 線の上に直接書くと読めない。背景を敷いてから描く。
						painter.fillColor = LabelBackColor;
						painter.BeginPath();
						painter.MoveTo(new Vector2(box.xMin, box.yMin));
						painter.LineTo(new Vector2(box.xMax, box.yMin));
						painter.LineTo(new Vector2(box.xMax, box.yMax));
						painter.LineTo(new Vector2(box.xMin, box.yMax));
						painter.ClosePath();
						painter.Fill();

						_labelText.Add(label);
						_labelPos.Add(new Vector2(box.xMin + 3f, box.yMin + 2f));
						_labelColor.Add(LabelTextColor);
					}
				}
			}

			// テキストは painter2D とは別系統なので、線と背景を全部描いてから出す。
			// 先に出すと後続の塗りに隠れる。
			for (int i = 0; i < _labelText.Count; i++)
			{
				mgc.DrawText(_labelText[i], _labelPos[i], LabelFontSize, _labelColor[i], labelFont);
			}

			if (Instrument)
			{
				_generateWatch.Stop();
				if (FrameCount > WarmupFrames)
				{
					AvgGenerateMs = Blend(AvgGenerateMs, _generateWatch.Elapsed.TotalMilliseconds);
				}
			}
		}

		private void DrawPending(Painter2D painter, float zoom)
		{
			if (!PendingActive)
			{
				return;
			}

			Vector2 p0 = _owner.Viewport.GraphToView(PendingFromGraph);
			Vector2 p1 = PendingToView;
			float bend = Mathf.Max(24f, Mathf.Abs(p1.x - p0.x) * 0.4f);

			bool pro = EditorGUIUtility.isProSkin;
			painter.strokeColor = PendingValid
				? (pro ? new Color(0.35f, 0.85f, 0.45f, 1f) : new Color(0.10f, 0.52f, 0.20f, 1f))
				: (pro ? new Color(0.85f, 0.85f, 0.9f, 0.55f) : new Color(0.25f, 0.25f, 0.30f, 0.55f));
			painter.lineWidth = Mathf.Max(MinLineWidth, 2f * zoom);
			painter.BeginPath();
			painter.MoveTo(p0);
			painter.BezierCurveTo(p0 + new Vector2(bend, 0f), p1 - new Vector2(bend, 0f), p1);
			painter.Stroke();
		}

		/// <summary>
		/// ID衝突しないノード対キー。IDに現れない制御文字で区切る
		/// （"a"+"b" と "ab"+"" を同じキーにしないため）。
		/// </summary>
		private static string MakePairKey(string fromNodeId, string toNodeId)
		{
			// <b>向きを区別しない。</b> A→B と B→A は同じ通路を通るので、
			// 別グループにするとどちらも count==1 になり、重なりを避けるための
			// オフセットが一度も効かない。VRChat の定番トグル（2つの State が
			// 同じ bool で往復する形）がまさにこれで、2本が完全に重なっていた。
			return string.CompareOrdinal(fromNodeId, toNodeId) <= 0
				? fromNodeId + "" + toNodeId
				: toNodeId + "" + fromNodeId;
		}

		/// <summary>
		/// ノード矩形のどの辺から出入りするかを相手の方向で選び、その辺の外向き法線も返す。
		///
		/// 以前は「起点の右辺中央 → 終点の左辺中央」に固定していた。縦に並べると
		/// 往復が必ず X に交差し、横に並べると戻り側が<b>両ノードの裏を大回り</b>する
		/// （制御点がさらに外へ張り出すため）。相手のいる側から出せば、
		/// どちらの並べ方でも2点間の通路をまっすぐ通る。
		/// </summary>
		/// <summary>
		/// 各端点を「ノード×辺」でまとめ、辺の上に並べる順番を決める。
		///
		/// 並べる順は<b>相手の位置をその辺の軸へ射影した値</b>。こうすると
		/// 線どうしが交差しない（左から来る線は左のスロット、右から来る線は右）。
		/// 同じ相手が2回出てくる往復ペアは、エッジの並び順で決める。
		/// </summary>
		private void AssignEndpointSlots()
		{
			_fromSlot.Clear();
			_fromSlotCount.Clear();
			_toSlot.Clear();
			_toSlotCount.Clear();
			_slotBuckets.Clear();

			for (int i = 0; i < _edges.Count; i++)
			{
				_fromSlot.Add(0);
				_fromSlotCount.Add(1);
				_toSlot.Add(0);
				_toSlotCount.Add(1);
			}

			for (int i = 0; i < _edges.Count; i++)
			{
				IFXCGraphEdge e = _edges[i];
				FXCNodeView from = _owner.FindNodeView(e.FromNodeId);
				FXCNodeView to = _owner.FindNodeView(e.ToNodeId);
				if (from == null || to == null)
				{
					continue;
				}

				// ポート指定のある端はポート自身が分かれているので散らさない。
				if (string.IsNullOrEmpty(e.FromPortId))
				{
					Vector2 ignored;
					int side = SideOf(from.GraphRect, to.GraphRect.center, out ignored);
					Bucket(BucketKey(from.GraphRect, side, true, i)).Add(i * 2);
				}
				if (string.IsNullOrEmpty(e.ToPortId))
				{
					Vector2 ignored;
					int side = SideOf(to.GraphRect, from.GraphRect.center, out ignored);
					Bucket(BucketKey(to.GraphRect, side, false, i)).Add(i * 2 + 1);
				}
			}

			foreach (KeyValuePair<long, List<int>> pair in _slotBuckets)
			{
				List<int> members = pair.Value;
				if (members.Count > 1)
				{
					members.Sort(CompareBySideAxis);
				}
				for (int k = 0; k < members.Count; k++)
				{
					int edgeIndex = members[k] >> 1;
					if ((members[k] & 1) == 0)
					{
						_fromSlot[edgeIndex] = k;
						_fromSlotCount[edgeIndex] = members.Count;
					}
					else
					{
						_toSlot[edgeIndex] = k;
						_toSlotCount[edgeIndex] = members.Count;
					}
				}
			}
		}

		private List<int> Bucket(long key)
		{
			List<int> list;
			if (!_slotBuckets.TryGetValue(key, out list))
			{
				list = new List<int>();
				_slotBuckets.Add(key, list);
			}
			return list;
		}

		/// <summary>ノードの同一性は、そのフレームでの矩形で代用する（位置が同じなら同じノード）。</summary>
		private long BucketKey(Rect rect, int side, bool outgoing, int edgeIndex)
		{
			unchecked
			{
				long h = (long)Mathf.RoundToInt(rect.x) * 73856093L
					^ (long)Mathf.RoundToInt(rect.y) * 19349663L;
				return h * 8L + side;
			}
		}

		/// <summary>
		/// 同じ辺に集まった端点を、相手の位置で並べ替えるための比較。
		/// 辺が上下なら相手の x、左右なら相手の y を見る。
		/// </summary>
		private int CompareBySideAxis(int a, int b)
		{
			float ka = SortKey(a);
			float kb = SortKey(b);
			int c = ka.CompareTo(kb);
			// 往復ペアは相手が同じなので決まらない。エッジの並び順で固定する
			// （両端のノードで同じ順になるので、線が交差しない）。
			return c != 0 ? c : (a >> 1).CompareTo(b >> 1);
		}

		private float SortKey(int packed)
		{
			int edgeIndex = packed >> 1;
			bool outgoing = (packed & 1) == 0;
			IFXCGraphEdge e = _edges[edgeIndex];

			FXCNodeView self = _owner.FindNodeView(outgoing ? e.FromNodeId : e.ToNodeId);
			FXCNodeView other = _owner.FindNodeView(outgoing ? e.ToNodeId : e.FromNodeId);
			if (self == null || other == null)
			{
				return 0f;
			}

			Vector2 normal;
			int side = SideOf(self.GraphRect, other.GraphRect.center, out normal);
			// 上下の辺なら x、左右の辺なら y で並べる。
			return side < 2 ? other.GraphRect.center.y : other.GraphRect.center.x;
		}

		/// <summary>0=左, 1=右, 2=上, 3=下。</summary>
		private static int SideOf(Rect rect, Vector2 towards, out Vector2 normal)
		{
			Vector2 center = rect.center;
			Vector2 d = towards - center;
			float hx = Mathf.Max(1f, rect.width * 0.5f);
			float hy = Mathf.Max(1f, rect.height * 0.5f);

			if (Mathf.Abs(d.x) * hy >= Mathf.Abs(d.y) * hx)
			{
				bool right = d.x >= 0f;
				normal = new Vector2(right ? 1f : -1f, 0f);
				return right ? 1 : 0;
			}

			bool down = d.y >= 0f;
			normal = new Vector2(0f, down ? 1f : -1f);
			return down ? 3 : 2;
		}

		/// <summary>辺の上のスロット位置。<paramref name="count"/> が1なら辺の中央。</summary>
		private static Vector2 SlotOnSide(Rect rect, int side, int index, int count)
		{
			float t = count > 1 ? (index + 1f) / (count + 1f) : 0.5f;

			// 角に寄りすぎると線が角を回り込んで見えるので、内側へ寄せる。
			const float margin = 0.12f;
			t = margin + t * (1f - margin * 2f);

			switch (side)
			{
				case 0: return new Vector2(rect.xMin, Mathf.Lerp(rect.yMin, rect.yMax, t));
				case 1: return new Vector2(rect.xMax, Mathf.Lerp(rect.yMin, rect.yMax, t));
				case 2: return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, t), rect.yMin);
				default: return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, t), rect.yMax);
			}
		}

		private static Vector2 AnchorOnRect(Rect rect, Vector2 towards, out Vector2 normal)
		{
			Vector2 center = rect.center;
			Vector2 d = towards - center;
			float hx = Mathf.Max(1f, rect.width * 0.5f);
			float hy = Mathf.Max(1f, rect.height * 0.5f);

			// 縦横の比で正規化してから比べる。ノードは横長（200x40）なので、
			// 生の差分で比べると真横の相手にも上下の辺が選ばれてしまう。
			if (Mathf.Abs(d.x) * hy >= Mathf.Abs(d.y) * hx)
			{
				float sx = d.x >= 0f ? 1f : -1f;
				normal = new Vector2(sx, 0f);
				return new Vector2(center.x + sx * hx, center.y);
			}

			float sy = d.y >= 0f ? 1f : -1f;
			normal = new Vector2(0f, sy);
			return new Vector2(center.x, center.y + sy * hy);
		}

		/// <summary>
		/// 3次ベジェは4つの制御点の凸包に収まるので、その AABB がビューと交差しなければ
		/// 曲線もビュー外。端点だけで判定すると、右から左へ向かうエッジ（制御点が外側に
		/// 張り出す）を誤ってカリングしてしまう。
		/// </summary>
		private static bool IsFullyOutside(Rect view, Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1)
		{
			float minX = Mathf.Min(Mathf.Min(p0.x, c0.x), Mathf.Min(c1.x, p1.x));
			float maxX = Mathf.Max(Mathf.Max(p0.x, c0.x), Mathf.Max(c1.x, p1.x));
			float minY = Mathf.Min(Mathf.Min(p0.y, c0.y), Mathf.Min(c1.y, p1.y));
			float maxY = Mathf.Max(Mathf.Max(p0.y, c0.y), Mathf.Max(c1.y, p1.y));
			return maxX < view.xMin || minX > view.xMax || maxY < view.yMin || minY > view.yMax;
		}
	}
}
