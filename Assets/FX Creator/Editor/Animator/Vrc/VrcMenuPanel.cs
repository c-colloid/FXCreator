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

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.style.flexGrow = 1;
			Add(_scroll);
		}

#if VRC
		public void SetMenu(VRCExpressionsMenu menu)
		{
			_scroll.Clear();

			if (menu == null)
			{
				_status.text = "アバターに Expression Menu が設定されていません";
				return;
			}

			int count = menu.controls != null ? menu.controls.Count : 0;
			int max = VRCExpressionsMenu.MAX_CONTROLS;
			_status.text = menu.name + "　" + count + " / " + max
				+ (count > max ? "　★超過しています" : string.Empty);
			_status.style.color = count > max
				? new Color(0.92f, 0.55f, 0.45f)
				: new Color(0.62f, 0.62f, 0.66f);

			for (int i = 0; i < count; i++)
			{
				VRCExpressionsMenu.Control control = menu.controls[i];
				if (control == null)
				{
					continue;
				}

				var row = new VisualElement
				{
					style =
					{
						flexDirection = FlexDirection.Row,
						alignItems = Align.Center,
						paddingLeft = 4f,
						marginBottom = 1f,
						flexShrink = 0
					}
				};

				var name = new Label(control.name);
				name.style.flexGrow = 1;
				name.style.overflow = Overflow.Hidden;
				row.Add(name);

				var type = new Label(control.type.ToString());
				type.style.width = 110f;
				type.style.color = new Color(0.58f, 0.58f, 0.62f);
				row.Add(type);

				string parameterName = control.parameter != null ? control.parameter.name : string.Empty;
				var parameter = new Label(string.IsNullOrEmpty(parameterName) ? "-" : parameterName);
				parameter.style.width = 130f;
				parameter.style.overflow = Overflow.Hidden;
				row.Add(parameter);

				var value = new Label(control.value.ToString("0.##"));
				value.style.width = 40f;
				value.style.color = new Color(0.58f, 0.58f, 0.62f);
				row.Add(value);

				// サブメニューは中身を辿らない（v0.1 は1階層だけ見せる）。
				if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
				{
					var sub = new Label(control.subMenu != null ? "▸ " + control.subMenu.name : "▸ (未設定)");
					sub.style.color = new Color(0.5f, 0.5f, 0.55f);
					row.Add(sub);
				}

				_scroll.Add(row);
			}
		}
#else
		public void SetMenu(UnityEngine.Object menu)
		{
			_status.text = "VRChat SDK が見つかりません";
		}
#endif
	}
}
