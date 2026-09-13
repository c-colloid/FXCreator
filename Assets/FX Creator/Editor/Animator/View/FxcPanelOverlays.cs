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
	/// 位置の保存が標準で付いてくるので、こちらで持つのは中身だけで済む。
	///
	/// ドッキングした3枚を常設していたときはグラフが<b>ウィンドウの29%</b>しか
	/// 残らなかった（実測）。主役はグラフなので、必要なときだけ出す形にした。
	/// </summary>
	public abstract class FxcPanelOverlay : Overlay, IFxcPanel
	{
		private FxcAnimatorWindow _window;

		/// <summary>パネルは自由に大きさを変えたいので、ツールバー形態は持たない。</summary>
		protected override Layout supportedLayouts => Layout.Panel;

		public override void OnCreated()
		{
			_window = containerWindow as FxcAnimatorWindow;
			if (_window != null)
			{
				_window.RegisterPanel(this);
			}
		}

		public override void OnWillBeDestroyed()
		{
			if (_window != null)
			{
				_window.UnregisterPanel(this);
				_window = null;
			}
		}

		public abstract void ApplyTarget(AnimatorController controller, bool readOnly, GameObject avatar);

		/// <summary>中身の既定の大きさ。Overlay 側が覚えるまでの初期値。</summary>
		protected static T Sized<T>(T element, float width, float height) where T : VisualElement
		{
			element.style.width = width;
			element.style.height = height;
			return element;
		}
	}

	/// <summary>資料の「VAR」（§6.1）。</summary>
	[Overlay(typeof(FxcAnimatorWindow), OverlayId, "VAR", true)]
	public sealed class VarOverlay : FxcPanelOverlay
	{
		public const string OverlayId = "fxc-var";

		private ParameterListView _view;

		public override VisualElement CreatePanelContent()
		{
			_view = Sized(new ParameterListView(), 250f, 240f);
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

		public override VisualElement CreatePanelContent()
		{
			_view = Sized(new Vrc.VrcParameterPanel(), 420f, 220f);
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

		public override VisualElement CreatePanelContent()
		{
			_view = Sized(new Vrc.VrcMenuPanel(), 420f, 200f);
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
