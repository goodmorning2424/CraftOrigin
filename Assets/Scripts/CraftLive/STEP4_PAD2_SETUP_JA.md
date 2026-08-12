# ステップ4 Pad2セットアップ・確認手順

## 実装済みの範囲

- 剣、突き、杖を含む武器候補の横スライド選択
- 左右ボタンによる武器候補の切り替え
- 武器の最終確定と中央への3D表示
- 武器確定前に素材枠を触れないようにする制御
- 参考画像に合わせた6枠の自動配置
- 素材カテゴリに合う枠だけを発光
- 枠タップ時の素材仮表示
- 配置先の確定、選び直し、キャンセル
- モデルや独自UIが未設定でも動く仮Primitive
- 独自PrefabとUIを後からInspector接続するための公開口

額縁の発射とPad2への落下はステップ5で実装済み。
液体演出はステップ7で実装する。
配置を確定すると素材はPad1の転送待ちへ追加される。

## 6枠の対応

参考画像を縦画面で見たときの配置を次で固定している。

| 画面上の位置 | 論理スロット | 配置できる素材 |
|---|---|---|
| 左上 | `Top` | 基礎ステータス |
| 左中 | `Left` | 基礎ステータス |
| 右上 | `Right` | 基礎ステータス |
| 右中 | `Bottom` | 基礎ステータス |
| 左下 | `Skill` | 固有スキル |
| 右下 | `Attribute` | 属性 |

中央は武器表示領域であり、素材枠には使用しない。

## 今すぐ必要なアタッチ

ない。Step4 Upgraderが次をPad2シーンへ設定済み。

- `CraftLivePad2WeaponCarousel`
- `CraftLivePad2PlacementController`
- 両コンポーネントから`CraftLivePad2Bindings`への参照

武器・素材モデルを後から設定しても問題ない。未設定中は種類に応じた
Primitiveで代用する。既存のPad2アンカーは削除していない。

## 最初の動作確認

1. UnityがPlay Modeなら停止する
2. `Assets/Scenes/CraftLive/CraftLiveBootstrap.unity`を開く
3. `Assets/CraftLiveData/DefaultCraftLiveLaunchConfig.asset`を選ぶ
4. `Editor Role`を`Workbench Pad`にする
5. `Use Firebase In Editor`をOFFにする
6. Playする
7. Pad2シーンがAdditiveロードされるまで待つ
8. 中央の武器候補を左右にドラッグする
9. `<`と`>`でも候補が切り替わることを確認する
10. `この武器にする`を押す
11. カルーセルが消え、確定武器が中央へ表示されることを確認する
12. `武器を選び直す`で選択画面へ戻れることを確認する
13. Consoleに赤いエラーがないことを確認する

仮表示の`TextMesh`は日本語フォントを持たない場合がある。本番UIでは
日本語グリフ入りのTextMeshPro Font Assetを使う。

## Pad2単独で配置枠を確認する

Pad1との通信をまだ使わずにテストするため、Editor専用コマンドを用意している。
このコマンドはWebGL本番ビルドには含まれない。

1. 上の手順で武器を確定する
2. Playを続けたままHierarchyで`Pad2_Workbench_Root`を選ぶ
3. Inspectorの`CraftLivePad2PlacementController`右上メニューを開く
4. `Debug/Select First Base Material`を押す
5. 左上、左中、右上、右中の4枠だけが明るくなることを確認する
6. 明るい枠をタップする
7. 素材の仮モデルと確認ボタンが出ることを確認する
8. `選び直す`を押し、別の基礎枠を選べることを確認する
9. `キャンセル`を押し、選択が解除されることを確認する

スキルと属性も同じ手順で確認できる。

- `Debug/Select First Skill Material`: 左下だけが有効
- `Debug/Select First Attribute Material`: 右下だけが有効

デバッグコマンドは未登録素材をそのPlay中だけ登録してから選択する。

## 配置確定の確認

1. デバッグコマンドで素材を選ぶ
2. 有効な枠をタップする
3. `この場所に置く`を押す
4. 表示が転送待ち追加へ変わることを確認する
5. それ以上枠を操作できないことを確認する

確定後はPad1で別素材を続けて選択できる。発射操作とPad2到着の確認方法は
`STEP56_TRANSFER_PAD3_SETUP_JA.md`を参照する。

## Weapon CarouselのInspector項目

- `Session`
  - シーン間参照なので未設定でよい。実行時に自動取得する
- `Bindings`
  - 自動設定済み。外さない
