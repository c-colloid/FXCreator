using System;
using System.Collections.Generic;
using colloid.FXCreator.Graph;
using colloid.FXCreator.Preview;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if VRC
using VRC.SDK3.Avatars.Components;
#endif

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// FX Creator の Animator エディタ（Docs/FXCreator-Design.md ⑤ / v0.1 本体）。
	///
	/// Phase 2 の到達点は<b>読み専用グラフ</b>。アバターか AnimatorController を
	/// 指定すると、レイヤーを選んでノードグラフとして眺められる。
	/// 編集（Phase 3）・toggle/switch 畳み込み（Phase 4）・プレビュー（Phase 5）は未実装。
	///
	/// ターゲットの解決はここに素朴に持っている。Direct / NDMF-MA の切替
	/// （<c>IFxTarget</c>）は Phase 7 で差し込むので、その時にこの
	/// <see cref="ResolveFromAvatar"/> が <c>FxTargetResolver</c> へ移る。
	/// </summary>
	public class FxcAnimatorWindow : EditorWindow
	{
		private AcGraphSource _source;
		private AcChangeWatcher _watcher;

		private FXCGraphView _graph;
		private LayerListView _layers;
		private ParameterListView _parameters;
		private Vrc.VrcParameterPanel _vrcParameters;
		private Vrc.VrcMenuPanel _vrcMenu;
		private ElementInspector _inspector;
		private VisualElement _breadcrumb;
		private Label _status;

		private ObjectField _avatarField;
		private ObjectField _controllerField;

		private GameObject _avatar;
		private AnimatorController _controller;
		private AvatarPreviewService _preview;

		/// <summary>初回レイアウト後に一度だけ Frame All する。</summary>
		private bool _framePending;

		[MenuItem("Tools/FXCreator/Animator Editor", priority = 1002)]
		public static void ShowWindow()
		{
			FxcAnimatorWindow window = GetWindow<FxcAnimatorWindow>();
			window.titleContent = new GUIContent("FXC Animator");
			window.minSize = new Vector2(760f, 480f);
		}

		public void CreateGUI()
		{
			_source = new AcGraphSource();

			_watcher = new AcChangeWatcher();
			_watcher.Changed += OnExternalChange;

			VisualElement root = rootVisualElement;
			root.Add(BuildToolbar());

			var split = new TwoPaneSplitView(0, 210f, TwoPaneSplitViewOrientation.Horizontal);
			split.style.flexGrow = 1;
			root.Add(split);

			// 資料の左サイドバーはレイヤー一覧と VAR の2段（§6.1）。
			var sidebar = new TwoPaneSplitView(0, 200f, TwoPaneSplitViewOrientation.Vertical);
			sidebar.style.flexGrow = 1;
			split.Add(sidebar);

			_layers = new LayerListView();
			_layers.LayerSelected += OnLayerSelected;
			sidebar.Add(_layers);

			_parameters = new ParameterListView();
			sidebar.Add(_parameters);

			// グラフ列と詳細パネルをもう一段の分割で並べる（§11-5 の入れ子検証）。
			// 固定するのは右側（index 1）で、グラフ側が伸び縮みする。
			var rightSplit = new TwoPaneSplitView(1, 260f, TwoPaneSplitViewOrientation.Horizontal);
			rightSplit.style.flexGrow = 1;
			split.Add(rightSplit);

			// 資料の下パネル（parameter / menu）をグラフの下に置く（§6.2 / §6.3）。
			var center = new TwoPaneSplitView(1, 150f, TwoPaneSplitViewOrientation.Vertical);
			center.style.flexGrow = 1;
			rightSplit.Add(center);

			var right = new VisualElement { style = { flexGrow = 1 } };
			center.Add(right);

			center.Add(BuildVrcPanels());

			_inspector = new ElementInspector();
			// 畳み込みの解除はサイドカーを触るのでソース側に任せる。
			_inspector.SetGroupExpanded = (owner, kind, parameter, expanded) =>
				_source.SetGroupExpanded(owner, kind, parameter, expanded);
			_inspector.GetPreviewFraming = state => _source.GetPreviewFraming(state);
			_inspector.SetPreviewFraming = (state, framing) => _source.SetPreviewFraming(state, framing);
			rightSplit.Add(_inspector);

			_breadcrumb = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexShrink = 0,
					height = 20f,
					paddingLeft = 4f
				}
			};
			right.Add(_breadcrumb);

			_graph = new FXCGraphView();
			_graph.NodeViewFactory = CreateNodeView;
			_graph.NodeActivated += OnNodeActivated;
			_graph.Selection.Changed += OnGraphSelectionChanged;
			right.Add(_graph);

			_status = new Label(string.Empty)
			{
				style =
				{
					flexShrink = 0,
					height = 18f,
					paddingLeft = 6f,
					unityTextAlign = TextAnchor.MiddleLeft,
					color = new Color(0.6f, 0.6f, 0.64f)
				}
			};
			right.Add(_status);

			// 編集が確定すると AcGraphSource が自分で作り直して Changed を出す。
			// グラフはそれをビューに反映するが、ステータスとパンくずはこちらの担当。
			_source.Changed += UpdateChrome;

			_graph.SetSource(_source);
			_graph.RegisterCallback<GeometryChangedEvent>(OnGraphGeometryChanged);

			// 起動時にヒエラルキーで選ばれているアバターを拾っておく。
			if (Selection.activeGameObject != null)
			{
				SetAvatar(Selection.activeGameObject);
			}
			UpdateChrome();
		}

		private void OnDisable()
		{
			if (_watcher != null)
			{
				_watcher.Changed -= OnExternalChange;
				_watcher.Dispose();
				_watcher = null;
			}
			if (_source != null)
			{
				// AcEdit.AfterEdit は静的イベント。外さないとドメインリロードまで
				// 死んだウィンドウのソースが購読し続ける。
				_source.Dispose();
				_source = null;
			}
			// プレビューシーンと RenderTexture を畳む。放置すると GPU メモリが残る。
			AvatarPreviewService.Shutdown();
			_preview = null;
		}

		private void OnFocus()
		{
			// 標準 Animator ウィンドウや外部ツールでの変更に追いつく（§4.5）。
			_watcher?.NotifyFocus();
		}

		#region Toolbar

		private VisualElement BuildToolbar()
		{
			var bar = new Toolbar();

			_avatarField = new ObjectField("Avatar")
			{
				objectType = typeof(GameObject),
				allowSceneObjects = true
			};
			_avatarField.style.width = 280f;
			_avatarField.labelElement.style.minWidth = 46f;
			_avatarField.RegisterValueChangedCallback(evt => SetAvatar(evt.newValue as GameObject));
			bar.Add(_avatarField);

			_controllerField = new ObjectField("Controller")
			{
				objectType = typeof(AnimatorController),
				allowSceneObjects = false
			};
			_controllerField.style.width = 320f;
			_controllerField.labelElement.style.minWidth = 66f;
			_controllerField.RegisterValueChangedCallback(
				evt => SetController(evt.newValue as AnimatorController, keepLayer: false));
			bar.Add(_controllerField);

			bar.Add(new ToolbarButton(() => _graph.FrameAll()) { text = "Frame All" });
			bar.Add(new ToolbarButton(Rebuild) { text = "Refresh" });

			return bar;
		}

		#endregion

		#region Target resolution

		/// <summary>
		/// ノード内プレビューの描画元（§5）。アバターが決まるまでは null で、
		/// その間ノードはクリップ種別アイコンに退避する。
		/// </summary>
		private AvatarPreviewService PreviewService()
		{
			return _preview != null && _preview.IsUsable ? _preview : null;
		}

		private void SetAvatar(GameObject avatar)
		{
			_avatar = avatar;
			// プレビューはアバター1体につき1シーン。差し替わったら前のものは畳まれる。
			_preview = AvatarPreviewService.ForAvatar(avatar);
			if (_avatarField != null && _avatarField.value != (UnityEngine.Object)avatar)
			{
				_avatarField.SetValueWithoutNotify(avatar);
			}

			AnimatorController resolved = ResolveFromAvatar(avatar);
			if (resolved != null)
			{
				SetController(resolved, keepLayer: false);
			}
			else
			{
				UpdateChrome();
			}
		}

		/// <summary>
		/// アバターから編集対象の Controller を引く。
		/// VRC アバターなら FX レイヤー、そうでなければ素の Animator のもの。
		/// Phase 7 で <c>IFxTarget</c> に置き換わる暫定版。
		/// </summary>
		private static AnimatorController ResolveFromAvatar(GameObject avatar)
		{
			if (avatar == null)
			{
				return null;
			}

#if VRC
			var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
			if (descriptor != null)
			{
				VRCAvatarDescriptor.CustomAnimLayer[] layers = descriptor.baseAnimationLayers;
				for (int i = 0; i < layers.Length; i++)
				{
					if (layers[i].type != VRCAvatarDescriptor.AnimLayerType.FX || layers[i].isDefault)
					{
						continue;
					}
					var controller = layers[i].animatorController as AnimatorController;
					if (controller != null)
					{
						return controller;
					}
				}
			}
#endif

			var animator = avatar.GetComponent<UnityEngine.Animator>();
			return animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
		}

		private void SetController(AnimatorController controller, bool keepLayer)
		{
			_controller = controller;

			if (_controllerField != null && _controllerField.value != (UnityEngine.Object)controller)
			{
				_controllerField.SetValueWithoutNotify(controller);
			}

			// レイヤーを保つ場合でも、差し替わった Controller にその番号があるとは限らない。
			AnimatorControllerLayer[] layers = controller != null ? controller.layers : null;
			int layerCount = layers != null ? layers.Length : 0;
			int layer = keepLayer && layerCount > 0
				? Mathf.Clamp(_source.LayerIndex, 0, layerCount - 1)
				: 0;

			_watcher.SetController(controller);
			_source.SetController(controller);
			if (layer > 0)
			{
				_source.SetLayer(layer);
			}

			if (controller != null)
			{
				RequestFrameAll();
			}
			UpdateChrome();
		}

		#endregion

		#region Rebuild / chrome

		private void OnExternalChange()
		{
			// ウィンドウが閉じられた後に delayCall が届くことがある。
			if (_source == null)
			{
				return;
			}
			Rebuild();
		}

		private void Rebuild()
		{
			// Controller 自体が差し替わった（Undo でアバターの参照が戻った等）場合に追いつく。
			AnimatorController fromAvatar = ResolveFromAvatar(_avatar);
			if (fromAvatar != null && fromAvatar != _controller)
			{
				SetController(fromAvatar, keepLayer: true);
				return;
			}

			// Refresh が選択とビューポートを保ったまま全再構築する（§4.5）。
			_source.Refresh();
			UpdateChrome();
		}

		private void OnLayerSelected(int index)
		{
			_source.SetLayer(index);
			RequestFrameAll();
			UpdateChrome();
		}

		/// <summary>
		/// 全体を画面に収める。まだレイアウトが確定していない（初回表示）ときは
		/// <see cref="GeometryChangedEvent"/> まで持ち越す。
		/// GeometryChangedEvent はレイアウトが変わったときしか来ないので、
		/// レイヤー切替のように大きさが変わらない場面ではその場で実行する必要がある。
		/// </summary>
		private void RequestFrameAll()
		{
			Rect view = _graph != null ? _graph.contentRect : new Rect();
			if (view.width > 1f && view.height > 1f)
			{
				_framePending = false;
				_graph.FrameAll();
				return;
			}
			_framePending = true;
		}

		/// <summary>下パネル。parameter / menu をタブで切り替える（§6.2 / §6.3）。</summary>
		private VisualElement BuildVrcPanels()
		{
			var host = new VisualElement { style = { flexGrow = 1 } };

			var tabs = new VisualElement
			{
				style = { flexDirection = FlexDirection.Row, flexShrink = 0 }
			};
			host.Add(tabs);

			_vrcParameters = new Vrc.VrcParameterPanel();
			_vrcMenu = new Vrc.VrcMenuPanel();
			host.Add(_vrcParameters);
			host.Add(_vrcMenu);

			var parameterTab = new Button { text = "parameter" };
			var menuTab = new Button { text = "menu" };
			Action<bool> select = showParameters =>
			{
				_vrcParameters.style.display = showParameters ? DisplayStyle.Flex : DisplayStyle.None;
				_vrcMenu.style.display = showParameters ? DisplayStyle.None : DisplayStyle.Flex;
				parameterTab.style.unityFontStyleAndWeight = showParameters ? FontStyle.Bold : FontStyle.Normal;
				menuTab.style.unityFontStyleAndWeight = showParameters ? FontStyle.Normal : FontStyle.Bold;
			};
			parameterTab.clicked += () => select(true);
			menuTab.clicked += () => select(false);
			tabs.Add(parameterTab);
			tabs.Add(menuTab);
			select(true);

			return host;
		}

		private void UpdateChrome()
		{
			_layers.SetController(_controller, _source.LayerIndex);
			_parameters.SetController(_controller, !_source.CanEdit);
			UpdateVrcPanels();
			RebuildBreadcrumb();
			UpdateStatus();

			// 畳み込みノードは再構築のたびに別オブジェクトになるので、選択が残っていれば
			// 取り直す（分岐の増減がそのまま見えるように）。
			AcTransitionGroup group = ResolveSelectedGroup();
			if (group != null)
			{
				_inspector.ShowGroup(_controller, group);
				return;
			}
			if (_inspector.IsShowingGroup)
			{
				// グループが消えた（Expand した、分岐を全部消した）。
				_inspector.ShowNothing();
				return;
			}

			// 対象は変えずに値だけ取り直す。作り直すと入力中のフィールドから
			// フォーカスが飛ぶので、Undo や外部変更のあとでもここは値同期に留める。
			_inspector.SyncValues();
		}

		/// <summary>
		/// 下パネルへアバターの Expression 資産を流す。
		/// Phase 7 で <c>IFxTarget</c> が入ったら、その解決結果に置き換える。
		/// </summary>
		private void UpdateVrcPanels()
		{
#if VRC
			VRCAvatarDescriptor descriptor = _avatar != null
				? _avatar.GetComponent<VRCAvatarDescriptor>()
				: null;
			_vrcParameters.SetTarget(_controller, descriptor != null ? descriptor.expressionParameters : null);
			_vrcMenu.SetMenu(descriptor != null ? descriptor.expressionsMenu : null);
#else
			_vrcParameters.SetTarget(_controller, null);
			_vrcMenu.SetMenu(null);
#endif
		}

		private void RebuildBreadcrumb()
		{
			_breadcrumb.Clear();

			IReadOnlyList<AnimatorStateMachine> path = _source.Path;
			for (int i = 0; i < path.Count; i++)
			{
				if (i > 0)
				{
					var sep = new Label("›");
					sep.style.marginLeft = 2f;
					sep.style.marginRight = 2f;
					sep.style.color = new Color(0.55f, 0.55f, 0.58f);
					_breadcrumb.Add(sep);
				}

				bool last = i == path.Count - 1;
				string name = path[i] != null ? path[i].name : "<missing>";
				if (last)
				{
					var here = new Label(name);
					here.style.unityFontStyleAndWeight = FontStyle.Bold;
					_breadcrumb.Add(here);
					continue;
				}

				int depth = i;
				var button = new Button(() => { _source.GoToDepth(depth); RequestFrameAll(); UpdateChrome(); })
				{
					text = name
				};
				button.style.marginLeft = 0f;
				button.style.marginRight = 0f;
				button.style.paddingLeft = 4f;
				button.style.paddingRight = 4f;
				_breadcrumb.Add(button);
			}
		}

		private void UpdateStatus()
		{
			if (_controller == null)
			{
				_status.text = _avatar != null
					? "このアバターから FX レイヤーの AnimatorController を解決できませんでした"
					: "アバターか AnimatorController を指定してください";
				return;
			}

			if (_source.Current == null)
			{
				_status.text = "レイヤー " + _source.LayerIndex + " にステートマシンがありません";
				return;
			}

			string mode = _source.CanEdit
				? "右クリックで追加 · ポートからドラッグで遷移 · Delete で削除"
				: (_source.ReadOnlyReason ?? "読み取り専用");

			_status.text = string.Format(
				"{0} nodes / {1} edges   ·   {2}",
				_source.Nodes.Count,
				_source.Edges.Count,
				mode);
		}

		private void OnGraphGeometryChanged(GeometryChangedEvent evt)
		{
			if (!_framePending)
			{
				return;
			}
			_framePending = false;
			_graph.FrameAll();
		}

		#endregion

		#region Graph callbacks

		private FXCNodeView CreateNodeView(FXCGraphView owner, IFXCGraphNode node)
		{
			var ac = node as AcGraphNode;
			if (ac == null)
			{
				return null;
			}
			switch (ac.Ref.Kind)
			{
				case AcNodeKind.State:
				case AcNodeKind.StateMachine:
					return new StateNodeView(owner)
					{
						ServiceProvider = PreviewService,
						FramingProvider = state => _source.GetPreviewFraming(state)
					};
				case AcNodeKind.Group:
					return new GroupNodeView(owner);
				default:
					return new SpecialNodeView(owner);
			}
		}

		/// <summary>ダブルクリック: サブステートマシンへ潜る / (Up) で戻る。</summary>
		private void OnNodeActivated(string nodeId)
		{
			AcGraphNode node;
			if (!_source.TryGetNode(nodeId, out node))
			{
				return;
			}

			if (node.Ref.Kind == AcNodeKind.StateMachine)
			{
				_source.EnterStateMachine(node.Ref.AsStateMachine());
				RequestFrameAll();
				UpdateChrome();
				return;
			}

			if (node.Ref.Kind == AcNodeKind.Parent)
			{
				_source.GoUp();
				RequestFrameAll();
				UpdateChrome();
			}
		}

		/// <summary>
		/// グラフの選択を Unity の選択へ流す。State も Transition も Controller の
		/// サブアセットなので、これだけで標準の Inspector が中身を出してくれる。
		/// Phase 2 が読み取り専用でも、値の確認はここから行える（R4 の「表示のみ・素通し」）。
		/// </summary>
		private void OnGraphSelectionChanged()
		{
			// 畳み込みノードは Unity 側に実体が無いので、先に見る。
			AcTransitionGroup group = ResolveSelectedGroup();
			if (group != null)
			{
				_inspector.ShowGroup(_controller, group);
				return;
			}

			UnityEngine.Object target = ResolveSelectedObject();

			var state = target as AnimatorState;
			var transition = target as AnimatorTransitionBase;
			if (state != null)
			{
				_inspector.ShowState(_controller, state);
			}
			else if (transition != null)
			{
				_inspector.ShowTransition(_controller, transition);
			}
			else
			{
				_inspector.ShowNothing();
			}

			if (target != null)
			{
				Selection.activeObject = target;
			}
		}

		private AcTransitionGroup ResolveSelectedGroup()
		{
			if (_source == null || _graph.Selection.Nodes.Count != 1 || _graph.Selection.Edges.Count != 0)
			{
				return null;
			}
			foreach (string nodeId in _graph.Selection.Nodes)
			{
				AcGraphNode node;
				if (_source.TryGetNode(nodeId, out node) && node.Ref.Kind == AcNodeKind.Group)
				{
					return node.Group;
				}
			}
			return null;
		}

		private UnityEngine.Object ResolveSelectedObject()
		{
			if (_source == null)
			{
				return null;
			}

			if (_graph.Selection.Nodes.Count == 1 && _graph.Selection.Edges.Count == 0)
			{
				foreach (string nodeId in _graph.Selection.Nodes)
				{
					AcGraphNode node;
					if (_source.TryGetNode(nodeId, out node)
						&& (node.Ref.Kind == AcNodeKind.State || node.Ref.Kind == AcNodeKind.StateMachine))
					{
						return node.Ref.Target;
					}
				}
				return null;
			}

			if (_graph.Selection.Edges.Count == 1 && _graph.Selection.Nodes.Count == 0)
			{
				foreach (string edgeId in _graph.Selection.Edges)
				{
					AnimatorTransitionBase transition;
					if (_source.TryGetTransition(edgeId, out transition))
					{
						return transition;
					}
				}
			}

			return null;
		}

		#endregion
	}
}
