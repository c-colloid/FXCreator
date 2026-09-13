using UnityEditor.Animations;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// ウィンドウから表示対象を受け取るパネル。
	/// <see cref="FxcAnimatorWindow"/> は出ているオーバーレイにだけ配ればよい。
	/// </summary>
	internal interface IFxcPanel
	{
		void ApplyTarget(AnimatorController controller, bool readOnly, GameObject avatar);
	}

	/// <summary>
	/// VAR / parameter / menu をフローティングにするための土台
	/// （Docs/FXCreator-Design.md §6）。
	///
	/// 自前でオーバーレイを作らず Unity の Overlay に載せている。
	/// フローティングと端へのドッキングの切り替え、折り畳み、表示の on/off、
	/// 位置と大きさの保存が標準で付いてくるので、こちらで持つのは中身だけで済む。
	///
	/// ドッキングした3枚を常設していたときはグラフが<b>ウィンドウの29%</b>しか
	/// 残らなかった（実測）。主役はグラフなので、必要なときだけ出す形にした。
	/// </summary>
	public abstract class FxcPanelOverlay : Overlay, IFxcPanel
	{
		/// <summary>パネルは自由に大きさを変えたいので、ツールバー形態は持たない。</summary>
		protected override Layout supportedLayouts => Layout.Panel;

		/// <summary>初回に開いたときの大きさ。以降は Overlay 側が覚えた値を使う。</summary>
		protected abstract Vector2 InitialSize { get; }

		public override void OnCreated()
		{
			minSize = new Vector2(180f, 90f);
			maxSize = new Vector2(1200f, 1200f);
			Window?.RegisterPanel(this);
		}

		public override void OnWillBeDestroyed()
		{
			Window?.UnregisterPanel(this);
		}

		public abstract void ApplyTarget(AnimatorController controller, bool readOnly, GameObject avatar);

		protected FxcAnimatorWindow Window => containerWindow as FxcAnimatorWindow;

		/// <summary>
		/// 各パネルの <see cref="Overlay.CreatePanelContent"/> はこれを通す。
		/// 中身を広げる・大きさを決める・表示対象を入れ直す、の3つをまとめる。
		/// </summary>
		protected T BuildContent<T>(T view) where T : VisualElement
		{
			// 中身に width / height を直接書くとその値で固定され、
			// <b>掴んでもリサイズできなくなる</b>。広がる指定だけ与える。
			view.style.flexGrow = 1;
			view.style.minWidth = 0f;
			view.style.minHeight = 0f;

			// 大きさが未設定のときだけ初期値を入れる。未設定は <b>NaN</b> で来る
			// （NaN との比較は常に false なので「size.x < 1」では判定できない）。
			// NaN のままだと内容に合わせた固定サイズになり、これもリサイズできない。
			// なお OnCreated で入れても、そのあと Unity が保存値（NaN）で上書きするので
			// ここ（パネルを開いた時点）で入れる。
			if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x < 1f || size.y < 1f)
			{
				size = InitialSize;
			}

			// 非表示 → 再表示のたびにここがやり直されるので、
			// 表示対象を入れ直さないと空のまま出てくる。
			Window?.RefreshPanel(this);
			return view;
		}
	}

	/// <summary>資料の「VAR」（§6.1）。</summary>
	[Overlay(typeof(FxcAnimatorWindow), OverlayId, "VAR", true)]
	public sealed class VarOverlay : FxcPanelOverlay
	{
		public const string OverlayId = "fxc-var";

		private ParameterListView _view;

		protected override Vector2 InitialSize => new Vector2(250f, 240f);

		public override VisualElement CreatePanelContent()
		{
			_view = BuildContent(new ParameterListView());
			return _view;
		}

		public override void ApplyTarget(AnimatorController controller, bool readOnly, GameObject avatar)
		{
			if (_view != null)
			{
				_view.SetController(controller, readOnly);
			}
		}
	}

	/// <summary>資料の「parameter (name, sync)」（§6.2）。</summary>
	[Overlay(typeof(FxcAnimatorWindow), OverlayId, "parameter", false)]
	public sealed class VrcParameterOverlay : FxcPanelOverlay
	{
		public const string OverlayId = "fxc-vrc-parameter";

		private Vrc.VrcParameterPanel _view;

		protected override Vector2 InitialSize => new Vector2(420f, 220f);

		public override VisualElement CreatePanelContent()
		{
			_view = BuildContent(new Vrc.VrcParameterPanel());
			return _view;
		}

		public override void ApplyTarget(AnimatorController controller, bool readOnly, GameObject avatar)
		{
			if (_view == null)
			{
				return;
			}
#if VRC
			var descriptor = avatar != null
				? avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>()
				: null;
			_view.SetTarget(controller, descriptor != null ? descriptor.expressionParameters : null);
#else
			_view.SetTarget(controller, null);
#endif
		}
	}

	/// <summary>資料の「menu (name, value)」（§6.3）。</summary>
	[Overlay(typeof(FxcAnimatorWindow), OverlayId, "menu", false)]
	public sealed class VrcMenuOverlay : FxcPanelOverlay
	{
		public const string OverlayId = "fxc-vrc-menu";

		private Vrc.VrcMenuPanel _view;

		protected override Vector2 InitialSize => new Vector2(420f, 200f);

		public override VisualElement CreatePanelContent()
		{
			_view = BuildContent(new Vrc.VrcMenuPanel());
			return _view;
		}

		public override void ApplyTarget(AnimatorController controller, bool readOnly, GameObject avatar)
		{
			if (_view == null)
			{
				return;
			}
#if VRC
			var descriptor = avatar != null
				? avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>()
				: null;
			_view.SetMenu(descriptor != null ? descriptor.expressionsMenu : null);
#else
			_view.SetMenu(null);
#endif
		}
	}
}
