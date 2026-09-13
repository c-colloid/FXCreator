using System;
using UnityEditor;
using UnityEditor.Animations;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>
	/// 外部変更・Undo への追従（Docs/FXCreator-Design.md §4.5）。
	///
	/// 差分同期はせず<b>全再構築</b>で対応する。State 数は通常数十で、
	/// 再構築は 1ms 未満。ビュー側が選択（ID保持）とビューポートを持ち越すので、
	/// 作り直しても操作感は途切れない。
	///
	/// 1フレーム内に複数のトリガが来ても <see cref="Changed"/> は1回にまとめる
	/// （Undo は undoRedoPerformed と projectChanged の両方を出すことがある）。
	/// </summary>
	public sealed class AcChangeWatcher : IDisposable
	{
		/// <summary>再構築してほしい、という通知。</summary>
		public event Action Changed;

		private string _assetPath;
		private bool _pending;
		private bool _disposed;

		public AcChangeWatcher()
		{
			Undo.undoRedoPerformed += Schedule;
			EditorApplication.projectChanged += Schedule;
			AcAssetPostprocessor.AssetsChanged += OnAssetsChanged;
		}

		/// <summary>監視対象。保持するのは突き合わせ用のアセットパスだけでよい。</summary>
		public void SetController(AnimatorController controller)
		{
			_assetPath = controller != null ? AssetDatabase.GetAssetPath(controller) : null;
		}

		/// <summary>
		/// ウィンドウがフォーカスを得たときに呼ぶ。標準 Animator ウィンドウでの編集は
		/// projectChanged を出さないことがあるので、この経路が実質の保険になる。
		/// </summary>
		public void NotifyFocus()
		{
			Schedule();
		}

		private void OnAssetsChanged(string[] paths)
		{
			if (string.IsNullOrEmpty(_assetPath))
			{
				return;
			}
			for (int i = 0; i < paths.Length; i++)
			{
				if (string.Equals(paths[i], _assetPath, StringComparison.Ordinal))
				{
					Schedule();
					return;
				}
			}
		}

		private void Schedule()
		{
			if (_pending || _disposed)
			{
				return;
			}
			_pending = true;
			EditorApplication.delayCall += Fire;
		}

		private void Fire()
		{
			_pending = false;
			if (_disposed)
			{
				return;
			}
			Changed?.Invoke();
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			Undo.undoRedoPerformed -= Schedule;
			EditorApplication.projectChanged -= Schedule;
			AcAssetPostprocessor.AssetsChanged -= OnAssetsChanged;
			_assetPath = null;
		}
	}

	/// <summary>
	/// 再インポート（外部ツールや手作業でのアセット書き換え）を拾うための入口。
	/// <see cref="AcChangeWatcher"/> はインスタンスなので、Unity が呼ぶ静的フックを
	/// ここで受けて静的イベントに流す。
	/// </summary>
	internal sealed class AcAssetPostprocessor : AssetPostprocessor
	{
		internal static event Action<string[]> AssetsChanged;

		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			if (AssetsChanged == null || importedAssets.Length == 0)
			{
				return;
			}
			AssetsChanged(importedAssets);
		}
	}
}
