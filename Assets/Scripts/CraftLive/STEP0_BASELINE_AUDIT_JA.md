# Craft-live ステップ0 基準監査

作成日: 2026-07-30

## 目的

ステップ0では新仕様をまだ実装しません。現在のコード、保存データ、
Scene、ScriptableObjectを移行前の基準として記録し、後続ステップで
既存機能を壊した場所を判別できるようにします。

## 現在の基準

- Unity: `6000.4.0f1`
- WebGL対象Scene: `Assets/Scenes/Craft.unity`
- RoomState schema: `2`
- Material: `11`
- Weapon: `3`
- Slot: `Attribute / Skill / Top / Right / Left / Bottom`
- Stat: `AttackRate / DefenseRate / EvasionRate / ElementBoost`
- QR登録: Inventory EntryとCountを使用
- 転送: 1素材ずつの状態遷移
- 合成入力: 円運動
- Firebase: RoomState全体のREST同期

## ステップ0で追加した安全機構

### Baseline Validator

Unityメニュー:

`Tools > Craft-live > Step 0 > Run Baseline Validation`

次を検証します。

- Enumのシリアライズ番号
- RoomState schema
- Build SettingsのScene
- CatalogとRulesの存在
- Material IDとWeapon IDの重複
- nullデータ参照
- 未設定IconとPrefab
- 読み込み済みSceneのSession設定
- Workbenchの6スロット
- 重複Anchor
- 未設定Arrival Entry

結果はConsoleと次のファイルへ出力されます。

`Library/CraftLiveReports/Step0Baseline_latest.md`

`WARNING`は未完成設定を示します。`ERROR`が1件でもある場合は、
次の仕様実装へ進みません。

### Baseline Tests

`CraftLiveStep0BaselineTests`で次を固定します。

- Slot Enumの現在の数値
- RoomState V2のJSON往復
- V1 QR登録データからV2への移行
- SlotとMaterial Categoryの対応
- Catalogの11素材と3武器
- Craft Sceneの6つの固有Anchor

## ステップ0で修正した不具合

`Assets/Scenes/Craft.unity`で`Top`と`Skill`が同じ
`SkillAnchor`を参照していました。

`Top`を既存の`TopAnchor`へ修正しました。ゲームルール、データ形式、
ほかのScene構成は変更していません。

## 現在確認できている移行リスク

### 後続ステップで必ず変更する項目

- Count制を永久登録制へ変更
- 4ステータスを攻撃力、防御率、回避率の3種類へ変更
- Slotの画面上の位置を新しい錬成台へ対応
- 単体即時転送を個別・複数転送キューへ変更
- 札表示を3列の絵画壁へ変更
- 円運動合成をトンカチレールへ変更
- Pad3をQRとガラス管の複合画面へ変更
- PadごとのSceneとWebGL Buildを追加

### 現在の未設定項目

- 11素材のIcon
- 11素材のWorld Prefab
- 11素材のTransfer Ticket/Frame Prefab
- 3武器のWorkbench Prefab
- Workbenchの6つのArrival Entry
- 本番用Role Router Root

素材と武器のモデルが完成済みでも、ScriptableObjectとSceneへの
Prefab割り当ては別作業です。後続ステップで新しいデータ構造が
確定してから割り当てるため、ステップ0では未設定のままで問題ありません。

## Git作業ツリー

作業ツリーにはCraft-live実装、モデル、画像、URP設定を含む多数の
未追跡・変更ファイルがあります。ステップ0ではユーザーが作成した
モデル、Material、ProjectSettingsを削除、復元、初期化していません。

後続作業を始める前に、Unityを保存してからGitまたはフォルダーコピーで
現在の状態をバックアップしてください。

## Unityで行う確認手順

### 1. コンパイル確認

1. Unityでプロジェクトを開きます。
2. 右下のImport表示が消えるまで待ちます。
3. `Window > General > Console`を開きます。
4. 赤いCompile Errorが0件であることを確認します。
5. Errorがある場合はPlay Modeへ入りません。

### 2. Baseline Validator

1. `Assets/Scenes/Craft.unity`を開きます。
2. Sceneを保存します。
3. `Tools > Craft-live > Step 0 > Run Baseline Validation`を実行します。
4. Consoleの`Craft-live Step 0`メッセージを確認します。
5. `errors=0`であることを確認します。
6. `Library/CraftLiveReports/Step0Baseline_latest.md`を確認します。

Prefab未設定とArrival Entry未設定は現段階では`WARNING`です。

### 3. EditMode Tests

1. `Window > General > Test Runner`を開きます。
2. `EditMode`タブを選択します。
3. `CraftOrigin.CraftLiveTests`を展開します。
4. `Run All`を押します。
5. `CraftLiveCoreTests`と`CraftLiveStep0BaselineTests`が
   すべて緑になることを確認します。

### 4. 現行SceneのPlay確認

1. `Craft.unity`を開きます。
2. `CraftLiveSystem`を選択します。
3. `CraftLiveRoomTransport > Use Firebase`がOFFであることを確認します。
4. Play Modeへ入ります。
5. ConsoleへNullReferenceExceptionが出ないことを確認します。
6. 中央の武器と6Anchorが同じ位置へ重複生成されないことを確認します。
7. Play Modeを終了します。

### 5. バックアップ

1. Unityで全SceneとAssetを保存します。
2. Unityを閉じます。
3. プロジェクト全体、またはGitの現在差分をバックアップします。
4. バックアップ日時を記録します。
5. Unityを再度開き、次のステップへ進みます。

`Library`、`Temp`、`Logs`は再生成可能です。フォルダーコピーで容量を
抑える場合は除外できます。

## ステップ1へ進む条件

- Compile Errorが0件
- ValidatorのErrorが0件
- EditMode Testsがすべて成功
- Play Modeで例外が出ない
- 現在のプロジェクトをバックアップ済み

次のステップでは`xHigh`推論を使い、RoomState V3、永久登録、
3ステータスへの安全なデータ移行を実装します。
