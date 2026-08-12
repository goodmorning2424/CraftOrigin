# ステップ1: RoomState V3とInspector設定

## 実装した内容

- RoomStateをV3へ更新
- V2のinventoryと旧QR登録リストを永久登録IDへ自動移行
- 同じ素材を何度配置しても登録状態を消費しない方式へ変更
- 攻撃力、防御力、回避率の3ステータス計算へ変更
- 武器基礎ステータスを追加
- 基礎素材ごとの3ステータス補正を追加
- 炎、凍結、雷の効果設定を追加
- 幸運、2連撃、自動回復、命の珠の効果設定を追加
- 基礎素材4枠を全部必須にする設定を追加
- セッション制限時間設定を追加
- Pad4のプラ板とモデル表示の調整アセットを追加
- 素材カテゴリに応じて必要項目だけを表示するInspectorを追加

このステップではPad別UI、3D演出、複数転送、完成武器履歴はまだ変更しません。

## 最初に行う安全確認

1. Unity Editorを一度クリックして前面にします。
2. 右下のコンパイル表示が消えるまで待ちます。
3. `Window > General > Console`を開きます。
4. 赤いコンパイルエラーが0件であることを確認します。
5. 次を実行します。

```text
Tools > Craft-live > Step 1 > Upgrade Data Assets To V3
```

この処理は複数回実行しても、すでに設定済みのV3値を上書きしません。

6. 次を実行します。

```text
Tools > Craft-live > Validate Current Project
```

`errors=0`であることを確認します。ゲーム値、Icon、Prefabが未設定の場合は
warningが残ります。

7. 次を実行します。

```text
Tools > Craft-live > Run EditMode Tests
```

Consoleに`failed=0`と表示されるまで待ちます。
結果は`Library/CraftLiveReports/EditModeTests_latest.md`にも保存されます。

## CraftLiveRulesの設定

`Assets/CraftLiveData/DefaultCraftLiveRules.asset`を選択します。

- Session Duration Seconds
  - 1ゲームの制限時間です。
  - 初期値は300秒です。
- Require Attribute Slot
  - オンの場合、属性素材がないと合成できません。
- Require Skill Slot
  - オンの場合、固有スキル素材がないと合成できません。
- Require All Four Base Slots
  - オンの場合、4個の基礎素材枠を全部埋める必要があります。
  - オフの場合、基礎素材が0から3個でも合成できます。
- Mixing Duration Seconds
  - トンカチ操作の制限時間です。
- Power Per Radian
  - スライド操作を合成パワーへ変換する係数です。
- Success、Great Success、Super Success Threshold
  - 各合成ランクへ到達するパワーです。
- Success、Great Success、Super Success Bonus
  - 各ランクで3ステータス全部へ加算する値です。
- Maximum Stat
  - 攻撃力、防御力、回避率の上限です。

## 武器の設定

`Assets/CraftLiveData/Weapons`内の武器アセットを1個ずつ選択します。

- Weapon Id
  - 保存データで使うため、運用開始後は変更しません。
- Display Name
  - 表示する武器名です。
- Weapon Type
  - Sword、Thrust、Staffから選びます。
- Base Stats
  - Attack Rate: 基礎攻撃力
  - Defense Rate: 基礎防御力
  - Evasion Rate: 基礎回避率
- Icon
  - 武器選択画面用画像です。
- Workbench Prefab
  - Pad2の錬成台で表示するモデルです。
- Hologram Prefab
  - Pad4で表示するモデルです。
  - 未設定の場合はWorkbench Prefabを使用します。
- Preview Scale
  - 武器ごとの表示倍率です。

## 基礎ステータス素材の設定

素材アセットを選び、Categoryを`Upgrade`にします。

`Base Stat Material > Stat Modifiers`を設定します。

- Attack Rate: 攻撃力への加算値
- Defense Rate: 防御力への加算値
- Evasion Rate: 回避率への加算値

