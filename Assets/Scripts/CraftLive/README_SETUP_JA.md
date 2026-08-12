# Craft-live Pad1 / Pad2 素材配置システム

> 新仕様への段階移行を開始しています。移行前の確認は
> `STEP0_BASELINE_AUDIT_JA.md`を先に実施してください。

## 1. 実装方針

本番対象は縦向きiPadのSafariです。Unity 6のWebプラットフォームは
iOS Safari 15以降をサポートするため、3D実装は継続します。

ただし、画面は軽量な2.5D寄りの3Dとして構成します。

- 木製作業台、札、フック、投石器、素材は3D
- 札一覧と案内表示はWorld Space CanvasまたはScreen Space Camera
- 演出はTransformアニメーション中心
- 照明はベイクを基本とする
- リアルタイム影、HDR、SSAO、被写界深度などは使用しない
- 30fpsを基準とする
- 1回の素材配置は約2～3秒

素材数、配置判定、消費、通信状態は3D表現と分離されています。
モデルや演出Prefabを後から交換してもコアロジックは変わりません。

## 2. 新しい操作フロー

```text
Pad1でQR読取
  ↓
札を新規登録、または既存札の数を+1
  ↓
Pad1で札を選択
  ↓
Pad2で有効な配置枠だけ点灯
  ↓
Pad2で候補枠を選択
  ↓
Pad2で確認、選び直し、またはキャンセル
  ↓
Pad1で投石器へ装填して発射
  ↓
Pad2の対応方向から札が到着
  ↓
札が素材へ変化して着地
  ↓
所持数を1消費して配置確定
```

確認前はスロットも所持数も変更しません。到着演出が完了した時点で
初めて所持数を消費します。既存素材を取り外した場合は所持数へ戻します。

## 3. 標準データを更新

Unityメニューで次を実行します。

`Tools > Craft-live > Create Default Data Assets`

`Assets/CraftLiveData`にCatalog、Rules、素材11個、武器3個が作成または更新されます。
既存のIconやPrefab参照は削除されません。

各Material Assetで次を設定します。

- `Icon`: 札に表示するイラスト
- `World Prefab`: Pad2へ配置する実物素材
- `Transfer Ticket Prefab`: 端末間を飛ぶ札
- `Placement Effect Prefab`: 着地時の短いエフェクト
- `Effect Color`: 枠、仮配置、着地演出の色
- `Material Form`: `Ore`、`Gem`、`Charm`、`Spirit`
- `Landing Audio Clip`: 素材固有の着地音
- `Description`: 素材説明
- `Ability Summary`: 属性や能力
- `Usage Summary`: 主な用途

Prefabが未設定でもCubeやSphereを使ってフロー確認できます。

## 4. Sceneの基本Hierarchy

```text
CraftLiveSystem
├─ Pad1Root
│  ├─ WoodWorkbench
│  ├─ MaterialScroll
│  │  └─ Viewport
│  │     └─ Content
│  ├─ SmallHologram
│  ├─ QrScanButton
│  └─ Launcher
│     ├─ TicketStart
│     ├─ LauncherSeat
│     ├─ Arm
│     ├─ Spring
│     ├─ ExitAttribute
│     ├─ ExitSkill
│     ├─ ExitTop
│     ├─ ExitRight
│     ├─ ExitLeft
│     └─ ExitBottom
├─ Pad2Root
│  ├─ WoodWorkbench
│  ├─ WeaponAnchor
│  ├─ TransferSpawn
│  ├─ AttributeSlot
│  ├─ SkillSlot
│  ├─ TopSlot
│  ├─ RightSlot
│  ├─ LeftSlot
│  ├─ BottomSlot
│  ├─ ConfirmButton
│  └─ CancelButton
├─ QrPadRoot
└─ HologramPadRoot
```

`QrPadRoot`は旧4画面構成との互換用です。新仕様では
`CraftLiveQrScanner`を`Pad1Root`内へ置きます。

## 5. 共通システム

`CraftLiveSystem`へ追加します。

- `CraftLiveSession`
- `CraftLiveRoleRouter`
- `CraftLiveRoomTransport`
- `CraftLivePlacementWatchdog`
- `CraftLiveWebPresentation`

### CraftLiveSession

- `Catalog`: `DefaultCraftLiveCatalog`
- `Rules`: `DefaultCraftLiveRules`
- `Room Id`: Editor確認用の`001`
- `Role`: `Auto`

コンポーネントメニューの
`Validate Craft-live Configuration`を実行します。

### CraftLiveRoleRouter

- `Material Pad Root`: `Pad1Root`
- `Workbench Pad Root`: `Pad2Root`
- 残り2Root: 必要な場合だけ設定

本番URL:

```text
ゲームURL?screen=pad1&room=001
ゲームURL?screen=pad2&room=001
```

`items`と`craft`も旧URL互換で使用できます。

### CraftLiveRoomTransport

- `Use Firebase`: 本番はON
- `Firebase Database Url`: 使用するRealtime Database URL
- `Poll Interval Seconds`: `0.5`
- `Request Timeout Seconds`: `10`

