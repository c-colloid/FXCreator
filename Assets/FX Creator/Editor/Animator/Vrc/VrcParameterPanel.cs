using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
#if VRC
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace colloid.FXCreator.AnimatorGraph.Vrc
{
	/// <summary>
	/// 資料の下パネル「parameter (name, sync)」（Docs/FXCreator-Design.md §6.2）。
	/// <c>VRCExpressionParameters</c> の編集と、Controller のパラメータとの差分表示。
	///
	/// コストの上限は SDK の定数を参照する。SDK 更新で上限が変わるので、
	/// ここで数字を持たない。
	/// </summary>
	public sealed class VrcParameterPanel : VisualElement
	{
		private readonly Label _status;
		private readonly ScrollView _scroll;

		private AnimatorController _controller;

#if VRC
		private VRCExpressionParameters _parameters;
#endif

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

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.style.flexGrow = 1;
			_scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			Add(_scroll);
		}

#if VRC
		public void SetTarget(AnimatorController controller, VRCExpressionParameters parameters)
		{
			_controller = controller;
			_parameters = parameters;
			Rebuild();
		}

		private void Rebuild()
		{
			_scroll.Clear();

			if (_parameters == null)
			{
				_status.text = "アバターに Expression Parameters が設定されていません";
				return;
			}

			// 上限は SDK 側の定数。バージョンで変わるのでハードコードしない。
			int cost = _parameters.CalcTotalCost();
			int max = VRCExpressionParameters.MAX_PARAMETER_COST;
			_status.text = "コスト " + cost + " / " + max + (cost > max ? "　★超過しています" : string.Empty);
			_status.style.color = cost > max
				? new Color(0.92f, 0.55f, 0.45f)
				: new Color(0.62f, 0.62f, 0.66f);

			var inController = new HashSet<string>(StringComparer.Ordinal);
			if (_controller != null)
			{
				foreach (AnimatorControllerParameter p in _controller.parameters)
				{
					inController.Add(p.name);
				}
			}

			_scroll.Add(BuildHeader());

			var declared = new HashSet<string>(StringComparer.Ordinal);
			VRCExpressionParameters.Parameter[] list = _parameters.parameters;
			if (list != null)
			{
				for (int i = 0; i < list.Length; i++)
				{
					if (list[i] == null)
					{
						continue;
					}
					declared.Add(list[i].name);
					_scroll.Add(BuildRow(list[i], i, inController.Contains(list[i].name)));
				}
			}

			// Controller にあるのに同期設定が無いものを挙げる。これが v0.1 の差分表示。
			// 数が多いので一覧ではなく件数と名前だけ出す。
			var missing = new List<string>();
			foreach (string name in inController)
			{
				if (!declared.Contains(name))
				{
					missing.Add(name);
				}
			}
			if (missing.Count > 0)
			{
				missing.Sort(StringComparer.Ordinal);
				var note = new Label("同期設定がない Controller パラメータ " + missing.Count + " 件: "
					+ string.Join(", ", missing.ToArray(), 0, Mathf.Min(8, missing.Count))
					+ (missing.Count > 8 ? " …" : string.Empty));
				note.style.whiteSpace = WhiteSpace.Normal;
				note.style.paddingLeft = 6f;
				note.style.marginTop = 4f;
				note.style.color = new Color(0.72f, 0.68f, 0.45f);
				note.tooltip = string.Join("\n", missing.ToArray());
				_scroll.Add(note);
			}
		}

		private const float TypeColumn = 40f;
		private const float FlagColumn = 44f;
		private const float ValueColumn = 46f;

		private static VisualElement BuildHeader()
		{
			var row = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					paddingLeft = 4f,
					marginBottom = 2f,
					flexShrink = 0
				}
			};

			Label Column(string text, float width)
			{
				var label = new Label(text);
				label.style.width = width;
				label.style.flexShrink = 0;
				label.style.fontSize = 9f;
				label.style.color = new Color(0.52f, 0.52f, 0.56f);
				return label;
			}

			var spacer = new VisualElement { style = { width = 10f, flexShrink = 0 } };
			row.Add(spacer);
			var name = Column("name", 0f);
			name.style.width = StyleKeyword.Auto;
			name.style.flexGrow = 1;
			row.Add(name);
			row.Add(Column("type", TypeColumn));
			row.Add(Column("saved", FlagColumn));
			row.Add(Column("sync", FlagColumn));
			row.Add(Column("default", ValueColumn));
			return row;
		}

		private VisualElement BuildRow(VRCExpressionParameters.Parameter parameter, int index, bool inController)
		{
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

			// Controller 側に無い＝どのレイヤーからも使われない同期パラメータ。
			var mark = new Label(inController ? " " : "?");
			mark.style.width = 10f;
			mark.style.flexShrink = 0;
			mark.style.color = new Color(0.90f, 0.72f, 0.30f);
			mark.tooltip = inController ? null : "Controller に同名のパラメータがありません";
			row.Add(mark);

			var name = new Label(parameter.name);
			name.style.flexGrow = 1;
			name.style.flexShrink = 1;
			name.style.minWidth = 0f;
			name.style.overflow = Overflow.Hidden;
			row.Add(name);

			var type = new Label(parameter.valueType.ToString());
			type.style.width = TypeColumn;
			type.style.flexShrink = 0;
			type.style.color = new Color(0.58f, 0.58f, 0.62f);
			row.Add(type);

			// ラベル付きの Toggle は幅を決め打ちするとチェックボックスが切れる。
			// 見出し行を1本置いて、各行はラベル無しのチェックボックスだけにする。
			var saved = new Toggle();
			saved.SetValueWithoutNotify(parameter.saved);
			saved.style.width = FlagColumn;
			saved.style.flexShrink = 0;
			saved.RegisterValueChangedCallback(evt => Apply("Set Saved", () => parameter.saved = evt.newValue));
			row.Add(saved);

			var synced = new Toggle();
			synced.SetValueWithoutNotify(parameter.networkSynced);
			synced.style.width = FlagColumn;
			synced.style.flexShrink = 0;
			synced.RegisterValueChangedCallback(
				evt => Apply("Set Synced", () => parameter.networkSynced = evt.newValue));
			row.Add(synced);

			var value = new FloatField { isDelayed = true };
			value.SetValueWithoutNotify(parameter.defaultValue);
			value.style.width = ValueColumn;
			value.style.flexShrink = 0;
			value.RegisterValueChangedCallback(
				evt => Apply("Set Default", () => parameter.defaultValue = evt.newValue));
			row.Add(value);

			return row;
		}

		/// <summary>
		/// ここは Controller ではなく別アセット（VRCExpressionParameters）なので、
		/// <see cref="AcEdit"/> ではなく直接 Undo を積む。
		/// </summary>
		private void Apply(string label, Action change)
		{
			if (_parameters == null)
			{
				return;
			}
			Undo.RegisterCompleteObjectUndo(_parameters, label);
			change();
			EditorUtility.SetDirty(_parameters);
			Rebuild();
		}
#else
		public void SetTarget(AnimatorController controller, UnityEngine.Object parameters)
		{
			_controller = controller;
			_status.text = "VRChat SDK が見つかりません";
		}
#endif
	}
}
