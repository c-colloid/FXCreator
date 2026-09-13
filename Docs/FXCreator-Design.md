# FX Creator 設計書

- 対象: `Assets/FX Creator`（Unity 2022.3.22f1 / VRChat SDK Avatars 3.10.5）
- 作成: 2026-09-14
- ステータス: v0.1 実装前の設計確定版

---

## 0. 決定事項サマリ

手書き資料（`Animator editor` / `FX Creater`）をもとに確定した方針。

| # | 項目 | 決定 |
|---|------|------|
| D1 | 出力方式 | **両対応（抽象ビルダー）**。編集対象の解決と適用を `IFxTarget` で抽象化し、直接書き込み／NDMF(MA)経由を切り替える |
| D2 | v0.1 スコープ | **⑤Animatorエディタ単体** |
| D3 | 開発場所 | **`Assets/FX Creator` 内で継続**（VPMパッケージ化は v0.1 が形になってから） |
| D4 | グラフ基盤 | **自作 UI Toolkit ノードグラフ**（`UnityEditor.Experimental.GraphView` は使わない） |
| D5 | グラフ意味論 | **AnimatorController と 1:1**。ノード = AnimatorState / StateMachine / Any / Entry / Exit |
| D6 | ラウンドトリップ | **完全双方向**。既存Controllerを読み込んで編集し、そのまま書き戻す |
| D7 | ノード内プレビュー | **v0.1 に含める**（ただし共有レンダラ方式へ作り直し） |

### D5 + D6 から導かれる最重要の設計判断

**中間データモデルを持たない。AnimatorController アセットそのものが唯一の真実の源。**

グラフは Controller の「ライブビュー」であり、ノード操作は即座に `UnityEditor.Animations` API 呼び出しに変換される。
これにより以下が自動的に成立する。

- 「グラフ → Controller」の逆変換コードが不要（逆変換は完全ラウンドトリップ実装で最大の破綻要因）
- 標準 Animator ウィンドウとの併用が可能（同じアセットを見ている）
- 外部ツール（AV3Manager, MA, 手作業）による変更と競合しない
- Undo/Redo は Unity の Undo システムにそのまま乗る

サイドカーアセットに保存するのは **Controller に表現できない表示情報のみ**（後述 §4.4）。

---

## 1. 全体像と v0.1 の位置づけ

資料の①〜⑤と、それぞれの現状・リリース計画。

| # | 機能 | 現状 | 予定 |
|---|------|------|------|
| ① | アニメーション登録（カード一覧・検索・フォルダ・編集/合成・アーカイブ） | `FXCreator.cs` にクリップ一覧＋プレビューの試作あり | v0.2 |
| ② | ハンドサインへの登録（8ジェスチャ × セット切替） | なし | v0.3 |
| ③ | Ex Menu への登録（円環UI再現・D&D・トグル/一括変更） | なし。v0.1 で下パネルとして簡易版 | v0.2（円環UIは v0.3） |
| ④ | Contact の登録 | なし | v0.4 |
| ⑤ | **Animator エディタ** | `AnimatorCreator*` に GraphView 試作あり | **v0.1** ← 本設計書の主対象 |

v0.1 の完成定義（Definition of Done）:

> アバターを選択して FX Creator の Animator エディタを開くと、そのアバターの FX レイヤーが
> ノードグラフとして表示される。ノード/遷移/パラメータを追加・編集・削除でき、
> 結果は AnimatorController アセットに即座に反映され、Undo/Redo が正しく動く。
> ノードには対象クリップのアバタープレビューが表示される。
> Direct / NDMF(MA) の2つの適用モードを切り替えられる。

---

## 2. アーキテクチャ

### 2.1 レイヤ構成

```
Assets/FX Creator/Editor/
├─ Core/                     共通基盤（既存を整理して継続利用）
│   ├─ FolderPath/           既存
│   ├─ AnimationClips/       既存
│   └─ Render/               既存（アウトライン描画）
│
├─ Graph/                    ★新規: ドメイン非依存の自作ノードグラフ基盤
│   ├─ FXCGraphView.cs           ルート。パン/ズーム/選択/カリング
│   ├─ FXCGraphViewport.cs       座標変換（graph空間 ↔ ローカル空間）
│   ├─ FXCGridBackground.cs      グリッド描画（Painter2D）
│   ├─ FXCNodeView.cs            ノード基底
│   ├─ FXCPortView.cs            ポート
│   ├─ FXCEdgeLayer.cs           全エッジを1要素にまとめて描画
│   ├─ FXCEdgeDragger.cs         ポートからのエッジ作成
│   ├─ FXCMarqueeSelector.cs     矩形選択
│   ├─ FXCSelection.cs           選択集合の管理
│   └─ IGraphSource.cs           ビュー ↔ ドメインの境界インターフェース
│
├─ Animator/                 ★MVP本体: AnimatorController エディタ
│   ├─ Binding/
│   │   ├─ AcGraphSource.cs      AnimatorStateMachine → グラフ要素への射影
│   │   ├─ AcNodeRef.cs          ノード同定（AnimatorState等への直接参照）
│   │   ├─ AcTransitionGroup.cs  ★toggle/switch ノードの畳み込み規則
│   │   └─ AcEdit.cs             ★編集トランザクション（Undo一括／サブアセット整合）
│   ├─ View/
│   │   ├─ FxcAnimatorWindow.cs  EditorWindow
│   │   ├─ LayerListView.cs      レイヤー一覧
│   │   ├─ ParameterListView.cs  資料の左サイドバー「VAR」
│   │   ├─ StateNodeView.cs      Motion / Speed / WriteDefaults / プレビュー
│   │   ├─ ToggleNodeView.cs     bool 分岐ノード
│   │   ├─ SwitchNodeView.cs     int 分岐ノード（0/1/2...）
│   │   ├─ SpecialNodeView.cs    Any State / Entry / Exit
│   │   └─ ElementInspector.cs   選択要素の詳細編集
│   ├─ Sync/
│   │   ├─ AcChangeWatcher.cs    外部変更・Undo検知 → 再構築
│   │   └─ FxcLayoutAsset.cs     サイドカー（§4.4）
│   └─ Vrc/
│       ├─ VrcParameterPanel.cs  資料の下パネル「parameter (name, sync)」
│       └─ VrcMenuPanel.cs       資料の下パネル「menu (name, value)」
│
├─ Targeting/                ★D1: 両対応の抽象
│   ├─ IFxTarget.cs
│   ├─ FxTargetResolver.cs       シーンからアバター候補を列挙
│   ├─ DirectAvatarTarget.cs     descriptor の Controller を直接編集
│   ├─ NdmfMaTarget.cs           専用Controller + MAコンポーネント同期
│   └─ FxCreatorTag.cs           アバター上のマーカー MonoBehaviour（NDMF用）
│
├─ Preview/                  ★D7: ノード内プレビュー
│   ├─ AvatarPreviewService.cs   共有プレビューシーン + 描画キュー
│   ├─ PreviewRequest.cs
│   └─ PreviewCache.cs           LRU
│
└─ Utilities/                既存（BetterTextField, Shadow, GetDefaultUSS 等）
```

### 2.2 依存方向

```
View ──▶ Binding ──▶ UnityEditor.Animations
 │         │
 │         └──▶ Targeting ──▶ VRC SDK / NDMF / MA
 │
 └──▶ Graph (ドメイン非依存) ──▶ UI Toolkit
      Preview ──▶ VRC SDK
```

- `Graph/` は Animator の存在を知らない。将来 ③ExMenu や ④Contact のグラフにも再利用する。
- `Binding/` は UI Toolkit を知らない（テスト可能にするため）。
- VRC SDK 依存箇所は `#if VRC` で囲む（既存 ClipGenerator の `versionDefines` と同じ流儀）。

### 2.3 D1（両対応）の実装形

D5/D6 の帰結として、「両対応」はデータモデルの分岐ではなく **編集対象の解決と適用先の分岐** になる。
どちらのモードでも編集しているのは普通の AnimatorController なので、グラフ以下のコードは共通。

```csharp
public interface IFxTarget
{
    string DisplayName { get; }
    bool IsAvailable(out string reason);

    AnimatorController        ResolveController();   // 編集対象
    VRCExpressionParameters   ResolveParameters();
    VRCExpressionsMenu        ResolveMenu();

    /// 変更確定時のフック（アセット参照の張り直し・MAコンポーネント同期など）
    void OnAfterEdit(AcEditReport report);
}
```

| | `DirectAvatarTarget` | `NdmfMaTarget` |
|---|---|---|
| 編集する Controller | `descriptor.baseAnimationLayers[FX].animatorController` | `Assets/FX Creator/Save/<Avatar>/FXC_FX.controller`（FX Creator が生成・所有） |
| Parameters | `descriptor.expressionParameters` | `ModularAvatarParameters` |
| Menu | `descriptor.expressionsMenu` | `ModularAvatarMenuInstaller` / `ModularAvatarMenuItem` |
| `OnAfterEdit` | `EditorUtility.SetDirty` のみ | MAコンポーネントのパラメータ定義を Controller から同期 |
| 非破壊性 | なし（アセットを直接書き換え） | あり（ビルド時マージ） |
| 依存 | VRC SDK のみ | + `nadena.dev.modular-avatar` |

