using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// グラフの背景グリッド。ビューポートのズームに連動する2段グリッド
	/// （細 20 グラフ単位 / 太 100 グラフ単位）を Painter2D で1要素にまとめて描く。
	///
	/// 線の間隔が画面上で <see cref="MinPixelSpacing"/> を下回る段は描かない
	/// （縮小時に真っ白な塗りになるのを防ぐ）。
	/// </summary>
	public sealed class FXCGridBackground : VisualElement
	{
		/// <summary>細線の間隔（グラフ単位）。ノードのスナップ幅と揃えている。</summary>
		public const float MinorSpacing = 20f;

		/// <summary>太線は細線の何本ごとか。</summary>
		public const int MajorEvery = 5;

		private const float MinPixelSpacing = 6f;

		/// <summary>暴走ループ防止の上限（1方向あたりの線の本数）。</summary>
		private const int MaxLinesPerAxis = 4096;

		private readonly FXCGraphViewport _viewport;

		public Color MinorLineColor { get; set; }
		public Color MajorLineColor { get; set; }

		public FXCGridBackground(FXCGraphViewport viewport)
		{
			_viewport = viewport;

			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;
			style.left = 0;
			style.top = 0;
			style.right = 0;
			style.bottom = 0;

			bool pro = EditorGUIUtility.isProSkin;
			style.backgroundColor = pro
				? new Color(0.16f, 0.16f, 0.17f, 1f)
				: new Color(0.76f, 0.76f, 0.78f, 1f);
			MinorLineColor = pro
				? new Color(1f, 1f, 1f, 0.045f)
				: new Color(0f, 0f, 0f, 0.055f);
			MajorLineColor = pro
				? new Color(1f, 1f, 1f, 0.10f)
				: new Color(0f, 0f, 0f, 0.12f);

			generateVisualContent += OnGenerateVisualContent;
		}

		private void OnGenerateVisualContent(MeshGenerationContext mgc)
		{
			Rect view = contentRect;
			if (view.width < 1f || view.height < 1f)
			{
				return;
			}

			Painter2D painter = mgc.painter2D;
			DrawTier(painter, view, MinorSpacing, MinorLineColor);
			DrawTier(painter, view, MinorSpacing * MajorEvery, MajorLineColor);
		}

		private void DrawTier(Painter2D painter, Rect view, float graphSpacing, Color color)
		{
			float pixelSpacing = graphSpacing * _viewport.Zoom;
			if (pixelSpacing < MinPixelSpacing)
			{
				return;
			}

			painter.strokeColor = color;
			painter.lineWidth = 1f;

			// 縦線: ビュー左端より左にある最初の格子線から右端まで。
			float graphLeft = _viewport.ViewToGraph(new Vector2(view.xMin, view.yMin)).x;
			float startX = Mathf.Floor(graphLeft / graphSpacing) * graphSpacing;
			int drawn = 0;
			for (float gx = startX; drawn < MaxLinesPerAxis; gx += graphSpacing, drawn++)
			{
				float vx = _viewport.GraphToView(new Vector2(gx, 0f)).x;
				if (vx > view.xMax)
				{
					break;
				}
				if (vx >= view.xMin)
				{
					painter.BeginPath();
					painter.MoveTo(new Vector2(vx, view.yMin));
					painter.LineTo(new Vector2(vx, view.yMax));
					painter.Stroke();
				}
			}

			// 横線。
			float graphTop = _viewport.ViewToGraph(new Vector2(view.xMin, view.yMin)).y;
			float startY = Mathf.Floor(graphTop / graphSpacing) * graphSpacing;
			drawn = 0;
			for (float gy = startY; drawn < MaxLinesPerAxis; gy += graphSpacing, drawn++)
			{
				float vy = _viewport.GraphToView(new Vector2(0f, gy)).y;
				if (vy > view.yMax)
				{
					break;
				}
				if (vy >= view.yMin)
				{
					painter.BeginPath();
					painter.MoveTo(new Vector2(view.xMin, vy));
					painter.LineTo(new Vector2(view.xMax, vy));
					painter.Stroke();
				}
			}
		}
	}
}
