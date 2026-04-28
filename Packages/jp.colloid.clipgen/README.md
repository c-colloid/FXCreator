# Clip Generator

選択したGameObject / Componentから、オン/オフ用AnimationClipを自動生成するUnity Editor拡張です。
VRChat Avatars向けのトグル系アニメーション作成を高速化します。

## 動作要件

- Unity 2022.3 以上
- VRChat Avatars SDK 3.5.0 以上

## 使い方

1. AnimationClip を作成したい GameObject を選択して右クリック
2. ClipGen のメニューから GameObject か Component かを選択
3. 保存したい場所と名前を設定して保存
4. オン/オフ アニメーションの両方が生成されたことを確認

### オプション

`Tools/ClipGen/Settings` 内に設定があります。

- **ClipName**: 接頭辞 (Prefix)・接尾辞 (Suffix) のオン/オフを制御
- **GenerateScript**: 未対応 Component のコンテキストメニューを生成

## 対応コンポーネント

- GameObject 全般
- SkinnedMeshRenderer / MeshRenderer / LineRenderer / TrailRenderer / ParticleSystemRenderer
- PhysBone / PhysBoneCollider
- Constraints (Aim / LookAt / Parent / Position / Rotation / Scale)
- Colliders (Box / Capsule / Mesh / Sphere)
- Joints (Character / Configurable / Fixed / Hinge / Spring)
- Animation / AudioSource / Camera / Cloth / FlareLayer / Light

## ライセンス

MIT License — 詳細は [LICENSE.md](./LICENSE.md) を参照してください。
