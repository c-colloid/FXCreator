using System;
using colloid.FXCreator.Graph;
using colloid.FXCreator.Preview;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// State / SubStateMachine のノード。基底の「タイトル + 副題」に
	/// 3行目（Speed と WriteDefaults）とプレビューを足す
	/// （Docs/FXCreator-Design.md §9 Phase 2 / Phase 5）。
	///
	/// プレビューは <see cref="AvatarPreviewService"/> に<b>要求を出すだけ</b>で、
	/// 描画資源は一切持たない（§5.1 の試作はノードごとにシーンとカメラを作っていた）。
	/// 画面外に出たら要求を取り下げ、ズームアウト中はそもそも要求しない。
	/// </summary>
	public sealed class StateNodeView : FXCNodeView
	{
		/// <summary>ノード内に置くプレビューの一辺（グラフ単位）。</summary>
		private const float PreviewSize = 46f;

		/// <summary>これより縮んだら描かない（§5.2-6）。読めない絵に GPU を使わない。</summary>
		private const float MinZoomForPreview = 0.5f;

		/// <summary>プレビューの解像度。ノードの表示サイズより少し大きめに焼く。</summary>
		private static readonly Vector2Int RenderSize = new Vector2Int(96, 96);

		/// <summary>要求先を差し込むための口。ウィンドウがノード生成時に渡す。</summary>
		public Func<AvatarPreviewService> ServiceProvider;

		/// <summary>この State のプレビューの構え方を引く（サイドカー §4.4）。</summary>
		public Func<AnimatorState, PreviewFraming> FramingProvider;

		private readonly Label _info;
		private readonly VisualElement _previewBox;
		private readonly Image _preview;
		private readonly Label _fallback;

		private bool _isDefault;
		private AnimationClip _clip;
		private bool _isBlendTree;
		private AnimatorState _state;

		public StateNodeView(FXCGraphView owner) : base(owner)
		{
			_info = new Label { pickingMode = PickingMode.Ignore };
			_info.style.fontSize = 9f;
			_info.style.overflow = Overflow.Hidden;
			Body.Add(_info);

			_previewBox = new VisualElement
			{
				pickingMode = PickingMode.Ignore,
				style =
				{
					width = PreviewSize,
					height = PreviewSize,
					flexShrink = 0,
					alignSelf = Align.Center,
					marginRight = 3f,
					justifyContent = Justify.Center,
					alignItems = Align.Center,
					overflow = Overflow.Hidden
				}
			};
			Add(_previewBox);

			_preview = new Image
			{
				pickingMode = PickingMode.Ignore,
				scaleMode = ScaleMode.ScaleAndCrop,
				style = { width = PreviewSize, height = PreviewSize }
			};
			_previewBox.Add(_preview);

			// ズームアウト時とプレビュー不可のときの退避表示（§5.2-6）。
			_fallback = new Label { pickingMode = PickingMode.Ignore };
			_fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
			_fallback.style.fontSize = 9f;
			_fallback.style.width = PreviewSize;
			_fallback.style.height = PreviewSize;
			_previewBox.Add(_fallback);

			// パン・ズーム・カリングの変化はすべて ViewportChanged の後に確定するので、
			// ここ1本で「見えているか」「小さすぎないか」をまとめて見直せる。
			RegisterCallback<AttachToPanelEvent>(_ => Owner.ViewportChanged += OnViewportChanged);
			RegisterCallback<DetachFromPanelEvent>(_ =>
			{
				Owner.ViewportChanged -= OnViewportChanged;
				CancelPreview();
			});
		}

		public override void Bind(IFXCGraphNode node)
		{
			var ac = node as AcGraphNode;
			_isDefault = ac != null && ac.IsDefault;

			string info = ac != null ? ac.Info : null;
			bool hasInfo = !string.IsNullOrEmpty(info);
			_info.text = hasInfo ? info : string.Empty;
			_info.style.display = hasInfo ? DisplayStyle.Flex : DisplayStyle.None;

			_state = ac != null && ac.Ref.Kind == AcNodeKind.State ? ac.Ref.AsState() : null;
			Motion motion = _state != null ? _state.motion : null;
			_isBlendTree = motion is BlendTree;
			AnimationClip clip = motion as AnimationClip;
			if (clip != _clip)
			{
				// 別のクリップになったので、前の絵は捨てて要求し直す。
				CancelPreview();
				_clip = clip;
				_preview.image = null;
			}

			// base.Bind が最後に ApplySelectionStyle を呼ぶので、
			// そこで読む _isDefault / _info は先に入れておく。
			base.Bind(node);

			UpdatePreview();
		}

		#region Preview

		private void OnViewportChanged()
		{
			UpdatePreview();
		}

		/// <summary>
		/// いま要求すべきかを判断して、要求するか取り下げるかを決める。
		/// 可視で・十分大きく・クリップがあるときだけ要求する（§5.2-1）。
		/// </summary>
		private void UpdatePreview()
		{
			AvatarPreviewService service = ServiceProvider != null ? ServiceProvider() : null;
			bool zoomedOut = Owner.Viewport.Zoom < MinZoomForPreview;
			bool canPreview = _clip != null && service != null && service.IsUsable;

			if (Culled || zoomedOut || !canPreview)
			{
				CancelPreview();
				ShowFallback(zoomedOut);
				return;
			}

			PreviewFraming framing = Framing();

			// 選択中のノードだけ再生する（§5.2-5）。他は静止フレームのまま。
			// 再生フレームはキャッシュに入らないので、静止プレビューを押し出さない。
			if (Selected && _clip.length > 0f)
			{
				service.StartPlaying(this, _clip, RenderSize, framing, OnRendered);
				return;
			}
			service.StopPlaying(this);

			// 既に絵があっても要求し直す。キャッシュに当たれば即返るうえ、
			// LRU の先頭に来るので「見えているノードの絵が追い出される」ことがなくなる。
			service.Request(new PreviewRequest
			{
				Owner = this,
				Clip = _clip,
				Time = 0f,
				Size = RenderSize,
				Focus = framing.Focus,
				Fov = framing.Fov,
				OnRendered = OnRendered
			});
		}

		private PreviewFraming Framing()
		{
			if (FramingProvider == null || _state == null)
			{
				return PreviewFraming.Default;
			}
			return FramingProvider(_state);
		}

		private void OnRendered(Texture texture)
		{
			if (texture == null)
			{
				return;
			}
			_preview.image = texture;
			_preview.style.display = DisplayStyle.Flex;
			_fallback.style.display = DisplayStyle.None;

			// 再生中は同じ RenderTexture の中身だけが変わる。Image.image の setter は
			// 参照が同じだと何もしない（＝再描画されない）ので、ここで明示的に汚す。
			// これが無いと、update は回っているのに絵が止まって見える。
			_preview.MarkDirtyRepaint();
		}

		private void CancelPreview()
		{
			AvatarPreviewService service = ServiceProvider != null ? ServiceProvider() : null;
			if (service != null)
			{
				service.CancelAll(this);
				service.StopPlaying(this);
			}
		}

		/// <summary>
		/// プレビューの代わりにクリップ種別を出す。絵が出せない理由を
		/// 「何も出ない」で済ませると、壊れているのか縮んでいるのか分からない。
		/// </summary>
		private void ShowFallback(bool zoomedOut)
		{
			_preview.style.display = DisplayStyle.None;
			_fallback.style.display = DisplayStyle.Flex;

			if (_isBlendTree)
			{
				_fallback.text = "BT";
			}
			else if (_clip != null)
			{
				_fallback.text = zoomedOut ? "·" : "AC";
			}
			else
			{
				_fallback.text = string.Empty;
			}
		}

		#endregion

		protected override void ApplySelectionStyle()
		{
			base.ApplySelectionStyle();

			bool pro = EditorGUIUtility.isProSkin;
			_info.style.color = pro ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.42f, 0.42f, 0.45f);

			_previewBox.style.backgroundColor = pro
				? new Color(0.16f, 0.16f, 0.18f, 1f)
				: new Color(0.72f, 0.72f, 0.75f, 1f);
			_fallback.style.color = pro ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);

			// 既定ステートは標準 Animator ウィンドウでもオレンジ。太字で一目で分かるようにする。
			TitleLabel.style.unityFontStyleAndWeight = _isDefault ? FontStyle.Bold : FontStyle.Normal;

			// 選択が変わるとここが呼ばれる。再生の開始・停止はその流れに乗せる
			// （選択中のノードだけ動く、というのが §5.2-5 の決めごと）。
			UpdatePreview();
		}
	}
}
