# Third-party code: Visual Expressions Editor (Haï~)

このフォルダのコードは FX Creator のオリジナルではなく、Haï~ 氏の
**Visual Expressions Editor**（現在は **Blendshape Viewer** に改称）から
取り込んだものです。いずれも MIT ライセンスです。

- 上流リポジトリ: <https://github.com/hai-vr/blendshape-viewer>
  （旧名 `visual-expressions-editor`。VCC/VPM 配布版は
  <https://github.com/hai-vr/visual-expressions-editor-vcc>）
- ライセンス: MIT

> **注意**: 取り込み元のバージョン／コミットは記録されていません。上流を参照して
> 差分を確認する場合、あるいは同梱をやめて VPM パッケージ依存に切り替える場合は、
> まず対応バージョンの特定が必要です。

## 同梱ファイルと著作権者

| ファイル | 著作権者 |
|---|---|
| `VisualExpressionsEditorWindow.cs` | Haï~ |
| `VisualExpressionsEditorGeneratorClip.cs` | Haï~ |
| `VisualExpressionsEditorGeneratorSingular.cs` | Haï~ |
| `VisualExpressionsEditorDiffCompute.cs` | Pema Malling（ファイル冒頭に MIT ヘッダあり） |
| `VEEDiffCompute.compute` | Pema Malling（ファイル冒頭に MIT ヘッダあり） |

---

## MIT License — Haï~

以下は上流リポジトリ `hai-vr/blendshape-viewer` の `LICENSE` の全文です。

```
MIT License

Copyright (c) 2023 Haï~ (@vr_hai github.com/hai-vr)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## MIT License — Pema Malling

`VisualExpressionsEditorDiffCompute.cs` と `VEEDiffCompute.compute` は
ファイル冒頭に以下の表記を持ちます（原文のまま）。

```
Copyright 2022 Pema Malling

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
