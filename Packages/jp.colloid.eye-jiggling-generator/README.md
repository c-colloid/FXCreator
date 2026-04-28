# Eye Jiggling Generator

VRChat Avatars向けの目揺れ（瞳ジグル）アニメーションを、BlendShape指定とプレビューUIで素早く構築できるUnity Editor拡張です。
NDMF（Non-Destructive Modular Framework）に対応しているため、ビルド時に非破壊で組み込まれます。

## 動作要件

- Unity 2022.3 以上
- VRChat Avatars SDK 3.5.0 以上
- NDMF 1.3.0 以上（Modular Avatar 利用時は同梱されています）

## 使い方

1. `Packages/Eye Jiggling Generator/Editor/Prefab/EyeJiggling.prefab` をアバター配下に配置
2. Inspector で対象メッシュ・BlendShape・揺れカーブを設定
3. プレビューウィンドウで再生して動きを確認
4. アバターをアップロード（NDMFが自動でレイヤー組み込み）

### プレビュー

- 再生 / ポーズ / 範囲指定スライダーが利用可能
- 範囲外で勝手にアニメーションが流れないよう制御済み

## ライセンス

MIT License — 詳細は [LICENSE.md](./LICENSE.md) を参照してください。

## 同梱シェーダーについて

`Editor/Resources/Shader/Internal-ColoredCopy.shader` は Unity Built-in Shaders 由来の改変版です。
詳細は同フォルダの `license.txt` を参照してください。