配置した場所によって補正値は変わりません。4個の基礎枠のどこへ置いても、
ここで設定した3つの値が加算されます。同じ登録済み素材を複数枠へ置くことも
できます。

旧データから次の値は自動移行済みです。

- SharpFang: 攻撃力30
- HardMetal: 防御力30
- WindFeather: 回避率30

MagicPowderは旧ElementBoost素材のため、V3では補正値0です。3ステータスの
どれへ割り当てるか決めてからInspectorで設定します。

## 属性素材の設定

素材アセットを選び、Categoryを`Attribute`にします。

- Attribute Id
  - `fire`、`freeze`、`lightning`などの一意なIDです。
- Attribute Display Name
  - 炎、凍結、雷などの表示名です。
- Element Effect > Type
  - Fire、Freeze、Lightningから選びます。
- Activation Chance Percent
  - 効果が発動する確率です。0から100で指定します。
- Effect Amount
  - 炎なら継続ダメージ、雷なら範囲ダメージなどの効果量です。
- Duration Seconds
  - 炎や凍結の持続秒数です。

FireCrystal、WaterStone、ThunderCoreのTypeはそれぞれFire、Freeze、
Lightningへ自動移行済みです。確率と効果量は未確定なので設定が必要です。

DarkStoneは現在の3属性に含まれない旧素材です。すぐには削除せず、後続の
コンテンツ整理でCatalogから外すか、3属性のどれかへ再設定します。

## 固有スキル素材の設定

素材アセットを選び、Categoryを`Skill`にします。

- Skill Effect > Type
  - Luck、Double Strike、Auto Heal、Life Orbから選びます。
- Activation Chance Percent
  - スキルの発動確率です。
- Primary Value
  - スキルの主効果値です。
- Secondary Value
  - 追加効果や代償の値です。
- Interval Seconds
  - 自動回復などの発動間隔です。

値の使い分け:

- Luck
  - Primary: クリティカル・幸運補正
  - Secondary: アイテム取得補正
- Double Strike
  - Primary: 追加攻撃のダメージ割合
- Auto Heal
  - Primary: 回復量
  - Interval: 回復間隔
- Life Orb
  - Primary: 攻撃力上昇量
  - Secondary: 自傷ダメージ量

CriticalOrbはLuck、LifeHerbはAutoHealへ自動移行済みです。
ReviveFeatherは現在の4スキルに含まれないため未設定です。

2連撃と命の珠の素材定義がない場合:

1. `Assets/CraftLiveData/Materials`をProjectウィンドウで開きます。
2. 右クリックします。
3. `Create > CraftOrigin > Craft-live > Material`を選びます。
4. Material Idを重複しない値にします。
5. Categoryを`Skill`にします。
6. Skill Effect Typeを設定します。
7. モデル、絵、色、効果値を設定します。
8. `DefaultCraftLiveCatalog.asset`のMaterialsリストへ追加します。

## Pad4 Calibrationの設定

`Assets/CraftLiveData/DefaultPad4Calibration.asset`を選択します。

- Plate Width Millimeters: プラ板の幅
- Plate Height Millimeters: プラ板の高さ
- Plate Angle Degrees: プラ板の角度
- Distance From Ipad Millimeters: iPad画面からプラ板までの距離
- Model Local Position: 武器モデルの表示位置補正
- Model Local Euler Angles: 武器モデルの角度補正
- Model Scale Multiplier: 全武器共通の表示倍率
- Rotation Speed Degrees Per Second: 回転速度

Pad4シーンを作成するステップで、Hologram ViewのCalibration欄へこの
アセットをアタッチします。

## この段階で変更しないもの

- Material IdとWeapon Id
- 6枠の列挙順
- FirebaseのroomId
- Craft.unity内の既存アンカー
- 旧V2フィールド

旧フィールドはV2保存データを読むため、移行完了までは内部に保持します。