同じ`room`のPad1とPad2だけが同期します。Editorだけで確認する場合は
`Use Firebase`をOFFにします。

### CraftLivePlacementWatchdog

Pad1またはPad2の演出コンポーネントが停止した場合に、配置フローを
次の安全な状態へ進めます。

- `Stage Timeout Seconds`: `6`
- `Completion Timeout Seconds`: `3`

通常の演出中は何もしません。

### CraftLiveWebPresentation

- `Target Camera`: 各PadのCamera
- `Target Frame Rate`: `30`
- `Target Aspect`: `3 : 4`
- `Letterbox Camera`: ON
- `On Portrait Changed`: falseの時だけ表示する回転案内へ接続可能

Canvasは`Screen Space - Camera`にするとCameraの3:4領域と一致します。

## 6. Pad1の札Prefab

札Prefabの例:

```text
MaterialTicket
├─ WoodenTag
├─ Icon
├─ Name
├─ CategoryMark
├─ Count
├─ IncrementFeedback
└─ SmallHologram
   ├─ Description
   ├─ Ability
   └─ Usage
```

ルートへ`CraftLiveMaterialTicketView`を追加します。

- `Moving Root`: 選択時に浮かせる札本体
- `Canvas Group`: 非操作時に暗くする場合に設定
- `Renderers`: 再登録時に発光させるRenderer
- `Selected Local Offset`: カメラ側へ少し浮く方向
- `Drop Local Offset`: 新規札の落下開始位置

Bindingsを次の表示へ接続します。

- `On Icon Changed`: Icon
- `On Name Changed`: Name
- `On Category Changed`: CategoryMark
- `On Count Changed`: Count
- `On Description Changed`: SmallHologram内の説明
- `On Ability Changed`: 属性、能力
- `On Usage Changed`: 用途
- `On Details Visible`: SmallHologramの`SetActive`
- `On Increment Feedback`: `×1追加`表示

3D札として直接タップする場合はColliderを付け、Cameraへ
`Physics Raycaster`を付けます。Canvas内の札として使う場合は、
背景Imageの`Raycast Target`をONにします。

## 7. Pad1の札一覧と絞り込み

`MaterialScroll/Viewport/Content`を通常の`Scroll Rect`として作ります。
`MaterialScroll`または管理Objectへ`CraftLiveMaterialBoardView`を追加します。

- `Session`: 共通Session
- `Content Root`: Scroll RectのContent
- `Ticket Prefab`: 手順6のPrefab
- `Show Unregistered For Debug`: 本番はOFF

絞り込みボタンから次を呼びます。

- `ShowAll()`
- `ShowAttributes()`
- `ShowSkills()`
- `ShowUpgrades()`

初回QRは札を生成して上から落とします。同じQRは新しい札を作らず、
所持数、`×1追加`、発光だけを更新します。所持数が0でも札は残ります。

## 8. Pad1のQR読取

`Pad1Root`へ`CraftLiveQrScanner`を追加します。

- `Session`: 共通Session
- `Timeout Seconds`: `8`
- `On Scan Error`: エラー表示
- `On Scan Cancelled`: スキャンボタン再表示

スキャンボタンから`StartScan()`を呼びます。

対応QR:

```text
craftlive:material:fireCrystal
{"materialId":"fireCrystal"}
https://example.invalid/material?material=fireCrystal
```

iPadのカメラ利用にはHTTPSとSafariのカメラ許可が必要です。

## 9. Pad2の配置枠

6個の枠へそれぞれColliderと`CraftLivePlacementSlotView`を追加します。

- `Session`: 共通Session
- `Slot`: その枠の種類
- `Preview Anchor`: 仮素材を表示する位置
- `Highlight Renderers`: 点灯させる枠Renderer
- `Available Color`: 配置可能色
- `Selected Color`: 候補選択色
- `Fallback Preview Prefab`: 任意

素材カテゴリと一致する枠だけが操作可能になります。

- 属性素材: `Attribute`
- 能力素材: `Skill`
- 強化素材: `Top`、`Right`、`Left`、`Bottom`

枠は直接タップできます。別Buttonから操作する場合は
`SelectPlacement()`を呼びます。

## 10. Pad2の確認とキャンセル

管理Objectへ`CraftLiveCommandActions`を追加します。

確認ボタン:

`CraftLiveCommandActions.ConfirmPlacement()`

選び直しボタン:

`CraftLiveSlotAction.ClearPlacementChoice()`

キャンセルボタン:

`CraftLiveCommandActions.CancelPlacement()`

`CraftLiveStateEvents`の次のイベントを表示制御へ接続します。

- `On Instruction`: 常時表示する短い案内
- `On Slot Selection Enabled`: 配置枠全体
- `On Placement Confirmation Visible`: 確認UI
- `On Material Selection Enabled`: Pad1の札操作

確認ボタンを押すまでスロットは確定されません。

## 11. 3Dボタン

3DボタンのルートにColliderと`CraftLiveWorldButton`を追加します。

```text
ConfirmButton
├─ Base
├─ Cap
└─ Label
```

