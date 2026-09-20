using UnityEditor.Animations;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// パネルへ配る表示対象ひとそろい。
	///
	/// パラメータとメニューは <c>IFxTarget</c> が解決したもの（Direct はアバターの
	/// アセット、NDMF(MA) は MA コンポーネント）なので、パネル側はどのモードで
	/// 動いているかを知らなくてよい。VRC SDK の型は <c>#if VRC</c> の外へ
	/// 出せないため、ここでは <see cref="UnityEngine.Object"/> で運ぶ。
	/// </summary>
	public struct FxcPanelContext
	{
		public AnimatorController Controller;
		public bool ReadOnly;
		public GameObject Avatar;

		/// <summary>
		/// 同期パラメータの宣言先（Direct は Expression Parameters、
		/// NDMF(MA) は MA Parameters）。無ければ null。
		/// </summary>
		public Targeting.IFxParameterStore ParameterStore;

		/// <summary>VRCExpressionsMenu（無ければ null）。</summary>
		public Object Menu;
	}

	/// <summary>
	/// ウィンドウから表示対象を受け取るパネル。
	/// <see cref="FxcAnimatorWindow"/> は出ているオーバーレイにだけ配ればよい。
	/// </summary>
	internal interface IFxcPanel
	{
		void ApplyTarget(FxcPanelContext context);
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

		public abstract void ApplyTarget(FxcPanelContext context);

		/// <summary>
		/// 別のパネルで選ばれたパラメータを光らせる（§6.2 の選択連動）。
		/// VAR と parameter は名前で結ばれているだけなので、対応関係を
		/// 目で追えないと差分の意味が分からない。
		/// </summary>
		public virtual void SetParameterHighlight(string name)
		{
		}

		protected FxcAnimatorWindow Window => containerWindow as FxcAnimatorWindow;

		/// <summary>
		/// 各パネルの <see cref="Overlay.CreatePanelContent"/> はこれを通す。
		/// 中身を広げる・大きさを決める・表示対象を入れ直す、の3つをまとめる。
		///
		/// <b>呼び出し側は、これを呼ぶ前に自分のフィールドへ新しいビューを入れておくこと。</b>
		/// ここから <see cref="FxcAnimatorWindow.RefreshPanel"/> 経由で
		/// <c>ApplyTarget</c> が走り、その中でフィールドのビューに表示対象を入れる。
		/// <c>_view = BuildContent(new ...)</c> と書くと右辺が先に評価されるので、
		/// そのとき <c>_view</c> はまだ<b>捨てる方の（初回は null の）ビュー</b>を指していて、
		/// 新しいビューには何も入らない。
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
			// 先にフィールドへ入れる。BuildContent の中で ApplyTarget が走り、
			// そこで参照されるのはこのフィールドの方。
			_view = new ParameterListView();
			_view.ParameterSelected += name =>
			{
				FxcAnimatorWindow window = Window;
				if (window != null)
				{
					window.OnParameterSelected(name);
				}
			};
			return BuildContent(_view);
		}

		public override void ApplyTarget(FxcPanelContext context)
		{
			if (_view != null)
			{
				_view.SetController(context.Controller, context.ReadOnly, context.ParameterStore);
			}
		}

		public override void SetParameterHighlight(string name)
		{
			if (_view != null)
			{
				_view.SetHighlight(name);
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
			// 先にフィールドへ入れる。BuildContent の中で ApplyTarget が走り、
			// そこで参照されるのはこのフィールドの方。
			_view = new Vrc.VrcParameterPanel();
			_view.ParameterSelected += name =>
			{
				FxcAnimatorWindow window = Window;
				if (window != null)
				{
					window.OnParameterSelected(name);
				}
			};
			return BuildContent(_view);
		}

		public override void ApplyTarget(FxcPanelContext context)
		{
			if (_view == null)
			{
				return;
			}
			_view.SetTarget(context.Controller, context.ParameterStore, context.ReadOnly);
		}

		public override void SetParameterHighlight(string name)
		{
			if (_view != null)
			{
				_view.SetHighlight(name);
			}
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
			// 先にフィールドへ入れる。BuildContent の中で ApplyTarget が走り、
			// そこで参照されるのはこのフィールドの方。
			_view = new Vrc.VrcMenuPanel();
			return BuildContent(_view);
		}

		public override void ApplyTarget(FxcPanelContext context)
		{
			if (_view == null)
			{
				return;
			}
#if VRC
			_view.SetMenu(context.Menu as VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu);
#else
			_view.SetMenu(context.Menu);
#endif
		}

		public override void SetParameterHighlight(string name)
		{
			if (_view != null)
			{
				// どのメニュー項目がそのパラメータを動かすのかを目で追えるようにする。
				_view.SetHighlight(name);
			}
		}
	}
}
