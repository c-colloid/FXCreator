using UnityEngine;
using UnityEngine.UIElements;
#if VRC
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace colloid.FXCreator.AnimatorGraph.Vrc
{
	/// <summary>
	/// 資料の下パネル「menu (name, value)」（Docs/FXCreator-Design.md §6.3）。
	/// v0.1 は<b>簡易リスト版</b>。円環 UI と D&D 登録は v0.2〜v0.3。
	///
	/// 編集はまだ持たない（R4 の「触らなければ壊さない」）。まずは
	/// どのコントロールがどのパラメータを動かすのかを見えるようにする。
	/// </summary>
	public sealed class VrcMenuPanel : VisualElement
	{
		private readonly Label _status;
		private readonly ScrollView _scroll;
		private readonly VisualElement _columns;

		/// <summary>いま強調しているパラメータ名（§6.2 の選択連動）。</summary>
		private string _highlight;

		private const string RowClass = "fxc-menu-row";

		/// <summary>列の幅。可変にするのは name と parameter の2つだけ。</summary>
		private const float TypeColumn = 62f;
		private const float ValueColumn = 34f;

		/// <summary>これより細くなったら型の列を畳んで、名前とパラメータに回す。</summary>
		private const float NarrowWidth = 260f;

		public VrcMenuPanel()
		{
			style.flexGrow = 1;
			style.minHeight = 60f;

			_status = new Label("-");
			_status.style.paddingLeft = 6f;
			_status.style.paddingTop = 3f;
			_status.style.flexShrink = 0;
			_status.style.whiteSpace = WhiteSpace.Normal;
			Add(_status);

			_columns = BuildHeader();
			_columns.style.display = DisplayStyle.None;
			Add(_columns);

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.style.flexGrow = 1;
			_scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			Add(_scroll);

#if VRC
			View.FxcPanelLayout.CollapseOptionalColumnsWhenNarrow(this, NarrowWidth);
#endif
		}

		/// <summary>
		/// そのパラメータを動かすメニュー項目を光らせる。
		/// 「このパラメータはどこから触れるのか」が一番知りたい情報なので、
		/// 行の<b>parameter 列</b>と突き合わせる。
		/// </summary>
		public void SetHighlight(string name)
		{
			if (string.Equals(_highlight, name, System.StringComparison.Ordinal))
			{
				return;
			}
			_highlight = name;
			ApplyHighlight();
		}

		private void ApplyHighlight()
		{
			_scroll.Query<VisualElement>(className: RowClass).ForEach(row =>
			{
				bool on = _highlight != null && (string)row.userData == _highlight;
				row.style.backgroundColor = on
					? View.FxcPanelLayout.HighlightColor
					: Color.clear;
			});
		}

		private static VisualElement BuildHeader()
		{
			VisualElement row = View.FxcPanelLayout.HeaderRow();
			row.Add(View.FxcPanelLayout.HeaderFlexCell("name"));
			row.Add(View.FxcPanelLayout.HeaderCell("type", TypeColumn, optional: true));
			row.Add(View.FxcPanelLayout.HeaderFlexCell("parameter"));
			row.Add(View.FxcPanelLayout.HeaderCell("value", ValueColumn));
			return row;
		}

#if VRC
		public void SetMenu(VRCExpressionsMenu menu)
		{
			_scroll.Clear();

			if (menu == null)
			{
				_status.text = "アバターに Expression Menu が設定されていません";
				_columns.style.display = DisplayStyle.None;
				return;
			}

			int count = menu.controls != null ? menu.controls.Count : 0;
			int max = VRCExpressionsMenu.MAX_CONTROLS;
			_status.text = menu.name + "　" + count + " / " + max
				+ (count > max ? "　★超過しています" : string.Empty);
			_status.style.color = count > max
				? View.FxcPanelLayout.ErrorColor
				: View.FxcPanelLayout.SubtleColor;

			_columns.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

			for (int i = 0; i < count; i++)
			{
				VRCExpressionsMenu.Control control = menu.controls[i];
				if (control == null)
				{
					continue;
				}

				VisualElement row = View.FxcPanelLayout.Row();
				row.AddToClassList(RowClass);

				row.Add(View.FxcPanelLayout.Flexible(control.name));

				var type = View.FxcPanelLayout.Fixed(new Label(control.type.ToString()), TypeColumn);
				type.style.fontSize = 9f;
				type.style.color = View.FxcPanelLayout.SubtleColor;
				type.tooltip = control.type.ToString();
				type.AddToClassList(View.FxcPanelLayout.OptionalColumnClass);
				row.Add(type);

				// サブメニューは中身を辿らない（v0.1 は1階層だけ見せる）。
				// 以前はここで<b>幅の無いラベルを行の末尾に足していた</b>ので、
				// 行の合計が窓幅を超えて右端の列が外へ出ていた。
				// 飛び先はパラメータ列に畳む（SubMenu にパラメータが要ることは稀で、
				// あるときはそちらを優先して飛び先はツールチップへ回す）。
				bool isSubMenu = control.type == VRCExpressionsMenu.Control.ControlType.SubMenu;
				string subMenuName = control.subMenu != null ? control.subMenu.name : "(未設定)";
				string parameterName = control.parameter != null ? control.parameter.name : string.Empty;

				Label parameter;
				if (string.IsNullOrEmpty(parameterName))
				{
					parameter = View.FxcPanelLayout.Flexible(isSubMenu ? "▸ " + subMenuName : "-");
					parameter.style.color = View.FxcPanelLayout.SubtleColor;
				}
				else
				{
					parameter = View.FxcPanelLayout.Flexible(parameterName);
					if (isSubMenu)
					{
						parameter.tooltip = parameterName + "\n▸ " + subMenuName;
					}
				}
				// 連動の突き合わせ先はパラメータ名。無いものは連動しない。
				row.userData = string.IsNullOrEmpty(parameterName) ? null : parameterName;
				row.Add(parameter);

				// SubMenu の value は使われないので、数字を出すと誤解のもとになる。
				var value = View.FxcPanelLayout.Fixed(
					new Label(isSubMenu ? string.Empty : control.value.ToString("0.##")), ValueColumn);
				value.style.fontSize = 9f;
				value.style.color = View.FxcPanelLayout.SubtleColor;
				row.Add(value);

				_scroll.Add(row);
			}

			// 作り直した行にも、いまの畳み具合と強調を反映する。
			View.FxcPanelLayout.ApplyCollapseState(this, NarrowWidth);
			ApplyHighlight();
		}
#else
		public void SetMenu(UnityEngine.Object menu)
		{
			_status.text = "VRChat SDK が見つかりません";
		}
#endif
	}
}
