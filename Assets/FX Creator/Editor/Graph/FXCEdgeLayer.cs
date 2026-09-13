using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
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

		/// <summary>この倍率をズームに掛けた線幅で描く（縮小時に消えないよう下限を設ける）。</summary>
		private const float MinLineWidth = 1f;

		private readonly FXCGraphView _owner;

		private readonly List<IFXCGraphEdge> _edges = new List<IFXCGraphEdge>();
		/// <summary>同一ノード対の中での連番と総数（平行エッジをずらすため）。</summary>
		private readonly List<int> _parallelIndex = new List<int>();
		private readonly List<int> _parallelCount = new List<int>();

		public Color SelectedColor { get; set; } = new Color(0.30f, 0.65f, 1f, 1f);

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
			Vector2 fromGraph;
			bool fromHasPort = from.TryGetPortAnchor(e.FromPortId, out fromGraph);
			if (!fromHasPort)
			{
				fromGraph = new Vector2(from.GraphRect.xMax, from.GraphRect.center.y);
			}

			Vector2 toGraph;
			bool toHasPort = to.TryGetPortAnchor(e.ToPortId, out toGraph);
			if (!toHasPort)
			{
				toGraph = new Vector2(to.GraphRect.xMin, to.GraphRect.center.y);
			}

			// ポートを使っている端はポート自体が分かれているのでずらさない。
			// ノードの縁に集まる端だけ、平行エッジを重ならないようにずらす。
			float spread = 0f;
			int count = _parallelCount[i];
			if (count > 1)
			{
				spread = (_parallelIndex[i] - (count - 1) * 0.5f) * ParallelSpread;
			}

			p0 = vp.GraphToView(fromGraph);
			p1 = vp.GraphToView(toGraph);
			if (!fromHasPort)
			{
				p0.y += spread;
			}
			if (!toHasPort)
			{
				p1.y += spread;
			}

			float bend = Mathf.Max(24f, Mathf.Abs(p1.x - p0.x) * 0.4f);
			c0 = p0 + new Vector2(bend, 0f);
			c1 = p1 - new Vector2(bend, 0f);
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
			painter.lineCap = LineCap.Round;

			float zoom = _owner.Viewport.Zoom;
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
				Color color = selected ? SelectedColor : _edges[i].Color;

				painter.strokeColor = color;
				painter.lineWidth = selected ? lineWidth + 1f : lineWidth;
				painter.BeginPath();
				painter.MoveTo(p0);
				painter.BezierCurveTo(c0, c1, p1);
				painter.Stroke();

				painter.fillColor = color;
				painter.BeginPath();
				painter.MoveTo(p1);
				painter.LineTo(p1 + new Vector2(-arrow, -arrow * 0.5f));
				painter.LineTo(p1 + new Vector2(-arrow, arrow * 0.5f));
				painter.ClosePath();
				painter.Fill();
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

			painter.strokeColor = PendingValid
				? new Color(0.35f, 0.85f, 0.45f, 1f)
				: new Color(0.85f, 0.85f, 0.9f, 0.55f);
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
			return fromNodeId + "\u0001" + toNodeId;
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
