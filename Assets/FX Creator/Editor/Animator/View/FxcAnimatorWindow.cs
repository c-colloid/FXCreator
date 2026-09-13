using System.Collections.Generic;
using colloid.FXCreator.Graph;
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
		private VisualElement _breadcrumb;
		private Label _status;

		private ObjectField _avatarField;
		private ObjectField _controllerField;

		private GameObject _avatar;
		private AnimatorController _controller;

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

			_layers = new LayerListView();
			_layers.LayerSelected += OnLayerSelected;
			split.Add(_layers);

			var right = new VisualElement { style = { flexGrow = 1 } };
			split.Add(right);

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

		private void SetAvatar(GameObject avatar)
		{
			_avatar = avatar;
			if (_avatarField != null && _avatarField.value != (Object)avatar)
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

			if (_controllerField != null && _controllerField.value != (Object)controller)
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

		private void UpdateChrome()
		{
			_layers.SetController(_controller, _source.LayerIndex);
			RebuildBreadcrumb();
			UpdateStatus();
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

			_status.text = string.Format(
				"{0} nodes / {1} edges   ·   読み取り専用（編集は Phase 3）",
				_source.Nodes.Count,
				_source.Edges.Count);
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
			return ac.Ref.Kind == AcNodeKind.State || ac.Ref.Kind == AcNodeKind.StateMachine
				? (FXCNodeView)new StateNodeView(owner)
				: new SpecialNodeView(owner);
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
			Object target = ResolveSelectedObject();
			if (target != null)
			{
				Selection.activeObject = target;
			}
		}

		private Object ResolveSelectedObject()
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
