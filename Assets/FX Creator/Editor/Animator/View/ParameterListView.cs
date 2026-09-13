using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// 資料の左サイドバー「VAR」（Docs/FXCreator-Design.md §6.1）。
	/// <c>AnimatorController.parameters</c> の一覧と編集。
	///
	/// 書き込みはすべて <see cref="AcEdit"/> を通す。とくに改名は
	/// Controller 内の全参照を追随させる必要があるので、ここでは名前を
	/// 直接書き換えず <see cref="AcEdit.RenameParameter"/> に任せる。
	/// </summary>
	public sealed class ParameterListView : VisualElement
	{
		private readonly Label _empty;
		private readonly ScrollView _scroll;
		private readonly Button _add;

		private AnimatorController _controller;
		private bool _readOnly;

		public ParameterListView()
		{
			style.flexGrow = 1;
			style.minHeight = 90f;

			// 見出しは置かない。Overlay 側が「VAR」と表示するので、
			// ここにも書くと同じ文字が2行並ぶ。
			var header = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					justifyContent = Justify.FlexEnd,
					flexShrink = 0
				}
			};

			_add = new Button(ShowAddMenu) { text = "+" };
			_add.style.width = 22f;
			_add.tooltip = "パラメータを追加";
			header.Add(_add);
			Add(header);

			_empty = new Label("パラメータがありません");
			_empty.style.paddingLeft = 6f;
			_empty.style.whiteSpace = WhiteSpace.Normal;
			_empty.style.color = new Color(0.55f, 0.55f, 0.58f);
			Add(_empty);

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.style.flexGrow = 1;
			// 横スクロールを出さない。出ると行の右側（型・既定値・削除）が
			// 画面外へ押し出されて、名前しか見えなくなる。
			_scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			Add(_scroll);
		}

		public void SetController(AnimatorController controller, bool readOnly)
		{
			_controller = controller;
			_readOnly = readOnly;
			Rebuild();
		}

		private void Rebuild()
		{
			_scroll.Clear();
			_add.SetEnabled(_controller != null && !_readOnly);

			AnimatorControllerParameter[] parameters =
				_controller != null ? _controller.parameters : new AnimatorControllerParameter[0];
			_empty.style.display = parameters.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			if (parameters.Length == 0)
			{
				return;
			}

			// 「未参照」の判定は改名の走査と同じ範囲で行う（片方だけ知っている参照があると事故る）。
			HashSet<string> used = AcParameterUsage.CollectReferenced(_controller);

			for (int i = 0; i < parameters.Length; i++)
			{
				_scroll.Add(BuildRow(parameters[i], used.Contains(parameters[i].name)));
			}
		}

		/// <summary>行の各列の幅。行ごとにばらつかせない。</summary>
		private const float MarkColumn = 9f;
		private const float TypeColumn = 14f;
		private const float ValueColumn = 38f;
		private const float RemoveColumn = 16f;
		private const float RowHeight = 18f;

		private VisualElement BuildRow(AnimatorControllerParameter parameter, bool referenced)
		{
			string originalName = parameter.name;

			var row = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					// 行の高さを決めておかないと、型ごとに違う編集部品の高さで
					// 行ごとに間隔が変わり、並びがばらついて見える。
					height = RowHeight,
					paddingLeft = 4f,
					paddingRight = 2f,
					marginBottom = 1f,
					flexShrink = 0
				}
			};

			// 使われていないパラメータは消し忘れであることが多い。行の頭で知らせる。
			var mark = new Label(referenced ? " " : "!");
			mark.style.width = MarkColumn;
			mark.style.flexShrink = 0;
			mark.style.unityTextAlign = TextAnchor.MiddleCenter;
			mark.style.color = new Color(0.90f, 0.72f, 0.30f);
			mark.tooltip = referenced ? null : "この Controller のどこからも参照されていません";
			row.Add(mark);

			var name = new TextField { value = originalName, isDelayed = true };
			name.style.height = RowHeight - 2f;
			name.style.flexGrow = 1;
			name.style.flexShrink = 1;
			// TextField は既定で最小幅を持つ。0 にしておかないと行が縮めず、
			// サイドバーの幅に収まらずに右側の要素がはみ出す。
			name.style.minWidth = 0f;
			name.style.marginRight = 2f;
			name.SetEnabled(!_readOnly);
			name.RegisterValueChangedCallback(evt =>
			{
				if (string.IsNullOrWhiteSpace(evt.newValue) || evt.newValue == originalName)
				{
					name.SetValueWithoutNotify(originalName);
					return;
				}
				using (AcEdit e = AcEdit.Begin(_controller, "Rename Parameter"))
				{
					e.RenameParameter(originalName, evt.newValue);
				}
			});
			row.Add(name);

			var type = new Label(Abbreviate(parameter.type));
			type.style.width = TypeColumn;
			type.style.flexShrink = 0;
			type.style.unityTextAlign = TextAnchor.MiddleCenter;
			type.style.color = new Color(0.58f, 0.58f, 0.62f);
			type.tooltip = parameter.type.ToString();
			row.Add(type);

			row.Add(BuildDefaultField(parameter));

			var remove = new Button(() =>
			{
				using (AcEdit e = AcEdit.Begin(_controller, "Delete Parameter"))
				{
					e.RemoveParameter(originalName);
				}
			})
			{
				text = "-"
			};
			remove.style.width = RemoveColumn;
			remove.style.flexShrink = 0;
			remove.SetEnabled(!_readOnly);
			row.Add(remove);

			return row;
		}

		/// <summary>
		/// 既定値は型ごとに編集手段が違う（Trigger は値を持たない）。
		/// 型ごとに幅が違うと右隣の削除ボタンの位置が行ごとにずれるので、
		/// <b>幅の決まった枠に入れて</b>列を揃える。
		/// </summary>
		private VisualElement BuildDefaultField(AnimatorControllerParameter parameter)
		{
			string name = parameter.name;

			var slot = new VisualElement
			{
				style =
				{
					width = ValueColumn,
					flexShrink = 0,
					flexDirection = FlexDirection.Row,
					justifyContent = Justify.Center,
					alignItems = Align.Center
				}
			};

			switch (parameter.type)
			{
				case AnimatorControllerParameterType.Bool:
				{
					var field = new Toggle();
					field.SetValueWithoutNotify(parameter.defaultBool);
					field.style.marginLeft = 0f;
					field.style.marginRight = 0f;
					field.SetEnabled(!_readOnly);
					field.RegisterValueChangedCallback(evt => Apply(name, p => p.defaultBool = evt.newValue));
					slot.Add(field);
					break;
				}
				case AnimatorControllerParameterType.Int:
				{
					var field = new IntegerField { isDelayed = true };
					field.SetValueWithoutNotify(parameter.defaultInt);
					field.style.flexGrow = 1;
					field.style.minWidth = 0f;
					field.style.marginLeft = 0f;
					field.style.marginRight = 0f;
					field.SetEnabled(!_readOnly);
					field.RegisterValueChangedCallback(evt => Apply(name, p => p.defaultInt = evt.newValue));
					slot.Add(field);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					var field = new FloatField { isDelayed = true };
					field.SetValueWithoutNotify(parameter.defaultFloat);
					field.style.flexGrow = 1;
					field.style.minWidth = 0f;
					field.style.marginLeft = 0f;
					field.style.marginRight = 0f;
					field.SetEnabled(!_readOnly);
					field.RegisterValueChangedCallback(evt => Apply(name, p => p.defaultFloat = evt.newValue));
					slot.Add(field);
					break;
				}
			}

			return slot;
		}

		private void Apply(string name, Action<AnimatorControllerParameter> change)
		{
			if (_controller == null || _readOnly)
			{
				return;
			}
			using (AcEdit e = AcEdit.Begin(_controller, "Set Parameter Default"))
			{
				e.ModifyParameter(name, change);
			}
		}

		private void ShowAddMenu()
		{
			if (_controller == null || _readOnly)
			{
				return;
			}

			var menu = new GenericMenu();
			foreach (AnimatorControllerParameterType type in new[]
			{
				AnimatorControllerParameterType.Bool,
				AnimatorControllerParameterType.Int,
				AnimatorControllerParameterType.Float,
				AnimatorControllerParameterType.Trigger
			})
			{
				AnimatorControllerParameterType captured = type;
				menu.AddItem(new GUIContent(type.ToString()), false, () =>
				{
					using (AcEdit e = AcEdit.Begin(_controller, "Add Parameter"))
					{
						e.AddParameter("New " + captured, captured);
					}
				});
			}
			menu.ShowAsContext();
		}

		private static string Abbreviate(AnimatorControllerParameterType type)
		{
			switch (type)
			{
				case AnimatorControllerParameterType.Bool: return "B";
				case AnimatorControllerParameterType.Int: return "I";
				case AnimatorControllerParameterType.Float: return "F";
				default: return "T";
			}
		}
	}
}
