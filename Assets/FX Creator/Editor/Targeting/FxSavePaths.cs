using System.Text;
using UnityEditor;
using UnityEngine;

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// FX Creator が所有するアセットの置き場所（§2.3 の
	/// <c>Assets/FX Creator/Save/&lt;Avatar&gt;/</c>）。
	///
	/// Direct（複製の差し替え先）と NDMF(MA)（生成する専用 Controller）の
	/// 両方が使うので1箇所に置く。
	/// </summary>
	public static class FxSavePaths
	{
		public const string Root = "Assets/FX Creator/Save";

		/// <summary>アバター用のフォルダを（無ければ作って）返す。</summary>
		public static string EnsureAvatarFolder(GameObject avatar)
		{
			EnsureFolder(Root);
			string folder = Root + "/" + Sanitize(avatar != null ? avatar.name : "Unnamed");
			EnsureFolder(folder);
			return folder;
		}

		/// <summary>作らずに場所だけ知りたいとき（存在チェック用）。</summary>
		public static string AvatarFolder(GameObject avatar)
		{
			return Root + "/" + Sanitize(avatar != null ? avatar.name : "Unnamed");
		}

		/// <summary>そのパスが FX Creator の管理下にあるか。</summary>
		public static bool IsManaged(string assetPath)
		{
			return !string.IsNullOrEmpty(assetPath)
				&& assetPath.Replace('\\', '/').StartsWith(Root + "/", System.StringComparison.Ordinal);
		}

		/// <summary>
		/// 既にあるパスと衝突しない名前を作る。複製を2回目に走らせたときに
		/// 前の複製を<b>黙って上書きしない</b>ためのもの。
		/// </summary>
		public static string UniqueAssetPath(string folder, string fileName, string extension)
		{
			string basePath = folder + "/" + Sanitize(fileName) + extension;
			if (AssetDatabase.LoadAssetAtPath<Object>(basePath) == null)
			{
				return basePath;
			}
			return AssetDatabase.GenerateUniqueAssetPath(basePath);
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
			{
				return;
			}
			int slash = path.LastIndexOf('/');
			string parent = path.Substring(0, slash);
			string leaf = path.Substring(slash + 1);
			EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, leaf);
		}

		/// <summary>アバター名はそのままではパスにできない（"/" や ":" が入る）。</summary>
		public static string Sanitize(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return "Unnamed";
			}
			var sb = new StringBuilder(name.Length);
			for (int i = 0; i < name.Length; i++)
			{
				char c = name[i];
				sb.Append(
					c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
					c == '"' || c == '<' || c == '>' || c == '|'
						? '_'
						: c);
			}
			string trimmed = sb.ToString().Trim();
			return trimmed.Length == 0 ? "Unnamed" : trimmed;
		}
	}
}