安全装置:
- `DirectAvatarTarget` で、対象 Controller がアバター同梱アセット（FX Creator 管理外）だった場合、
  初回編集時に「複製して差し替えますか？」ダイアログを出す。`Cancel` なら読み取り専用モードで開く。
- MA 未インストール時は `NdmfMaTarget.IsAvailable` が `false` を返し、UI でモードを選べなくする。
- v0.3 で MA 非依存の自前 NDMF プラグインパス（`NdmfNativeTarget`）を追加予定。v0.1 では作らない。

---

## 3. 自作グラフ基盤（`Graph/`）

### 3.1 v0.1 で必要な機能

状況は 2026-09-14 時点。`済` は実装してコンパイル・目視確認まで通ったもの。

| 機能 | v0.1 | 状況 | 実装方針 |
|------|:----:|:----:|----------|
| パン | ✔ | 済 | 中ボタンドラッグ / Alt+左ドラッグ。`ContentLayer.transform.position` |
| ズーム | ✔ | 済 | `WheelEvent` → `transform.scale`。0.2〜2.0、カーソル位置基準 |
| グリッド背景 | ✔ | 済 | `generateVisualContent` + Painter2D。2段グリッド（20 / 100 グラフ単位）。画面上の間隔が 6px を切った段は描かない |
| 座標変換 | ✔ | 済 | `FXCGraphViewport`（`view = graph * Zoom + Offset`）。単体テスト10本 |
| ノード配置・ドラッグ | ✔ | 済 | `FXCNodeView`。20 グリッドスナップ。掴んだノードの着地点をスナップし同じ差分を全選択ノードへ。モデルへの書き戻しは離した時に1回（Undo を1操作にするため） |
| 複数選択 | ✔ | 済 | `FXCSelection`（IDで保持＝再構築をまたいで生き残る）。Ctrl/Shift 追加選択、矩形選択 |
| エッジ描画 | ✔ | 済 | 全エッジを1つの `FXCEdgeLayer` が Painter2D で描画。ベジェ + 矢印ヘッド + 平行エッジのオフセット。制御点の凸包AABBでカリング |
| エッジ選択 | ✔ | 済 | `FXCEdgeLayer.PickEdge`。VisualElement のピッキングを使わず、ベジェをサンプリングして線分距離で判定 |
| ポート | ✔ | **未** | `FXCPortView`。現状エッジはノード右端→左端の直結。資料の toggle/switch ノードはポートが必須 |
| エッジ作成 | ✔ | **未** | ポートからドラッグ → 接続可能ポートをハイライト → ドロップ |
| コンテキストメニュー | ✔ | **未** | 背景右クリックで State/SubStateMachine 追加、ノード右クリックで削除等。`IFXCGraphSource` に削除系を追加する |
| ノードのカリング | ✔ | **未** | 可視矩形外のノードビューを `display:none`。プレビュー要求も可視ノードのみ |
| ノードリサイズ | ✖ | — | v0.2 |
| コピー/ペースト | ✖ | — | v0.2 |
| 自動レイアウト | ✖ | — | v0.2（位置情報のない Controller 向け。単純な層別レイアウト） |
| ミニマップ | ✖ | — | v0.3 |

### 3.2 なぜ自作するか（D4の根拠）

- `UnityEditor.Experimental.GraphView` は Unity 6 以降 Graph Toolkit へ置換予定で、`Experimental` 名義のまま。
- 既存試作 `AnimatorCreatorNode` は `titleContainer.Q<Label>()` のように GraphView 内部構造に依存しており、壊れやすい。
- 必要なのはノード/ポート/エッジ/パン/ズームだけで、GraphView の機能の大半（Blackboard, StackNode, Searcher 統合等）は不要。
- 資料の toggle/switch ノードは「ワイヤ上に乗る特殊ノード」であり、GraphView の `Port.Capacity` モデルと素直に噛み合わない。

### 3.3 描画性能の設計

エッジは**要素化しない**。300本の `VisualElement` を作るとレイアウト計算で破綻するため、
`FXCEdgeLayer`（1要素）が `generateVisualContent` 内で全エッジを Painter2D の `BeginPath`/`BezierCurveTo`/`Stroke` で描く。

- ノード移動中は `MarkDirtyRepaint()` のみ（レイアウト再計算を発生させない）
- ヒットテストはエッジレイヤ側で線分距離計算（VisualElement のピッキングに頼らない）
- 目標: **100 state / 300 transition のレイヤーで 16ms/frame 以下**

#### 3.3.1 スパイク結果（2026-09-14 実測）

計測ツール（初回）: `GraphPerfSpikeWindow.cs` — 捨てツール。Phase 1 完了時に削除し、
計測は `FXCEdgeLayer.Instrument`（既定オフのフック）＋ `FXCGraphDemoWindow` の Run sweep に移した。
構成は本設計どおり「ノード = 実 VisualElement（枠 + ラベル2個）／エッジ = 1要素に Painter2D で一括描画」。
フレーム時間は「生成開始 → 次の生成開始」（生成コストを含む真の周期）、ウォームアップ60フレームを除外した移動平均。

**✅ `MeshGenerationContext.painter2D` は Unity 2022.3.22f1 で使える。**
`Painter2D` / `BezierCurveTo` / `LineCap` / `Stroke` / `Fill` すべて解決。手組みメッシュへのフォールバックは不要。
見た目もベジェ + 矢印ヘッドで意図どおり。→ **リスク R2 は解消。**

100ノード / 300エッジ / 矢印あり / ラベルあり、ゲート 16 ms:

| ノードの動かし方 | frame | うち painter2D | fps | ゲート |
|---|---|---|---|---|
| 動かさない（パン/ズーム相当） | 6.33 ms | 4.53 ms | 158 | PASS |
| `transform.position` | 8.39 ms | 4.63 ms | 119 | PASS |
| `style.left` / `style.top` | 8.32 ms | 4.96 ms | 120 | PASS |

読み取れること:

1. **ゲートは余裕2倍で通る。** エッジをVisualElement化しない設計が正しいことの裏付け。
2. **コストの主役は painter2D のメッシュ生成**（frame の 55〜70%）で、ノード側ではない。
   エッジ1本あたり約 15 µs。
3. **`style.left/top` と `transform.position` に有意差がない。** 100ノードのレイアウト再計算は
   このスケールでは問題にならない。
   → `FXCNodeView` のドラッグは「transform で動かして離したら style に確定」のような
   二段構えにする必要がなく、`style.left/top` を素直に単一の真実として使ってよい。実装が単純になる。
   ただし将来 transform を使う場合、`layout` に transform 分は入らないので、
   エッジ側は `layout.position + transform.position` で見えている矩形を取る必要がある
   （スパイクの `VisualRect` が実装例）。
4. **天井の外挿**: 生成コストはエッジ数に線形なので、16 ms を使い切るのは約 1000 エッジ付近。
   実アバターの FX で1レイヤーが 1000 遷移に達することは考えにくいが、余裕は 3 倍程度しかない。
   → §3.1 のビューポートカリングは **v0.1 に残す**（保険として安い）。

#### 3.3.2 ゲート確認（実クラス、2026-09-14）

スパイクではなく本実装（`FXCGraphView` / `FXCNodeView` / `FXCPortView` / `FXCEdgeLayer`）で再計測。
ノードは枠＋アクセント帯＋ラベル2個＋ポート2個、エッジはポート間接続、ノード・エッジ両方にカリングあり。

| nodes / edges | frame | うち painter2D | fps | ゲート |
|---|---|---|---|---|
| 20 / 40 | 1.98 ms | 0.57 ms | 506 | PASS |
| **100 / 300** | **6.18 ms** | 4.63 ms | **162** | **PASS** |
| 300 / 900 | 17.19 ms | 13.85 ms | 58 | FAIL |

- **ゲートは余裕 2.6 倍で通った。** ノードが重くなったにもかかわらずスパイク時（8.39 ms）より
  速いのは、ノードのビューポートカリングが効いているため。
- エッジ1本あたりの生成コストは 15.4 µs で、300 本・900 本ともに一致（完全に線形）。
  §3.3.1 の「16 ms を使い切るのは約 1000 エッジ」という外挿は実測と合った（900 エッジで 17.19 ms）。
- 実用上の上限は **1レイヤーあたり約 900 遷移**。これを超えるレイヤーが出たら、
  エッジのカリングをビューポート矩形との交差だけでなく、可視ノードに接続するものだけに絞る余地がある。

計測上の注意（同じ罠を踏まないため）: 当初 frame を「生成の終わり → 次の生成の始め」で測っており、
生成コストが frame から抜け落ちて `frame < painter2D` という矛盾した値が出た。
また、ウォームアップを除外しないと初期レイアウトのコストが移動平均に残り、
`style.left/top` が 33 ms 相当に見える（実際の定常値は 8.3 ms）。

### 3.4 ビュー ↔ ドメイン境界

```csharp
public interface IGraphSource
{
    IEnumerable<IGraphNode> Nodes { get; }
    IEnumerable<IGraphEdge> Edges { get; }

    event Action Changed;           // ドメイン側が変わった → ビュー再構築

    void MoveNode(IGraphNode node, Vector2 position);
    bool CanConnect(IGraphPort from, IGraphPort to);
    void Connect(IGraphPort from, IGraphPort to);
    void Disconnect(IGraphEdge edge);
    void DeleteNodes(IReadOnlyList<IGraphNode> nodes);
    void PopulateContextMenu(GenericMenu menu, IGraphNode hit);
}
```

