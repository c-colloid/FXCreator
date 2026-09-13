using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>
	/// サイドカー（Docs/FXCreator-Design.md §4.4）。
	/// Controller に表現できない<b>表示情報だけ</b>を持つ。
	///
	/// 設計原則は「無くても・壊れていてもグラフは動く」。ここが null でも、
	/// 参照切れでも、中身が矛盾していても、グラフは既定の見た目で構築できる。
	/// だから読むだけの操作では生成しない（遅延生成）。
	///
	/// 同定は文字列キーではなく <see cref="UnityEngine.Object"/> の直接参照。
	/// State は Controller のサブアセットとして永続化されているので参照が生き続け、
	/// リネームで壊れない。
	/// </summary>
	public class FxcLayoutAsset : ScriptableObject
	{
		public AnimatorController controller;

		/// <summary>toggle / switch ノードの表示情報。実体が Controller に無いのでここに置く。</summary>
		[Serializable]
		public class CondNodeLayout
		{
			public AnimatorState sourceState;
			public string parameter;
			public Vector2 position;

			/// <summary>true = 畳み込みを解除して直結エッジで描く。</summary>
			public bool expanded;
		}

		public List<CondNodeLayout> condNodes = new List<CondNodeLayout>();

		/// <summary>ノードごとの追加表示情報。プレビュー関連は Phase 5 で使う。</summary>
		[Serializable]
		public class NodeExtra
		{
			public UnityEngine.Object target;
			public bool collapsed;
			public HumanBodyBones previewFocus = HumanBodyBones.Head;
			public float previewFov = 30f;
		}

		public List<NodeExtra> nodeExtras = new List<NodeExtra>();
	}

	/// <summary>
	/// <see cref="FxcLayoutAsset"/> への出入り口。
	/// 読むときは無ければ既定値を返すだけ、書くときに初めてアセットを作る。
	/// </summary>
	public sealed class FxcLayout
	{
		private const string Suffix = ".fxclayout.asset";

		private readonly AnimatorController _controller;
		private FxcLayoutAsset _asset;
		private bool _searched;

		public FxcLayout(AnimatorController controller)
		{
			_controller = controller;
		}

		/// <summary>読み込み専用。無ければ null のまま（作らない）。</summary>
		private FxcLayoutAsset Asset
		{
			get
			{
				if (_searched)
				{
					return _asset;
				}
				_searched = true;

				string path = LayoutPath();
				if (path == null)
				{
					return null;
				}
				_asset = AssetDatabase.LoadAssetAtPath<FxcLayoutAsset>(path);
				if (_asset != null)
				{
					Prune(_asset);
				}
				return _asset;
			}
		}

		private string LayoutPath()
		{
			if (_controller == null)
			{
				return null;
			}
			string controllerPath = AssetDatabase.GetAssetPath(_controller);
			if (string.IsNullOrEmpty(controllerPath))
			{
				return null;
			}
			string directory = System.IO.Path.GetDirectoryName(controllerPath);
			string name = System.IO.Path.GetFileNameWithoutExtension(controllerPath);
			return (directory + "/" + name + Suffix).Replace('\\', '/');
		}

		/// <summary>参照が切れたエントリを落とす。壊れたサイドカーで落ちないための掃除。</summary>
		private static void Prune(FxcLayoutAsset asset)
		{
			int removed = asset.condNodes.RemoveAll(e => e == null || e.sourceState == null);
			removed += asset.nodeExtras.RemoveAll(e => e == null || e.target == null);
			if (removed > 0)
			{
				EditorUtility.SetDirty(asset);
			}
		}

		/// <summary>書き込みのために用意する。ここで初めてアセットが生まれる。</summary>
		private FxcLayoutAsset Require()
		{
			FxcLayoutAsset existing = Asset;
			if (existing != null)
			{
				return existing;
			}

			string path = LayoutPath();
			if (path == null)
			{
				// 保存先が無い Controller（メモリ上のもの）では表示情報を持てない。
				// 呼び出し側は「保存されないだけ」で動き続ける。
				return null;
			}

			var created = ScriptableObject.CreateInstance<FxcLayoutAsset>();
			created.controller = _controller;
			AssetDatabase.CreateAsset(created, path);
			Undo.RegisterCreatedObjectUndo(created, "Create Layout");
			_asset = created;
			return created;
		}

		private static bool Matches(FxcLayoutAsset.CondNodeLayout entry, AnimatorState state, string parameter)
		{
			return entry.sourceState == state && string.Equals(entry.parameter, parameter, StringComparison.Ordinal);
		}

		private FxcLayoutAsset.CondNodeLayout Find(AnimatorState state, string parameter)
		{
			FxcLayoutAsset asset = Asset;
			if (asset == null)
			{
				return null;
			}
			for (int i = 0; i < asset.condNodes.Count; i++)
			{
				if (Matches(asset.condNodes[i], state, parameter))
				{
					return asset.condNodes[i];
				}
			}
			return null;
		}

		public bool IsExpanded(AnimatorState state, string parameter)
		{
			FxcLayoutAsset.CondNodeLayout entry = Find(state, parameter);
			return entry != null && entry.expanded;
		}

		public bool TryGetPosition(AnimatorState state, string parameter, out Vector2 position)
		{
			FxcLayoutAsset.CondNodeLayout entry = Find(state, parameter);
			if (entry == null)
			{
				position = Vector2.zero;
				return false;
			}
			position = entry.position;
			return true;
		}

		public void SetExpanded(AnimatorState state, string parameter, bool expanded)
		{
			Write(state, parameter, entry => entry.expanded = expanded);
		}

		public void SetPosition(AnimatorState state, string parameter, Vector2 position)
		{
			Write(state, parameter, entry => entry.position = position);
		}

		private void Write(AnimatorState state, string parameter, Action<FxcLayoutAsset.CondNodeLayout> apply)
		{
			if (state == null)
			{
				return;
			}

			FxcLayoutAsset asset = Require();
			if (asset == null)
			{
				return;
			}

			Undo.RegisterCompleteObjectUndo(asset, "Edit Layout");

			FxcLayoutAsset.CondNodeLayout entry = null;
			for (int i = 0; i < asset.condNodes.Count; i++)
			{
				if (Matches(asset.condNodes[i], state, parameter))
				{
					entry = asset.condNodes[i];
					break;
				}
			}
			if (entry == null)
			{
				entry = new FxcLayoutAsset.CondNodeLayout { sourceState = state, parameter = parameter };
				asset.condNodes.Add(entry);
			}

			apply(entry);
			EditorUtility.SetDirty(asset);
		}

		/// <summary>Controller が差し替わったときに読み直させる。</summary>
		public void Invalidate()
		{
			_searched = false;
			_asset = null;
		}
	}
}
