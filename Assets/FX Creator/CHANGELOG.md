# CHANGELOG

このファイルは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に倣い、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Added

- **Animator エディタ**（設計書の⑤ / v0.1 本体）
  - 自作 UI Toolkit ノードグラフ。パン・ズーム（0.2〜6.0）・矩形選択・
    グリッドスナップ・ビューポートカリング
  - AnimatorController の **1:1 ライブビュー**。中間データモデルを持たないので、
    グラフ操作はそのまま `UnityEditor.Animations` の呼び出しになる
  - State / Sub-State Machine / Any State / Entry / Exit の表示、
    サブステートマシンへのドリルダウンとパンくず
  - 編集（State・遷移の追加削除、Motion・Speed・Write Defaults、遷移の条件）。
    1 操作 = 1 Undo
  - 同じパラメータで分岐する遷移群を 1 つの **toggle / switch ノード**に畳む。
    分岐元は State だけでなく Entry / Any State / サブステートマシンも対象
  - ノード内の**アバタープレビュー**。共有プレビューシーン + 描画キュー + LRU キャッシュ。
    再生は選択中のノードだけ。寄り先（顔 / 全身など）と画角を State ごとに保存
  - **VAR / parameter / menu パネル**（Unity Overlay 上のフローティング）
  - **Direct / NDMF (MA) の 2 モード**。編集対象の解決と適用先だけが分岐し、
    グラフ以下のコードは共通
- パラメータの**型変更**。条件を新しい型へ読み替え、読み替えられない参照は
  件数と理由を出したうえでそのまま残す
- VAR と同期設定（Expression Parameters / MA Parameters）の**連携**。
  差分をボタンで解消、改名・型変更の相互追従、3 パネル間の選択連動

### Fixed

- オーバーレイパネルを操作すると中身が消える問題。
  `_view = BuildContent(new ...)` は右辺が先に評価されるため、
  表示対象を配る時点でフィールドがまだ古いビューを指していた
- パネルの列レイアウト。名前が `VRCI` のように切り詰められ、
  menu パネルの右端が窓の外へはみ出していた
- Any State / Entry / Exit を State と同じ大きさで描いていたため、
  State 間を通る遷移の線が隠れていた（標準 Animator の実測は 160×28 対 200×36）
- VRChat 組み込みパラメータ（`Viseme` / `GestureLeft` など）を
  同期設定へ追加するよう勧めてしまう問題

### Known limitations

- ノードのリサイズ・コピー / ペースト・自動レイアウト・ミニマップは未実装（v0.2 以降）
- Expression Menu パネルは**表示のみ**。円環 UI と D&D 登録は v0.2〜v0.3
- メニューから値を動かして遷移先を試す経路は未実装（設計書 §6.7）
- 高倍率ではノード 1 つが画面より広くなり、プレビューを見るにはパンが要る（§5.5）
- MA 非依存の NDMF プラグインパス（`NdmfNativeTarget`）は v0.3