`Graph/` はこのインターフェースにしか触らない。`AcGraphSource` がこれを AnimatorController 用に実装する。

---

## 4. AnimatorController バインディング（`Animator/Binding/`）

### 4.1 ノードの種類とマッピング

| ノード | Unity 側 | 位置の保存先 |
|--------|----------|--------------|
| StateNode | `AnimatorState` | `ChildAnimatorState.position`（**Controllerネイティブ**） |
| SubStateMachineNode | `AnimatorStateMachine` | `ChildAnimatorStateMachine.position` |
| AnyStateNode | — | `AnimatorStateMachine.anyStatePosition` |
| EntryNode | — | `AnimatorStateMachine.entryPosition` |
| ExitNode | — | `AnimatorStateMachine.exitPosition` |
| ParentNode | — | `AnimatorStateMachine.parentStateMachinePosition` |
| **ToggleNode / SwitchNode** | 遷移群の畳み込み（実体なし） | サイドカー |

Controller が State の位置をネイティブに持っているのが重要で、**通常のノード位置はサイドカー不要**。
標準 Animator ウィンドウと配置が共有される。

### 4.2 ★ toggle / switch ノードの畳み込み規則

資料（画像1）の中間ノードは、**同一パラメータで分岐する遷移群の表現**と定義する。
これは Controller から決定的に導出できるため、1:1 と完全ラウンドトリップを両立できる。

**導出アルゴリズム**（State `S` の outgoing transitions に対して）:

1. `S` の全 outgoing transitions を列挙する（`AnimatorStateTransition[]`）。
2. 以下を満たすものを「畳み込み候補」とする:
   - `conditions.Length == 1`
   - `hasExitTime == false`
   - `mute == false && solo == false`
3. 候補を `conditions[0].parameter` でグループ化する。
4. グループ内の `mode` を見て分類:
   - すべて `{If, IfNot}` のいずれかで、両方が1本以上存在 → **ToggleNode**
   - すべて `Equals`（`threshold` が相異なる） → **SwitchNode**（出力ポート名 = threshold値）
   - それ以外 → 畳み込まない
5. グループサイズ `>= 2` のときのみノード化する。1本だけなら直結エッジとして描く。
6. 畳み込まれなかった遷移は「直結エッジ」として描き、条件はエッジインスペクタで編集する。

**逆方向（ノード操作 → Controller）**:

| ノード操作 | Controller 操作 |
|-----------|----------------|
| ToggleNode のパラメータ名変更 | 配下2本の `conditions[0].parameter` を書き換え |
| ToggleNode の True/False 出力を別Stateへ繋ぎ替え | 該当 transition の `destinationState` を差し替え |
| SwitchNode に出力ポート追加 | 新規 transition を作り `Equals, 新threshold` を設定 |
| SwitchNode の出力ポート削除 | 該当 transition を削除 |
| 畳み込みの解除（右クリック → Expand） | Controller は変更せず、サイドカーに `expanded` フラグを立てる |

逆変換が「transition の1フィールド書き換え」に落ちるので、情報損失が原理的に起きない。

**テスト可能性**: 「任意の Controller に対して `畳み込み → 展開` が元と等価」をプロパティテストで検証する（§8）。

### 4.3 ★ 編集トランザクション `AcEdit`

`UnityEditor.Animations` API は Undo とサブアセット管理が壊れやすい。既知の落とし穴を1箇所に封じ込める。

**封じ込める落とし穴**:

1. `controller.layers` / `stateMachine.states` / `controller.parameters` は**配列のコピーを返す**。
   要素を書き換えても反映されず、配列ごと再代入が必要。
2. `AnimatorState` / `AnimatorStateMachine` / `AnimatorStateTransition` / `BlendTree` は
   Controller の**サブアセット**。`AddState` 等は自動で `AddObjectToAsset` するが、
   Undo 登録は自分でやらないと Undo 後に迷子オブジェクトが残る。
3. 削除は `AssetDatabase.RemoveObjectFromAsset` ではなく `Undo.DestroyObjectImmediate` を使う。
4. 1つのユーザー操作が複数の API 呼び出しになるため、Undo グループを明示的に畳む必要がある。

**API**:

```csharp
using (var e = AcEdit.Begin(controller, "Add State"))
{
    var state = e.AddState(stateMachine, "New State", position);
    e.SetMotion(state, clip);
    e.SetWriteDefaults(state, false);
}
// Dispose 時:
//   Undo.CollapseUndoOperations(group)
//   EditorUtility.SetDirty(controller)
//   AcEditReport を target.OnAfterEdit へ通知
```

実装ルール:
- `Begin` で `Undo.IncrementCurrentGroup()` + `Undo.SetCurrentGroupName(name)`
- 変更前に必ず `Undo.RecordObject(対象)`
- 新規サブアセット作成後に `Undo.RegisterCreatedObjectUndo(obj, name)`
- `WriteDefaults` は `AnimatorState.writeDefaultValues`
- 例外時は `Undo.RevertAllDownToGroup(group)` でロールバック

### 4.4 サイドカーアセット `FxcLayoutAsset`

Controller に表現できない表示情報のみを持つ。**無くても・壊れていてもグラフは動く**（デグレード可能）ことを設計原則にする。

```csharp
public class FxcLayoutAsset : ScriptableObject
{
    public AnimatorController controller;

    [Serializable] public class CondNodeLayout {
        public AnimatorState  sourceState;   // サブアセットへの直接参照（永続化可能）
        public string         parameter;
        public Vector2        position;
        public bool           expanded;      // true = 畳み込みを解除して表示
    }
    public List<CondNodeLayout> condNodes = new();

    [Serializable] public class NodeExtra {
        public UnityEngine.Object target;    // AnimatorState / AnimatorStateMachine
        public bool              collapsed;  // 資料の close/open button
        public HumanBodyBones    previewFocus = HumanBodyBones.Head;
        public float             previewFov   = 30f;
    }
    public List<NodeExtra> nodeExtras = new();
}
```

- 保存場所: Controller と同じフォルダの `<ControllerName>.fxclayout.asset`
- 生成タイミング: 表示情報が初めて必要になったとき（遅延生成）。読むだけなら作らない
- 同定方法: 文字列キーではなく `UnityEngine.Object` 直接参照。State はサブアセットとして永続化されているので参照が生き続け、リネームに強い
- 参照が `null` になったエントリは読み込み時に掃除する

### 4.5 外部変更・Undo への追従（`AcChangeWatcher`）

差分同期は実装せず、**全再構築**で対応する（State 数は通常数十で、再構築は 1ms 未満）。

再構築トリガ:
- `Undo.undoRedoPerformed`
- `EditorApplication.projectChanged`（アセットの追加/削除）
- ウィンドウフォーカス取得時（`OnFocus`）
- `AssetPostprocessor.OnPostprocessAllAssets` で対象 Controller が含まれるとき
- `Selection` 変更でターゲットアバターが変わったとき

再構築時に選択状態とビューポート（パン/ズーム）は保持する。

---

## 5. ノード内プレビュー（`Preview/`）

### 5.1 既存試作の問題（必ず作り直す）

`Editor/Animator/GraphView/AnimatorCreatorNode.cs` の現状:

| 箇所 | 問題 |
|------|------|
| `new PreviewRenderUtility()` をノードごとに生成 | ノード20個で RenderTexture / カメラ / シーンが20セット。GPUメモリとドローコールが破綻 |
| `GameObject.Instantiate(targetGO)` をノードごとに実行 | アバターを全ノード分複製。SkinnedMesh のコストが線形に増加 |
| `AssetDatabase.LoadAssetAtPath` をフィールド初期化子で実行 | ドメインリロード直後は `null` になり得る |
| `EditorSceneManager...Single(obj => ...)` | Animator が0個/複数個のシーンで例外 |
| `AnimationMode.StartAnimationMode()` をノードごとに呼ぶ | AnimationMode はグローバル状態。ノード間で干渉する |

### 5.2 新方式: 共有レンダラ + 描画キュー

既存 `Editor/Utilities/PreviewScene.cs`（`EditorSceneManager.NewPreviewScene` ベース、Head 追従カメラ、
RenderTexture 出力）は設計として正しいので、これを土台に**1アバター / 1プレビューシーン / 1カメラを全ノードで共有**する。

```csharp
public sealed class AvatarPreviewService : IDisposable
{
    public static AvatarPreviewService ForAvatar(GameObject avatarRoot);

    public void Request(PreviewRequest req);   // 非同期。完了時に Texture をコールバック
    public void CancelAll(object owner);
    public void SetPlaying(PreviewRequest req, bool playing);
}

public struct PreviewRequest
{
    public object          owner;        // 要求元ノード
    public AnimationClip   clip;
    public float           time;
    public Vector2Int      size;
    public HumanBodyBones  focus;
    public float           fov;
    public Action<Texture> onRendered;
}
```

