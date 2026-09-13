using colloid.FXCreator.Graph;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// Any State / Entry / Exit / (Up) のノード。Unity 側に実体が無く位置だけ持つ
	/// 特殊ノード（Docs/FXCreator-Design.md §4.1）を、標準 Animator ウィンドウと
	/// 同じく色付きのピル型で描く。
	///
	/// 基底のアクセント帯は使わず、<see cref="IFXCGraphNode.AccentColor"/> を
	/// 背景の塗りとして使う。
	/// </summary>
	public sealed class SpecialNodeView : FXCNodeView
	{
		private Color _fill = new Color(0.4f, 0.4f, 0.45f);

		public SpecialNodeView(FXCGraphView owner) : base(owner)
		{
			Accent.style.display = DisplayStyle.None;
			Body.style.justifyContent = Justify.Center;
			Body.style.paddingLeft = 4f;
			Body.style.paddingRight = 4f;
			Body.style.paddingTop = 0f;

			TitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
			TitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			TitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
		}

		public override void Bind(IFXCGraphNode node)
		{
			_fill = node.AccentColor;
			base.Bind(node);
		}

		protected override void ApplySelectionStyle()
		{
			base.ApplySelectionStyle();

			style.backgroundColor = _fill;
			// 高さの半分を半径にすると端が半円になる（ピル型）。
			SetBorderRadius(GraphSize.y * 0.5f);
			TitleLabel.style.color = new Color(0.96f, 0.96f, 0.98f);

			if (!Selected)
			{
				// 非選択時は塗りをわずかに締める色で縁取る（選択時の青枠は基底のまま残す）。
				// Color の乗算はアルファも巻き込むので、RGB だけ落とす。
				SetBorderColor(new Color(_fill.r * 0.65f, _fill.g * 0.65f, _fill.b * 0.65f, 1f));
			}
		}
	}
}
