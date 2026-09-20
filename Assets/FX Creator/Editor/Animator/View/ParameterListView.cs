using System;
using System.Collections.Generic;
using colloid.FXCreator.Targeting;
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
		private readonly VisualElement _columns;

		private AnimatorController _controller;
		private bool _readOnly;

		/// <summary>同期パラメータの宣言先。差分の表示と「同期に追加」に使う（§6.2）。</summary>
		private IFxParameterStore _store;

		/// <summary>宣言先にある名前。行ごとに Read() を呼ばないよう1回だけ集める。</summary>
		private HashSet<string> _declared = new HashSet<string>(StringComparer.Ordinal);

		/// <summary>いま強調している名前（parameter パネルとの選択連動）。</summary>
		private string _highlight;

		private const string RowClass = "fxc-var-row";

		/// <summary>行が選ばれた。ウィンドウが受けて parameter パネル側を光らせる。</summary>
		public event Action<string> ParameterSelected;

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
			_empty.style.color = FxcPanelLayout.PlaceholderColor;
			Add(_empty);

			_columns = BuildHeader();
			Add(_columns);

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.style.flexGrow = 1;
			// 横スクロールを出さない。出ると行の右側（型・既定値・削除）が
			// 画面外へ押し出されて、名前しか見えなくなる。
			_scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			Add(_scroll);

			FxcPanelLayout.CollapseOptionalColumnsWhenNarrow(this, NarrowWidth);
		}

		/// <summary>列見出し。どのチェックボックスが何なのかを1本で説明する。</summary>
		private static VisualElement BuildHeader()
		{
			VisualElement row = FxcPanelLayout.HeaderRow();
			row.Add(FxcPanelLayout.Fixed(new VisualElement(), MarkColumn));
			row.Add(FxcPanelLayout.HeaderFlexCell("name"));
			row.Add(FxcPanelLayout.HeaderCell("type", TypeColumn, optional: true));
			row.Add(FxcPanelLayout.HeaderCell("S", SyncColumn));
			row.Add(FxcPanelLayout.HeaderCell("default", ValueColumn));
			row.Add(FxcPanelLayout.Fixed(new VisualElement(), RemoveColumn));
			return row;
		}

		public void SetController(AnimatorController controller, bool readOnly, IFxParameterStore store)
		{
			_controller = controller;
			_readOnly = readOnly;
			_store = store;
			Rebuild();
		}

		/// <summary>parameter パネルで選ばれた名前を光らせる（選択の連動）。</summary>
		public void SetHighlight(string name)
		{
			if (string.Equals(_highlight, name, StringComparison.Ordinal))
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
					? FxcPanelLayout.HighlightColor
					: Color.clear;
			});
		}

		private void Rebuild()
		{
			_scroll.Clear();
			_add.SetEnabled(_controller != null && !_readOnly);

			AnimatorControllerParameter[] parameters =
				_controller != null ? _controller.parameters : new AnimatorControllerParameter[0];
			bool any = parameters.Length > 0;
			_empty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
			_columns.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
			if (!any)
			{
				return;
			}

			// 「未参照」の判定は改名の走査と同じ範囲で行う（片方だけ知っている参照があると事故る）。
			HashSet<string> used = AcParameterUsage.CollectReferenced(_controller);

			// 宣言先にある名前を1回だけ集める。行ごとに Read() を呼ぶと
			// パラメータ数の2乗で走査することになる。
			_declared = new HashSet<string>(StringComparer.Ordinal);
			if (_store != null && _store.UnavailableReason == null)
			{
				IReadOnlyList<FxSyncParameter> declared = _store.Read();
				for (int i = 0; i < declared.Count; i++)
				{
					_declared.Add(declared[i].Name);
				}
			}

			for (int i = 0; i < parameters.Length; i++)
			{
				_scroll.Add(BuildRow(parameters[i], used.Contains(parameters[i].name)));
			}

			// 作り直した行にも、いまの畳み具合を反映する。
			FxcPanelLayout.ApplyCollapseState(this, NarrowWidth);
			ApplyHighlight();
		}

		/// <summary>行の各列の幅。行ごとにばらつかせない。</summary>
		private const float MarkColumn = 9f;
		private const float TypeColumn = 34f;
		private const float SyncColumn = 12f;
		private const float ValueColumn = 38f;
		private const float RemoveColumn = 16f;
		private const float RowHeight = FxcPanelLayout.RowHeight;

		/// <summary>
		/// これより細くなったら型の列を畳む。固定列の合計（約 100px）に対して
		/// 名前が 90px 程度は残るところで切っている。
		/// </summary>
		private const float NarrowWidth = 200f;

		private VisualElement BuildRow(AnimatorControllerParameter parameter, bool referenced)
		{
			string originalName = parameter.name;

			// 行の高さを決めておかないと、型ごとに違う編集部品の高さで
			// 行ごとに間隔が変わり、並びがばらついて見える。
			VisualElement row = FxcPanelLayout.Row(RowHeight);
			row.AddToClassList(RowClass);
			row.userData = originalName;
			row.RegisterCallback<PointerDownEvent>(_ =>
			{
				SetHighlight(originalName);
				Action<string> handler = ParameterSelected;
				if (handler != null)
				{
					handler(originalName);
				}
			});

			// 使われていないパラメータは消し忘れであることが多い。行の頭で知らせる。
			var mark = FxcPanelLayout.Fixed(new Label(referenced ? " " : "!"), MarkColumn);
			mark.style.unityTextAlign = TextAnchor.MiddleCenter;
			mark.style.color = FxcPanelLayout.WarningColor;
			mark.tooltip = referenced ? null : "この Controller のどこからも参照されていません";
			row.Add(mark);

			var name = new TextField { value = originalName, isDelayed = true };
			name.style.height = RowHeight - 2f;
			// TextField は既定で最小幅を持つ。可変列にしておかないと行が縮めず、
			// パネルの幅に収まらずに右側の要素がはみ出す。
			FxcPanelLayout.MakeFlexible(name);
			name.style.marginRight = 2f;
			// 細くすると入力欄の中で名前の頭しか見えない。全文はここから読める。
			name.tooltip = originalName;
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

			// 型は略号ではなく名前で出す。B / I / F / T は書く側にしか通じない。
			// 押すと型を変えられる。見た目はラベルのままにして、行が賑やかになるのを避ける。
			var type = FxcPanelLayout.Fixed(
				new Button(() => ShowTypeMenu(originalName, parameter.type)) { text = TypeName(parameter.type) },
				TypeColumn);
			type.style.fontSize = 9f;
			type.style.unityTextAlign = TextAnchor.MiddleLeft;
			type.style.color = FxcPanelLayout.SubtleColor;
			type.style.backgroundColor = Color.clear;
			type.style.borderTopWidth = 0f;
			type.style.borderBottomWidth = 0f;
			type.style.borderLeftWidth = 0f;
			type.style.borderRightWidth = 0f;
			type.style.marginLeft = 0f;
			type.style.marginRight = 0f;
			type.style.paddingLeft = 2f;
			type.style.paddingRight = 0f;
			type.tooltip = parameter.type + "　（押すと型を変更）";
			type.SetEnabled(!_readOnly);
			type.AddToClassList(FxcPanelLayout.OptionalColumnClass);
			row.Add(type);

			// 型の列は幅が足りないと畳まれる（§6 の列規則）。畳まれていても
			// 型を変えられるように、行の右クリックからも同じ口を出す。
			row.AddManipulator(new ContextualMenuManipulator(evt =>
			{
				if (_readOnly)
				{
					evt.menu.AppendAction("読み取り専用です", _ => { }, DropdownMenuAction.Status.Disabled);
					return;
				}
				foreach (AnimatorControllerParameterType candidate in TypeChoices)
				{
					AnimatorControllerParameterType captured = candidate;
					evt.menu.AppendAction(
						"型を変更/" + candidate,
						_ => ChangeType(originalName, captured),
						candidate == parameter.type
							? DropdownMenuAction.Status.Checked
							: DropdownMenuAction.Status.Normal);
				}
				evt.menu.AppendSeparator();

				if (_store != null && _store.UnavailableReason == null && !_store.IsReadOnly)
				{
					bool declared = _declared.Contains(originalName);
					string blocked;
					bool syncable = FxParameterSync.CanDeclare(parameter, out blocked);

					if (declared)
					{
						evt.menu.AppendAction(_store.DisplayName + " から外す", _ =>
						{
							_store.Remove(originalName);
							Rebuild();
						});
					}
					else
					{
						evt.menu.AppendAction(
							syncable
								? _store.DisplayName + " へ追加"
								: _store.DisplayName + " へ追加（" + blocked + "）",
							_ =>
							{
								FxParameterSync.DeclareInStore(parameter, _store);
								Rebuild();
							},
							syncable ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
					}
					evt.menu.AppendSeparator();
				}

				evt.menu.AppendAction("削除", _ =>
				{
					using (AcEdit e = AcEdit.Begin(_controller, "Delete Parameter"))
					{
						e.RemoveParameter(originalName);
					}
				});
			}));

			// 同期設定があるか。VAR と parameter は名前で結ばれているだけなので、
			// 揃っていないことがここで見えないと「メニューは出るのに何も起きない」になる。
			row.Add(BuildSyncMark(parameter));

			row.Add(BuildDefaultField(parameter));

			var remove = FxcPanelLayout.Fixed(
				new Button(() =>
				{
					using (AcEdit e = AcEdit.Begin(_controller, "Delete Parameter"))
					{
						e.RemoveParameter(originalName);
					}
				})
				{
					text = "-"
				},
				RemoveColumn);
			remove.tooltip = originalName + " を削除";
			remove.SetEnabled(!_readOnly);
			row.Add(remove);

			return row;
		}

		/// <summary>
		/// 同期設定の有無を1文字で。押すと追加・解除できる。
		/// <list type="bullet">
		/// <item><c>S</c> = 宣言先にある</item>
		/// <item><c>·</c> = 無い（同期できる型）</item>
		/// <item>空 = 同期できる型ではない（Trigger）、または宣言先が無い</item>
		/// </list>
		/// </summary>
		private VisualElement BuildSyncMark(AnimatorControllerParameter parameter)
		{
			string name = parameter.name;
			bool storeUsable = _store != null && _store.UnavailableReason == null;
			// 追加してよいかの判定は差分一覧と同じ関数を見る（別々に書くとズレる）。
			string blockedReason;
			bool syncable = FxParameterSync.CanDeclare(parameter, out blockedReason);
			bool declared = storeUsable && _declared.Contains(name);

			var button = FxcPanelLayout.Fixed(new Button(), SyncColumn);
			// 既に宣言されているものは、組み込みであっても外せるように出す。
			button.text = !storeUsable || (!syncable && !declared) ? string.Empty : (declared ? "S" : "·");
			button.style.fontSize = 9f;
			button.style.unityTextAlign = TextAnchor.MiddleCenter;
			button.style.color = declared
				? FxcPanelLayout.OkColor
				: FxcPanelLayout.SubtleColor;
			button.style.backgroundColor = Color.clear;
			button.style.borderTopWidth = 0f;
			button.style.borderBottomWidth = 0f;
			button.style.borderLeftWidth = 0f;
			button.style.borderRightWidth = 0f;
			button.style.marginLeft = 0f;
			button.style.marginRight = 0f;
			button.style.paddingLeft = 0f;
			button.style.paddingRight = 0f;

			if (!storeUsable || (!syncable && !declared))
			{
				button.tooltip = !storeUsable ? "同期設定の宣言先がありません" : blockedReason;
				button.SetEnabled(false);
				return button;
			}

			button.tooltip = declared
				? _store.DisplayName + " にあります（押すと外す）"
				: _store.DisplayName + " にありません（押すと追加）";
			button.SetEnabled(!_readOnly && !_store.IsReadOnly);
			button.clicked += () =>
			{
				if (declared)
				{
					_store.Remove(name);
				}
				else
				{
					FxParameterSync.DeclareInStore(parameter, _store);
				}
				Rebuild();
			};
			return button;
		}

		/// <summary>
		/// 既定値は型ごとに編集手段が違う（Trigger は値を持たない）。
		/// 型ごとに幅が違うと右隣の削除ボタンの位置が行ごとにずれるので、
		/// <b>幅の決まった枠に入れて</b>列を揃える。
		/// </summary>
		private VisualElement BuildDefaultField(AnimatorControllerParameter parameter)
		{
			string name = parameter.name;

			var slot = FxcPanelLayout.Fixed(
				new VisualElement
				{
					style =
					{
						flexDirection = FlexDirection.Row,
						justifyContent = Justify.Center,
						alignItems = Align.Center
					}
				},
				ValueColumn);
			// Trigger は値を持たないので、空欄であることを明示する。
			slot.tooltip = parameter.type == AnimatorControllerParameterType.Trigger
				? "Trigger は既定値を持ちません"
				: "既定値";

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

		/// <summary>追加でも型変更でも、選べる型は同じ4つ。</summary>
		private static readonly AnimatorControllerParameterType[] TypeChoices =
		{
			AnimatorControllerParameterType.Bool,
			AnimatorControllerParameterType.Int,
			AnimatorControllerParameterType.Float,
			AnimatorControllerParameterType.Trigger
		};

		private void ShowTypeMenu(string name, AnimatorControllerParameterType current)
		{
			if (_controller == null || _readOnly)
			{
				return;
			}

			var menu = new GenericMenu();
			foreach (AnimatorControllerParameterType type in TypeChoices)
			{
				AnimatorControllerParameterType captured = type;
				menu.AddItem(new GUIContent(type.ToString()), type == current,
					() => ChangeType(name, captured));
			}
			menu.ShowAsContext();
		}

		/// <summary>
		/// 型を変える。条件の読み替えは <see cref="AcParameterTypeChange"/> が決め、
		/// <b>何が起きるかを先に見せてから</b>適用する。
		/// 黙って条件を書き換えると、動かなくなった理由を追えない。
		/// </summary>
		private void ChangeType(string name, AnimatorControllerParameterType to)
		{
			if (_controller == null || _readOnly)
			{
				return;
			}

			AcParameterTypeChangePlan plan = AcParameterTypeChange.Plan(_controller, name, to);
			if (plan.IsNoOp)
			{
				return;
			}

			// 何も巻き込まないなら黙って変える。確認は「巻き込みがあるとき」だけ。
			if (plan.Rewrites.Count > 0 || plan.HasBlockers)
			{
				bool ok = EditorUtility.DisplayDialog(
					"FX Creator",
					plan.Summary() + "\n\n続けますか？（Undo 1回で戻せます）",
					"変更する",
					"やめる");
				if (!ok)
				{
					return;
				}
			}

			using (AcEdit e = AcEdit.Begin(_controller, "Change Parameter Type"))
			{
				e.ChangeParameterType(name, to);
			}
		}

		private void ShowAddMenu()
		{
			if (_controller == null || _readOnly)
			{
				return;
			}

			var menu = new GenericMenu();
			foreach (AnimatorControllerParameterType type in TypeChoices)
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

		private static string TypeName(AnimatorControllerParameterType type)
		{
			switch (type)
			{
				case AnimatorControllerParameterType.Bool: return "Bool";
				case AnimatorControllerParameterType.Int: return "Int";
				case AnimatorControllerParameterType.Float: return "Float";
				// Trigger は列幅に収まらないので縮める。全文はツールチップにある。
				default: return "Trig";
			}
		}
	}
}