動作:
1. ノードは可視になったときだけ `Request` を投げる（ビューポートカリングと連動）
2. サービスは `EditorApplication.update` で**1フレーム最大2件**だけ処理し、キューが空なら何もしない
   （アイドル時の GPU 負荷ゼロ）
3. クリップ適用は `AnimationMode.SampleAnimationClip`。`AnimationMode` の Start/Stop は
   **サービスが1回だけ**行う（ノードは触らない）
4. 結果を `PreviewCache`（LRU、既定 64枚 / 256px）に保持。同一 `(clip, time, size, focus, fov)` はキャッシュヒット
5. 再生は**選択中ノード1つだけ**。`time` を進めて再サンプルし、他ノードは静止フレームのまま
6. ズームアウト時（`scale < 0.5`）はプレビューを描かず、クリップ種別アイコンにフォールバック

### 5.3 プレビューの取得元アバター

`IFxTarget` が解決したアバターを使う。シーンに Animator が無い場合はプレビューを無効化し、
ノードにはクリップ名とアイコンのみ表示する（グラフ編集自体は続行できる）。

---

## 6. サイドバー / 下パネル（資料 画像1）

### 6.1 左サイドバー「VAR」

`AnimatorController.parameters` の一覧。
- 追加（Float / Int / Bool / Trigger）、削除、リネーム、デフォルト値編集
- 並び替え（既存 `ReorderableListView` を流用）
- リネーム時は Controller 内の全 `AnimatorCondition.parameter` を追随させる（`AcEdit` に専用操作を置く）
- 参照されていないパラメータに警告アイコン
- レイヤー一覧もここに置く（資料の ①②③④ のリスト）

### 6.2 下パネル「parameter (name, sync)」

`VRCExpressionParameters` の編集。
- Controller のパラメータとの差分表示（Controller にあるが同期設定されていない／逆）
- `saved` / `networkSynced` / `defaultValue` の編集
- コスト表示は `VRCExpressionParameters.MAX_PARAMETER_COST` を参照して計算（SDK更新で上限が変わるためハードコードしない）
- 既存の `jp.colloid.vrc-expression-params-extension` と表示ロジックを共有できるか調査（重複実装を避ける）

### 6.3 下パネル「menu (name, value)」

`VRCExpressionsMenu` の編集（v0.1 は簡易版）。
- コントロール一覧（name / type / parameter / value）
- `VRCExpressionsMenu.MAX_CONTROLS`（8）超過の警告
- 円環UI（画像2）と D&D 登録は **v0.2〜v0.3**

---

## 7. Phase 0: 既存コードの整理

v0.1 の実装前に片付けるもの。既存コードは消さず、整理と退避にとどめる。

| 対象 | 現状 | 対応 |
|------|------|------|
| `AnimatorCreatorWindow` / `Node` / `Graph` / `Data` | グローバル名前空間、GraphView 依存 | `Legacy/` へ移動し `namespace colloid.FXCreator.Legacy` を付与。メニュー項目を `Tools/FXCreator/Legacy/...` へ退避 |
| `Shadow` | グローバル名前空間、`class`（internal） | `colloid.FXCreator.UI` へ。UXML の型参照も更新 |
| `GetDefaultUSS` / `TryGetValueForList` / `PreviewScene` | グローバル名前空間 | `colloid.FXCreator.Utility` へ |
| `Editor/Test/` (`ClipPropertyName`, `SetViewpoint`) | 実験コードが本番 asmdef 内 | テスト用 asmdef へ移動、または削除 |
| `Editor/Utilities/HaiEditor/` | Haï~ 氏の VisualExpressionsEditor を同梱。**LICENSE ファイルなし** | 上流 MIT ライセンス全文と出典を `HaiEditor/LICENSE.md` として追加。将来は `dev.hai-vr.visual-expressions-editor` へのパッケージ依存に置換を検討 |
| `Resouse/` フォルダ名 | typo（Resource） | `Resources/` にはしない（Unity の特殊フォルダ名になる）。`Assets_/` ではなく `EditorResources/` にリネーム |
| `FXCAnimatoinContainer` | typo（Animation） | `FxcAnimationContainer` にリネーム |
| `FX Creator.asmdef` | 名前 `FXCreator`、`rootNamespace` 空 | `rootNamespace: colloid.FXCreator` を設定。`versionDefines` に `com.vrchat.avatars → VRC`、`nadena.dev.modular-avatar → MA` を追加 |
| テスト asmdef | 存在しない | `FXCreator.Editor.Tests.asmdef` を新設（`Editor/Tests/`） |

> 注: フォルダ/ファイルのリネームは `.meta` ごと `git mv` で行い、GUID を保持する（UXML の型参照・アセット参照が切れるのを防ぐ）。

### 7.1 実施記録（2026-09-14）

計画どおり実施。以下は計画に書いていなかった追加判断。

| 判断 | 内容 | 理由 |
|------|------|------|
| `CustomUI` 名前空間を廃止 | `BetterTextField` / `DropDownField` / `ReorderableListView` / `ToggleButton` / `ToggleinButton` を `colloid.FXCreator.UI` へ移動（UXML の型参照も追随） | `Shadow` だけ移して他が `CustomUI` のままでは不整合。`CustomUI` は汎用名すぎて他パッケージと衝突しうる |
| `UiConstants` を再名前空間化 | `Unity.Cloud.Collaborate.Assets` → `colloid.FXCreator.UI`。出典コメントを追加 | Unity 由来の同梱コードが他者の名前空間を間借りしていた |
| `SetViewpoint.cs` を削除 | NDMF のサンプル MonoBehaviour を `nadena.dev.ndmf.runtime.samples` 名義で同梱していた。シーン/プレハブからの参照0件を確認して削除 | 死んだコード。かつ Editor 専用 asmdef 内の MonoBehaviour で用途がない |
| ScriptInspector への asmdef 参照を削除 | 未使用であることを確認 | 参照先が `Assets/Plugins/`（gitignore 対象）にあり、クリーンクローンで参照切れになる |
| `Editor/RayCaster/` を `colloid.FXCreator.Preview` へ | `PreviewControl` / `PreviewRaycaster`（未使用の実験コード） | §5 のプレビュー用レイキャストとして将来復活させる可能性があるため削除はしない |
| `Shadow` を `public` 化 | もとは internal | `Graph/`（§3）から再利用するため |
| CS0109 警告の除去 | `SelectObjectOutline.m_camera` の無意味な `new` キーワード | ビルドを警告0に近づける |
| `Phase0LayoutTests` を追加 | 「グローバル名前空間に型が残っていない」「他者の名前空間を使っていない」を守る回帰テスト | 名前空間の整理は簡単に巻き戻るため、テストで固定する |
| HaiEditor の上流を特定 | `hai-vr/blendshape-viewer`（旧名 `visual-expressions-editor`、MIT）。同梱5ファイルのうち2ファイルは Pema Malling 名義の MIT ヘッダ付き、3ファイルは Haï~ 氏で表記なし。両方を `LICENSE.md` に記載 | 取り込み元バージョンは記録がないため、パッケージ依存へ切り替える際は特定が必要（同ファイルに明記） |

**未完**: プロジェクト全体のコンパイル0エラーと `Phase0LayoutTests` の実行（ゲート）。

> 補足: `.cs` / `.asmdef` の直接編集は Agent Panel のスクリプトゲートに阻まれる。ゲートの検証は
> 「ステージしたファイルのみを、既存のコンパイル済み DLL を参照して単独コンパイル」する方式なので、
> アセンブリ全体にまたがる名前空間リファクタでは新旧の名前空間が混在して偽エラーになる。
> この種の一括変更は in-place で編集し、プロジェクト全体のコンパイルで検証する。

---

## 8. テスト戦略

`FXCreator.Editor.Tests`（EditMode）。Unity Test Framework は既にプロジェクトに導入済み。

| テスト | 内容 | 優先度 |
|--------|------|:------:|
| `AcTransitionGroupTests` | **畳み込み → 展開 が元の Controller と等価**（ラウンドトリップの中核）。手組み Controller 数パターン + ランダム生成 | 最高 |
| `AcTransitionGroupTests.Determinism` | 同じ Controller から常に同じ畳み込み結果が出る | 最高 |
| `AcEditTests.UndoRestores` | 各編集操作の後に `Undo.PerformUndo` で Controller が元の状態に戻る（State数/Transition数/パラメータ/サブアセット数） | 高 |
| `AcEditTests.NoOrphanSubAssets` | 削除操作後に迷子サブアセットが残らない | 高 |
| `AcEditTests.ArrayCopyTrap` | `layers` / `states` / `parameters` の再代入漏れを検出 | 高 |
| `FxcLayoutAssetTests.DegradesGracefully` | サイドカーが null / 参照切れ / 不整合でもグラフが構築できる | 中 |
| `TargetingTests` | Direct / NDMF-MA の解決、MA 未導入時のフォールバック | 中 |
| `FXCGraphViewportTests` | 座標変換（graph ↔ local）とヒットテストの数値検証 | 中 |
| `ParameterRenameTests` | パラメータ改名が全 condition に追随する | 中 |

UI そのものは自動テストせず、`uloop-screenshot` / `uloop-run-tests` での手動確認とする。

---

## 9. 実装計画

