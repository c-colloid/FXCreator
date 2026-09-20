# FX Creator

VRChat アバターの FX レイヤーを、ノードグラフとして編集する Unity エディタ拡張です。

- 対象: Unity 2022.3.22f1 / VRChat SDK Avatars 3.10.5
- 設計書: [`Docs/FXCreator-Design.md`](../../Docs/FXCreator-Design.md)

## v0.1 でできること

アバターを選ぶと FX レイヤーの AnimatorController がノードグラフとして出ます。
ノード・遷移・パラメータを編集でき、結果は **AnimatorController アセットに即座に反映**され、
Undo / Redo がそのまま効きます。

- ノードグラフ（パン・ズーム・矩形選択・ポートからのドラッグで遷移作成）
- State / Sub-State Machine / Any State / Entry / Exit の表示と編集
- 同じパラメータで分岐する遷移群を 1 つの toggle / switch ノードに畳む
- ノード内にアバターのプレビュー（選択中のノードだけ再生）
- VAR（Controller のパラメータ）/ parameter（同期設定）/ menu（Expression Menu）パネル
- Direct（直接編集）と NDMF (MA)（非破壊マージ）の 2 モード

## 使い方

1. ヒエラルキーでアバターを選ぶ
2. `Tools > FXCreator > Animator Editor` を開く
3. ツールバーの **Mode** で Direct / NDMF (MA) を選ぶ
4. 左のレイヤー一覧からレイヤーを選ぶ

パネル（VAR / parameter / menu）はツールバーの **Panels** から出し入れします。
フローティングなので、位置と大きさは Unity が覚えます。

### 2 つのモード

| | Direct | NDMF (MA) |
|---|---|---|
| 編集する Controller | アバターの FX レイヤーにあるもの | `Assets/FX Creator/Save/<Avatar>/FXC_FX.controller`（FX Creator が生成） |
| 同期パラメータ | `VRCExpressionParameters` | `ModularAvatarParameters` |
| 非破壊 | いいえ | はい（ビルド時にマージ） |
| 必要なもの | VRChat SDK | + Modular Avatar |

Modular Avatar が入っていない場合、NDMF (MA) は理由付きで選べない状態になります。

### 安全装置

- 書き込めない場所（`Packages/` 配下・未保存）の Controller は、
  `Save/<Avatar>/` へ**複製して差し替えるか**を確認します。断れば読み取り専用で開きます
- FX レイヤーに Controller が無いアバターは、確認のうえ新規作成して割り当てます
- 編集できない要素（BlendTree の中身・StateMachineBehaviour など）は**表示のみ**で、
  触らずにそのまま保持します

## 標準 Animator ウィンドウとの併用

同じアセットを見ているので同時に開いて構いません。
FX Creator はフォーカスを取り戻したときに再構築して追従します。

## メニュー

| メニュー | 内容 |
|---|---|
| `Tools/FXCreator/Animator Editor` | 本体（v0.1） |
| `Tools/FXCreator/Animation List (WIP)` | v0.2 のアニメーション登録 UI の試作 |
| `Tools/FXCreator/Debug/...` | 開発用（性能計測・デバッグ表示） |
| `Tools/FXCreator/Legacy/...` | GraphView 時代の旧実装（参照用に退避） |
| `Assets/FX Creator/Select This Folder` | プロジェクトビューでフォルダを登録 |

## 同梱物のライセンス

`Editor/Utilities/HaiEditor/` は Haï~ 氏の
[blendshape-viewer](https://github.com/hai-vr/blendshape-viewer)（MIT）を同梱しています。
詳細は [`Editor/Utilities/HaiEditor/LICENSE.md`](Editor/Utilities/HaiEditor/LICENSE.md) を参照してください。

## テスト

EditMode テストは `Tools > Test Runner`（`FXCreator.Editor.Tests`）から実行できます。