- `Press Target`: 押し下げるCap
- `Renderers`: CapのRenderer
- `Press Depth`: `0.02`～`0.04`
- `Animation Duration`: `0.08`
- `Cooldown Seconds`: `0.15`
- `On Pressed`: 実行するCommand

Cameraへ`Physics Raycaster`、Sceneへ1個だけ`EventSystem`を置きます。
新Input Systemを使う場合は`InputSystemUIInputModule`を使用します。

`CraftLiveStateEvents`のboolイベントを
`CraftLiveWorldButton.SetInteractable(bool)`へ接続すると、
操作できない段階では自動的に暗くできます。

## 12. Pad1の投石器

`Pad1Root/Launcher`へ`CraftLiveTransferLauncherView`を追加します。

- `Ticket Start`: 選択札が演出を開始する位置
- `Launcher Seat`: 札を固定する位置
- `Launcher Arm`: 引き絞る可動部
- `Spring Visual`: 縮ませるバネ
- `Slot Exits`: 6スロット分の画面外出口
- `Fallback Ticket Prefab`: 任意
- `Loading Duration`: `0.35`
- `Launch Duration`: `0.55`
- `Launch Arc Height`: `0.7`
- `Loaded Arm Euler`: 投石器を引いた角度
- `Compressed Spring Scale`: バネの圧縮率
- `Audio Source`、`Loading Clip`、`Launch Clip`: 任意

各`Slot Exit`はPad2側の対応する`Arrival Entry`と見た目の方向を
一致させます。例えばPad1の右端へ飛んだ札はPad2の左端から入れます。

## 13. Pad2の到着と素材化

`Pad2Root`へ`CraftLiveWorkbenchView`を追加します。

- `Session`: 共通Session
- `Weapon Anchor`: 武器表示位置
- `Transfer Spawn`: Arrival Entry未設定時の予備位置
- `Slot Anchors`: 6要素
- 各`Anchor`: 最終配置位置
- 各`Arrival Entry`: その枠へ飛来する画面端の位置
- `Transfer Duration`: `0.8`
- `Transfer Arc Height`: `1.0`前後
- `Completion Hold Seconds`: `0.8`
- `Fallback Material Prefab`: 任意
- `Fallback Ticket Prefab`: 任意
- `Audio Source`: 着地音の再生元

`Material Form`に応じて到着後の動きが変わります。

- `Ore`: 重く落下
- `Gem`: 小さく跳ねて拡縮
- `Charm`: 揺れながら収まる
- `Spirit`: 左右へ漂いながら収まる

着地後に`Placement Effect Prefab`と`Landing Audio Clip`を再生し、
所持数を1消費して配置を確定します。

## 14. iPad WebGL設定

最初の実機基準を次にします。

- iPadOS / Safari 15以降
- Portrait固定
- 30fps
- URP Forward
- HDR OFF
- Opaque Texture OFF
- Depth Texture OFF
- SSAO OFF
- Additional LightsはPer VertexまたはDisabled
- リアルタイム影は原則OFF
- 影が必要な木製作業台はベイク
- テクスチャは原則1024以下
- 同時表示パーティクルは少数
- Skinned Meshは避け、Transformで機構を動かす
- WebGL Buildの例外設定は本番で最小化
- iFrame内で公開せず、直接HTTPSページとして開く

モデル品質を上げる前に、対象となる最も古いiPadで実機計測します。
30fpsを維持できない場合は、まず影、ポストエフェクト、透明描画、
テクスチャ解像度、パーティクル数の順で削減します。

## 15. 段階確認

### 段階A: 通信なし

1. `Use Firebase`をOFF
2. Pad1でQRの代わりに`OnQrScanResult("fireCrystal")`を実行
3. 札が1枚だけ作られることを確認
4. 再実行して`×2`になることを確認
5. 絞り込みとスクロールを確認

### 段階B: 選択と確認

1. 札をタップ
2. Pad2で有効枠だけ点灯
3. 候補枠を変更できることを確認
4. キャンセルで所持数が変わらないことを確認
5. 確認前に最終スロットが変わらないことを確認

### 段階C: 転送演出

1. 確認後にPad1で装填演出
2. 選択枠に対応した出口へ飛ぶことを確認
3. Pad2の対応方向から入ることを確認
4. 素材へ変化して枠へ収まることを確認
5. 完了後だけ所持数が1減ることを確認

### 段階D: iPad実機

1. HTTPSのWebGL BuildをPad1とPad2で開く
2. 同じ`room`を指定
3. Safariのカメラ許可を確認
4. 10回連続でQR登録と配置を実施
5. 誤タップ、画面回転、タブ復帰、通信遅延を確認
6. Safari Web Inspectorと実機表示でメモリ、発熱、fpsを確認

## 16. 自動テスト

`Window > General > Test Runner > EditMode`で
`CraftLiveCoreTests`を実行します。

確認対象:

- 配置位置別の強化値
- 合成必須条件
- 完成武器名と能力
- QRペイロード3形式
- 同じQRによる所持数加算
- 確認前にスロットと所持数が変わらないこと
- Pad2到着完了後の消費と配置
- 取り外した素材が所持数へ戻ること
