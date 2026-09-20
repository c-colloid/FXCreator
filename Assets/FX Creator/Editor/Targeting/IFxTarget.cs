using colloid.FXCreator.AnimatorGraph;
using UnityEditor.Animations;
using UnityEngine;
#if VRC
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// 編集対象の解決と適用先（Docs/FXCreator-Design.md D1 / §2.3）。
	///
	/// D5（グラフと Controller は 1:1）と D6（完全ラウンドトリップ）の帰結として、
	/// 「Direct / NDMF(MA) の両対応」は<b>データモデルの分岐ではない</b>。
	/// どちらのモードでも編集しているのは普通の AnimatorController なので、
	/// グラフ以下のコードは何も知らなくてよい。分岐するのは
	/// <list type="bullet">
	/// <item>どの Controller を編集対象として引くか（<see cref="ResolveController"/>）</item>
	/// <item>パラメータとメニューをどこから引くか</item>
	/// <item>編集が確定したあと何を同期するか（<see cref="OnAfterEdit"/>）</item>
	/// </list>
	/// の3点だけ。
	///
	/// VRC SDK が入っていない環境でもコンパイルできるよう、パラメータ／メニューの
	/// 戻り値は <c>#if VRC</c> で切り替える（既存の <c>VrcParameterPanel</c> と同じ流儀）。
	/// </summary>
	public interface IFxTarget
	{
		/// <summary>モードの同定に使う不変のキー（"direct" / "ndmf-ma"）。設定の保存先キーにもなる。</summary>
		string Id { get; }

		/// <summary>セレクタに出す短い名前。</summary>
		string DisplayName { get; }

		/// <summary>セレクタのツールチップ。何が起きるモードなのかを1行で。</summary>
		string Description { get; }

		GameObject Avatar { get; }

		/// <summary>
		/// このモードが使えるか。使えないときは <paramref name="reason"/> に理由を入れる。
		/// MA 未インストールなら <c>NdmfMaTarget</c> がここで false を返し、UI はモードを選ばせない。
		/// </summary>
		bool IsAvailable(out string reason);

		/// <summary>
		/// このアバターで<b>すでにこのモードの支度ができている</b>か。
		/// 既定モードの推測に使う（設定ではなくシーンの実体から決める）。
		/// </summary>
		bool IsAlreadySetUp { get; }

		/// <summary>
		/// 編集対象の Controller。まだ支度ができていなければ null を返す
		/// （ここでは<b>何も作らない・何も聞かない</b>。副作用は
		/// <see cref="TryPrepareForEditing"/> に寄せる）。
		/// </summary>
		AnimatorController ResolveController();

		/// <summary>
		/// 編集できる状態にする。Controller が無ければ作り、書けない場所にあるなら複製を提案する。
		///
		/// 副作用のあるものはここだけ。再構築のたびに呼ばれる <see cref="ResolveController"/> と
		/// 分けてあるのは、外部変更や Undo のたびにダイアログが出ないようにするため。
		/// </summary>
		/// <param name="interactive">false ならダイアログを出さずに既定の処置を行う（テスト・自動化用）。</param>
		/// <param name="reason">できなかった理由。ユーザーが断った場合もここに入る。</param>
		/// <returns>編集できる Controller が用意できたら true。false なら読み取り専用で開く。</returns>
		bool TryPrepareForEditing(bool interactive, out string reason);

		/// <summary>
		/// 同期パラメータの宣言先（§6.2）。Direct は <c>VRCExpressionParameters</c>、
		/// NDMF(MA) は <c>ModularAvatarParameters</c>。
		///
		/// 生の型ではなく <see cref="IFxParameterStore"/> を返すのは、パネルに
		/// 「いまどちらのモードか」を持ち込まないため。持ち込むと、MA モードで
		/// 何もできないか、非破壊のつもりでアバター同梱アセットを書き換えるかになる。
		/// </summary>
		IFxParameterStore ResolveParameterStore();

#if VRC
		VRCExpressionsMenu ResolveMenu();
#else
		UnityEngine.Object ResolveMenu();
#endif

		/// <summary>
		/// 変更確定時のフック。Direct は <c>SetDirty</c> だけ、
		/// NDMF(MA) は MA コンポーネントのパラメータ定義を Controller から同期する。
		/// </summary>
		void OnAfterEdit(AcEditReport report);
	}
}