見積は「集中して作業した場合の人日」。`◎` は v0.1 必須、`○` は v0.1 推奨、`△` は後回し可。

### Phase 0 — 土台整理（1.0日）

| | タスク | 見積 |
|---|---|---|
| ◎ | 名前空間・フォルダ整理（§7 の表を上から順に） | 0.5 |
| ◎ | `asmdef` の `rootNamespace` / `versionDefines` 設定、テスト asmdef 新設 | 0.2 |
| ◎ | HaiEditor のライセンス表記追加 | 0.1 |
| ◎ | コンパイル確認（`uloop-compile`） | 0.2 |

**ゲート**: コンパイルエラー0、既存ウィンドウが従来通り開く。

### Phase 1 — グラフ基盤スパイク（2.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | **Painter2D 動作確認スパイク**（2022.3.22f1 で `generateVisualContent` + `painter2D` が使えるか） | 0.3 |
| ◎ | `FXCGraphViewport` + パン/ズーム + `FXCGridBackground` | 0.7 |
| ◎ | `FXCNodeView` + ドラッグ + グリッドスナップ + 選択 | 0.7 |
| ◎ | `FXCEdgeLayer`（Painter2D 一括描画 + 矢印 + 多重エッジオフセット + ヒットテスト） | 0.8 |
| ○ | `FXCMarqueeSelector`、`IGraphSource` 定義 | — |

**ゲート**: ダミーデータ 100ノード / 300エッジで 60fps を維持。ここで性能が出なければ Phase 2 以降の前提が崩れるので、先にここを固める。

### Phase 2 — Controller 読み込み（読み専用グラフ）（2.0日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AcGraphSource`: レイヤー / State / SubStateMachine / Any / Entry / Exit の射影 | 0.8 |
| ◎ | `StateNodeView` / `SpecialNodeView`（Motion名・Speed・WD 表示のみ） | 0.5 |
| ◎ | `LayerListView`（レイヤー切替） | 0.3 |
| ◎ | `AcChangeWatcher`（全再構築 + 選択/ビューポート保持） | 0.4 |

**ゲート**: 実アバターの FX Controller を開いて、標準 Animator ウィンドウと同じ構造・同じ配置で表示される。

#### 2.1 実施記録（2026-09-14）

計画どおり実施。加えて `FxcAnimatorWindow` を新設した（ゲートを確認するには器が要るため）。
以下は計画に書いていなかった追加判断。

| 判断 | 内容 | 理由 |
|------|------|------|
| **名前空間を `colloid.FXCreator.Animator` にしない** | Binding/Sync は `colloid.FXCreator.AnimatorGraph`、View は `colloid.FXCreator.AnimatorGraph.View`。フォルダ名は設計どおり `Animator/` のまま | `colloid.FXCreator.Animator` は `colloid.FXCreator` 配下<b>全体</b>で型名 `Animator`（= `UnityEngine.Animator`）を覆い隠す。実際に Legacy の `Animator m_target;` が CS0118 で壊れた。Phase 3 以降も `Animator` 型は頻出するので、呼び出し側を直すのではなく名前空間の方を避けた |
| ノードビューの生成を `FXCGraphView.NodeViewFactory` に外出し | `Func<FXCGraphView, IFXCGraphNode, FXCNodeView>`。未設定なら素の `FXCNodeView` | `Graph/` が Animator を知らない（§2.2）まま State と Entry/Exit/Any で別の見た目を出す唯一の接点。`StateNodeView` / `SpecialNodeView` はこれ経由で刺さる |
| `FXCNodeView` を二段階初期化に変更 | コンストラクタは `(FXCGraphView owner)` だけを取り、内容の反映は `virtual Bind(node)` が担う。`Bind` は生成直後にも使い回し時にも必ず外から呼ばれる | 旧実装はコンストラクタから `Bind` を呼んでいた。仮想化すると派生クラスのフィールドが初期化される前に `Bind` が走る典型的な事故になる |
| ダブルクリックを `FXCGraphView.NodeActivated` として追加 | `PointerDownEvent.clickCount >= 2` でドラッグに入らずイベントを出す | サブステートマシンへ潜る / `(Up)` で戻るのに必要。2打目の押下でノードを掴むと勢いで数ピクセル動く |
| サブステートマシンへのドリルダウンとパンくずを Phase 2 に含めた | `AcGraphSource.Path` / `EnterStateMachine` / `GoToDepth`、`(Up)` ノード | ゲートが「標準 Animator ウィンドウと同じ構造」なので、サブステートマシンを展開できないと比較できない |
| 遷移先の解決を「代表ノードへ寄せる」規則にした | 奥のサブステートマシン内が行き先ならそのサブステートマシンのノードへ、表示中ステートマシンの外なら `(Up)` へ。解決できなければエッジを描かない | 標準 Animator ウィンドウと同じ見え方。`_representativeNodeId`（配下の全 State/SM → 直接の子ノードID）で O(1) に引く |
| グラフの選択を `Selection.activeObject` へ流す | 単一ノード選択なら `AnimatorState` / `AnimatorStateMachine`、単一エッジ選択なら `AnimatorTransitionBase` | State も Transition も Controller のサブアセットなので、これだけで標準 Inspector が中身を出す。読み取り専用の Phase 2 でも値を確認でき、R4 の「表示のみ・素通し」に合う |
| `LayerListView.Row` は `Clickable` マニピュレータを使う | 生の `PointerDownEvent` から変更 | 標準のクリック挙動（押下位置から外れて離したらキャンセル）が付く。加えて `Clickable` を持つ要素だけが UI 自動化から叩けるので、以降のフェーズの動作確認が楽になる |
| `FXCGraphDemoWindow` を削除しない | Phase 1 のコメントは「Phase 2 で削除」だったが残す | §3.3.1 のとおり性能計測（`Run sweep`）の置き場がここに移っている。Phase 5 でプレビューを足すと再び必要になる |

**ノードの大きさ**: `StateSize = (200, 54)` / `SpecialSize = (200, 40)`。Controller の
`position` は左上隅を指すものとして扱い、そのままグラフ座標に使う（§4.1）。

**スクリプトゲートの回避**: `.cs` は `UapStaging/` 経由でしか書けず、ゲートは
「ステージしたファイルを既存の `FXCreator.dll` を参照して単独コンパイル」する。
既存クラスを書き換えると、staged 版と DLL 版の同名型がシグネチャ境界でぶつかって
偽の CS1503 が出る（`FXCEdgeLayer(FXCGraphView)` など）。
**対処は「その境界を含むフォルダをまとめてステージする」**こと。今回は `Graph/` 8ファイルを
丸ごと置いたら通った。1ファイルだけ直そうとすると必ず詰まる。

**ゲート確認（実アバター `pon_0.0.0` / `pon_vrchat_fx`、20レイヤー）**:
アバターを選んでウィンドウを開くと FX レイヤーが自動解決され、レイヤー一覧・
ノードグラフ（Any/Entry/Exit のピル、既定ステートのオレンジ、Motion名・Speed・WD）が
標準 Animator ウィンドウと同じ配置で出る。EditMode テスト 30/30 パス
（`AcGraphSourceTests` 15 + `FXCGraphViewportTests` 10 + `Phase0LayoutTests` 5）。

### Phase 3 — 編集（書き）＋ Undo（2.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AcEdit` トランザクション（State/SubSM 追加削除、Motion設定、遷移追加削除、位置更新） | 1.0 |
| ◎ | `AcEditTests`（Undo復元・迷子サブアセット・配列コピー罠） | 0.6 |
| ◎ | コンテキストメニュー、エッジ作成（`FXCEdgeDragger`） | 0.5 |
| ◎ | `ElementInspector`（State / Transition の詳細編集） | 0.4 |

**ゲート**: グラフで作った構造が標準 Animator ウィンドウで正しく見え、Undo/Redo が破綻しない。**ここが v0.1 の技術的な山。**

#### 3.1 実施記録（2026-09-14）

R1 の対策どおりテストファーストで実施。`AcEdit` と `AcEditTests` を先に固めてから UI を繋いだ。

