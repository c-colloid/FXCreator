# VRC Expression Parameters Extension

VRChat Avatars の `VRCExpressionParameters` の Inspector を強化する Unity Editor 拡張です。
Animator 内のパラメーターや VRC 内部パラメーターを視覚的に確認・編集できます。

## 動作要件

- Unity 2022.3 以上
- VRChat Avatars SDK 3.5.0 以上（3.7.0 以上で内部Parameter表示に対応）

## 主な機能

- Animator に存在するパラメーターをリスト表示し、Expression Parameters への追加を補助
- VRC 内部パラメーター（`PreviewMode` / `IsAnimatorEnabled` 等）の表示
- 各種 Parameter リストを視覚的に区別する色分け表示

## 使い方

`VRCExpressionParameters` アセットを選択すると、拡張された Inspector が自動的に表示されます。
表示テーマは Unity の Light / Dark に追従します。

## ライセンス

MIT License — 詳細は [LICENSE.md](./LICENSE.md) を参照してください。
