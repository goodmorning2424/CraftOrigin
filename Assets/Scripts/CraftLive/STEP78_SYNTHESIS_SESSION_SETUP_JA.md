# 大ステップ7〜8 合成・セッション完了セットアップ

## 実装済み

- 各素材枠から中央へ流れるテーマカラー液体
- 液体が流れ切った後のPad3ステータス更新
- 合成ボタン
- トンカチと左右ガイドレール
- 規定往復数による合成完了
- Pad2の武器名、ランク、3ステータス、属性、スキル表示
- Pad4の完成武器回転表示
- セッションタイマー
- 完成武器履歴
- 次の武器作成
- 時間終了後の最終武器選択
- 武器コード発行

## 今すぐ必要なアタッチ

ない。以下はシーンへ設定済み。

- Bootstrap: `CraftLiveSessionTimerController`
- Pad2: `CraftLiveLiquidFlowController`
- Pad2: `CraftLiveHammerSynthesisController`
- Pad2: `CraftLivePad2ResultController`
- Pad4: `CraftLiveHologramView`
- Pad4: `CraftLivePad4Controller`

モデル、液体、トンカチ、ホログラム板が未設定でも仮Primitiveで確認できる。

## Pad2だけで合成を確認する

1. `CraftLiveBootstrap.unity`を開く
2. `DefaultCraftLiveLaunchConfig.asset`を選ぶ
3. `Editor Role`を`Workbench Pad`にする
4. `Use Firebase In Editor`をOFFにする
5. Playする
6. Hierarchyで`Pad2_Workbench_Root`を選ぶ
7. `CraftLiveHammerSynthesisController`のメニューから
   `Debug/Prepare Materials and Start Synthesis`を実行する
8. トンカチとガイドレールが表示されることを確認する
9. ガイドレール上を左右へ大きく往復ドラッグする
10. 表示された残り往復数が減ることを確認する
11. 規定回数後に結果ホログラムが出ることを確認する
12. `次の武器を作る`で武器選択へ戻ることを確認する

このデバッグ項目はEditor専用で、WebGL本番には含まれない。

## 液体を確認する

Pad2単独で到着確認を行う。

1. 武器を確定する
2. Placement Controllerのデバッグ項目で素材を選ぶ
3. 配置枠を確定する
4. Transfer Receiverの`Debug/Start First Queued Arrival`を実行する
5. 素材が着地することを確認する
6. 素材色の液体粒が中央へ流れることを確認する
7. 液体終了後にPad3公開値が更新されることを確認する

独自液体PrefabはLiquid Controllerの`Liquid Drop Prefab`へ設定する。

## 制限時間と最終選択を確認する

1. 1本以上の武器を完成させる
2. Bootstrapシーンの`CraftLiveSessionTimerController`を選ぶ
3. メニューから`Debug/Expire Session Now`を実行する
4. Pad2に完成武器一覧が表示されることを確認する
5. 1本をタップする
6. `XXXXXX`形式の6文字コードが表示されることを確認する
7. Pad4にも選択武器とコードが共有されることを確認する

当日の武器判別には`WEAPON_CODE_GUIDE_JA.md`を印刷して使用する。
コードは武器、属性、スキル、攻撃・防御・回避素材数を順番に表す。

実際の終了時間は`CraftLiveRules`の`Session Duration Seconds`を使う。

## CraftLiveRulesの追加項目

- `Required Hammer Passes`
  - 合成完了に必要な左右ストローク数
- `Hammer Stroke Pixels`
  - 1ストロークとして認識する移動距離
- `Maximum Completed Weapons`
  - 1セッションに保持する完成武器数。仮UIは最大12件表示
- `Weapon Code Prefix`
  - 発行コードの先頭。既定値は`CL`

すべてInspectorから変更できる。

## Liquid Flow Controller

- `Liquid Drop Prefab`
  - 独自液体粒Prefab
- `Drop Count`
  - 同時に流す液体粒数
- `Flow Duration`
  - 1粒が中央へ到達する時間
- `Drop Spacing Seconds`
  - 粒ごとの開始間隔
- `Wave Amount`
  - 流路の揺れ幅
- `On Flow Started`
  - 素材テーマカラーを通知
- `On Flow Progress`
  - 0から1の進行率
- `On Flow Completed`
  - 液体終了通知

独自メッシュやParticle Systemは各イベントへ接続できる。

## Hammer Synthesis Controller

- `Hammer Prefab`
  - 完成済みトンカチPrefab
- `Create Fallback Visuals`
  - 独自UI完成まではON
- `Rail Half Width`
  - ガイドレールの片側幅
- `Stroke Pixels Override`
  - 0ならRulesの値を使う
- `On Hammer Visible`
- `On Pass Count Changed`
- `On Passes Remaining Changed`
- `On Rail Progress`
- `On Hammer Strike`
- `On Start Rejected`

独自合成ボタンから`StartSynthesis`を呼ぶ。

## Pad2結果UI

`CraftLivePad2ResultController`のイベント:

- `On Result Visible`
- `On Weapon Name Changed`
- `On Rank Changed`
- `On Attack Changed`
- `On Defense Changed`
- `On Evasion Changed`
- `On Attribute Changed`
- `On Skill Changed`
- `On History Count Changed`
- `On Weapon Code Changed`

独自の次武器ボタンから`BeginNextWeapon`を呼ぶ。
最終候補ボタンから`SelectFinalWeapon(int resultSerial)`を呼ぶ。

## Pad4モデル

武器定義の`Hologram Prefab`へPad4用モデルを設定する。
未設定なら仮Cubeを表示する。

表示補正は`DefaultPad4Calibration.asset`で行う。

- `Model Local Position`
- `Model Local Euler`
- `Model Scale Multiplier`
- `Rotation Speed Degrees Per Second`
- プラ板の幅、高さ、角度、iPadからの距離

Pad4の独自UIは`CraftLivePad4Controller`の次のイベントへ接続する。

- `On Weapon Name Changed`
- `On Weapon Code Changed`
- `On Final Weapon Selected`

## RoomState V5

追加された主な状態:

- `completedWeapons`
- `sessionPhase`
- `sessionStartedAtUnixMs`
- `sessionEndsAtUnixMs`
- `selectedFinalResultSerial`
- `finalWeaponCode`
- `hammerPassCount`

V1からV4の保存データはV5へ自動移行する。

## 現在の検証

```text
Validation Errors: 0
EditMode Tests: 105 / 105 Passed
```

残るWarning 2件は、未設定のIcon/Prefabとゲーム数値。