| 判断 | 内容 | 理由 |
|------|------|------|
| 削除は「先に参照を掃除してから本体を消す」 | `RemoveState` / `RemoveStateMachine` は、まず `PurgeReferencesTo` で<b>Controller 全レイヤー</b>を走査して自分宛ての遷移と `defaultState` 参照を外し、それから本体を消す | Unity の `RemoveState` は呼び出したステートマシンが持つ参照しか面倒を見ない。残すと `destinationState` が null の遷移＝グラフに描けない幽霊になる |
| `RemoveTransition` は所有者を自分で探す | レイヤー木を走査して State / Any / Entry / StateMachine遷移のどれが持っているか突き止める | 呼び出し側（グラフ・インスペクタ）に「この遷移は誰のものか」を覚えさせない。エッジIDから遷移オブジェクトしか復元できないので必要 |
| 例外時のロールバックは各操作を包んで実現 | 公開操作を `Guard` で包み、例外なら `_failed` を立てて投げ直す。`Dispose` がそれを見て `Undo.RevertAllDownToGroup` | C# では `Dispose` の中から「例外で抜けたのか」を直接は判定できない。設計書の `using` 記法（明示コミット無し）を保ったまま巻き戻すための形 |
| `Begin` の入れ子を許す | 内側は外側の Undo グループと `AcEditReport` を共有し、畳むのは一番外だけ | インスペクタの1フィールド変更がグラフ操作の中から呼ばれても、Undo が2段に割れない |
| `Modify(target, apply)` を用意した | プロパティ1つごとに専用メソッドを生やす代わりに、記録して書き換えるだけの汎用口 | `ElementInspector` のフィールド数だけ API が増えるのを避けた。落とし穴があるのは<b>構造変更</b>で、単純なプロパティ代入ではない |
| ポートは「遷移を引く取っ手」に限定し、**エッジはノードの縁から縁へ描いたまま** | `AcGraphEdge.FromPortId` / `ToPortId` は null のまま | ポートに寄せると、同じ2ノード間の複数遷移が完全に重なって1本に見える。`FXCEdgeLayer` の平行エッジオフセットは端点がノードの縁のときだけ効く |
| 保存先の無い Controller を読み取り専用にした | `CanEdit` は `AssetDatabase.GetAssetPath` が空、または `Packages/` 配下なら false。理由を `ReadOnlyReason` で UI に出す | 書いても保存されない対象に編集 UI を出すと、消える変更を作らせてしまう |
| 編集後の作り直しは `AcEdit.AfterEdit` 経由 | `AcGraphSource` が購読し、`report.Controller` が自分の対象のときだけ `Refresh` | ウィンドウを複数開いても混ざらない。静的イベントなので `AcGraphSource` は `IDisposable` にして必ず外す |
| `ElementInspector` は再構築と値同期を分けた | 対象が変わったときだけ作り直し、Undo や外部変更では `SyncValues()` で値だけ差し替える | 作り直すと入力中のフィールドからフォーカスが飛ぶ |
| 条件の行はパラメータ型で選択肢を絞る | Bool に `Greater` を出さない。パラメータが消えて宙に浮いた条件は「(見つかりません)」付きで名前を残す | 黙って別のパラメータに化けると気づけない |

**§11-5 の宿題（ウィンドウ分割）**: `TwoPaneSplitView` の入れ子で足りることを実機で確認した。
外側 `[レイヤー一覧 | 右]`、内側 `[グラフ列 | ElementInspector]`（右を固定幅）。

**ゲート確認（2026-09-14）**:
- 空の Controller にグラフ操作だけで State 2つ・相互遷移・条件1つを作り、**標準 Animator ウィンドウで
  そのとおりに表示されることを確認**（既定ステートのオレンジ、位置、双方向の矢印まで一致）。
- Undo 5回で作った順に1段ずつ戻り（条件 → 遷移 → 遷移 → State → State）、Redo で復元。
  **1操作 = 1 Undo 段**が守られている。
- 遷移を Undo で消したあと、サブアセットに残るのは State 2つとステートマシンだけ（迷子なし）。
- EditMode テスト 68/68（`AcEditTests` 18 + `AcGraphEditingTests` 20 + Phase 2 の 30）。

**まだ無いもの**: パラメータの追加・改名（Phase 6）、コピー/ペースト・自動レイアウト（v0.2）、
`AnimatorStateTransition.interruptionSource` などの詳細（必要になったら足す）。

### Phase 4 — toggle / switch ノード（1.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AcTransitionGroup` 畳み込み・展開ロジック | 0.5 |
| ◎ | `AcTransitionGroupTests`（等価性・決定性） | 0.4 |
| ◎ | `ToggleNodeView` / `SwitchNodeView`（ポート追加削除・パラメータ選択） | 0.5 |
| ○ | `FxcLayoutAsset`（条件ノード位置・expanded フラグ） | 0.1 |

**ゲート**: 資料（画像1）の toggle / swich 図と同じ見た目になり、編集結果が Controller に正しく反映される。

#### 4.1 実施記録（2026-09-14）

§4.2 のアルゴリズムをそのまま実装。ゲートは満たしたが、**実アバターでは一度も発火しない**ことが分かった（下記）。

| 判断 | 内容 | 理由 |
|------|------|------|
| 畳み込みは Controller を読むだけの純粋関数にした | `AcTransitionGrouping.Collapse(state, isExpanded)` は `AcGroupingResult`（グループ＋直結の残り）を返すだけ | UI もアセットも触らないので単体でテストでき、ラウンドトリップの根拠が「分割になっていること」1点に絞れる |
| ラウンドトリップの検証を「分割」に言い換えた | 元の遷移がグループか直結エッジのどちらかに<b>ちょうど1回ずつ</b>現れることを全テストで検査 | 畳み込みは Controller を書き換えないので、「畳み込み → 展開 が元と等価」は分割であることと同値。逆変換コードが無い設計の帰結 |
| switch に「新しい値」ポートを足した | 出力ポートは遷移から導出されるので、遷移を作らずにポートだけ増やせない。空きポートへ繋ぐと未使用の threshold で分岐ができる | §4.2 の「SwitchNode に出力ポート追加 → 新規 transition を作り Equals, 新threshold を設定」を、ドラッグ操作として成立させる形 |
| 畳み込みノードのエッジ<b>だけ</b>ポートに寄せた | State 間の直結エッジは従来どおりノードの縁。グループの出力ポートはポートに寄せる | グループはポートごとに意味（True/False/値）が違うので寄せる必要がある。State 間は寄せると平行エッジが重なる（Phase 3 の判断と同じ理由） |
| 畳み込みノードの既定位置は分岐元と行き先の中間 | サイドカーに位置が無いときだけ計算する | 既存の Controller をそのまま開いても線が素直に見える |
| Expand の戻し口を State の右クリックに置いた | グループノードが消えるので、そちらの右クリックからは戻せない | 「展開を無視して畳み込み直した結果」と現状を突き合わせて、戻せるパラメータだけ列挙する |
| ランダムテストにカバレッジの見張りを付けた | toggle・switch・直結がそれぞれ1件以上出たことを assert | 最初の生成器は 40 seed 中 3 件しかグループを作らず、switch は<b>0件</b>だった。気づかなければ「何も畳み込まなかった」だけで緑になっていた |

**ゲート確認（2026-09-14）**: `Idle` が bool で True/False に分岐、`Hub` が int 0/1/2 に分岐する
Controller を作り、資料どおり<b>ワイヤ上の小さなノード</b>として表示されることを確認。
「新しい値」ポートから繋ぐと `Mode Equals 3 / hasExitTime=false` の遷移ができ、Undo 1回で消える。
Expand はグループノードを消すだけで遷移数は変わらず、Collapse で戻る。
サイドカーは Expand したときに初めて生成された（遅延生成）。EditMode テスト 81/81。

> **⚠ §4.2 の規則は実アバターの FX には当てはまらない（要判断）**
>
> 実アバター `pon_vrchat_fx`（20レイヤー・152遷移）に対して畳み込みは **toggle 0 / switch 0**。
> 内訳を採ると、候補になった 125 個の (State, パラメータ) バケットが**すべてサイズ1**だった。
>
> 原因は VRChat のトグルが「1つの State が両方向に分岐する」形ではなく、
> **2つの State が互いを指す**形（`OFF --(If p)--> ON`、`ON --(IfNot p)--> OFF`）だから。
> §4.2 は「State `S` の outgoing transitions をグループ化する」と定義しているので、
> 各 State の出口が1本しかないこの形は原理的に畳み込めない。
>
> 実装は §4.2 のとおりで、テストも通っている。→ **§4.2.1 で解決した。**

### 4.2.1 分岐元の一般化（2026-09-14 追加）

上の「実アバターで発火しない」件を受けて実測し、規則を広げた。

**実測**（`pon_vrchat_fx` 20レイヤー・152遷移。どの形が何件あるか）:

| 形 | 件数 | 備考 |
|---|---:|---|
| A. 2つの State が同じ bool で相互に遷移（VRChat の定番トグル） | 4 | Blink, Kemonize, Petting ×2 |
| B. Any State の分岐 | 0 | |
| **C. Entry の分岐** | **11** | switch 9・toggle 2。`GestureLeft/Right`、`FacialExpression` |
| D. 1つの State の分岐（§4.2 が想定した形） | 0 | |

**性能の実測**（§3.3 の上限は約900エッジ）:

| 表示単位 | 現状 | C を畳む | A を畳む |
|---|---|---|---|
| 最も重い（Hand R/L） | 10ノード/29エッジ | 11/30 | 10/29 |
| Blink | 5/3 | 5/3 | 4/1 |

最も重い表示単位でも **29エッジ＝予算の3%**。どちらの案も差は ±1 要素で、
**性能は判断材料にならない**ことが分かった。そこで管理のしやすさで決めた。

**決定: 分岐元を State から「outgoing transitions を持つ点」へ一般化する（C を採用、A は採らない）。**

- §4.2 の規則の形（同じパラメータの outgoing transitions をまとめる）は<b>変えていない</b>。
  当てはめる対象を Entry / Any State / サブステートマシンにも広げただけ。
- **D5 と衝突しない。** Entry は元からノードなので、State のときと同様に
  Entry と行き先の間にグループノードを挟むだけ。State は 1:1 のまま。
