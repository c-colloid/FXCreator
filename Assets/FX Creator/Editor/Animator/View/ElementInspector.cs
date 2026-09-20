using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// 選択した State / Transition の詳細編集（Docs/FXCreator-Design.md §9 Phase 3）。
	///
	/// 書き込みはすべて <see cref="AcEdit"/> を通すので、1フィールドの変更が
	/// 1 Undo 段になり、確定と同時にグラフが作り直される。
	///
	/// 編集 UI を出すのは v0.1 が面倒を見ると決めた項目だけ。BlendTree や
	/// StateMachineBehaviour のように「表示のみ・素通し」と決めたもの（R4）は
	/// 件数だけ出して標準インスペクタに委ねる。触らなければ壊さない。
	/// </summary>
	public sealed class ElementInspector : VisualElement
	{
		private readonly Label _title;
		private readonly VisualElement _body;
		private readonly Label _empty;

		private AnimatorController _controller;
		private AnimatorState _state;
		private AnimatorTransitionBase _transition;
		private AcTransitionGroup _group;

		/// <summary>畳み込みノードの Expand を押されたときに呼ぶ（サイドカーを触るのはソース側）。</summary>
		public System.Action<UnityEngine.Object, AcNodeKind, string, bool> SetGroupExpanded;

		/// <summary>いま畳み込みノードを出しているか。グループが消えたときの片付け判定に使う。</summary>
		public bool IsShowingGroup => _group != null;

		/// <summary>プレビューの構え方の読み書き（サイドカー §4.4）。Controller は変えない。</summary>
		public Func<AnimatorState, Preview.PreviewFraming> GetPreviewFraming;

		public Action<AnimatorState, Preview.PreviewFraming> SetPreviewFraming;

		/// <summary>値を流し込んでいる最中か。UI 起点の変更と区別してループを防ぐ。</summary>
		private bool _syncing;

		/// <summary>再構築せずに値だけ更新するための対応表。</summary>
		private readonly List<Action> _valueSyncs = new List<Action>();

		/// <summary>条件の行は数が変わるので、値同期ではなく行ごと作り直す。</summary>
		private Action _syncConditions;

		public ElementInspector()
		{
			style.minWidth = 220f;
			style.paddingLeft = 6f;
			style.paddingRight = 6f;
			style.paddingTop = 4f;

			_title = new Label("Inspector");
			_title.style.unityFontStyleAndWeight = FontStyle.Bold;
			_title.style.marginBottom = 4f;
			Add(_title);

			_empty = new Label("ノードか遷移を選ぶと内容が出ます");
			_empty.style.whiteSpace = WhiteSpace.Normal;
			_empty.style.color = FxcPanelLayout.PlaceholderColor;
			Add(_empty);

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.style.flexGrow = 1;
			Add(scroll);
			_body = scroll.contentContainer;
		}

		#region Target

		public void ShowNothing()
		{
			// 破棄済みの対象を持っている場合も作り直したいので、Unity の null 比較ではなく
			// 参照そのもので「何か持っているか」を見る。
			if (ReferenceEquals(_state, null) && ReferenceEquals(_transition, null) && _group == null)
			{
				return;
			}
			_state = null;
			_transition = null;
			_group = null;
			Rebuild();
		}

		/// <summary>
		/// 畳み込みノード（§4.2）。グループは毎回作り直されるオブジェクトなので、
		/// 同じ内容でも参照は変わる。ID で同一性を見る。
		/// </summary>
		public void ShowGroup(AnimatorController controller, AcTransitionGroup group)
		{
			if (_controller == controller && _group != null && group != null && _group.Id == group.Id)
			{
				// 中身（分岐の数など）は変わりうるので、参照だけ差し替えて作り直す。
				_group = group;
				Rebuild();
				return;
			}
			_controller = controller;
			_state = null;
			_transition = null;
			_group = group;
			Rebuild();
		}

		public void ShowState(AnimatorController controller, AnimatorState state)
		{
			if (_controller == controller && _state == state && _transition == null)
			{
				return;
			}
			_controller = controller;
			_state = state;
			_transition = null;
			Rebuild();
		}

		public void ShowTransition(AnimatorController controller, AnimatorTransitionBase transition)
		{
			if (_controller == controller && _transition == transition && _state == null)
			{
				return;
			}
			_controller = controller;
			_state = null;
			_transition = transition;
			Rebuild();
		}

		/// <summary>
		/// 対象はそのままに、表示中の値だけ取り直す。Undo や外部変更のあとに呼ぶ。
		/// 作り直さないのは、入力中のフィールドからフォーカスを奪わないため。
		/// </summary>
		public void SyncValues()
		{
			if (ReferenceEquals(_state, null) && ReferenceEquals(_transition, null))
			{
				return;
			}

			// 削除や Undo で対象が壊れていたら空にする。参照は残るが Unity の
			// null 比較では null になる、というのがここの判定。
			bool stateGone = !ReferenceEquals(_state, null) && _state == null;
			bool transitionGone = !ReferenceEquals(_transition, null) && _transition == null;
			if (stateGone || transitionGone)
			{
				ShowNothing();
				return;
			}

			_syncing = true;
			try
			{
				for (int i = 0; i < _valueSyncs.Count; i++)
				{
					_valueSyncs[i]();
				}
				if (_syncConditions != null)
				{
					_syncConditions();
				}
			}
			finally
			{
				_syncing = false;
			}
		}

		#endregion

		#region Build

		private void Rebuild()
		{
			_body.Clear();
			_valueSyncs.Clear();
			_syncConditions = null;

			bool has = _state != null || _transition != null || _group != null;
			_empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
			if (!has)
			{
				_title.text = "Inspector";
				return;
			}

			if (_group != null)
			{
				_title.text = _group.Kind == AcGroupKind.Toggle ? "Toggle" : "Switch";
				BuildGroupFields(_group);
				return;
			}

			if (_state != null)
			{
				_title.text = "State";
				BuildStateFields(_state);
				return;
			}

			_title.text = _transition is AnimatorStateTransition ? "Transition" : "Transition (Entry / State Machine)";
			BuildTransitionFields(_transition);
		}

		private bool Editable => _controller != null;

		/// <summary>1フィールドの変更を1 Undo 段にする共通経路。</summary>
		private void Apply(string operation, UnityEngine.Object target, Action change)
		{
			if (_syncing || !Editable || target == null)
			{
				return;
			}
			using (AcEdit e = AcEdit.Begin(_controller, operation))
			{
				e.Modify(target, change);
			}
		}

		private void BuildStateFields(AnimatorState state)
		{
			var name = new TextField("Name") { isDelayed = true };
			name.SetValueWithoutNotify(state.name);
			name.RegisterValueChangedCallback(evt =>
			{
				if (!string.IsNullOrEmpty(evt.newValue))
				{
					Apply("Rename State", state, () => state.name = evt.newValue);
				}
			});
			Track(() => name.SetValueWithoutNotify(state.name));
			_body.Add(name);

			var motion = new ObjectField("Motion")
			{
				objectType = typeof(Motion),
				allowSceneObjects = false
			};
			motion.SetValueWithoutNotify(state.motion);
			motion.RegisterValueChangedCallback(evt =>
				Apply("Set Motion", state, () => state.motion = evt.newValue as Motion));
			Track(() => motion.SetValueWithoutNotify(state.motion));
			_body.Add(motion);

			var speed = new FloatField("Speed");
			speed.SetValueWithoutNotify(state.speed);
			speed.RegisterValueChangedCallback(evt =>
				Apply("Set Speed", state, () => state.speed = evt.newValue));
			Track(() => speed.SetValueWithoutNotify(state.speed));
			_body.Add(speed);

			var writeDefaults = new Toggle("Write Defaults");
			writeDefaults.SetValueWithoutNotify(state.writeDefaultValues);
			writeDefaults.RegisterValueChangedCallback(evt =>
				Apply("Set Write Defaults", state, () => state.writeDefaultValues = evt.newValue));
			Track(() => writeDefaults.SetValueWithoutNotify(state.writeDefaultValues));
			_body.Add(writeDefaults);

			var mirror = new Toggle("Mirror");
			mirror.SetValueWithoutNotify(state.mirror);
			mirror.RegisterValueChangedCallback(evt =>
				Apply("Set Mirror", state, () => state.mirror = evt.newValue));
			Track(() => mirror.SetValueWithoutNotify(state.mirror));
			_body.Add(mirror);

			BuildPreviewFields(state);

			// R4: 触らないと決めたものは、隠さずに件数だけ知らせる。
			if (state.behaviours != null && state.behaviours.Length > 0)
			{
				_body.Add(PassthroughNote(
					state.behaviours.Length + " 個の StateMachineBehaviour（v0.1 では素通し）"));
			}
			if (state.motion is BlendTree)
			{
				_body.Add(PassthroughNote("BlendTree の中身は標準インスペクタで編集してください"));
			}
		}

		/// <summary>
		/// プレビューの寄り先と画角（§5.2-6 / §4.4）。Controller ではなく
		/// サイドカーに入るので、ここだけ <see cref="AcEdit"/> を通さない。
		/// </summary>
		private void BuildPreviewFields(AnimatorState state)
		{
			if (GetPreviewFraming == null || SetPreviewFraming == null)
			{
				return;
			}

			var header = new Label("Preview");
			header.style.unityFontStyleAndWeight = FontStyle.Bold;
			header.style.marginTop = 6f;
			_body.Add(header);

			Preview.PreviewFraming current = GetPreviewFraming(state);

			var names = new List<string>();
			for (int i = 0; i < Preview.PreviewFraming.Choices.Length; i++)
			{
				names.Add(Preview.PreviewFraming.LabelOf(Preview.PreviewFraming.Choices[i]));
			}

			var focus = new DropdownField("寄り先")
			{
				choices = names,
				value = Preview.PreviewFraming.LabelOf(current.Focus)
			};
			focus.RegisterValueChangedCallback(evt =>
			{
				if (_syncing)
				{
					return;
				}
				int index = names.IndexOf(evt.newValue);
				if (index < 0)
				{
					return;
				}
				Preview.PreviewFraming next = GetPreviewFraming(state);
				next.Focus = Preview.PreviewFraming.Choices[index];
				SetPreviewFraming(state, next);
			});
			_body.Add(focus);

			var fov = new Slider("画角", 10f, 70f) { value = current.Fov };
			fov.RegisterValueChangedCallback(evt =>
			{
				if (_syncing)
				{
					return;
				}
				Preview.PreviewFraming next = GetPreviewFraming(state);
				next.Fov = evt.newValue;
				SetPreviewFraming(state, next);
			});
			_body.Add(fov);

			_body.Add(PassthroughNote("選択中のノードだけ再生されます"));
		}

		/// <summary>
		/// 畳み込みノードの中身。実体は遷移群なので、ここでの編集は
		/// 「配下の遷移の conditions[0] を一斉に書き換える」に落ちる（§4.2 の逆方向）。
		/// </summary>
		private void BuildGroupFields(AcTransitionGroup group)
		{
			_body.Add(PassthroughNote(
				group.Branches.Count + " 本の遷移をまとめています（" + group.SourceLabel + " から）"));

			// パラメータの付け替え。型が合わないものを選ぶと畳み込みが崩れるので、
			// toggle には bool、switch には int だけを出す。
			var names = new List<string>();
			AnimatorControllerParameter[] parameters =
				_controller != null ? _controller.parameters : new AnimatorControllerParameter[0];
			AnimatorControllerParameterType wanted = group.Kind == AcGroupKind.Toggle
				? AnimatorControllerParameterType.Bool
				: AnimatorControllerParameterType.Int;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].type == wanted)
				{
					names.Add(parameters[i].name);
				}
			}
			if (!names.Contains(group.Parameter))
			{
				names.Insert(0, group.Parameter);
			}

			var parameter = new DropdownField("Parameter") { choices = names, value = group.Parameter };
			parameter.RegisterValueChangedCallback(evt =>
			{
				if (_syncing || !Editable || evt.newValue == group.Parameter)
				{
					return;
				}
				using (AcEdit e = AcEdit.Begin(_controller, "Set Group Parameter"))
				{
					// 配下を一斉に書き換えないと、グループが2つに割れてしまう。
					for (int i = 0; i < group.Branches.Count; i++)
					{
						AnimatorTransitionBase t = group.Branches[i].Transition;
						AnimatorCondition[] conditions = t.conditions;
						if (conditions.Length != 1)
						{
							continue;
						}
						conditions[0].parameter = evt.newValue;
						e.SetConditions(t, conditions);
					}
				}
			});
			_body.Add(parameter);

			var header = new Label("Branches");
			header.style.unityFontStyleAndWeight = FontStyle.Bold;
			header.style.marginTop = 6f;
			_body.Add(header);

			for (int i = 0; i < group.Branches.Count; i++)
			{
				AcGroupBranch branch = group.Branches[i];
				AnimatorTransitionBase transition = branch.Transition;

				var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2f } };

				var label = new Label(branch.Label + "  →  " + DescribeEndpoints(transition).Substring(2));
				label.style.flexGrow = 1;
				label.style.overflow = Overflow.Hidden;
				row.Add(label);

				var remove = new Button(() =>
				{
					using (AcEdit e = AcEdit.Begin(_controller, "Delete Branch"))
					{
						e.RemoveTransition(transition);
					}
				})
				{
					text = "-"
				};
				remove.style.width = 20f;
				row.Add(remove);

				_body.Add(row);
			}

			var expand = new Button(() =>
			{
				if (SetGroupExpanded != null)
				{
					SetGroupExpanded(group.SourceOwner, group.SourceRef.Kind, group.Parameter, true);
				}
			})
			{
				text = "Expand (畳み込みを解除)"
			};
			expand.style.marginTop = 6f;
			_body.Add(expand);
			_body.Add(PassthroughNote("解除しても Controller は変わりません（表示だけ）"));
		}

		private void BuildTransitionFields(AnimatorTransitionBase transition)
		{
			_body.Add(new Label(DescribeEndpoints(transition))
			{
				style =
				{
					whiteSpace = WhiteSpace.Normal,
					color = FxcPanelLayout.PlaceholderColor,
					marginBottom = 4f
				}
			});

			var stateTransition = transition as AnimatorStateTransition;
			if (stateTransition != null)
			{
				var hasExitTime = new Toggle("Has Exit Time");
				hasExitTime.SetValueWithoutNotify(stateTransition.hasExitTime);
				hasExitTime.RegisterValueChangedCallback(evt =>
					Apply("Set Has Exit Time", stateTransition, () => stateTransition.hasExitTime = evt.newValue));
				Track(() => hasExitTime.SetValueWithoutNotify(stateTransition.hasExitTime));
				_body.Add(hasExitTime);

				var exitTime = new FloatField("Exit Time");
				exitTime.SetValueWithoutNotify(stateTransition.exitTime);
				exitTime.RegisterValueChangedCallback(evt =>
					Apply("Set Exit Time", stateTransition, () => stateTransition.exitTime = evt.newValue));
				Track(() => exitTime.SetValueWithoutNotify(stateTransition.exitTime));
				_body.Add(exitTime);

				var fixedDuration = new Toggle("Fixed Duration");
				fixedDuration.SetValueWithoutNotify(stateTransition.hasFixedDuration);
				fixedDuration.RegisterValueChangedCallback(evt =>
					Apply("Set Fixed Duration", stateTransition,
						() => stateTransition.hasFixedDuration = evt.newValue));
				Track(() => fixedDuration.SetValueWithoutNotify(stateTransition.hasFixedDuration));
				_body.Add(fixedDuration);

				var duration = new FloatField("Duration");
				duration.SetValueWithoutNotify(stateTransition.duration);
				duration.RegisterValueChangedCallback(evt =>
					Apply("Set Duration", stateTransition, () => stateTransition.duration = evt.newValue));
				Track(() => duration.SetValueWithoutNotify(stateTransition.duration));
				_body.Add(duration);

				var offset = new FloatField("Offset");
				offset.SetValueWithoutNotify(stateTransition.offset);
				offset.RegisterValueChangedCallback(evt =>
					Apply("Set Offset", stateTransition, () => stateTransition.offset = evt.newValue));
				Track(() => offset.SetValueWithoutNotify(stateTransition.offset));
				_body.Add(offset);
			}

			var mute = new Toggle("Mute");
			mute.SetValueWithoutNotify(transition.mute);
			mute.RegisterValueChangedCallback(evt =>
				Apply("Mute Transition", transition, () => transition.mute = evt.newValue));
			Track(() => mute.SetValueWithoutNotify(transition.mute));
			_body.Add(mute);

			var solo = new Toggle("Solo");
			solo.SetValueWithoutNotify(transition.solo);
			solo.RegisterValueChangedCallback(evt =>
				Apply("Solo Transition", transition, () => transition.solo = evt.newValue));
			Track(() => solo.SetValueWithoutNotify(transition.solo));
			_body.Add(solo);

			BuildConditions(transition);
		}

		#endregion

		#region Conditions

		private void BuildConditions(AnimatorTransitionBase transition)
		{
			var header = new Label("Conditions");
			header.style.unityFontStyleAndWeight = FontStyle.Bold;
			header.style.marginTop = 6f;
			_body.Add(header);

			AnimatorControllerParameter[] parameters =
				_controller != null ? _controller.parameters : new AnimatorControllerParameter[0];

			if (parameters.Length == 0)
			{
				_body.Add(PassthroughNote("Controller にパラメータがありません"));
				return;
			}

			var rows = new VisualElement();
			_body.Add(rows);
			RebuildConditionRows(rows, transition, parameters);
			_syncConditions = () => RebuildConditionRows(rows, transition, _controller.parameters);

			var add = new Button(() =>
			{
				var list = new List<AnimatorCondition>(transition.conditions);
				list.Add(new AnimatorCondition
				{
					parameter = parameters[0].name,
					mode = DefaultMode(parameters[0].type),
					threshold = 0f
				});
				ApplyConditions(transition, list.ToArray());
				RebuildConditionRows(rows, transition, _controller.parameters);
			})
			{
				text = "Add Condition"
			};
			_body.Add(add);
		}

		private void RebuildConditionRows(
			VisualElement host, AnimatorTransitionBase transition, AnimatorControllerParameter[] parameters)
		{
			host.Clear();

			AnimatorCondition[] conditions = transition.conditions;
			var names = new List<string>();
			for (int i = 0; i < parameters.Length; i++)
			{
				names.Add(parameters[i].name);
			}

			for (int i = 0; i < conditions.Length; i++)
			{
				int index = i;
				AnimatorCondition condition = conditions[i];

				var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2f } };

				// パラメータが消されて宙に浮いた条件も、名前を見せて直せるようにする
				// （黙って別のパラメータに化けると気づけない）。
				var choices = new List<string>(names);
				if (!string.IsNullOrEmpty(condition.parameter) && !choices.Contains(condition.parameter))
				{
					choices.Insert(0, condition.parameter + "  (見つかりません)");
					condition.parameter = choices[0];
				}

				var parameter = new DropdownField { choices = choices, value = condition.parameter };
				parameter.style.flexGrow = 1;
				parameter.RegisterValueChangedCallback(evt =>
				{
					AnimatorControllerParameter picked = FindParameter(evt.newValue);
					EditCondition(transition, index, c =>
					{
						c.parameter = evt.newValue;
						// 型が変わると成立しないモードが残るので、既定に戻す。
						if (picked != null && !IsModeValid(c.mode, picked.type))
						{
							c.mode = DefaultMode(picked.type);
						}
						return c;
					});
					RebuildConditionRows(host, transition, _controller.parameters);
				});
				row.Add(parameter);

				AnimatorControllerParameter current = FindParameter(condition.parameter);
				List<string> modes = ModeChoices(current);
				var mode = new DropdownField { choices = modes, value = condition.mode.ToString() };
				mode.style.width = 80f;
				mode.RegisterValueChangedCallback(evt =>
				{
					AnimatorConditionMode parsed;
					if (Enum.TryParse(evt.newValue, out parsed))
					{
						EditCondition(transition, index, c => { c.mode = parsed; return c; });
					}
				});
				row.Add(mode);

				if (current != null
					&& (current.type == AnimatorControllerParameterType.Float
						|| current.type == AnimatorControllerParameterType.Int))
				{
					var threshold = new FloatField { value = condition.threshold };
					threshold.style.width = 56f;
					threshold.RegisterValueChangedCallback(evt =>
						EditCondition(transition, index, c => { c.threshold = evt.newValue; return c; }));
					row.Add(threshold);
				}

				var remove = new Button(() =>
				{
					var list = new List<AnimatorCondition>(transition.conditions);
					if (index < list.Count)
					{
						list.RemoveAt(index);
						ApplyConditions(transition, list.ToArray());
					}
					RebuildConditionRows(host, transition, _controller.parameters);
				})
				{
					text = "-"
				};
				remove.style.width = 20f;
				row.Add(remove);

				host.Add(row);
			}
		}

		private void EditCondition(
			AnimatorTransitionBase transition, int index, Func<AnimatorCondition, AnimatorCondition> change)
		{
			AnimatorCondition[] conditions = transition.conditions;
			if (index < 0 || index >= conditions.Length)
			{
				return;
			}
			conditions[index] = change(conditions[index]);
			ApplyConditions(transition, conditions);
		}

		private void ApplyConditions(AnimatorTransitionBase transition, AnimatorCondition[] conditions)
		{
			if (_syncing || !Editable)
			{
				return;
			}
			using (AcEdit e = AcEdit.Begin(_controller, "Edit Conditions"))
			{
				e.SetConditions(transition, conditions);
			}
		}

		private AnimatorControllerParameter FindParameter(string name)
		{
			if (_controller == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			AnimatorControllerParameter[] parameters = _controller.parameters;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].name == name)
				{
					return parameters[i];
				}
			}
			return null;
		}

		/// <summary>パラメータの型で成立しうる比較だけを出す（Bool に Greater は無い）。</summary>
		private static List<string> ModeChoices(AnimatorControllerParameter parameter)
		{
			if (parameter == null)
			{
				return new List<string> { AnimatorConditionMode.If.ToString() };
			}
			switch (parameter.type)
			{
				case AnimatorControllerParameterType.Float:
					return new List<string>
					{
						AnimatorConditionMode.Greater.ToString(),
						AnimatorConditionMode.Less.ToString()
					};
				case AnimatorControllerParameterType.Int:
					return new List<string>
					{
						AnimatorConditionMode.Greater.ToString(),
						AnimatorConditionMode.Less.ToString(),
						AnimatorConditionMode.Equals.ToString(),
						AnimatorConditionMode.NotEqual.ToString()
					};
				case AnimatorControllerParameterType.Bool:
					return new List<string>
					{
						AnimatorConditionMode.If.ToString(),
						AnimatorConditionMode.IfNot.ToString()
					};
				default:
					return new List<string> { AnimatorConditionMode.If.ToString() };
			}
		}

		private static bool IsModeValid(AnimatorConditionMode mode, AnimatorControllerParameterType type)
		{
			return ModeChoices(new AnimatorControllerParameter { type = type }).Contains(mode.ToString());
		}

		private static AnimatorConditionMode DefaultMode(AnimatorControllerParameterType type)
		{
			switch (type)
			{
				case AnimatorControllerParameterType.Float:
				case AnimatorControllerParameterType.Int:
					return AnimatorConditionMode.Greater;
				default:
					return AnimatorConditionMode.If;
			}
		}

		#endregion

		#region Helpers

		private void Track(Action sync)
		{
			_valueSyncs.Add(sync);
		}

		private static Label PassthroughNote(string text)
		{
			var label = new Label(text);
			label.style.whiteSpace = WhiteSpace.Normal;
			label.style.color = FxcPanelLayout.SubtleColor;
			label.style.marginTop = 4f;
			return label;
		}

		private static string DescribeEndpoints(AnimatorTransitionBase transition)
		{
			if (transition.isExit)
			{
				return "→ Exit";
			}
			if (transition.destinationState != null)
			{
				return "→ " + transition.destinationState.name;
			}
			if (transition.destinationStateMachine != null)
			{
				return "→ " + transition.destinationStateMachine.name + " (Sub-State Machine)";
			}
			return "→ (行き先なし)";
		}

		#endregion
	}
}
