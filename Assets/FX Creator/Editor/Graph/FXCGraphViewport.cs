using UnityEngine;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// グラフ空間 ↔ ビュー空間（<see cref="FXCGraphView"/> 内のピクセル）の変換を持つ。
	///
	///     view  = graph * Zoom + Offset
	///     graph = (view - Offset) / Zoom
	///
	/// グラフ空間は AnimatorController がネイティブに持つ座標
	/// （<c>ChildAnimatorState.position</c> 等）とそのまま一致させる。
	/// UI に依存しないので単体テストできる（FXCGraphViewportTests）。
	/// </summary>
	public sealed class FXCGraphViewport
	{
		public const float MinZoom = 0.2f;
		public const float MaxZoom = 2.0f;

		/// <summary>ホイール1ノッチあたりの倍率。</summary>
		private const float ZoomStepBase = 1.1f;

		public float Zoom { get; private set; } = 1f;
		public Vector2 Offset { get; private set; } = Vector2.zero;

		public Vector2 GraphToView(Vector2 graphPoint)
		{
			return graphPoint * Zoom + Offset;
		}

		public Vector2 ViewToGraph(Vector2 viewPoint)
		{
			return (viewPoint - Offset) / Zoom;
		}

		public Rect GraphToView(Rect graphRect)
		{
			return new Rect(GraphToView(graphRect.position), graphRect.size * Zoom);
		}

		public Rect ViewToGraph(Rect viewRect)
		{
			return new Rect(ViewToGraph(viewRect.position), viewRect.size / Zoom);
		}

		public void PanByView(Vector2 viewDelta)
		{
			Offset += viewDelta;
		}

		/// <summary>
		/// <paramref name="viewPivot"/> の下にあるグラフ座標を動かさずにズームする
		/// （カーソル基準ズーム）。
		/// </summary>
		public void ZoomAt(float newZoom, Vector2 viewPivot)
		{
			newZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
			Vector2 anchor = ViewToGraph(viewPivot);
			Zoom = newZoom;
			Offset = viewPivot - anchor * newZoom;
		}

		/// <summary>ホイールのノッチ数ぶんズームする。正で拡大。</summary>
		public void ZoomByStepsAt(float steps, Vector2 viewPivot)
		{
			ZoomAt(Zoom * Mathf.Pow(ZoomStepBase, steps), viewPivot);
		}

		public void Reset()
		{
			Zoom = 1f;
			Offset = Vector2.zero;
		}

		/// <summary>
		/// グラフ空間の <paramref name="graphRect"/> が <paramref name="viewRect"/> に
		/// 収まるようにズームと位置を合わせる（Frame All 相当）。
		/// 入力が縮退している場合は何もしない。
		/// </summary>
		public void FrameGraphRect(Rect graphRect, Rect viewRect, float viewPadding = 24f)
		{
			if (graphRect.width <= 0f || graphRect.height <= 0f)
			{
				return;
			}
			if (viewRect.width <= 1f || viewRect.height <= 1f)
			{
				return;
			}

			float availableW = Mathf.Max(1f, viewRect.width - viewPadding * 2f);
			float availableH = Mathf.Max(1f, viewRect.height - viewPadding * 2f);
			float fit = Mathf.Min(availableW / graphRect.width, availableH / graphRect.height);

			Zoom = Mathf.Clamp(fit, MinZoom, MaxZoom);
			Offset = viewRect.center - graphRect.center * Zoom;
		}
	}
}