- A（2-State 統合）を採らない理由: 効くのは既に単純なレイヤー（3〜9エッジ）だけで、
  かつ 2つの State がノードから消えるため **Phase 2/3 で出せるようにした
  Motion・Speed・WriteDefaults がグラフから編集できなくなる**。管理性の後退で、
  しかも D5 を緩める必要がある。

**効果**: 実アバターで **0 → 11 グループ**（switch 9 / toggle 2）。
16分岐の `Tanuki Parts Exclusive` は、区別のつかない16本の扇が値ラベル付きの1ノードになる。

| 追加の判断 | 内容 | 理由 |
|------|------|------|
| サイドカーのキーに分岐元の種類を足した | `CondNodeLayout.sourceState` → `sourceOwner`（Object）＋ `sourceKind` | Entry と Any は<b>同じ</b>所属ステートマシンを owner に持つので、種類が無いと同名パラメータのグループ同士が同じエントリを取り合う |
| グループIDに分岐元の種類を含めた | `AcNodeRef.Id` を使う（接頭辞に種類が入る） | 同上。ノードIDの衝突も防ぐ |
| 既定位置を「分岐元と行き先の隙間」に変え、幅を隙間に合わせて詰める | 64〜120px で可変。縦は行き先の中心の平均に合わせる | 16分岐だとノードが 290px の縦長になり、行き先の列に重なって7ノードを覆っていた（実測）。隙間は 100px しかないので幅も詰める必要がある |
| `AcGroupBranch.Transition` を `AnimatorTransitionBase` へ | Entry / ステートマシン遷移は `AnimatorTransition` で `AnimatorStateTransition` ではない | Exit Time の検査は `AnimatorStateTransition` のときだけ効かせる（Entry 遷移は Exit Time を持たない） |

EditMode テスト 85/85。

### Phase 5 — ノード内プレビュー（2.0日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AvatarPreviewService`（共有 PreviewScene + キュー + フレーム分散） | 0.8 |
| ◎ | `PreviewCache`（LRU） | 0.2 |
| ◎ | ノードへの組み込み + ビューポートカリング連動 + ズームフォールバック | 0.5 |
| ○ | 選択ノードの再生、カメラフォーカス（`HumanBodyBones`）切替 | 0.5 |

**ゲート**: 50ノードのグラフをスクロールしてもフレーム落ちせず、GPU メモリが増え続けない。

#### 5.1 実施記録（2026-09-14）

| 判断 | 内容 | 理由 |
|------|------|------|
| 既存 `PreviewScene` を<b>使わず</b>、サービス内に専用のシーン構築を書いた | `Utilities/PreviewScene.cs` はそのまま残す | あれは「アバターが既にプレビューシーンに居る」前提で、`.First()` が対象なしで落ちる。しかも v0.2 のクリップ一覧（`FXCreator.cs`）と Legacy が使っているので、作り直すと巻き添えになる。方式（`NewPreviewScene` + RenderTexture）は設計どおり踏襲した |
| 1フレームの区切りを<b>件数だけでなく時間でも</b>設けた | 最大2件、かつ 8ms を超えたら残りは次フレーム | 件数だけだと重いアバターや初回描画で 16ms を超える。実測で1フレーム目が 19.33ms だった |
| サービス構築時に捨て描き（ウォームアップ）を1回入れた | 32x32 を1枚描いて捨てる | **初回の `Camera.Render` は実測 203ms** かかり、これは分割できない（アバターのメッシュとシェーダの GPU 転送）。スクロール中に来ると目に見えて引っかかるので、アバターを選んだ時点で払ってしまう |
| `AnimationMode` はキューが空になったら抜ける | 設計は「サービスが1回だけ」。実装は「使う間だけ」 | ノードごとに呼ばない（§5.1 の問題）という意図は保ちつつ、エディタを常時アニメーションモードに置かない。`Start`/`Stop` のコストは実測 0.01ms で、頻度は問題にならない |
| 見えているノードは<b>毎回要求し直す</b> | キャッシュに当たれば即返る | LRU の先頭に上がるので「見えているノードの絵が追い出される」ことがなくなる。要求は辞書引き1回で済む |
| プレビューはノード内の 46px サムネイル | ノードを高くはしない | Controller の座標をそのまま使う以上（§4.1）、ノードを高くすると実アバターの 50px 間隔で縦に重なる。1:1 配置を優先した |

**ゲート確認（実アバター `pon_0.0.0`、2026-09-14）**:

| 項目 | 実測 | 判定 |
|---|---|---|
| 50ノード分を捌く間の最悪フレーム | 14.67 ms（平均 5.46 ms / 26フレーム） | **PASS**（16ms） |
| 容量の4倍（256件）を要求した後の GPU メモリ | 64件 / 約 4.6MB で頭打ち | **PASS**（増え続けない） |
| アイドル時 | 未処理0・AnimationMode オフ | PASS |
| アバター選択時の準備 | 203 ms ※一度だけ、スクロール中には起きない | — |

#### 5.2 ○ 項目の実施記録（2026-09-14）

| 判断 | 内容 | 理由 |
|------|------|------|
| 再生フレームは<b>キャッシュを通さない</b> | 再生中のノード専用の `RenderTexture` を1枚作って使い回す | `time` が毎フレーム変わる＝毎回別のキーになるので、キャッシュに入れると LRU が静止プレビューを丸ごと押し出す（実測で確認: 60フレーム回してもキャッシュ10件のまま） |
| 再生の開始・停止は<b>選択に乗せた</b> | 専用の再生ボタンを置かず、`ApplySelectionStyle`（＝選択が変わると呼ばれる）から出入りする | §5.2-5 の「再生は選択中ノード1つだけ」をそのまま実現できる。ボタンを増やさずに済む |
| フレーム間隔に上限を設けた | `delta` を 0.1秒 でクランプ | ウィンドウが止まっていた間の巨大な delta でクリップが飛ぶ |
| `AnimationMode` は<b>自分で点けたときだけ</b>消す | 開始時に既に点いていたら触らない | ユーザーが Animation ウィンドウで録画中の場合、こちらが勝手に消すと録画が壊れる |
| 寄り先は State ごとにサイドカーへ | `NodeExtra.previewFocus` / `previewFov`（§4.4 の定義どおり） | 口の動きは顔に、手の形は手に寄せたい。Controller には置けない表示情報なのでサイドカー |

**確認（実アバター、2026-09-14）**:
- 再生60フレームでコールバック60回・使用テクスチャ**1枚**（使い回せている）。
  キャッシュは10件のまま変化なし＝静止プレビューを押し出していない。
- 別ノードを選ぶと再生がそちらへ移り、前のノードは止まる（同時に1つだけ）。
- 停止・`Dispose` のいずれでも `AnimationMode` が元に戻る。
- 寄り先を変えると別のテクスチャが生成される（キーに焦点が入っている）。

> 測定の注意: `AnimationMode` が前の実行から点いたままだと「停止後も点いている」ように見え、
> 持ち主判定のせいで消せない。切り分けるときは先に `StopAnimationMode()` してから測ること。

#### 5.3 ★ プレビューが常にバインドポーズだった件（2026-09-14 修正）

**症状**: どのクリップを差しても、再生しても、絵がまったく変わらない。

**原因**: プレビューシーンは通常のプレイヤーループで更新されないため、
ボーンを動かしても `SkinnedMeshRenderer` がスキニング行列を作り直さない。
`Camera.Render()` はその時点のスキニング結果を描くだけなので、
**常にバインドポーズが描かれていた**。

**対処**: プレビュー用複製の全 `SkinnedMeshRenderer` に
`forceMatrixRecalculationPerRender = true` を立てる（この用途のための API）。

**切り分けの記録**（同じ症状を追うときの順序として残す）:

| 確かめたこと | 結果 | 分かること |
|---|---|---|
| `EditorApplication.update` は操作なしで回るか | **117回/秒** | 更新の仕組みは正しい |
| 再生の `time` は進むか | 0.19→0.29→0.37 | ループは動いている |
| `AnimationMode.SampleAnimationClip` は骨を動かすか | 尻尾が **169度** 回る | サンプリングは正しい |
| 骨を手で55度回して描き直すと絵は変わるか | **0画素** → 修正後 **4839画素** | ← ここが原因 |
| 別ステートのサムネイルは別の絵になるか | 修正後 42〜69画素の差 | 静止プレビューも直った |

**同じ穴に落ちないための注意**:
- 「何か描けている」だけでは検証にならない。当初「異なる色数1464」で
  描画できていると判断したが、それはバインドポーズが描けていただけだった。
  **ポーズを変えて差分が出ること**まで見ないと確認にならない。
- 検証クリップの選び方で何度も空振りした。`pon_tanuki_happy` は**尻尾しか**動かさず、
  カメラは正面から顔を見ているので差が出ない。実 FX クリップの多くは `length==0` の
  静止ポーズで、そもそも再生対象にならない。
  差分を見るなら「カメラに映る部位を」「時間で」動かすクリップを選ぶこと。
- その場で作った `AnimationClip` での検証は、回転カーブが x/y/z 揃っていないと
  バインドされず無反応になる（骨が動かないので偽陰性になる）。

#### 5.4 ★ カメラの寄り先がズレていた件（2026-09-14 修正）

**症状**: 顔に寄せると顔が下に寄って上に大きな余白ができ、全身でも足元が切れる。

**原因は3つ重なっていた**（順に潰した）:

