using System;
using System.Reflection;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// <c>MeshGenerationContext.DrawText</c> に渡すフォントの解決
	/// （Docs/FXCreator-Design.md §3 のエッジラベル用）。
	///
	/// <b>ラベル描画にはフォントのフォールバックが効かない。</b>
	/// UI Toolkit の <c>Label</c> は字が無ければ別のフォントに落ちるが、
	/// <c>DrawText</c> は<b>渡した FontAsset そのものだけ</b>で描く。
	/// エディタ既定の <c>Inter-Regular SDF</c> をそのまま渡すと、
	/// 日本語のパラメータ名やステート名が全部豆腐になる。
	///
	/// そこで <c>jp.colloid.uitk-font-fix</c> の <c>FontFix.CjkUiFontAsset</c> を使う。
	/// 参照は<b>リフレクション</b>で引く。asmdef に直接書くとパッケージが
	/// 入っていないプロジェクトで参照切れになり、FX Creator 本体が
	/// コンパイルできなくなるため（MA を別アセンブリに分けたのと同じ理由）。
	/// 見つからなければパネルの解決済みフォントに落ちるので、
	/// 無くても動く（英数字だけは正しく出る）。
	/// </summary>
	internal static class FXCTextFont
	{
		private const string FontFixTypeName = "Colloid.UitkFontFix.FontFix, Colloid.UitkFontFix.Editor";

		private static bool _probed;
		private static PropertyInfo _cjkUiFontAsset;
		private static MethodInfo _sanitize;
		private static MethodInfo _applyCjkUi;

		private static void Probe()
		{
			if (_probed)
			{
				return;
			}
			_probed = true;

			try
			{
				Type type = Type.GetType(FontFixTypeName, false);
				if (type == null)
				{
					return;
				}
				_cjkUiFontAsset = type.GetProperty(
					"CjkUiFontAsset", BindingFlags.Public | BindingFlags.Static);
				_sanitize = type.GetMethod(
					"SanitizeDisplayText", BindingFlags.Public | BindingFlags.Static, null,
					new[] { typeof(string) }, null);
				_applyCjkUi = type.GetMethod(
					"ApplyCjkUi", BindingFlags.Public | BindingFlags.Static, null,
					new[] { typeof(VisualElement) }, null);
			}
			catch (Exception)
			{
				// パッケージの構成が変わっていても、ラベルが出ないだけで済ませる。
				_cjkUiFontAsset = null;
				_sanitize = null;
				_applyCjkUi = null;
			}
		}

		/// <summary>
		/// いま使うべきフォント。<paramref name="context"/> は取れなかったときの
		/// フォールバック元（その要素が解決しているフォント）。
		///
		/// <b>戻り値をフィールドに保持してはいけない。</b>
		/// <c>FontFix.CjkUiFontAsset</c> は「読むたびにアトラスの破損を検査して
		/// その場で直す」という契約で、Play Mode の出入りでマテリアルが壊れるため、
		/// ゲッターを通らないと壊れたアセットを使い続けることになる。
		/// 毎フレーム呼ぶ前提の軽い処理。
		/// </summary>
		public static FontAsset Resolve(VisualElement context)
		{
			Probe();

			if (_cjkUiFontAsset != null)
			{
				try
				{
					var asset = _cjkUiFontAsset.GetValue(null) as FontAsset;
					if (asset != null)
					{
						return asset;
					}
				}
				catch (Exception)
				{
					// 解決に失敗したら以降は問い合わせない。
					_cjkUiFontAsset = null;
				}
			}

			return context != null ? context.resolvedStyle.unityFontDefinition.fontAsset : null;
		}

		/// <summary>
		/// コンテナに Latin+CJK フォントを敷く。子孫は継承する。
		/// パッケージが無ければ何もしない（呼び出し側はフォントが変わった前提を置かない）。
		/// </summary>
		public static void ApplyCjkUi(VisualElement containerRoot)
		{
			if (containerRoot == null)
			{
				return;
			}

			Probe();
			if (_applyCjkUi == null)
			{
				return;
			}

			try
			{
				_applyCjkUi.Invoke(null, new object[] { containerRoot });
			}
			catch (Exception)
			{
				_applyCjkUi = null;
			}
		}

		/// <summary>
		/// 表示用に文字列を整える。異体字セレクタやゼロ幅文字は字を持たないので、
		/// そのまま描くと四角が出るうえ<b>描画のたびに警告が出る</b>。
		/// パラメータ名やステート名はユーザーが付けるものなので、実際に混ざる。
		/// </summary>
		public static string Sanitize(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			Probe();
			if (_sanitize == null)
			{
				return text;
			}

			try
			{
				return _sanitize.Invoke(null, new object[] { text }) as string ?? text;
			}
			catch (Exception)
			{
				_sanitize = null;
				return text;
			}
		}
	}
}
