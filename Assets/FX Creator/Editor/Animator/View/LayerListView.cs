using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.AnimatorGraph.View
{
	/// <summary>
	/// レイヤー一覧（Docs/FXCreator-Design.md §6.1 の左サイドバー上段）。
	/// v0.1 は1レイヤーずつ表示する方針（§11-1）なので、ここは切替だけを担う。
	///
	/// Phase 2 は読み取り専用。追加・並び替え・ウェイト編集は Phase 6 の
	/// <c>ParameterListView</c> と併せて入れる。
	/// </summary>
	public sealed class LayerListView : VisualElement
	{
		/// <summary>レイヤーが選ばれた（引数はレイヤー番号）。</summary>
		public event Action<int> LayerSelected;

		private readonly ScrollView _scroll;
		private readonly Label _empty;
		private readonly List<Row> _rows = new List<Row>();

		private int _selectedIndex = -1;

		public LayerListView()
		{
			style.flexGrow = 1;
			style.minWidth = 140f;

			var header = new Label("Layers");
			header.style.unityFontStyleAndWeight = FontStyle.Bold;
			header.style.paddingLeft = 6f;
			header.style.paddingTop = 4f;
			header.style.paddingBottom = 4f;
			header.style.flexShrink = 0;
			Add(header);

			_empty = new Label("Controller を選ぶとレイヤーが並びます");
			_empty.style.paddingLeft = 6f;
			_empty.style.whiteSpace = WhiteSpace.Normal;
			_empty.style.color = FxcPanelLayout.PlaceholderColor;
			Add(_empty);

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.style.flexGrow = 1;
			Add(_scroll);
		}

		public int SelectedIndex => _selectedIndex;

		/// <summary>
		/// 一覧を作り直す。<paramref name="selectedIndex"/> は選択済みのレイヤー番号。
		/// 選択の通知は出さない（呼び出し側が既に知っている状態を反映するだけ）。
		/// </summary>
		public void SetController(AnimatorController controller, int selectedIndex)
		{
			_scroll.Clear();
			_rows.Clear();
			_selectedIndex = selectedIndex;

			AnimatorControllerLayer[] layers = controller != null ? controller.layers : null;
			bool any = layers != null && layers.Length > 0;
			_empty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
			if (!any)
			{
				return;
			}

			for (int i = 0; i < layers.Length; i++)
			{
				var row = new Row(i, layers[i], OnRowClicked);
				_rows.Add(row);
				_scroll.Add(row);
			}

			ApplySelection();
		}

		private void OnRowClicked(int index)
		{
			if (index == _selectedIndex)
			{
				return;
			}
			_selectedIndex = index;
			ApplySelection();
			LayerSelected?.Invoke(index);
		}

		private void ApplySelection()
		{
			for (int i = 0; i < _rows.Count; i++)
			{
				_rows[i].SetSelected(_rows[i].Index == _selectedIndex);
			}
		}

		private sealed class Row : VisualElement
		{
			/// <summary>
		/// 選択行の背景。文字の上に敷くので、スキンに合わせないと文字が沈む。
		/// static フィールドにするとスキン切り替えに追従しないのでプロパティで持つ。
		/// </summary>
		private static Color SelectedColor
		{
			get
			{
				return EditorGUIUtility.isProSkin
					? new Color(0.24f, 0.38f, 0.55f, 1f)
					: new Color(0.44f, 0.62f, 0.86f, 1f);
			}
		}

			public int Index { get; }

			private readonly Label _name;
			private readonly Label _detail;

			public Row(int index, AnimatorControllerLayer layer, Action<int> onClick)
			{
				Index = index;

				style.paddingLeft = 6f;
				style.paddingRight = 4f;
				style.paddingTop = 3f;
				style.paddingBottom = 3f;
				style.flexShrink = 0;

				_name = new Label(index + ". " + layer.name) { pickingMode = PickingMode.Ignore };
				_name.style.overflow = Overflow.Hidden;
				Add(_name);

				_detail = new Label(Describe(layer)) { pickingMode = PickingMode.Ignore };
				_detail.style.fontSize = 9f;
				_detail.style.color = FxcPanelLayout.PlaceholderColor;
				_detail.style.overflow = Overflow.Hidden;
				Add(_detail);

				// 生の PointerDownEvent ではなく Clickable を使う。標準のクリック挙動
				// （押下位置から外れて離したらキャンセル）が付くうえ、
				// Clickable を持つ要素は UI 自動化から叩けるので動作確認しやすい。
				this.AddManipulator(new Clickable(() => onClick(Index)));
			}

			public void SetSelected(bool selected)
			{
				style.backgroundColor = selected ? SelectedColor : Color.clear;
				_name.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
			}

			private static string Describe(AnimatorControllerLayer layer)
			{
				string text = string.Format("w {0:0.##}  ·  {1}", layer.defaultWeight, layer.blendingMode);
				if (layer.avatarMask != null)
				{
					text += "  ·  " + layer.avatarMask.name;
				}
				if (layer.syncedLayerIndex >= 0)
				{
					// 同期レイヤーは v0.1 では素通し（R4）。見えていないと事故るので印だけ出す。
					text += "  ·  sync " + layer.syncedLayerIndex;
				}
				return text;
			}
		}
	}
}