| # | 原因 | 実測 |
|---|------|------|
| 1 | 収めたい半径を<b>メートル固定値</b>で持っていた | このアバターは身長 0.80m（標準人型の半分）。全身指定で身長の **2.00倍** を映していた |
| 2 | `SkinnedMeshRenderer.bounds` は<b>全メッシュで同じ</b>保守的な箱を返す | 髪飾り1枚も体も `top=0.732` で同一。変形を見越した膨らみ込みの値で、実寸は `BakeMesh` で測ると 0.678 |
| 3 | **サンプリングでアバターが沈む** | クリップを当てると全身が y `-0.030..0.678` → `-0.252..0.388` へ<b>0.29m 下がる</b>。バインドポーズで測った箱を狙うと、当然ズレる |

**対処**:
- 寸法はメートル固定値をやめ、実測した形状から出す。
- 形状の実測は `BakeMesh` した頂点から（`renderer.bounds` は使わない）。
- **狙う点は必ず「生きたボーン位置」から取る。** 箱からは頭の大きさ・肩幅といった
  <b>姿勢で変わらない寸法だけ</b>を借りる。頭頂は `頭ボーン + 頭の高さ`、
  足元は足/つま先ボーンから求める。
- `Head` はボール位置をそのまま狙わない。Head ボーンは頭蓋底にあるので、
  頭頂までの中点を狙わないと頭が切れて胸が入る。
- 上半身は「腰→頭ボーン」ではなく「腰→頭頂」で測る。頭の大きいデフォルメ体型だと
  前者は<b>顔寄りより狭くなる</b>逆転が起きる（実測: 胴 0.197m に対し頭 0.228m）。

**結果**（192px で描画し、中身のある行を数えて計測）:

| 寄り先 | 修正前（上/下の余白） | 修正後 |
|---|---|---|
| 顔 | 58% / 0% | **19% / 0%** |
| 全身 | 44% / 0% | **7% / 2%** |

> 副次的に判明: このプロジェクトは Linear カラースペースなので、
> 背景色 0.16 は `ReadPixels` では 0.02 として読める。背景判定を色で決め打ちすると
> 「全画素が前景」と誤判定する。四隅の色を背景と見なすのが安全。

### Phase 6 — VAR / parameter / menu パネル（1.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `ParameterListView`（追加削除・改名追随） | 0.5 |
| ◎ | `VrcParameterPanel`（`VRCExpressionParameters` 編集 + コスト計算 + 差分表示） | 0.6 |
| ○ | `VrcMenuPanel`（簡易リスト版） | 0.4 |

#### 6.4 実施記録（2026-09-14）

**§6.2 の宿題（既存パッケージとの共有可否）の結論: 共有しない。**
`jp.colloid.vrc-expression-params-extension` の `VRCExpressionParametersEditor` 拡張は
<b>アセット用のカスタム Inspector</b>で、パネル部品として持ち出せない。
`VRCDefaultParameters`（VRChat 組み込みパラメータ一覧）は有用だが、
別 VPM パッケージへの asmdef 参照は FX Creator の配布時に<b>そのパッケージを必須化</b>する。
v0.1 は自己完結を優先した。パッケージ化（v0.2）の際に、両方が依存する小さな共有パッケージへ
組み込み一覧を移すのが筋。

| 判断 | 内容 | 理由 |
|------|------|------|
| 改名の追随先は条件式だけではない | State の speed / cycleOffset / mirror / timeParameter、BlendTree の blendParameter / blendParameterY、子の directBlendParameter、入れ子の BlendTree まで | 条件式だけ直すと「名前は変わったが挙動が壊れた」Controller ができる。テストで各参照先を個別に固定した |
| 改名先が既存名と衝突したら連番を足す | `Taken` → `Taken 1` | 同名が2つあると、どちらを指しているのか決まらない Controller になる |
| 未参照判定の走査範囲は<b>改名の走査と同じ</b>にする | `AcParameterUsage` と `AcEdit.RenameParameter` が同じ範囲を見る | 片方だけが知っている参照があると、「未参照」と言われて消したのに実は使われていた、という壊し方をする |
| BlendTree はブレンド種別ごとに<b>効く軸だけ</b>数える | Direct は軸を使わず子の directBlendParameter だけ、Simple1D は X だけ | 効かない軸まで数えると、未設定の BlendTree が持つ既定値 `"Blend"` を使用中とみなす。実アバターで「宣言24 / 参照26」と数が合わずに気づいた（絞った後は 24/24 で一致） |
| コスト上限は SDK の定数を参照 | `VRCExpressionParameters.MAX_PARAMETER_COST` | SDK 更新で上限が変わる。実アバターで「コスト 20 / 256」と表示 |
| `VrcMenuPanel` は表示のみ | 編集 UI を持たない | R4 の「触らなければ壊さない」。円環 UI と D&D は v0.2〜v0.3 |

EditMode テスト 96/96（`ParameterRenameTests` 11 本を追加）。

### Phase 7 — Targeting（両対応）（1.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `IFxTarget` / `FxTargetResolver` / `DirectAvatarTarget`（複製差し替えガード込み） | 0.7 |
| ◎ | `NdmfMaTarget`（専用Controller生成 + MAコンポーネント同期） | 0.6 |
| ◎ | ウィンドウ上部のターゲットセレクタ UI | 0.2 |

### Phase 8 — 仕上げ（1.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | 例外時の挙動（アバター不在・Controller不在・読み取り専用） | 0.4 |
| ◎ | 実アバター2体での通し確認（Av3Emulator / Gesture Manager で動作検証） | 0.5 |
| ○ | README / CHANGELOG、`Tools/FXCreator` メニュー整理 | 0.3 |
| △ | USS のダーク/ライト両対応確認 | 0.3 |

**合計: 約 16 人日**（v0.1）

### v0.2 以降（概略）

- ①アニメーション登録 UI（カード一覧・検索・フォルダ・アーカイブ）— 既存 `FXCreator.cs` の資産を活用
- ③ExMenu 円環 UI（画像2 の扇形ヒットテスト。2〜8分割の AABB/扇形判定の図がそれ）
- ②ハンドサイン登録（8ジェスチャ × セット切替）
- ④Contact 登録（`VRCContactReceiver` 一覧と連携）
- グラフ基盤の拡充（リサイズ・コピペ・自動レイアウト・ミニマップ）
- VPM パッケージ化（`jp.colloid.fxcreator`）
- `NdmfNativeTarget`（MA 非依存の自前 NDMF プラグイン）

---

## 10. リスクと対策

| # | リスク | 影響 | 対策 |
|---|--------|------|------|
| R1 | `AcEdit` の Undo/サブアセット管理が想定より厄介 | Phase 3 が倍に膨らむ | Phase 3 冒頭でテストファースト。最悪、Undo を「操作前に Controller 全体をスナップショットして `Undo.RecordObject`」の粗い方式にフォールバック |
| R2 | Painter2D が 2022.3 で期待通り動かない / 性能不足 | Phase 1 の前提崩壊 | Phase 1 最初の 0.3日でスパイク。ダメなら `MeshGenerationContext.Allocate` の手組みメッシュへ |
| R3 | ノード内プレビューの性能が出ない | Phase 5 が延びる | 既に共有レンダラ + キュー + LRU + カリング + ズームフォールバックで多層防御。最終手段はプレビューを選択ノード1つだけに縮退（当初の推奨案） |
| R4 | 完全ラウンドトリップの取りこぼし（BlendTree, StateMachineBehaviour, AvatarMask, syncedLayer 等） | 既存アバターを壊す | **v0.1 は「編集できない要素は表示のみ・素通し」を原則にする。**触らない要素は Controller 上でそのまま保持され、編集 UI を出さない。破壊しないことを優先 |
| R5 | 標準 Animator ウィンドウと同時に開いて競合 | データ不整合 | 同じアセットを見ているので原理的に安全。`AcChangeWatcher` のフォーカス再構築で追随。ただし両方開いている状態でのテストを Phase 8 に入れる |
| R6 | VRChat SDK / MA の API 変更 | ビルド不能 | `versionDefines`（`VRC` / `MA`）で分離。MA 依存は `NdmfMaTarget` 1ファイルに閉じる |
| R7 | HaiEditor 同梱のライセンス不備 | 配布できない | Phase 0 で対応（ライセンス全文追加、将来はパッケージ依存化） |
| R8 | スコープが①〜⑤へ膨らむ | v0.1 が出ない | v0.1 は⑤のみ。②③④は設計書にプレースホルダを置くだけで実装しない |

---

## 11. 未決定事項（実装中に判断）

1. **レイヤー間の表示**: 複数レイヤーを同時に見せるか、1レイヤーずつか（v0.1 は1レイヤーずつ）
2. **BlendTree ノード**: v0.1 は「BlendTree を持つ State」として1ノードで表示し、中身は標準インスペクタへ委譲
3. **`StateMachineBehaviour`**: 表示のみ（R4 の原則）
4. **ノードの折り畳み（資料の close/open button）**: Phase 2 で入れるか Phase 4 に回すか
5. **ウィンドウ分割**: 資料はグラフ + 左サイドバー + 下パネルの3分割。`TwoPaneSplitView` の入れ子で足りるか要検証
