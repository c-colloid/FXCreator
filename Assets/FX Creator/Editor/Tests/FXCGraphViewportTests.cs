using colloid.FXCreator.Graph;
using NUnit.Framework;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// <see cref="FXCGraphViewport"/> の座標変換の数値検証。
	/// グラフ基盤のバグはほぼ全部ここの取り違えから出るので、UI を通さずに固める。
	/// </summary>
	public class FXCGraphViewportTests
	{
		private const float Tol = 1e-4f;

		private static void AssertApprox(Vector2 expected, Vector2 actual, string message = null)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tol), message);
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tol), message);
		}

		[Test]
		public void DefaultsToIdentity()
		{
			var vp = new FXCGraphViewport();
			Assert.That(vp.Zoom, Is.EqualTo(1f).Within(Tol));
			AssertApprox(Vector2.zero, vp.Offset);
			AssertApprox(new Vector2(12f, -34f), vp.GraphToView(new Vector2(12f, -34f)));
		}

		[Test]
		public void GraphToViewAndBackRoundTrips()
		{
			var vp = new FXCGraphViewport();
			vp.ZoomAt(1.75f, new Vector2(120f, 80f));
			vp.PanByView(new Vector2(-37f, 19f));

			var graph = new Vector2(345.5f, -210.25f);
			AssertApprox(graph, vp.ViewToGraph(vp.GraphToView(graph)), "graph -> view -> graph");

			var view = new Vector2(-19f, 640f);
			AssertApprox(view, vp.GraphToView(vp.ViewToGraph(view)), "view -> graph -> view");
		}

		[Test]
		public void RectRoundTripsIncludingSize()
		{
			var vp = new FXCGraphViewport();
			vp.ZoomAt(0.5f, new Vector2(300f, 300f));

			var graphRect = new Rect(-40f, 60f, 200f, 90f);
			Rect viewRect = vp.GraphToView(graphRect);

			Assert.That(viewRect.width, Is.EqualTo(100f).Within(Tol));
			Assert.That(viewRect.height, Is.EqualTo(45f).Within(Tol));

			Rect back = vp.ViewToGraph(viewRect);
			AssertApprox(graphRect.position, back.position);
			AssertApprox(graphRect.size, back.size);
		}

		/// <summary>カーソル基準ズームの本質: ピボットの下のグラフ座標が動かないこと。</summary>
		[Test]
		public void ZoomAtKeepsThePivotAnchored()
		{
			var vp = new FXCGraphViewport();
			vp.PanByView(new Vector2(55f, -12f));

			var pivot = new Vector2(210f, 175f);
			Vector2 anchoredGraphPoint = vp.ViewToGraph(pivot);

			vp.ZoomAt(1.9f, pivot);
			AssertApprox(anchoredGraphPoint, vp.ViewToGraph(pivot), "zoom in");

			vp.ZoomAt(0.3f, pivot);
			AssertApprox(anchoredGraphPoint, vp.ViewToGraph(pivot), "zoom out");
		}

		[Test]
		public void ZoomIsClampedToTheAllowedRange()
		{
			var vp = new FXCGraphViewport();
			var pivot = new Vector2(10f, 10f);

			vp.ZoomAt(999f, pivot);
			Assert.That(vp.Zoom, Is.EqualTo(FXCGraphViewport.MaxZoom).Within(Tol));

			vp.ZoomAt(0.0001f, pivot);
			Assert.That(vp.Zoom, Is.EqualTo(FXCGraphViewport.MinZoom).Within(Tol));
		}

		[Test]
		public void ZoomByStepsIsSymmetric()
		{
			var vp = new FXCGraphViewport();
			var pivot = new Vector2(64f, 64f);

			vp.ZoomByStepsAt(3f, pivot);
			vp.ZoomByStepsAt(-3f, pivot);

			Assert.That(vp.Zoom, Is.EqualTo(1f).Within(1e-3f));
			AssertApprox(Vector2.zero, vp.Offset, "戻したら元の位置に戻る");
		}

		[Test]
		public void PanMovesInViewPixelsRegardlessOfZoom()
		{
			var vp = new FXCGraphViewport();
			vp.ZoomAt(2f, Vector2.zero);

			Vector2 before = vp.GraphToView(new Vector2(100f, 100f));
			vp.PanByView(new Vector2(30f, -10f));
			Vector2 after = vp.GraphToView(new Vector2(100f, 100f));

			AssertApprox(new Vector2(30f, -10f), after - before);
		}

		[Test]
		public void FrameGraphRectCentersAndFits()
		{
			var vp = new FXCGraphViewport();
			var graphRect = new Rect(0f, 0f, 400f, 200f);
			var viewRect = new Rect(0f, 0f, 848f, 448f);

			vp.FrameGraphRect(graphRect, viewRect, 24f);

			// 収まる: 余白を除いた範囲に入っている。
			Rect projected = vp.GraphToView(graphRect);
			Assert.That(projected.width, Is.LessThanOrEqualTo(viewRect.width - 48f + Tol));
			Assert.That(projected.height, Is.LessThanOrEqualTo(viewRect.height - 48f + Tol));

			// 中央: グラフ矩形の中心がビュー中心に来る。
			AssertApprox(viewRect.center, vp.GraphToView(graphRect.center));
		}

		[Test]
		public void FrameGraphRectRespectsMaxZoomForTinyGraphs()
		{
			var vp = new FXCGraphViewport();
			vp.FrameGraphRect(new Rect(0f, 0f, 4f, 4f), new Rect(0f, 0f, 800f, 600f));

			Assert.That(vp.Zoom, Is.EqualTo(FXCGraphViewport.MaxZoom).Within(Tol));
		}

		/// <summary>縮退入力で NaN を作らない（レイアウト前の 0 サイズで呼ばれる）。</summary>
		[Test]
		public void FrameGraphRectIgnoresDegenerateInput()
		{
			var vp = new FXCGraphViewport();
			vp.ZoomAt(1.4f, new Vector2(5f, 5f));
			float zoomBefore = vp.Zoom;
			Vector2 offsetBefore = vp.Offset;

			vp.FrameGraphRect(new Rect(0f, 0f, 0f, 0f), new Rect(0f, 0f, 800f, 600f));
			vp.FrameGraphRect(new Rect(0f, 0f, 100f, 100f), new Rect(0f, 0f, 0f, 0f));

			Assert.That(vp.Zoom, Is.EqualTo(zoomBefore).Within(Tol));
			AssertApprox(offsetBefore, vp.Offset);
		}
	}
}
