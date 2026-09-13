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

| 機能 | v0.1 | 実装方針 |
|------|:----:|----------|
| パン | ✔ | 中ボタンドラッグ / Alt+左ドラッグ / スペース+ドラッグ。`contentContainer.transform.position` |
| ズーム | ✔ | `WheelEvent` → `transform.scale`。0.2〜2.0、カーソル位置基準 |
| グリッド背景 | ✔ | `generateVisualContent` + Painter2D。ズーム連動の2段グリッド（20px / 100px） |
| ノード配置・ドラッグ | ✔ | `PointerManipulator`。20px グリッドスナップ（既存試作の値を継承） |
| 複数選択 | ✔ | Ctrl/Shift 追加選択、矩形選択、Ctrl+A |
| エッジ描画 | ✔ | 全エッジを1つの `FXCEdgeLayer` が Painter2D で描画。ベジェ + 矢印ヘッド + 多重エッジのオフセット |
| エッジ作成 | ✔ | ポートからドラッグ → 接続可能ポートをハイライト → ドロップ |
| コンテキストメニュー | ✔ | 背景右クリックで State/SubStateMachine 追加、ノード右クリックで削除等 |
| ビューポートカリング | ✔ | 可視矩形外のノードは `display:none`。プレビュー要求も可視ノードのみ |
| ノードリサイズ | ✖ | v0.2 |
| コピー/ペースト | ✖ | v0.2 |
| 自動レイアウト | ✖ | v0.2（位置情報のない Controller 向け。単純な層別レイアウト） |
| ミニマップ | ✖ | v0.3 |

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

> ⚠️ 検証項目: `MeshGenerationContext.painter2D` は Unity 2022.2 以降の API。
> 2022.3.22f1 で使えることを Phase 1 の最初のスパイクで実機確認する。
> 使えない場合のフォールバックは `MeshGenerationContext.Allocate` による手組みメッシュ。

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

### Phase 3 — 編集（書き）＋ Undo（2.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AcEdit` トランザクション（State/SubSM 追加削除、Motion設定、遷移追加削除、位置更新） | 1.0 |
| ◎ | `AcEditTests`（Undo復元・迷子サブアセット・配列コピー罠） | 0.6 |
| ◎ | コンテキストメニュー、エッジ作成（`FXCEdgeDragger`） | 0.5 |
| ◎ | `ElementInspector`（State / Transition の詳細編集） | 0.4 |

**ゲート**: グラフで作った構造が標準 Animator ウィンドウで正しく見え、Undo/Redo が破綻しない。**ここが v0.1 の技術的な山。**

### Phase 4 — toggle / switch ノード（1.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AcTransitionGroup` 畳み込み・展開ロジック | 0.5 |
| ◎ | `AcTransitionGroupTests`（等価性・決定性） | 0.4 |
| ◎ | `ToggleNodeView` / `SwitchNodeView`（ポート追加削除・パラメータ選択） | 0.5 |
| ○ | `FxcLayoutAsset`（条件ノード位置・expanded フラグ） | 0.1 |

**ゲート**: 資料（画像1）の toggle / swich 図と同じ見た目になり、編集結果が Controller に正しく反映される。

### Phase 5 — ノード内プレビュー（2.0日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `AvatarPreviewService`（共有 PreviewScene + キュー + フレーム分散） | 0.8 |
| ◎ | `PreviewCache`（LRU） | 0.2 |
| ◎ | ノードへの組み込み + ビューポートカリング連動 + ズームフォールバック | 0.5 |
| ○ | 選択ノードの再生、カメラフォーカス（`HumanBodyBones`）切替 | 0.5 |

**ゲート**: 50ノードのグラフをスクロールしてもフレーム落ちせず、GPU メモリが増え続けない。

### Phase 6 — VAR / parameter / menu パネル（1.5日）

| | タスク | 見積 |
|---|---|---|
| ◎ | `ParameterListView`（追加削除・改名追随） | 0.5 |
| ◎ | `VrcParameterPanel`（`VRCExpressionParameters` 編集 + コスト計算 + 差分表示） | 0.6 |
| ○ | `VrcMenuPanel`（簡易リスト版） | 0.4 |

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
