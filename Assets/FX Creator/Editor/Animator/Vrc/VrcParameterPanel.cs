using System;
using System.Collections.Generic;
using colloid.FXCreator.AnimatorGraph.View;
using colloid.FXCreator.Targeting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.Vrc
{
	/// <summary>
	/// 資料の「parameter (name, sync)」（Docs/FXCreator-Design.md §6.2）。
	///
	/// 中身は <see cref="IFxParameterStore"/> 越しに扱うので、このパネルは
	/// Direct（Expression Parameters）と NDMF(MA)（MA Parameters）の<b>どちらで
	/// 動いているかを知らない</b>。知ってしまうと、MA モードで何もできないか、
	/// 非破壊のつもりでアバター同梱アセットを書き換えるかのどちらかになる。
	///
	/// VAR（Controller のパラメータ）との差分は、見せるだけでなく<b>押すと解消できる</b>。
	/// この2つは名前で結ばれているだけで、Unity も VRChat も一致を保証しない。
	/// 揃っていない状態は「メニューは出るのに何も起きない」として現れる。
	/// </summary>
	public sealed class VrcParameterPanel : VisualElement
	{
		private readonly Label _status;
		private readonly VisualElement _columns;
		private readonly ScrollView _scroll;

		private AnimatorController _controller;
		private IFxParameterStore _store;
		private bool _readOnly;

		/// <summary>いま強調している名前（VAR との選択連動）。</summary>
		private string _highlight;

		/// <summary>行が選ばれた。ウィンドウが受けて VAR 側を光らせる。</summary>
		public event Action<string> ParameterSelected;

		private const float MarkColumn = 10f;
		private const float TypeColumn = 36f;
		// チェックボックスは 18px 程度。見出しの文字幅に合わせると名前が削られる。
		private const float FlagColumn = 30f;
		private const float ValueColumn = 42f;

		/// <summary>これより細くなったら型の列を畳んで、名前に回す。</summary>
		private const float NarrowWidth = 250f;

		public VrcParameterPanel()
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

			FxcPanelLayout.CollapseOptionalColumnsWhenNarrow(this, NarrowWidth);
		}

		private static VisualElement BuildHeader()
		{
			VisualElement row = FxcPanelLayout.HeaderRow();
			row.Add(FxcPanelLayout.Fixed(new VisualElement(), MarkColumn));
			row.Add(FxcPanelLayout.HeaderFlexCell("name"));
			row.Add(FxcPanelLayout.HeaderCell("type", TypeColumn, optional: true));
			row.Add(FxcPanelLayout.HeaderCell("saved", FlagColumn));
			row.Add(FxcPanelLayout.HeaderCell("sync", FlagColumn));
			row.Add(FxcPanelLayout.HeaderCell("default", ValueColumn));
			return row;
		}

		public void SetTarget(AnimatorController controller, IFxParameterStore store, bool readOnly)
		{
			_controller = controller;
			_store = store;
			_readOnly = readOnly;
			Rebuild();
		}

		/// <summary>VAR で選ばれた名前を光らせる（選択の連動）。</summary>
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
					? View.FxcPanelLayout.HighlightColor
					: Color.clear;
			});
		}

		private const string RowClass = "fxc-parameter-row";

		private void Rebuild()
		{
			_scroll.Clear();

			if (_store == null)
			{
				_status.text = "編集対象が選ばれていません";
				_columns.style.display = DisplayStyle.None;
				return;
			}

			string unavailable = _store.UnavailableReason;
			if (unavailable != null)
			{
				_status.text = unavailable;
				_status.style.color = FxcPanelLayout.SubtleColor;
				_columns.style.display = DisplayStyle.None;
				return;
			}

			bool locked = _readOnly || _store.IsReadOnly;

			// 上限は SDK 側の定数。バージョンで変わるのでハードコードしない。
			int cost = _store.Cost;
			int max = _store.MaxCost;
			_status.text = _store.DisplayName + "　コスト " + cost + " / " + max
				+ (cost > max ? "　★超過しています" : string.Empty);
			_status.style.color = cost > max
				? View.FxcPanelLayout.ErrorColor
				: FxcPanelLayout.SubtleColor;

			IReadOnlyList<FxSyncParameter> declared = _store.Read();
			_columns.style.display = declared.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

			var inController = new HashSet<string>(StringComparer.Ordinal);
			if (_controller != null)
			{
				foreach (AnimatorControllerParameter p in _controller.parameters)
				{
					inController.Add(p.name);
				}
			}

			for (int i = 0; i < declared.Count; i++)
			{
				_scroll.Add(BuildRow(declared[i], inController.Contains(declared[i].Name), locked));
			}

			BuildDiffSection(locked);

			FxcPanelLayout.ApplyCollapseState(this, NarrowWidth);
			ApplyHighlight();
		}

		#region Rows

		private VisualElement BuildRow(FxSyncParameter parameter, bool inController, bool locked)
		{
			string name = parameter.Name;

			VisualElement row = FxcPanelLayout.Row();
			row.AddToClassList(RowClass);
			row.userData = name;
			row.RegisterCallback<PointerDownEvent>(_ =>
			{
				SetHighlight(name);
				Action<string> handler = ParameterSelected;
				if (handler != null)
				{
					handler(name);
				}
			});

			// Controller 側に無い＝どのレイヤーからも動かされない同期パラメータ。
			var mark = FxcPanelLayout.Fixed(new Label(inController ? " " : "?"), MarkColumn);
			mark.style.color = View.FxcPanelLayout.WarningColor;
			mark.tooltip = inController
				? null
				: "Controller に同名のパラメータがありません（右クリックで VAR へ追加できます）";
			row.Add(mark);

			// 名前は編集できる。改名は Controller 側と<b>両方</b>直す（§6.2）。
			var nameField = new TextField { value = name, isDelayed = true };
			nameField.style.height = FxcPanelLayout.RowHeight - 2f;
			FxcPanelLayout.MakeFlexible(nameField);
			nameField.style.marginRight = 2f;
			nameField.tooltip = name;
			nameField.SetEnabled(!locked);
			nameField.RegisterValueChangedCallback(evt =>
			{
				if (string.IsNullOrWhiteSpace(evt.newValue) || evt.newValue == name)
				{
					nameField.SetValueWithoutNotify(name);
					return;
				}
				Rename(name, evt.newValue);
			});
			row.Add(nameField);

			var type = FxcPanelLayout.Fixed(
				new Button(() => ShowTypeMenu(name, parameter.Type)) { text = parameter.Type.ToString() },
				TypeColumn);
			StyleFlatButton(type);
			type.tooltip = parameter.Type + "　（押すと型を変更）";
			type.SetEnabled(!locked);
			type.AddToClassList(FxcPanelLayout.OptionalColumnClass);
			row.Add(type);

			row.Add(BuildFlag(parameter.Saved, "ワールドを移動しても値を保つ", locked,
				v => Apply(() => _store.SetSaved(name, v))));

			row.Add(BuildFlag(parameter.Synced, "他の人に同期する（同期コストを消費します）", locked,
				v => Apply(() => _store.SetSynced(name, v))));

			var value = FxcPanelLayout.Fixed(new FloatField { isDelayed = true }, ValueColumn);
			value.SetValueWithoutNotify(parameter.Default);
			value.tooltip = "既定値";
			value.SetEnabled(!locked);
			value.RegisterValueChangedCallback(evt => Apply(() => _store.SetDefault(name, evt.newValue)));
			row.Add(value);

			row.AddManipulator(new ContextualMenuManipulator(evt =>
			{
				if (locked)
				{
					evt.menu.AppendAction("読み取り専用です", _ => { }, DropdownMenuAction.Status.Disabled);
					return;
				}
				if (!inController)
				{
					evt.menu.AppendAction("VAR（Controller）へ追加", _ =>
					{
						FxParameterSync.DeclareInController(parameter, _controller);
						Rebuild();
					}, _controller != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
					evt.menu.AppendSeparator();
				}
				evt.menu.AppendAction("この行を削除", _ => Apply(() => _store.Remove(name)));
			}));

			return row;
		}

		private VisualElement BuildFlag(bool value, string tooltip, bool locked, Action<bool> onChanged)
		{
			// 列の幅いっぱいの Toggle にすると当たり判定が列全体に広がり、
			// 隣の列を狙ったつもりで値が変わる。枠で中央に置く。
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
				FlagColumn);

			var toggle = new Toggle();
			toggle.SetValueWithoutNotify(value);
			toggle.style.marginLeft = 0f;
			toggle.style.marginRight = 0f;
			toggle.tooltip = tooltip;
			toggle.SetEnabled(!locked);
			toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
			slot.Add(toggle);
			return slot;
		}

		private static void StyleFlatButton(Button button)
		{
			button.style.fontSize = 9f;
			button.style.unityTextAlign = TextAnchor.MiddleLeft;
			button.style.color = FxcPanelLayout.SubtleColor;
			button.style.backgroundColor = Color.clear;
			button.style.borderTopWidth = 0f;
			button.style.borderBottomWidth = 0f;
			button.style.borderLeftWidth = 0f;
			button.style.borderRightWidth = 0f;
			button.style.marginLeft = 0f;
			button.style.marginRight = 0f;
			button.style.paddingLeft = 2f;
			button.style.paddingRight = 0f;
		}

		#endregion

		#region Diff

		/// <summary>
		/// Controller にあって宣言されていないものを、押すだけで解消できる形で出す。
		/// v0.1 では件数と名前を並べるだけだったが、見えても直せないなら
		/// 差分表示は「気になるが動けない」しか生まない。
		/// </summary>
		private void BuildDiffSection(bool locked)
		{
			List<AnimatorControllerParameter> missing =
				FxParameterSync.MissingInStore(_controller, _store);
			if (missing.Count == 0)
			{
				return;
			}

			var header = FxcPanelLayout.Row(20f);
			header.style.marginTop = 4f;

			var label = FxcPanelLayout.Flexible(
				"同期設定がない Controller パラメータ " + missing.Count + " 件");
			label.style.color = View.FxcPanelLayout.NoteColor;
			label.tooltip = string.Join("\n", Names(missing));
			header.Add(label);

			var addAll = FxcPanelLayout.Fixed(new Button(() =>
			{
				for (int i = 0; i < missing.Count; i++)
				{
					FxParameterSync.DeclareInStore(missing[i], _store);
				}
				Rebuild();
			})
			{
				text = "すべて追加"
			}, 72f);
			addAll.tooltip = "すべて " + _store.DisplayName + " へ追加します";
			addAll.SetEnabled(!locked);
			header.Add(addAll);
			_scroll.Add(header);

			for (int i = 0; i < missing.Count; i++)
			{
				AnimatorControllerParameter parameter = missing[i];
				VisualElement row = FxcPanelLayout.Row();

				row.Add(FxcPanelLayout.Fixed(new VisualElement(), MarkColumn));

				var name = FxcPanelLayout.Flexible(parameter.name);
				name.style.color = FxcPanelLayout.SubtleColor;
				row.Add(name);

				var type = FxcPanelLayout.Fixed(new Label(parameter.type.ToString()), TypeColumn);
				type.style.fontSize = 9f;
				type.style.color = FxcPanelLayout.SubtleColor;
				type.AddToClassList(FxcPanelLayout.OptionalColumnClass);
				row.Add(type);

				var add = FxcPanelLayout.Fixed(new Button(() =>
				{
					FxParameterSync.DeclareInStore(parameter, _store);
					Rebuild();
				})
				{
					text = "追加"
				}, FlagColumn * 2f + ValueColumn);
				add.tooltip = _store.DisplayName + " へ追加";
				add.SetEnabled(!locked);
				row.Add(add);

				_scroll.Add(row);
			}
		}

		private static string[] Names(List<AnimatorControllerParameter> parameters)
		{
			var names = new string[parameters.Count];
			for (int i = 0; i < parameters.Count; i++)
			{
				names[i] = parameters[i].name;
			}
			return names;
		}

		#endregion

		#region Edits

		private void Apply(Action change)
		{
			if (_store == null || _readOnly || _store.IsReadOnly)
			{
				return;
			}
			change();
			Rebuild();
		}

		/// <summary>
		/// 改名は Controller 側を正とする。Controller に同名があるなら
		/// <see cref="AcEdit.RenameParameter"/> に任せ、宣言先は
		/// <c>OnAfterEdit</c> 経由で追随させる（経路を1本に保つ）。
		/// Controller に無い（宣言だけある）ものはここで直接直す。
		/// </summary>
		private void Rename(string oldName, string newName)
		{
			if (_store == null || _readOnly || _store.IsReadOnly)
			{
				return;
			}

			if (_controller != null && HasParameter(_controller, oldName))
			{
				using (AcEdit e = AcEdit.Begin(_controller, "Rename Parameter"))
				{
					e.RenameParameter(oldName, newName);
				}
				return;
			}

			_store.Rename(oldName, newName);
			Rebuild();
		}

		private void ShowTypeMenu(string name, FxSyncType current)
		{
			if (_store == null || _readOnly || _store.IsReadOnly)
			{
				return;
			}

			var menu = new GenericMenu();
			foreach (FxSyncType type in new[] { FxSyncType.Bool, FxSyncType.Int, FxSyncType.Float })
			{
				FxSyncType captured = type;
				menu.AddItem(new GUIContent(type.ToString()), type == current, () => ChangeType(name, captured));
			}
			menu.ShowAsContext();
		}

		/// <summary>
		/// 型変更も Controller 側を正とする。Controller の型を変えると条件の
		/// 読み替えが要るので、その判断は <see cref="AcParameterTypeChange"/> に任せる。
		/// </summary>
		private void ChangeType(string name, FxSyncType type)
		{
			if (_controller != null && HasParameter(_controller, name))
			{
				AnimatorControllerParameterType animatorType = FxSyncTypes.ToAnimator(type);
				AcParameterTypeChangePlan plan =
					AcParameterTypeChange.Plan(_controller, name, animatorType);

				if (!plan.IsNoOp && (plan.Rewrites.Count > 0 || plan.HasBlockers))
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
					e.ChangeParameterType(name, animatorType);
				}
				return;
			}

			Apply(() => _store.SetType(name, type));
		}

		private static bool HasParameter(AnimatorController controller, string name)
		{
			AnimatorControllerParameter[] parameters = controller.parameters;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i] != null && string.Equals(parameters[i].name, name, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		#endregion
	}
}
