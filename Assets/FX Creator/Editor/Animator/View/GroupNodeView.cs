using colloid.FXCreator.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// toggle / switch の畳み込みノード（Docs/FXCreator-Design.md §4.2、資料の中間ノード）。
	/// 資料どおり「ワイヤ上に乗る小さなノード」として、パラメータ名と種類だけを出す。
	///
	/// 実体は遷移群なので、このビューには編集フィールドを置かない。
	/// 分岐の足し引きはポートからのドラッグ、パラメータの変更は
	/// <see cref="ElementInspector"/> が担当する。
	/// </summary>
	public sealed class GroupNodeView : FXCNodeView
	{
		private readonly Label _kind;
		private AcGroupKind _groupKind = AcGroupKind.Toggle;

		public GroupNodeView(FXCGraphView owner) : base(owner)
		{
			// ポートのラベルはノードの外に出さず、ツールチップに任せる（資料の見た目に合わせて小さく保つ）。
			Body.style.paddingLeft = 4f;
			Body.style.paddingRight = 4f;
			Body.style.justifyContent = Justify.Center;

			TitleLabel.style.fontSize = 10f;
			TitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
			TitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			TitleLabel.style.whiteSpace = WhiteSpace.NoWrap;

			SubtitleLabel.style.fontSize = 8f;
			SubtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

			_kind = new Label { pickingMode = PickingMode.Ignore };
			_kind.style.display = DisplayStyle.None;
			Body.Add(_kind);
		}

		public override void Bind(IFXCGraphNode node)
		{
			var ac = node as AcGraphNode;
			if (ac != null && ac.Group != null)
			{
				_groupKind = ac.Group.Kind;
			}
			base.Bind(node);
		}

		protected override void ApplySelectionStyle()
		{
			base.ApplySelectionStyle();

			bool pro = EditorGUIUtility.isProSkin;
			// 通常ノードより角を丸め、少し沈んだ色にして「線の上の飾り」に見せる。
			SetBorderRadius(8f);
			style.backgroundColor = pro
				? new Color(0.20f, 0.20f, 0.23f, 1f)
				: new Color(0.80f, 0.80f, 0.83f, 1f);

			SubtitleLabel.style.color = _groupKind == AcGroupKind.Toggle
				? new Color(0.68f, 0.58f, 0.86f)
				: new Color(0.52f, 0.76f, 0.64f);
		}
	}
}
