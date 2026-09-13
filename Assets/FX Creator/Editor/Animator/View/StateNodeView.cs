using colloid.FXCreator.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// State / SubStateMachine のノード。基底の「タイトル + 副題」に
	/// 3行目（Speed と WriteDefaults）を足しただけ（Docs/FXCreator-Design.md §9 Phase 2）。
	///
	/// Phase 2 は表示のみ。編集 UI（Motion 差し替え等）は Phase 3 の
	/// <c>ElementInspector</c> 側に付く。プレビューは Phase 5。
	/// </summary>
	public sealed class StateNodeView : FXCNodeView
	{
		private readonly Label _info;
		private bool _isDefault;

		public StateNodeView(FXCGraphView owner) : base(owner)
		{
			_info = new Label { pickingMode = PickingMode.Ignore };
			_info.style.fontSize = 9f;
			_info.style.overflow = Overflow.Hidden;
			Body.Add(_info);
		}

		public override void Bind(IFXCGraphNode node)
		{
			var ac = node as AcGraphNode;
			_isDefault = ac != null && ac.IsDefault;

			string info = ac != null ? ac.Info : null;
			bool hasInfo = !string.IsNullOrEmpty(info);
			_info.text = hasInfo ? info : string.Empty;
			_info.style.display = hasInfo ? DisplayStyle.Flex : DisplayStyle.None;

			// base.Bind が最後に ApplySelectionStyle を呼ぶので、
			// そこで読む _isDefault / _info は先に入れておく。
			base.Bind(node);
		}

		protected override void ApplySelectionStyle()
		{
			base.ApplySelectionStyle();

			bool pro = EditorGUIUtility.isProSkin;
			_info.style.color = pro ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.42f, 0.42f, 0.45f);

			// 既定ステートは標準 Animator ウィンドウでもオレンジ。太字で一目で分かるようにする。
			TitleLabel.style.unityFontStyleAndWeight = _isDefault ? FontStyle.Bold : FontStyle.Normal;
		}
	}
}
