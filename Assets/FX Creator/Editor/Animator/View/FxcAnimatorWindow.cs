using System;
using System.Collections.Generic;
using colloid.FXCreator.Graph;
using colloid.FXCreator.Preview;
using colloid.FXCreator.Targeting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// FX Creator の Animator エディタ（Docs/FXCreator-Design.md ⑤ / v0.1 本体）。
	///
	/// アバターを選ぶと <see cref="IFxTarget"/> が編集対象の Controller を解決し、
	/// レイヤーを選んでノードグラフとして編集できる。Direct（直接編集）と
	/// NDMF(MA)（非破壊マージ）はツールバーの Mode で切り替える（D1 / §2.3）。
	/// </summary>
	public class FxcAnimatorWindow : EditorWindow, UnityEditor.Overlays.ISupportsOverlays
	{
		private AcGraphSource _source;
		private AcChangeWatcher _watcher;

		private FXCGraphView _graph;
		private LayerListView _layers;



		private ElementInspector _inspector;
		private VisualElement _breadcrumb;
		private Label _status;

		private ObjectField _avatarField;
		private ObjectField _controllerField;
		private ToolbarMenu _modeMenu;

		private GameObject _avatar;
		private AnimatorController _controller;
		private AvatarPreviewService _preview;

		/// <summary>いま選べるモードと、選ばれているモード（§2.3）。</summary>
		private List<IFxTarget> _targets = new List<IFxTarget>();
		private IFxTarget _target;

		/// <summary>
		/// ツールバーで Controller を直に指定された。
		/// このときターゲットの <c>ResolveController</c> で上書きしない
		/// （アバターに紐づかない Controller を眺めたい場面がある）。
		/// </summary>
		private bool _manualController;

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

			// 編集が確定したらターゲットへ知らせる（Direct は SetDirty、
			// NDMF(MA) は MA コンポーネントのパラメータ定義を同期する）。
			AcEdit.AfterEdit += OnAfterEdit;

			VisualElement root = rootVisualElement;
			root.Add(BuildToolbar());

			// VAR / parameter / menu は Overlay（フローティング）へ移した（§6）。
			// 常設していたときはグラフがウィンドウの 29% しか残らなかった。
			// ここに残すのは、常に要るレイヤー一覧と選択中の詳細だけ。
			var split = new TwoPaneSplitView(0, 190f, TwoPaneSplitViewOrientation.Horizontal);
			split.style.flexGrow = 1;
			root.Add(split);

			_layers = new LayerListView();
			_layers.LayerSelected += OnLayerSelected;
			split.Add(_layers);

			var rightSplit = new TwoPaneSplitView(1, 260f, TwoPaneSplitViewOrientation.Horizontal);
			rightSplit.style.flexGrow = 1;
			split.Add(rightSplit);

			var right = new VisualElement { style = { flexGrow = 1 } };
			rightSplit.Add(right);

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
					color = FxcPanelLayout.PlaceholderColor
				}
			};
			right.Add(_status);

			// 編集が確定すると AcGraphSource が自分で作り直して Changed を出す。
			// グラフはそれをビューに反映するが、ステータスとパンくずはこちらの担当。
			_source.Changed += UpdateChrome;

			_graph.SetSource(_source);
			_graph.RegisterCallback<GeometryChangedEvent>(OnGraphGeometryChanged);

			// 起動時にヒエラルキーで選ばれているアバターを拾っておく。
			// ここは<b>ユーザーが指定したわけではない</b>ので、作成や複製は提案しない
			// （ウィンドウを開いただけでダイアログが出るのは論外）。
			if (Selection.activeGameObject != null)
			{
				SetAvatar(Selection.activeGameObject, interactive: false);
			}
			UpdateChrome();
		}

		private void OnDisable()
		{
			AcEdit.AfterEdit -= OnAfterEdit;
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

			// シーンから候補を拾う口。ヒエラルキーで探して D&D するより速い。
			bar.Add(BuildAvatarPicker());

			_modeMenu = new ToolbarMenu { text = "Mode" };
			_modeMenu.tooltip = "編集対象の解決と適用先を切り替えます（Direct / NDMF(MA)）";
			bar.Add(_modeMenu);

			_controllerField = new ObjectField("Controller")
			{
				objectType = typeof(AnimatorController),
				allowSceneObjects = false
			};
			_controllerField.style.width = 320f;
			_controllerField.labelElement.style.minWidth = 66f;
			_controllerField.RegisterValueChangedCallback(evt =>
			{
				// ユーザーが直に指したものは、以降の再構築で上書きしない。
				_manualController = true;
				SetController(evt.newValue as AnimatorController, keepLayer: false);
			});
			bar.Add(_controllerField);

			bar.Add(new ToolbarButton(() => _graph.FrameAll()) { text = "Frame All" });
			bar.Add(new ToolbarButton(Rebuild) { text = "Refresh" });
			bar.Add(BuildPanelMenu());

			return bar;
		}

		/// <summary>シーンにあるアバター候補から選ぶドロップダウン（§2.1）。</summary>
		private VisualElement BuildAvatarPicker()
		{
			var menu = new ToolbarMenu { text = "▾" };
			menu.tooltip = "シーンのアバターから選ぶ";
			menu.style.width = 24f;

			// 中身は開くたびに作り直す。シーンは編集中に変わる。
			menu.RegisterCallback<PointerDownEvent>(_ =>
			{
				// DropdownMenu は使い回されるので、一度空にしてから積む。
				for (int i = menu.menu.MenuItems().Count - 1; i >= 0; i--)
				{
					menu.menu.RemoveItemAt(i);
				}

				List<GameObject> avatars = FxTargetResolver.FindAvatars();
				if (avatars.Count == 0)
				{
					menu.menu.AppendAction("（シーンにアバターがありません）", _2 => { }, DropdownMenuAction.Status.Disabled);
					return;
				}
				for (int i = 0; i < avatars.Count; i++)
				{
					GameObject avatar = avatars[i];
					menu.menu.AppendAction(
						avatar.name,
						_2 => SetAvatar(avatar),
						_2 => avatar == _avatar
							? DropdownMenuAction.Status.Checked
							: DropdownMenuAction.Status.Normal);
				}
			}, TrickleDown.TrickleDown);

			return menu;
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

		/// <param name="interactive">
		/// 支度のできていないターゲットに作成・複製を提案してよいか。
		/// ユーザーが自分でアバターを指したときだけ true にする。
		/// </param>
		private void SetAvatar(GameObject avatar, bool interactive = true)
		{
			_avatar = avatar;
			_manualController = false;
			// プレビューはアバター1体につき1シーン。差し替わったら前のものは畳まれる。
			_preview = AvatarPreviewService.ForAvatar(avatar);
			if (_avatarField != null && _avatarField.value != (UnityEngine.Object)avatar)
			{
				_avatarField.SetValueWithoutNotify(avatar);
			}

			_targets = FxTargetResolver.CreateTargets(avatar);
			_target = FxTargetResolver.ChooseDefault(_targets, FxTargetResolver.PreferredMode);
			RebuildModeMenu();

			ApplyTarget(keepLayer: false, interactive: interactive);
		}

		/// <summary>ユーザーがモードを選んだ。</summary>
		private void SetMode(IFxTarget target)
		{
			if (target == null || target == _target)
			{
				return;
			}
			_target = target;
			_manualController = false;
			FxTargetResolver.PreferredMode = target.Id;
			RebuildModeMenu();
			ApplyTarget(keepLayer: false, interactive: true);
		}

		/// <summary>
		/// 選ばれているターゲットから Controller を引いて表示する。
		///
		/// <paramref name="interactive"/> が true のときだけ、支度のできていない
		/// ターゲットに <c>TryPrepareForEditing</c> を投げる（＝ダイアログが出る）。
		/// Undo や外部変更からの再構築で呼ぶときは必ず false にすること。
		/// 毎回聞かれるのは、編集を邪魔する以外の何物でもない。
		/// </summary>
		private void ApplyTarget(bool keepLayer, bool interactive)
		{
			if (_target == null)
			{
				SetController(null, keepLayer);
				return;
			}

			AnimatorController resolved = _target.ResolveController();

			if (interactive)
			{
				string writeReason;
				bool needsPrepare = resolved == null
					|| !AcControllerAccess.IsWritable(resolved, out writeReason);
				if (needsPrepare)
				{
					string reason;
					if (_target.TryPrepareForEditing(true, out reason))
					{
						resolved = _target.ResolveController();
					}
					// 断られた／できなかった場合はそのまま進む。
					// 解決できた Controller があれば読み取り専用で開く（§2.3）。
				}
			}

			SetController(resolved, keepLayer);
		}

		private void RebuildModeMenu()
		{
			if (_modeMenu == null)
			{
				return;
			}

			for (int i = _modeMenu.menu.MenuItems().Count - 1; i >= 0; i--)
			{
				_modeMenu.menu.RemoveItemAt(i);
			}

			if (_targets.Count == 0)
			{
				_modeMenu.text = "Mode";
				_modeMenu.menu.AppendAction(
					"（アバターを選んでください）", _ => { }, DropdownMenuAction.Status.Disabled);
				return;
			}

			for (int i = 0; i < _targets.Count; i++)
			{
				IFxTarget target = _targets[i];
				string reason;
				bool available = target.IsAvailable(out reason);

				// 使えないモードは理由ごと並べる。黙って消すと
				// 「MA を入れたのに出てこない」の切り分けができない。
				string label = available ? target.DisplayName : target.DisplayName + "（" + reason + "）";

				_modeMenu.menu.AppendAction(
					label,
					_ => SetMode(target),
					_ =>
					{
						if (!available)
						{
							return DropdownMenuAction.Status.Disabled;
						}
						return target == _target
							? DropdownMenuAction.Status.Checked
							: DropdownMenuAction.Status.Normal;
					});
			}

			_modeMenu.text = _target != null ? _target.DisplayName : "Mode";
			_modeMenu.tooltip = _target != null ? _target.Description : "編集対象の解決と適用先";
		}

		/// <summary>編集確定をターゲットへ流す（§2.3 の <c>OnAfterEdit</c>）。</summary>
		private void OnAfterEdit(AcEditReport report)
		{
			if (_target == null || report == null || report.Controller != _controller)
			{
				return;
			}
			_target.OnAfterEdit(report);
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
			// ここはダイアログ厳禁。Undo・フォーカス・アセット変更のたびに通る。
			if (!_manualController && _target != null)
			{
				AnimatorController resolved = _target.ResolveController();
				if (resolved != null && resolved != _controller)
				{
					SetController(resolved, keepLayer: true);
					return;
				}
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

		#region Floating panels (Overlay)

		/// <summary>
		/// 出ているフローティングパネル（§6）。Overlay は Unity が生成・破棄するので、
		/// ウィンドウ側は「いま出ているもの」だけを持ち、表示対象が変わったら配る。
		/// </summary>
		private readonly List<FxcPanelOverlay> _panels = new List<FxcPanelOverlay>();

		internal void RegisterPanel(FxcPanelOverlay panel)
		{
			if (panel == null || _panels.Contains(panel))
			{
				return;
			}
			_panels.Add(panel);
			PushTargetTo(panel);
		}

		internal void UnregisterPanel(FxcPanelOverlay panel)
		{
			_panels.Remove(panel);
		}

		/// <summary>いま選ばれているパラメータ名（パネルをまたいで共有する）。</summary>
		private string _selectedParameter;

		/// <summary>
		/// どれかのパネルでパラメータが選ばれた。3枚に配って対応する行を光らせる。
		/// VAR・parameter・menu は名前で結ばれているだけなので、
		/// 対応関係が目で追えないと差分や「効かないメニュー」の意味が分からない（§6.2）。
		/// </summary>
		internal void OnParameterSelected(string name)
		{
			_selectedParameter = name;
			for (int i = 0; i < _panels.Count; i++)
			{
				_panels[i].SetParameterHighlight(name);
			}
		}

		/// <summary>再表示などで中身が作り直されたパネルに、表示対象を入れ直す。</summary>
		internal void RefreshPanel(FxcPanelOverlay panel)
		{
			if (panel != null)
			{
				PushTargetTo(panel);
			}
		}

		private FxcPanelOverlay FindPanel(string id)
		{
			for (int i = 0; i < _panels.Count; i++)
			{
				if (_panels[i].id == id)
				{
					return _panels[i];
				}
			}
			return null;
		}

		/// <summary>
		/// パネルの表示切替。Unity 標準のオーバーレイメニューだけだと
		/// 一度隠したパネルの戻し方が分からないので、ツールバーにも口を用意する。
		/// </summary>
		private VisualElement BuildPanelMenu()
		{
			var menu = new ToolbarMenu { text = "Panels" };

			void Add(string label, string id)
			{
				menu.menu.AppendAction(
					label,
					_ =>
					{
						FxcPanelOverlay panel = FindPanel(id);
						if (panel != null)
						{
							panel.displayed = !panel.displayed;
						}
					},
					_ =>
					{
						FxcPanelOverlay panel = FindPanel(id);
						if (panel == null)
						{
							return DropdownMenuAction.Status.Disabled;
						}
						return panel.displayed
							? DropdownMenuAction.Status.Checked
							: DropdownMenuAction.Status.Normal;
					});
			}

			Add("VAR", VarOverlay.OverlayId);
			Add("parameter", VrcParameterOverlay.OverlayId);
			Add("menu", VrcMenuOverlay.OverlayId);
			return menu;
		}

		private void PushTargetTo(FxcPanelOverlay panel)
		{
			// パネルはウィンドウより先に作られることがある（レイアウト復元時）。
			// その場合 _source がまだ無いので、編集可否は「不可」に倒しておく。
			var context = new FxcPanelContext
			{
				Controller = _controller,
				ReadOnly = _source == null || !_source.CanEdit,
				Avatar = _avatar,
				ParameterStore = _target != null ? _target.ResolveParameterStore() : null,
				Menu = _target != null ? _target.ResolveMenu() : null,
			};
			panel.ApplyTarget(context);
			// 中身を作り直すと強調は消える。選択は保つ（§6 の「空のパネルが出る」と同じ話）。
			panel.SetParameterHighlight(_selectedParameter);
		}

		private void PushTargetToPanels()
		{
			for (int i = 0; i < _panels.Count; i++)
			{
				PushTargetTo(_panels[i]);
			}
		}

		#endregion

		private void UpdateChrome()
		{
			_layers.SetController(_controller, _source.LayerIndex);
			PushTargetToPanels();
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
					sep.style.color = FxcPanelLayout.PlaceholderColor;
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
				if (_avatar == null)
				{
					_status.text = "アバターか AnimatorController を指定してください";
					return;
				}

				// 解決できない理由はモードによって違う。使えないモードが
				// 選ばれているならその理由を、そうでなければ支度の仕方を出す。
				string reason;
				if (_target == null)
				{
					_status.text = "このアバターで使えるモードがありません";
				}
				else if (!_target.IsAvailable(out reason))
				{
					_status.text = _target.DisplayName + " は使えません: " + reason;
				}
				else
				{
					_status.text = _target.DisplayName
						+ " の編集対象がまだありません　·　Mode から選び直すか、もう一度アバターを指定すると作成できます";
				}
				return;
			}

			if (_source.Current == null)
			{
				_status.text = "レイヤー " + _source.LayerIndex + " にステートマシンがありません";
				return;
			}

			// parameter / menu は既定で隠れている。出し方が分からないと存在に気づけないので、
			// Unity 標準のオーバーレイメニューの開き方をここに書いておく。
			string mode = _source.CanEdit
				? "右クリックで追加 · ポートからドラッグで遷移 · Delete で削除"
				: (_source.ReadOnlyReason ?? "読み取り専用");

			string targetName = _manualController
				? "手動"
				: (_target != null ? _target.DisplayName : "手動");

			_status.text = string.Format(
				"[{0}]   {1} nodes / {2} edges   ·   {3}",
				targetName,
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
