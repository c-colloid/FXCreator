using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// VAR / parameter / menu の3枚が共有する行と列の作り方（§6）。
	///
	/// オーバーレイは<b>ユーザーが自由に細くできる</b>のが利点で、同時に難点でもある。
	/// 各パネルが自前で幅を決めていた結果、細くしたときに
	/// 「名前が <c>VRCI</c> まで削られる」「右端の列が窓の外へ出る」が起きていた。
	/// 規則を1箇所に置いて、3枚とも同じ振る舞いにする。
	///
	/// 規則は3つだけ。
	/// <list type="number">
	/// <item><b>固定列は縮まない</b>（<c>flexShrink = 0</c>）。チェックボックスや数値欄は
	/// 縮めても読めるようにならず、潰れるだけ。</item>
	/// <item><b>可変列（名前）が余りを引き受ける</b>。<c>minWidth = 0</c> まで縮み、
	/// 入らない分は<b>省略記号</b>にする。ツールチップに全文を残すので情報は落ちない。</item>
	/// <item><b>行からはみ出させない</b>。行は <c>overflow = Hidden</c>。
	/// 横スクロールを出すと、名前しか見えない一覧になる。</item>
	/// </list>
	/// </summary>
	internal static class FxcPanelLayout
	{
		/// <summary>行の高さ。型ごとに違う編集部品を並べても間隔をばらつかせない。</summary>
		public const float RowHeight = 18f;

		#region Colors

		/// <summary>
		/// パネルの配色（Docs/FXCreator-Design.md Phase 8「USS のダーク/ライト両対応」）。
		///
		/// ノード側（<c>FXCNodeView</c> 等）は最初から <c>isProSkin</c> を見ていたのに、
		/// パネルは中間グレーを直に書いていた。ダークスキン（背景 0.22）では読めるが、
		/// ライトスキン（背景 0.76）では<b>薄すぎて沈む</b>。
		/// とくに選択ハイライトは濃紺の<b>背景</b>なので、ライトスキンの濃い文字と
		/// 重なると読めなくなる。
		///
		/// 静的フィールドにすると<b>スキンを切り替えても古い色のまま</b>になる
		/// （static は最初のドメインロード時に1度しか評価されない）。プロパティで都度引く。
		/// </summary>
		private static bool Pro { get { return EditorGUIUtility.isProSkin; } }

		/// <summary>列見出し。本文より一段落とす。</summary>
		public static Color HeaderColor
		{
			get { return Pro ? new Color(0.52f, 0.52f, 0.56f) : new Color(0.35f, 0.35f, 0.38f); }
		}

		/// <summary>型名など、読めればよい補助情報。</summary>
		public static Color SubtleColor
		{
			get { return Pro ? new Color(0.58f, 0.58f, 0.62f) : new Color(0.38f, 0.38f, 0.42f); }
		}

		/// <summary>「対象がありません」等の案内。</summary>
		public static Color PlaceholderColor
		{
			get { return Pro ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.42f, 0.42f, 0.45f); }
		}

		/// <summary>行頭の注意マーク（未参照・Controller に無い）。</summary>
		public static Color WarningColor
		{
			get { return Pro ? new Color(0.90f, 0.72f, 0.30f) : new Color(0.70f, 0.50f, 0.05f); }
		}

		/// <summary>コスト超過など、放置できないもの。</summary>
		public static Color ErrorColor
		{
			get { return Pro ? new Color(0.92f, 0.55f, 0.45f) : new Color(0.72f, 0.24f, 0.16f); }
		}

		/// <summary>差分の案内文。</summary>
		public static Color NoteColor
		{
			get { return Pro ? new Color(0.72f, 0.68f, 0.45f) : new Color(0.50f, 0.44f, 0.12f); }
		}

		/// <summary>同期済みの印。</summary>
		public static Color OkColor
		{
			get { return Pro ? new Color(0.55f, 0.78f, 0.55f) : new Color(0.18f, 0.48f, 0.20f); }
		}

		/// <summary>
		/// 選択行の<b>背景</b>。文字の上に敷くので、明るさをスキンに合わせないと
		/// 文字が読めなくなる。ライトでは淡い青を薄く敷く。
		/// </summary>
		public static Color HighlightColor
		{
			get
			{
				return Pro
					? new Color(0.24f, 0.36f, 0.52f, 0.55f)
					: new Color(0.42f, 0.60f, 0.85f, 0.45f);
			}
		}

		#endregion

		/// <summary>
		/// 幅に余裕が無いときに畳む列に付ける。型名のように
		/// 「あると嬉しいが、名前が読めなくなるほどではない」ものだけ。
		/// </summary>
		public const string OptionalColumnClass = "fxc-optional-column";

		/// <summary>1行。中身は絶対にはみ出さない。</summary>
		public static VisualElement Row(float height = RowHeight)
		{
			return new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					height = height,
					paddingLeft = 4f,
					paddingRight = 2f,
					marginBottom = 1f,
					// 行は縦に縮まない（スクロールビューの中で潰れさせない）。
					flexShrink = 0,
					// 横は切る。列が窓の外へ出るのを止める唯一の指定。
					overflow = Overflow.Hidden
				}
			};
		}

		/// <summary>列見出しの行。各パネルの先頭に1本だけ置く。</summary>
		public static VisualElement HeaderRow()
		{
			VisualElement row = Row(14f);
			row.style.marginBottom = 2f;
			return row;
		}

		/// <summary>幅の決まった列。縮まない。</summary>
		public static T Fixed<T>(T element, float width) where T : VisualElement
		{
			element.style.width = width;
			element.style.flexShrink = 0;
			return element;
		}

		/// <summary>
		/// 余りを分け合う列。長さの読めない文字列（名前）はこれ。
		/// 入らない分は省略記号にして、全文はツールチップへ逃がす。
		/// </summary>
		public static Label Flexible(string text, float grow = 1f)
		{
			var label = new Label(text);
			MakeFlexible(label, grow);
			// 省略されたときに全文が見えないと、名前の一覧として用をなさない。
			label.tooltip = text;
			return label;
		}

		/// <summary>既存の要素を可変列にする（TextField など Label 以外にも使う）。</summary>
		public static T MakeFlexible<T>(T element, float grow = 1f) where T : VisualElement
		{
			element.style.flexGrow = grow;
			element.style.flexShrink = 1;
			// これが無いと既定の最小幅で突っ張って、右隣の列が押し出される。
			element.style.minWidth = 0f;
			element.style.overflow = Overflow.Hidden;

			var text = element as TextElement;
			if (text != null)
			{
				text.style.whiteSpace = WhiteSpace.NoWrap;
				text.style.textOverflow = TextOverflow.Ellipsis;
			}
			return element;
		}

		/// <summary>幅の決まった見出しセル。</summary>
		public static Label HeaderCell(string text, float width, bool optional = false)
		{
			Label label = Fixed(new Label(text), width);
			StyleHeader(label);
			if (optional)
			{
				label.AddToClassList(OptionalColumnClass);
			}
			return label;
		}

		/// <summary>可変幅の見出しセル。</summary>
		public static Label HeaderFlexCell(string text, float grow = 1f)
		{
			var label = new Label(text);
			MakeFlexible(label, grow);
			StyleHeader(label);
			return label;
		}

		private static void StyleHeader(Label label)
		{
			label.style.fontSize = 9f;
			label.style.color = HeaderColor;
			label.style.unityTextAlign = TextAnchor.MiddleLeft;
		}

		/// <summary>
		/// パネルが細くなったら <see cref="OptionalColumnClass"/> の列を畳む。
		///
		/// 見出しと各行の両方に同じクラスが付いている前提。畳んだ分は
		/// 可変列（名前）へ回るので、細くするほど名前が読めるようになる。
		/// </summary>
		public static void CollapseOptionalColumnsWhenNarrow(VisualElement panel, float threshold)
		{
			bool? applied = null;
			panel.RegisterCallback<GeometryChangedEvent>(evt =>
			{
				bool narrow = evt.newRect.width < threshold;
				// GeometryChangedEvent は頻繁に来る。変わったときだけ木を歩く。
				if (applied == narrow)
				{
					return;
				}
				applied = narrow;

				DisplayStyle display = narrow ? DisplayStyle.None : DisplayStyle.Flex;
				panel.Query<VisualElement>(className: OptionalColumnClass)
					.ForEach(e => e.style.display = display);
			});
		}

		/// <summary>
		/// 作り直した中身にも、いまの畳み具合を反映する。
		/// 行はスクロールビューへ後から足されるので、
		/// <see cref="CollapseOptionalColumnsWhenNarrow"/> の判定だけでは
		/// 新しい行が「広いとき用」のまま出てくる。
		/// </summary>
		public static void ApplyCollapseState(VisualElement panel, float threshold)
		{
			float width = panel.resolvedStyle.width;
			if (float.IsNaN(width) || width <= 0f)
			{
				// まだレイアウトが決まっていない。直後の GeometryChangedEvent が拾う。
				return;
			}

			DisplayStyle display = width < threshold ? DisplayStyle.None : DisplayStyle.Flex;
			panel.Query<VisualElement>(className: OptionalColumnClass)
				.ForEach(e => e.style.display = display);
		}
	}
}
