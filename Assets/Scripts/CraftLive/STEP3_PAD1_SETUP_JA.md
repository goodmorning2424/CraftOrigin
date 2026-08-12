# ステップ3 Pad1セットアップ・確認手順

## 実装済みの範囲

- パワーアップ、スキル、タイプの3列を自動生成
- 各列を指またはマウスで縦スクロール
- 登録済み素材の絵画をカテゴリ別に表示
- 絵画タップで`CraftLiveSession.SelectMaterial`を実行
- 選択素材の3Dモデルを手前へ表示
- 小型ホログラム板へ素材情報を表示
- 選択状態をPad2へ共有
- モデルとIconが未設定でも仮Primitiveで動作

Pad2で配置枠を選択する処理はステップ4で実装する。

## 今すぐアタッチが必要なもの

ない。Step3 Upgraderが次を設定済み。

- Pad1ルートの`CraftLivePad1GalleryController`
- Pad1ルートの`CraftLivePad1MaterialPreview`
- Bootstrap共有Cameraの`PhysicsRaycaster`
- 2つのPad1コンポーネントから`CraftLivePad1Bindings`への参照

既存の`PowerUpWall`、`SkillWall`、`TypeWall`などは削除しない。

## 最初の動作確認

1. UnityがPlay Modeなら停止する
2. `Assets/Scenes/CraftLive/CraftLiveBootstrap.unity`を開く
3. `DefaultCraftLiveLaunchConfig.asset`を選択する
4. `Editor Role`を`Material Pad`にする
5. `Use Firebase In Editor`をOFFにする
6. Playする
7. Pad1シーンがAdditiveロードされるまで待つ

初期状態ではQR不要の基礎素材だけがパワーアップ列へ表示される。
スキル列とタイプ列は、素材が未登録なら「QRで素材を登録」と表示される。

## 操作確認

1. パワーアップ列を上方向へドラッグする
2. 下側の絵画が表示されることを確認する
3. 下方向へドラッグして先頭へ戻れることを確認する
4. PCではマウスホイールでも動くことを確認する
5. 絵画を1つタップする
6. 絵画が少し手前へ移動することを確認する
7. 素材の仮3Dモデルが画面手前へ飛び出すことを確認する
8. 右側に説明ホログラム板が表示されることを確認する
9. Consoleに赤いエラーがないことを確認する

素材選択後はPad2の配置待ち状態になる。Pad2で配置先を確定すると転送待ちへ入り、
Pad1で次の素材を続けて選択できる。

## 未登録素材も仮表示する方法

本番仕様では未登録素材を表示しない。レイアウト確認だけ行う場合は次の手順を使う。

1. Playを停止する
2. `Pad1_MaterialGallery.unity`を開く
3. `Pad1_MaterialGallery_Root`を選択する
4. `CraftLivePad1GalleryController`を探す
5. `Show Locked Materials`をONにする
6. Bootstrapシーンへ戻ってPlayする

未登録素材は暗く表示され、タップできない。本番前にOFFへ戻す。

## Gallery ControllerのInspector項目

- `Session`
  - シーン間参照なので未設定のままでよい。実行時に自動取得する
- `Bindings`
  - 自動設定済み。外さない
- `Painting Prefab`
  - 後から独自の額縁Prefabを設定する場所
- `Show Locked Materials`
  - 本番はOFF
- `Apply Default Layout`
  - 自動配置を使う場合はON
- `Column Spacing`
  - 3列の横間隔
- `Column Vertical Position`
  - 壁全体の高さ
- `Painting Spacing`
  - 絵画同士の縦間隔
- `Visible Paintings`
  - 一度に表示する絵画数
- `Drag Sensitivity`
  - 指ドラッグの感度
- `Mouse Wheel Step`
  - Editor確認用ホイール移動量
- `Painting Size`
  - 仮額縁の大きさ
- `Frame Color`
  - 仮額縁の色
- `Wall Color`
  - 仮壁の色

## Material PreviewのInspector項目

- `Session`
  - 未設定でよい。実行時に自動取得する
- `Bindings`
  - 自動設定済み
- `Use Material World Prefab`
  - 後から設定した本モデルを使用するためON
- `Create Placeholder When Missing`
  - モデル未設定時にPrimitiveを出す。現在はON
- `Target Model Size`
  - プレビューの基準サイズ
- `Model Rotation`
  - 初期角度
- `Spin Degrees Per Second`
  - 自動回転速度
- `Reveal Duration`
  - 手前へ飛び出す時間
- `Create Fallback Hologram`
  - UI未設定時に仮説明板を生成する
- `Hologram Color`
  - 仮説明板の基準色

## 後から素材モデルを接続する方法

モデルが完成したら素材定義へ設定する。

1. `Assets/CraftLiveData/Materials`を開く
2. 対象素材アセットを選択する
3. `World Prefab`へ素材3D Prefabを設定する
4. 必要なら`Icon`へ絵画用Spriteを設定する
5. `Effect Color`へテーマカラーを設定する
6. BootstrapからPlayして選択する

`World Prefab`が設定されると、Primitiveの代わりにそのPrefabが自動表示される。
Pad1シーンへ素材モデルを直接置く必要はない。

## 後から独自額縁Prefabを接続する方法

独自Prefabのルートへ`CraftLiveMaterialPaintingView`を付ける。

- クリック判定用Colliderを付ける
- `Moving Root`へ選択時に動かすTransformを指定する
- `Tint Renderers`へ選択・ロック色を反映するRendererを指定する
- `Interaction Colliders`へタップを有効・無効にするColliderを指定する
- 名前やIconは各UnityEventから自作UIへ接続する

完成したPrefabをGallery Controllerの`Painting Prefab`へ設定する。
配列や表示参照を未設定にしてもnullエラーにはならない。

## 独自ホログラムUIへの接続

`CraftLivePad1MaterialPreview`のUI Eventsから接続する。

- `On Details Visible`
- `On Icon Changed`
- `On Name Changed`
- `On Category Changed`
- `On Description Changed`
- `On Ability Changed`
- `On Usage Changed`
- `On Detail Text Changed`
- `On Theme Color Changed`

独自UIが完成したら`Create Fallback Hologram`をOFFにする。

仮表示は旧式`TextMesh`を使用するため、日本語フォントの最終品質は保証しない。
本番UIでは日本語グリフを含むTextMeshPro Font Assetを用意する。

## 安全確認

Unityメニューから次を実行する。

```text
Tools > Craft-live > Validate Current Project
Tools > Craft-live > Run EditMode Tests
```

現在の確認結果:

```text
Validation Errors: 0
EditMode Tests: 66 / 66 Passed
```

残る2件のWarningは、後から設定するIcon/Prefabとゲーム数値についての警告。