- `Create Fallback Visuals`
  - 独自カルーセルを接続するまではON
- `Swipe Threshold Pixels`
  - 何pxドラッグしたら候補を切り替えるか
- `Card Spacing`
  - 候補間の横幅
- `Neighbor Scale`
  - 左右候補の縮小率
- `Selected Model Size`
  - カルーセル中央候補の基準サイズ
- `Center Model Size`
  - 確定後の中央武器の基準サイズ
- `Card Color`
  - 仮カード色

`UI Events`には次を独自UIへ接続できる。

- `On Selection Visible`
- `On Weapon Name Changed`
- `On Weapon Type Changed`
- `On Attack Changed`
- `On Defense Changed`
- `On Evasion Changed`
- `On Weapon Confirmed`

## Placement ControllerのInspector項目

- `Session`
  - 未設定でよい。実行時に自動取得する
- `Bindings`
  - 自動設定済み
- `Fallback Material Preview Prefab`
  - 素材側`World Prefab`がない場合に使う共通Prefab
- `Create Fallback Slots`
  - 独自枠を接続するまではON
- `Apply Reference Layout`
  - 参考画像に合わせた既定座標を使う場合はON
- `Slot Diameter`
  - 仮枠の直径
- `Base Slot Color`
  - 基礎4枠の仮色
- `Skill Slot Color`
  - 左下スキル枠の仮色
- `Attribute Slot Color`
  - 右下属性枠の仮色
- `Create Fallback Controls`
  - 独自確認UIを接続するまではON

`UI Events`には案内文、各ボタンの表示状態、候補枠を接続できる。

- `On Instruction Changed`
- `On Confirm Visible`
- `On Change Visible`
- `On Cancel Visible`
- `On Candidate Slot Changed`

## 後から武器モデルを接続する

1. `Assets/CraftLiveData/Weapons`を開く
2. 対象の武器定義アセットを選ぶ
3. `Workbench Prefab`へPad2表示用Prefabを設定する
4. 必要なら`Hologram Prefab`へPad4用Prefabを設定する
5. `Display Scale`と`Display Euler`を調整する
6. BootstrapからPad2をPlayして選択・確定表示を確認する

モデルPrefabにはColliderを付けなくてもよい。付いているColliderは表示時に
無効化され、カルーセルのタップ・ドラッグを妨げない。

## 後から素材モデルを接続する

1. `Assets/CraftLiveData/Materials`を開く
2. 対象素材定義を選ぶ
3. `World Prefab`へ素材3D Prefabを設定する
4. `Effect Color`へテーマカラーを設定する
5. Pad2で仮配置し、該当枠内へ表示されることを確認する

Pad2シーンの各枠へ素材モデルを直接置く必要はない。

## 独自の6枠を接続する

各アンカーの子に独自枠オブジェクトを置き、
`CraftLivePlacementSlotView`を1つ付ける。

1. タップ判定用Colliderを付ける
2. `Slot`を上の対応表どおりに設定する
3. `Preview Anchor`へ素材モデルの仮配置位置を指定する
4. `Highlight Renderers`へ発光させるRendererを登録する
5. `Require Confirmed Weapon`をONにする
6. `Create Fallback Slots`をOFFにする
7. `Apply Reference Layout`は独自座標を保つならOFFにする

独自枠が存在する場合、自動生成処理はその枠を削除しない。

## 独自ボタンを接続する

独自の3DボタンまたはCanvas Buttonから、次のpublicメソッドを呼ぶ。

- 確定: `CraftLivePad2PlacementController.ConfirmCandidate`
- 選び直し: `CraftLivePad2PlacementController.ChangeCandidate`
- キャンセル: `CraftLivePad2PlacementController.CancelPlacement`
- 前の武器: `CraftLivePad2WeaponCarousel.SelectPrevious`
- 次の武器: `CraftLivePad2WeaponCarousel.SelectNext`
- 武器確定: `CraftLivePad2WeaponCarousel.ConfirmSelected`
- 武器選び直し: `CraftLivePad2WeaponCarousel.OpenSelection`

独自ボタンの接続後は`Create Fallback Controls`または
`Create Fallback Visuals`をOFFにして仮ボタンを消す。

## 安全確認

Unityメニューから次を実行する。

```text
Tools > Craft-live > Validate Current Project
Tools > Craft-live > Run EditMode Tests
```

現在の確認結果:

```text
Validation Errors: 0
EditMode Tests: 80 / 80 Passed
```

残る2件のWarningは、後から設定するIcon/Prefabとゲーム数値についての警告。
