using System;
using UnityEditor;
using UnityEditor.Animations;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>
	/// 「この Controller に書いても保存されるか」の判定。
	///
	/// グラフ（<see cref="AcGraphSource"/> の読み取り専用判定）と
	/// Targeting（複製して差し替えるかの判断）が<b>同じ規則</b>を見る必要がある。
	/// 片方だけが「編集できる」と思っていると、消える変更を作らせるか、
	/// 編集できるものに複製を勧めるかのどちらかになる。
	/// </summary>
	public static class AcControllerAccess
	{
		/// <summary>
		/// 書き込めるなら true。書けないときは <paramref name="reason"/> に
		/// そのまま UI に出せる日本語の理由が入る。
		/// </summary>
		public static bool IsWritable(AnimatorController controller, out string reason)
		{
			reason = null;

			if (controller == null)
			{
				reason = "Controller がありません";
				return false;
			}

			string path = AssetDatabase.GetAssetPath(controller);
			if (string.IsNullOrEmpty(path))
			{
				reason = "アセットとして保存されていない Controller です";
				return false;
			}

			if (path.StartsWith("Packages/", StringComparison.Ordinal))
			{
				// 不変パッケージ内のアセットは書き換えても保存されない。
				reason = "パッケージ内の Controller なので編集できません";
				return false;
			}

			return true;
		}
	}
}
