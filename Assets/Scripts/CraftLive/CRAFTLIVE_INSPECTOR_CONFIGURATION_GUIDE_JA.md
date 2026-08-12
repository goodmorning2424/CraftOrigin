# Craft-live Inspector設定完全ガイド

更新日: 2026-07-31  
対象: Unity 6000.4.0f1 / URP / WebGL / iPad第9世代・縦画面

この文書は、Craft-liveで制作者側がInspectorから変更できる設定をまとめた操作マニュアルです。

対象範囲:

- 素材の名前、説明、3Dモデル、絵画、色、能力値
- 武器の名前、種類、3Dモデル、基礎ステータス
- 素材配置条件と合成ルール
- セッション時間と結果判定
- Pad1からPad4の表示、演出、操作感
- 3Dボタン、ステータス管、液体、ホログラム
- Firebase、Room ID、WebGL、iPad表示
- UnityEventを使った独自UIの接続

## 1. 最初に知っておくこと

### 1.1 設定値と参照の違い

Inspector項目には大きく2種類あります。

| 種類 | 例 | 基本方針 |
|---|---|---|
| 設定値 | 時間、色、確率、倍率 | 制作者側で変更してよい |
| 参照 | Session、Bindings、Camera | 自動設定済み。理由なく変更しない |

`Session`、`Bindings`、`Catalog`、`Rules`などの参照を`None`にすると、ゲーム進行が停止します。

### 1.2 Play中の変更

Play中に変更したInspector値は、Play終了時に元へ戻る場合があります。

確定した設定は必ずEdit Modeで入力し、`Ctrl + S`で保存してください。

### 1.3 IDを変更するタイミング

次のIDはQR、Firebase、保存データの照合に使用します。

- `Material Id`
- `Attribute Id`
- `Skill Id`
- `Weapon Id`

本番テスト開始後は変更しないでください。

IDを変更した場合は、Firebaseの古い`rooms`データとブラウザの保存状態を削除してから再試験します。

### 1.4 変更後の共通検証

設定変更後は、Unity上部メニューから次を実行します。

```text
Tools > Craft-live > Validate Current Project
Tools > Craft-live > Step 9 > Validate Production Readiness
```

本番前は`Errors: 0`を確認します。

---

# 2. 設定アセット一覧

主要設定は次のフォルダーにあります。

```text
Assets/CraftLiveData
```

| アセット | 内容 |
|---|---|
| `DefaultCraftLiveCatalog.asset` | 使用する素材と武器の一覧 |
| `DefaultCraftLiveRules.asset` | 時間、必須枠、合成、ランク、上限 |
| `DefaultCraftLiveLaunchConfig.asset` | Pad選択、Room ID、Firebase |
| `DefaultPad4Calibration.asset` | Pad4のプラ板とモデル表示補正 |
| `Materials/*.asset` | 各素材の情報 |
| `Weapons/*.asset` | 各武器の情報 |

---

# 3. 素材設定

## 3.1 設定場所

```text
Assets/CraftLiveData/Materials
```

設定する素材Definitionを選択します。

## 3.2 最新仕様で必要な素材

最新仕様では合計10種類です。

| 素材 | 推奨Material ID | Category |
|---|---|---|
| 攻撃鉱石 | `ore_attack` | Upgrade |
| 防御鉱石 | `ore_defense` | Upgrade |
| 回避鉱石 | `ore_evasion` | Upgrade |
| 炎 | `attribute_fire` | Attribute |
| 凍結 | `attribute_freeze` | Attribute |
| 雷 | `attribute_lightning` | Attribute |
| 幸運 | `skill_luck` | Skill |
| 2連撃 | `skill_double_strike` | Skill |
| 自動回復 | `skill_auto_heal` | Skill |
| 命の珠 | `skill_life_orb` | Skill |

注意: 現在のプロジェクトには旧仕様の素材Definitionが11個あります。最終設定前に、Catalogを最新10種類へ整理する必要があります。

## 3.3 Identity

### Material Id

素材をプログラム内部で識別するIDです。

用途:

- QRコード
- Firebase保存
- Pad間同期
- 配置枠
- 合成結果

設定規則:

- 半角英数字と`_`を使用
- 重複禁止
- 空白を入れない
- 本番開始後は変更しない

QRコード例:

```text
craftlive:material:ore_attack
```

### Display Name

来場者へ表示する素材名です。

表示場所:

- Pad1の絵画
- 素材説明ホログラム
- 合成結果

日本語を使用できます。

### Description

素材そのものの説明文です。

Pad1の説明板へ表示します。初見で理解できるよう、2から4行程度を推奨します。

### Category

素材カテゴリとPad2の配置可能枠を決めます。

| Category | 意味 | 配置可能枠 |
|---|---|---|
| `Upgrade` | 基礎ステータス素材 | 上・左・右・下の4枠 |
| `Attribute` | 炎・凍結・雷 | 右下枠 |
| `Skill` | 固有スキル | 左下枠 |

配置条件はCategoryから自動判定されます。

### Requires Qr Unlock

ONの場合、Pad3でQR登録するまでPad1で使用できません。

| 状況 | 推奨 |
|---|---|
| 制作中の単体テスト | OFF |
| QRを含む本番試験 | ON |
| 文化祭本番 | ON |

一度登録された素材は、そのセッション中に何度でも使えます。個数は減りません。

## 3.4 Presentation

### Icon

Pad1の絵画に表示するSpriteです。

推奨Import Settings:

```text
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Max Size: 1024以下
Compression: Normal Quality
```

3:4または正方形に近い画像を推奨します。

### World Prefab

素材の実物表現に使用する3D Prefabです。

使用場所:

- Pad1で絵画から飛び出す
- Pad2へ到着する
- Pad2の配置枠へ残る

Prefab推奨構成:

```text
PF_Material_OreAttack
└── Model
```

ルートTransform:

```text
Position: 0, 0, 0
Rotation: 0, 0, 0
Scale: 1, 1, 1
Static: OFF
```

見た目の向きと大きさは子`Model`で調整します。

### Transfer Ticket Prefab

Pad1からPad2へ飛ぶ額縁・絵画Prefabです。

空欄の場合は仮の板が表示されます。

現在は素材ごとにPrefabを設定できます。

選択肢:

- 全素材に同じ共通額縁Prefabを設定
- 素材の絵を組み込んだ額縁Prefabを素材ごとに設定

### Effect Color

素材のテーマカラーです。

使用場所:

- Pad2の液体
- 配置枠の発光補助
- Pad4の属性着色
- 各演出のUnityEvent

暗すぎる色は発光が分かりにくいため、ある程度明度を確保します。

### Placement Effect Prefab

Pad2で素材が枠へ到着したときに生成する演出Prefabです。

推奨:

```text
Particle System
Looping: OFF
Play On Awake: ON
Duration: 1～2秒
```

生成から5秒後に自動削除されます。

### Material Form

素材の着地表現を分類します。

| 値 | 想定表現 |
|---|---|
| `Generic` | 共通の着地 |
| `Ore` | 重く落下 |
| `Gem` | 軽く跳ねて発光 |
| `Charm` | 揺れながら収まる |
| `Spirit` | 浮遊して収まる |

### Landing Audio Clip

素材がPad2へ着地するときの音です。

推奨形式:

- 短い効果音
- 0.2から1.5秒程度
- ループなし
- WebGL容量を抑えるためPCMよりVorbisを推奨

### Ability Summary

能力の短い説明です。

例:

```text
攻撃力を15上昇させる
```

### Usage Summary

素材の使い方や配置先の説明です。

例:

```text
錬成台の4つの基礎枠に配置できる
```

## 3.5 Upgrade素材

Categoryを`Upgrade`にすると`Stat Modifiers`が表示されます。

### Attack Rate

武器の攻撃力へ加算する値です。

### Defense Rate

武器の防御力へ加算する値です。

### Evasion Rate

武器の回避率へ加算する値です。

初期テスト例:

| 素材 | Attack | Defense | Evasion |
|---|---:|---:|---:|
| 攻撃鉱石 | 15 | 0 | 0 |
| 防御鉱石 | 0 | 15 | 0 |
| 回避鉱石 | 0 | 0 | 10 |

同じ素材を複数の基礎枠へ配置すると、その回数分加算されます。

## 3.6 Attribute素材

Categoryを`Attribute`にすると属性設定が表示されます。

### Attribute Id

属性内部IDです。

推奨値:

```text
fire
freeze
lightning
```

### Attribute Display Name

画面に表示する名前です。

```text
炎
凍結
雷
```

### Element Effect / Type

属性種類を選びます。

| Type | 内容 |
|---|---|
| `Fire` | やけど継続ダメージ |
| `Freeze` | 行動制限 |
| `Lightning` | 周囲への範囲ダメージ |

### Activation Chance Percent

効果の発動確率です。

範囲:

```text
0～100
```

`25`は25%を示します。

### Effect Amount

属性の効果量です。

意味はTypeによって変わります。

- Fire: 継続ダメージ量
- Freeze: 行動制限の強さ
- Lightning: 追加範囲ダメージ量

### Duration Seconds

効果の継続時間です。

- Fire: やけど時間
- Freeze: 行動制限時間
- Lightning: 通常は0でよい

初期テスト例:

| 属性 | 確率 | 効果量 | 時間 |
|---|---:|---:|---:|
| 炎 | 25 | 10 | 3 |
| 凍結 | 20 | 1 | 2 |
| 雷 | 20 | 12 | 0 |

## 3.7 Skill素材

Categoryを`Skill`にすると固有スキル設定が表示されます。

### Skill Id

推奨値:

```text
luck
double_strike
auto_heal
life_orb
```

### Skill Display Name

来場者へ表示する名前です。

### Skill Description

固有スキルの詳細説明です。

### Skill Effect / Type

| Type | 内容 |
|---|---|
| `Luck` | クリティカル・取得補正 |
| `DoubleStrike` | 確率で追加攻撃 |
| `AutoHeal` | 一定時間ごとに回復 |
| `LifeOrb` | 自傷と引き換えに攻撃上昇 |

### Activation Chance Percent

スキルの発動確率です。

### Primary Value

スキルの主効果です。

| Type | Primary Value |
|---|---|
| Luck | 幸運・クリティカル補正 |
| DoubleStrike | 追加攻撃の威力割合 |
| AutoHeal | 1回の回復量 |
| LifeOrb | 攻撃上昇量 |

### Secondary Value

スキルの副効果です。

| Type | Secondary Value |
|---|---|
| Luck | アイテム取得補正 |
| DoubleStrike | 予約項目。通常0 |
| AutoHeal | 予約項目。通常0 |
| LifeOrb | 自傷ダメージ |

### Interval Seconds

時間間隔を使用するスキルの発動間隔です。

主にAutoHealで使います。

初期テスト例:

| Skill | 確率 | Primary | Secondary | Interval |
|---|---:|---:|---:|---:|
| 幸運 | 100 | 10 | 10 | 0 |
| 2連撃 | 25 | 50 | 0 | 0 |
| 自動回復 | 100 | 5 | 0 | 5 |
| 命の珠 | 100 | 20 | 5 | 0 |

---

# 4. 武器設定

## 4.1 設定場所

```text
Assets/CraftLiveData/Weapons
```

現在の初期武器:

- `IronSword.asset`
- `Rapier.asset`
- `ArcaneStaff.asset`

## 4.2 Weapon Id

武器内部IDです。

QRには使いませんが、Firebase、結果履歴、武器コードで使用します。

本番開始後は変更しません。

## 4.3 Display Name

Pad2、Pad4、結果画面へ表示する武器名です。

## 4.4 Weapon Type

| 値 | 意味 |
|---|---|
| `Sword` | 剣 |
| `Thrust` | 突き武器 |
| `Staff` | 杖 |

Pad2の分類表示と仮モデル形状に使います。

## 4.5 Base Stats

武器が素材なしで持つ基礎値です。

- Attack Rate
- Defense Rate
- Evasion Rate

最終ステータスは、武器基礎値＋素材補正＋合成結果補正で計算されます。

## 4.6 Icon

武器の画像です。結果UIなどに使用できます。

## 4.7 Workbench Prefab

Pad2で使用する武器3D Prefabです。

使用場所:

- 武器カルーセル
- 選択確定後の錬成台中央

## 4.8 Hologram Prefab

Pad4で表示する武器Prefabです。

空欄の場合は`Workbench Prefab`が自動使用されます。

最初は同じPrefabを使用して構いません。

## 4.9 Preview Scale

武器ごとの表示倍率補正です。

例:

```text
X: 1
Y: 1
Z: 1
```

武器だけ小さい場合は全軸を`1.2`などへ上げます。

非均等倍率は武器形状が歪むため、特別な理由がなければ3軸を同じ値にします。

---

# 5. Catalog設定

設定場所:

```text
Assets/CraftLiveData/DefaultCraftLiveCatalog.asset
```

## Materials

ゲームで使用する素材Definition一覧です。

最新仕様では`Size: 10`を推奨します。

確認事項:

- `None`がない
- 同じDefinitionが重複していない
- Material IDが重複していない
- 旧素材が残っていない

## Weapons

ゲームで選択できる武器Definition一覧です。

最低3種類:

- Sword
- Thrust
- Staff

配列の先頭は初期選択武器として使われます。

---

# 6. ゲームルール設定

設定場所:

```text
Assets/CraftLiveData/DefaultCraftLiveRules.asset
```

## 6.1 Session

### Session Duration Seconds

1ゲームの制限時間です。

初期値:

```text
300秒
```

5分を意味します。

## 6.2 Required Materials

### Require Attribute Slot

ONの場合、合成に右下の属性素材が必須です。

### Require Skill Slot

ONの場合、合成に左下の固有スキル素材が必須です。

### Require All Four Base Slots

ONの場合、4つの基礎素材枠をすべて埋めないと合成できません。

| 設定 | ゲーム性 |
|---|---|
| OFF | 少ない素材でもすぐ合成できる |
| ON | すべての基礎枠を考えて埋める必要がある |

## 6.3 Mixing

### Mixing Duration Seconds

旧回転合成方式との互換用時間です。現行のトンカチ方式では主に安全タイムアウトとして残ります。

### Power Per Radian

旧回転入力の加算量です。現行トンカチ方式では通常変更不要です。

### Required Hammer Passes

合成成功に必要なトンカチの往復回数です。

初期値:

```text
6
```

短い体験にするなら4から6、しっかり操作させるなら6から10を推奨します。

### Hammer Stroke Pixels

1往復として認識するために必要な指の移動距離です。

初期値:

```text
120
```

小さくすると軽いスワイプで進み、大きくすると長く動かす必要があります。

## 6.4 Results

### Maximum Completed Weapons

1セッションに保存できる完成武器数の上限です。

### Weapon Code Prefix

発行コードの先頭文字です。

例:

```text
CL
```

生成例:

```text
CL-XXXX-XXXX
```

## 6.5 Rank Thresholds

合成ランク判定に使う境界値です。

- Success Threshold
- Great Success Threshold
- Super Success Threshold

値は小さい順に設定します。

```text
Success < Great Success < Super Success
```

## 6.6 Rank Stat Bonuses

ランクに応じて最終ステータスへ加える値です。

- Success Bonus
- Great Success Bonus
- Super Success Bonus

### Maximum Stat

攻撃力、防御力、回避率の表示・計算上限です。

Pad3のガラス管では、この値を満タンとして正規化します。

初期値:

```text
100
```

---

# 7. 起動・Firebase設定

設定場所:

```text
Assets/CraftLiveData/DefaultCraftLiveLaunchConfig.asset
```

## 7.1 Editor Preview

### Editor Role

Unity EditorでPlayしたときに表示するPadです。

| 値 | 表示 |
|---|---|
| MaterialPad | Pad1 |
| WorkbenchPad | Pad2 |
| QrPad | Pad3 |
| HologramPad | Pad4 |

### Editor Room Id

Editor試験用Room IDです。

通常:

```text
001
```

## 7.2 Pad Scenes

- Pad1 Scene Name
- Pad2 Scene Name
- Pad3 Scene Name
- Pad4 Scene Name

現在のシーン名と対応しています。

```text
Pad1_MaterialGallery
Pad2_Workbench
Pad3_StatusQr
Pad4_Hologram
```

シーン名を変更しない限り触りません。

## 7.3 Firebase

### Use Firebase In Editor

EditorからFirebaseへ接続するかを決めます。

通常はOFFを推奨します。

### Use Firebase In Web Gl

WebGL本番でFirebase同期を使うかを決めます。

本番はONです。

### Firebase Database Url

Realtime DatabaseのルートURLです。

例:

```text
https://PROJECT-default-rtdb.asia-southeast1.firebasedatabase.app
```

URL末尾へ`rooms`や`.json`を付けません。

### Poll Interval Seconds

他Padの状態を確認する間隔です。

初期値:

```text
0.5秒
```

小さくすると反映が速くなりますが、通信回数が増えます。

### Request Timeout Seconds

通信1回を失敗と判断する時間です。

初期値:

```text
10秒
```

### Initial Retry Delay Seconds

通信失敗後、最初の再送まで待つ時間です。

初期値:

```text
0.75秒
```

### Maximum Retry Delay Seconds

連続失敗時の再送間隔上限です。

初期値:

```text
8秒
```

### Cache Pending State

未送信操作をiPad内へ一時保存します。

本番はONを推奨します。

## 7.4 CraftLiveRoomTransportの見かけ上の設定

Bootstrapシーンの`CraftLiveRoomTransport`にも、次の欄が表示される場合があります。

- Firebase Database Url
- Poll Interval Seconds
- Request Timeout Seconds
- Initial Retry Delay Seconds
- Maximum Retry Delay Seconds
- Cache Pending State

これらは通信処理が実際に使用する値ですが、通常の起動時には
`DefaultCraftLiveLaunchConfig.asset`の同名設定から自動的に上書きされます。

したがって、制作者が変更する場所は原則として
`DefaultCraftLiveLaunchConfig.asset`に統一してください。
シーン側だけを変更すると、Play開始時に元へ戻ったように見えるため注意が必要です。

### On Connection Status Changed

通信状態を文字列で受け取るUnityEventです。

独自UIの接続状況表示へ使えます。設定しなくても通信自体は動作します。

### On Online Changed

オンラインなら`true`、オフラインなら`false`を受け取るUnityEventです。

接続ランプ、警告パネル、操作制限などへ接続できます。

---

# 8. Pad4プラ板・表示補正

設定場所:

```text
Assets/CraftLiveData/DefaultPad4Calibration.asset
```

## 8.1 Physical Acrylic Plate

### Plate Width Millimeters

使用するプラ板の横幅です。

### Plate Height Millimeters

使用するプラ板の縦幅です。

### Plate Angle Degrees

iPad画面に対するプラ板の角度です。

一般的な反射式ホログラムでは45度前後から調整します。

### Distance From Ipad Millimeters

iPad画面からプラ板までの距離です。

これらは記録・現場調整用の値です。実際の見え方は物理配置とModel Displayも合わせて調整します。

## 8.2 Model Display

### Model Local Position

Pad4で表示する武器の位置補正です。

### Model Local Euler Angles

武器の初期回転です。

### Model Scale Multiplier

Pad4全体の武器表示倍率です。

武器個別の`Preview Scale`と掛け合わせて使用されます。

### Rotation Speed Degrees Per Second

完成武器が1秒間に回転する角度です。

```text
0: 停止
30: ゆっくり回転
60: 速め
```

---

# 9. 共通シーン設定

## 9.1 CraftLiveBootstrap

シーン:

```text
Assets/Scenes/CraftLive/CraftLiveBootstrap.unity
```

参照:

- Session
- Transport
- Launch Config
- Target Camera

すべて自動設定済みです。`None`にしません。

## 9.2 CraftLiveSession

### Catalog

使用するCatalogアセットです。

### Rules

使用するRulesアセットです。

### Room Id

Bootstrap実行時にURLまたはLaunch Configから上書きされます。通常は直接変更しません。

### Role

Bootstrap実行時にPad役割から設定されます。通常は`Auto`です。

### On Message Changed

ゲーム進行メッセージが変わったときに文字列を通知します。

自作UIの案内Textへ接続できます。

## 9.3 CraftLiveWebPresentation

### Target Camera

表示対象カメラです。

### Target Frame Rate

目標フレームレートです。

iPad第9世代では30fpsを推奨します。

### Target Aspect

表示比率です。

```text
X: 3
Y: 4
```

### Letterbox Camera

画面比率が違う場合、3:4を維持する余白を入れます。

本番はONです。

### Respect Safe Area

ブラウザUIや画面端を避けて表示します。

本番はONです。

### On Portrait Changed

縦画面・横画面が切り替わったときに`bool`を通知します。

### On Safe Area Changed

Safe Areaが変わったときに`Rect`を通知します。

## 9.4 CraftLiveRuntimeDiagnostics

### Stale Connection Seconds

最後の正常通信から何秒で「通信が古い」と判断するかを設定します。

初期値:

```text
15秒
```

### On Summary Changed

接続状態、Room、Pad、revisionを文字列で通知します。

管理者用Textへ接続できます。

### On Healthy Changed

通信状態が正常かを`bool`で通知します。

---

# 10. Pad Scene Rootとカメラ

各Padシーンの`CraftLivePadSceneRoot`で設定します。

## Role

シーンが担当するPadです。

シーンとRoleが一致しないとBootstrapがエラーを出します。

## Camera Anchor

Bootstrapの共通カメラを移動させる位置・回転です。

各Padの構図はこのTransformを移動して調整します。

## Orthographic

ONなら平行投影、OFFなら透視投影です。

## Orthographic Size

OrthographicがONのときの表示範囲です。

## Field Of View

OrthographicがOFFのときの画角です。

小さいほど望遠、大きいほど広角です。

## Background Color

カメラ背景色です。

---

# 11. Pad1設定

シーン:

```text
Assets/Scenes/CraftLive/Pad1_MaterialGallery.unity
```

## 11.1 CraftLivePad1Bindings

Bindingsは各機能の配置基準点です。

| 項目 | 意味 |
|---|---|
| Power Up Wall | 基礎素材の壁 |
| Skill Wall | 固有スキルの壁 |
| Type Wall | 属性素材の壁 |
| Material Preview Root | 飛び出す3D素材の基準点 |
| Hologram Info Root | 説明板の基準点 |
| Transfer Queue Root | 額縁の転送待機場所 |
| Spring Launcher Root | 発射装置の基準点 |
| Rail Camera Anchor | 発射時にカメラを寄せる位置 |
| Ui Root | Pad1 UIの親 |

独自モデルを配置するときは、対応するRootの子にします。

## 11.2 CraftLivePad1GalleryController

### Painting Prefab

独自の絵画Prefabです。

空欄の場合は自動生成の額縁を使います。

### Show Locked Materials

ONの場合、未登録素材も暗く表示します。

OFFの場合、QR登録済み素材だけ表示します。

### Apply Default Layout

3列の標準配置を自動適用します。

独自に壁位置を決める場合はOFFにします。

### Column Spacing

3列の横間隔です。

### Column Vertical Position

3列全体の高さです。

### Painting Spacing

同じ列に並ぶ絵画の縦間隔です。

### Visible Paintings

一度に見える絵画数の基準です。

### Drag Sensitivity

縦ドラッグに対する壁スクロール量です。

### Mouse Wheel Step

Editorでマウスホイールを使ったときの移動量です。

### Painting Size

仮絵画の幅と高さです。

### Frame Color

仮額縁の色です。

### Wall Color

仮壁の色です。

## 11.3 CraftLiveMaterialPaintingView

独自絵画Prefabへ付けるコンポーネントです。

### Moving Root

選択時に手前へ動かすTransformです。

通常はPrefabルートまたは絵画本体です。

### Tint Renderers

ロック・選択状態で色を変えるRenderer一覧です。

### Interaction Colliders

タップ判定に使うCollider一覧です。

### Fallback Name Text

名前を表示するTextMeshです。

### Fallback State Text

登録状態を表示するTextMeshです。

### Selected Offset

選択時に絵画を移動する距離です。

Z方向を手前へ設定します。

### Selected Scale

選択時の拡大率です。

### Locked Brightness

未登録素材の明るさです。

### Events

- On Icon Changed
- On Name Changed
- On Category Changed
- On Selected Changed
- On Locked Changed

独自CanvasやRendererへ状態を渡すために使います。

## 11.4 CraftLivePad1MaterialPreview

### Use Material World Prefab

ONなら素材Definitionの`World Prefab`を使います。

本番はONです。

### Create Placeholder When Missing

World Prefabが空の場合に仮Primitiveを出します。

制作中はON、本番確認後はOFFにできます。

### Target Model Size

飛び出した素材モデルを自動調整する目標サイズです。

### Model Rotation

素材モデルの表示回転です。

### Spin Degrees Per Second

1秒間の回転角度です。

### Reveal Duration

絵画から飛び出す時間です。

### Create Fallback Hologram

自動生成の説明板を表示します。

独自Canvasを完成させたらOFFにできます。

### Hologram Color

仮説明板の基本色です。

### UI Events

- On Details Visible
- On Icon Changed
- On Name Changed
- On Category Changed
- On Description Changed
- On Ability Changed
- On Usage Changed
- On Detail Text Changed
- On Theme Color Changed

独自説明UIへ接続します。

## 11.5 CraftLivePad1TransferController

### Fallback Ticket Prefab

素材Definitionに額縁Prefabがない場合の共通Prefabです。

### Create Fallback Visuals

仮のバネ、アーム、レール、ボタンを自動生成します。

独自発射装置を完全接続するまではONにします。

### Queue Columns

転送待ち額縁を並べる列数です。

### Queue Spacing

転送待ち額縁の間隔です。

### Required Pull Pixels

発射に必要なバネのドラッグ距離です。

### Launch All By Default

開始時に一括発射モードを選ぶかを決めます。

### Pulled Arm Euler

バネを引いたときのアーム回転です。

### Compressed Spring Scale

引いたときのバネ縮小率です。

### Load Duration

額縁を発射台へセットする時間です。

### Launch Duration

額縁が画面外へ飛ぶ時間です。

### Launch Arc Height

斜方投射の弧の高さです。

### Camera Shift Duration

レールを見るためにカメラが移動する時間です。

### Events

- On Queue Count Changed
- On Launch All Mode Changed
- On Pull Changed
- On Loading Started
- On Launched

独自UI、音、Animatorへ接続できます。

---

# 12. Pad2設定

シーン:

```text
Assets/Scenes/CraftLive/Pad2_Workbench.unity
```

## 12.1 CraftLivePad2Bindings

| 項目 | 意味 |
|---|---|
| Weapon Carousel Root | 武器選択表示の親 |
| Center Weapon Root | 確定武器の表示位置 |
| Hammer Root | トンカチとガイド |
| Upper Left Slot | 基礎枠1 |
| Middle Left Slot | 基礎枠2 |
| Upper Right Slot | 基礎枠3 |
| Middle Right Slot | 基礎枠4 |
| Lower Left Skill Slot | 固有スキル枠 |
| Lower Right Attribute Slot | 属性枠 |
| Transfer Arrival Root | 素材出現位置 |
| Liquid Flow Root | 液体の到達点 |
| Result Hologram Root | 結果説明板 |
| Ui Root | Pad2 UIの親 |

## 12.2 CraftLivePad2WeaponCarousel

### Create Fallback Visuals

仮カルーセルとボタンを自動生成します。

独自UI完成後にOFFにします。

### Swipe Threshold Pixels

次の武器へ切り替えるための横スワイプ距離です。

### Card Spacing

武器カード間の距離です。

### Neighbor Scale

左右に見える未選択武器の倍率です。

### Selected Model Size

中央選択中モデルの目標サイズです。

### Center Model Size

錬成台中央へ確定表示する武器サイズです。

### Card Color

仮ホログラムカードの基本色です。

### UI Events

- On Selection Visible
- On Weapon Name Changed
- On Weapon Type Changed
- On Attack Changed
- On Defense Changed
- On Evasion Changed
- On Weapon Confirmed

## 12.3 CraftLivePad2PlacementController

### Fallback Material Preview Prefab

World Prefabがない素材の仮表示です。

### Create Fallback Slots

6枠の仮モデルを自動生成します。

独自枠へ`CraftLivePlacementSlotView`を付けた後にOFFにできます。

### Apply Reference Layout

参考画像に合わせた標準6枠配置を適用します。

独自位置を使う場合はOFFです。

### Slot Diameter

仮枠の直径です。

### Base Slot Color

4つの基礎枠の色です。

### Skill Slot Color

左下スキル枠の色です。

### Attribute Slot Color

右下属性枠の色です。

### Create Fallback Controls

確認、変更、キャンセルの仮ボタンを生成します。

### UI Events

- On Instruction Changed
- On Confirm Visible
- On Change Visible
- On Cancel Visible
- On Candidate Slot Changed

## 12.4 CraftLivePlacementSlotView

独自配置枠へ付けます。

### Slot

その枠の論理役割です。

| 物理位置 | Slot |
|---|---|
| 左上 | Top |
| 左中 | Left |
| 右上 | Right |
| 右中 | Bottom |
| 左下 | Skill |
| 右下 | Attribute |

### Preview Anchor

仮配置・確定素材を表示する位置です。

### Highlight Renderers

選択可能時に発光させるRendererです。

### Idle Color

操作できない通常色です。

### Available Color

選択可能なときの色です。

### Selected Color

仮選択中の色です。

### Available Emission

選択可能時の発光強度です。

### Selected Emission

仮選択中の発光強度です。

### Fallback Preview Prefab

素材World Prefabがない場合の仮モデルです。

### Require Confirmed Weapon

武器確定前に枠操作を禁止するかを決めます。

通常はONを推奨します。

### Events

- On Available Changed
- On Selected Changed

## 12.5 CraftLivePad2TransferReceiver

### Fallback Ticket Prefab

額縁Prefabがない場合の代替です。

### Fallback Material Prefab

素材World Prefabがない場合の代替です。

### Arrival Delay

Pad1発射後、Pad2で到着演出を始めるまでの待ち時間です。

### Arrival Duration

素材が上から枠へ収まる時間です。

### Arrival Arc Height

到着軌道の高さです。

### Completion Hold Seconds

配置完了状態を見せる時間です。

### Publish Stats After Arrival

到着直後にPad3へステータスを公開する設定です。

現在は液体終了時に公開する構成のため、シーンではOFFが推奨です。

### Status Publish Delay

到着後に公開する場合の待ち時間です。

### Events

- On Arrival Started
- On Theme Color Changed
- On Placement Completed

## 12.6 CraftLiveLiquidFlowController

### Liquid Drop Prefab

流れる液体の1粒に使うPrefabです。

空欄ならSphereを生成します。

### Drop Count

一度に流す液体粒数です。

増やすと滑らかになりますが、WebGL負荷が上がります。

### Flow Duration

1粒が中央へ到達する時間です。

### Drop Spacing Seconds

粒同士の発射間隔です。

### Wave Amount

液体経路の波打ち量です。

### Events

- On Flow Started: テーマカラーを通知
- On Flow Progress: 0から1の進行度
- On Flow Completed: 流れ終了

## 12.7 CraftLiveHammerSynthesisController

### Hammer Prefab

合成時に表示するトンカチPrefabです。

### Create Fallback Visuals

仮トンカチ、レール、合成ボタンを生成します。

### Rail Half Width

ガイドレールの中心から片側までの長さです。

### Stroke Pixels Override

0より大きい場合、Rulesの`Hammer Stroke Pixels`を上書きします。

全体ルールを使う場合は0のままにします。

### Events

- On Hammer Visible
- On Pass Count Changed
- On Passes Remaining Changed
- On Rail Progress
- On Hammer Strike
- On Start Rejected

Animator、音、UI進捗へ接続できます。

## 12.8 CraftLivePad2ResultController

### Create Fallback Visuals

結果表示、履歴、次の武器ボタンを仮生成します。

### Events

- On Result Visible
- On Weapon Name Changed
- On Rank Changed
- On Attack Changed
- On Defense Changed
- On Evasion Changed
- On Attribute Changed
- On Skill Changed
- On History Count Changed
- On Weapon Code Changed

独自結果ホログラムUIへ接続します。

---

# 13. Pad3設定

シーン:

```text
Assets/Scenes/CraftLive/Pad3_StatusQr.unity
```

## 13.1 CraftLivePad3Bindings

| 項目 | 意味 |
|---|---|
| Attack Tube Root | 攻撃力管 |
| Defense Tube Root | 防御力管 |
| Evasion Tube Root | 回避率管 |
| Qr Read Button Root | QR開始ボタン |
| Qr Feedback Root | QR結果メッセージ |
| Ui Root | Pad3 UIの親 |

## 13.2 CraftLiveStatusTubeView

3本の管それぞれへ付けます。

### Stat Type

管が表示する値です。

- Attack Rate
- Defense Rate
- Evasion Rate

### Liquid Fill

上下へ伸縮させる液体Transformです。

### Liquid Renderers

液体色を適用するRenderer一覧です。

### Full Height

100%時の液体高さです。

### Bottom Local Y

液体の底位置です。

値を変えても液体の底が動かないようにする基準です。

### Fill Width

液体の太さです。

### Animation Seconds

現在値から新しい値へ動く時間です。

### Liquid Color

管ごとの液体色です。

### Create Fallback Visual

独自管が未設定の場合に仮管を生成します。

### Events

- On Value Changed: 実数値
- On Normalized Changed: 0から1の満タン割合

## 13.3 CraftLivePad3Controller

### Create Fallback Visuals

仮QRボタン、説明、管表示を生成します。

### Attack Color

攻撃力管の色です。

### Defense Color

防御力管の色です。

### Evasion Color

回避率管の色です。

### Events

- On Qr Button Enabled
- On Feedback Changed
- On Instruction Changed
- On Scanning Changed

## 13.4 CraftLiveQrScanner

### Timeout Seconds

QR読み取りを待つ最大時間です。

初期値:

```text
12秒
```

### Callback Cooldown Seconds

連続した二重コールバックを防ぐ時間です。

### On Scan Error

読み取り失敗メッセージを通知します。

### On Scan Cancelled

キャンセルを通知します。

QRカメラはHTTPS WebGLでのみ本番確認できます。

---

# 14. Pad4設定

シーン:

```text
Assets/Scenes/CraftLive/Pad4_Hologram.unity
```

## 14.1 CraftLivePad4Bindings

| 項目 | 意味 |
|---|---|
| Weapon Display Root | 武器モデル生成位置 |
| Effect Root | ホログラム演出の親 |
| Ui Root | 武器名・コードUIの親 |
| Calibration | Pad4補正アセット |

## 14.2 CraftLiveHologramView

### Spawn Root

完成武器を生成するTransformです。

### Fallback Prefab

武器Prefabがない場合の共通代替です。

### Calibration

Pad4補正アセットです。

### Rotate

完成武器を回転させるかを設定します。

### Rotation Speed

Calibrationがない場合に使う回転速度です。

通常はCalibration側の速度が優先されます。

### Apply Attribute Color

完成武器を選択属性のテーマカラーで着色します。

元マテリアルをそのまま見せたい場合はOFFにします。

### Emission Strength

属性着色時の発光強度です。

WebGLで白飛びする場合は下げます。

## 14.3 CraftLivePad4Controller

### Create Fallback Text

武器名とコードの仮TextMeshを生成します。

独自UI完成後にOFFにできます。

### Events

- On Weapon Name Changed
- On Weapon Code Changed
- On Final Weapon Selected

---

# 15. セッションタイマー

Bootstrapの`CraftLiveSessionTimerController`で設定します。

### Create Fallback Text

仮タイマーを自動生成します。

独自UI完成後にOFFにします。

### Refresh Interval

タイマー表示の更新間隔です。

初期値:

```text
0.2秒
```

### Events

- On Remaining Seconds
- On Timer Text Changed
- On Phase Changed

---

# 16. 3Dボタン設定

独自3Dボタンへ`CraftLiveWorldButton`を追加します。

必要条件:

- Collider
- EventSystem
- CameraのPhysicsRaycaster

## Press Target

押したときに動かすTransformです。

ボタン全体ではなく、押し込む天板だけを指定できます。

## Renderers

状態色を適用するRenderer一覧です。

## Colors

- Normal Color
- Hover Color
- Pressed Color
- Selected Color
- Disabled Color

## Press Depth

押し込む距離です。

## Animation Duration

押し込み・復帰アニメーション時間です。

## Cooldown Seconds

連打防止時間です。

## Interactable

操作可能かを設定します。

## Selected

選択状態として表示するかを設定します。

## Audio Source / Press Clip

ボタン押下音です。

## On Pressed

実際に実行するメソッドを接続します。

例:

```text
CraftLiveQrScanner.StartScan
CraftLivePad2PlacementController.ConfirmCandidate
CraftLiveHammerSynthesisController.StartSynthesis
```

---

# 17. UnityEventの接続方法

1. 対象コンポーネントをInspectorで開く。
2. Event欄の`+`を押す。
3. 呼び出したいGameObjectをドラッグする。
4. 関数一覧から対象メソッドを選ぶ。

値付きEventの例:

| Event型 | 接続先例 |
|---|---|
| `string` | TMP_Textへ中継する独自スクリプト |
| `float` | Slider、ゲージ制御 |
| `bool` | GameObject表示、Animator |
| `Color` | Image、Renderer色変更 |
| `Sprite` | UI Image |

TextMeshProへ文字列を直接入れにくい場合は、短い中継コンポーネントを作って接続します。

---

# 18. モデルImport Settings

FBXなどのモデルを選択して設定します。

## Model

### Scale Factor

元ソフトとの単位差を調整します。

最終的にPrefabルートを`1,1,1`へできる値を推奨します。

### Read/Write

通常はOFFで構いません。メッシュをCPUで変更する場合だけONにします。

WebGLではOFFのほうがメモリを節約できます。

### Optimize Mesh

通常はONを推奨します。

### Import BlendShapes

変形を使わない素材・武器ではOFFにできます。

## Materials

URP対応Shaderを使用します。

推奨:

```text
Universal Render Pipeline/Lit
Universal Render Pipeline/Simple Lit
```

透明素材はiPad負荷が高いため、必要な部分だけに使います。

## Animation

アニメーションを含まないモデルは`Import Animation`をOFFにできます。

## Collider

表示専用素材・武器の複雑なMesh Colliderは不要です。

タップ対象には単純なBox Colliderを使用します。

## Static

次はStaticにしません。

- 素材
- 武器
- 額縁
- トンカチ
- 液体
- エフェクト

壁、床、動かない作業台だけStaticにできます。

---

# 19. WebGL本番設定

次のメニューで推奨設定を自動適用します。

```text
Tools > Craft-live > Step 9 > Apply WebGL Production Settings
```

適用内容:

- 768×1024
- 3:4縦画面
- Run In Background
- Brotli圧縮
- Decompression Fallback
- Data Caching
- CraftLive専用テンプレート

ビルド:

```text
Tools > Craft-live > Step 9 > Build WebGL
```

出力先:

```text
Builds/CraftLiveWebGL
```

---

# 20. 制作者が通常触らない項目

次は自動設定されたシステム参照です。

- CraftLiveSession参照
- Bindings参照
- Bootstrap参照
- Transport参照
- Camera参照
- Pad Scene RootのRole
- Launch ConfigのScene Name
- Build Settingsのシーン順序

参照が`None`になった場合は、手動で推測せずValidatorを実行して確認します。

---

# 21. 旧実装の設定

次のスクリプトは旧シーンまたは互換用で、現在の4Pad本番構成では新規設定しません。

- `CraftLiveRoleRouter`
- `CraftLiveWorkbenchView`
- `CraftLiveTransferLauncherView`
- `CraftLiveMixInput`
- `CraftLiveMaterialBoardView`
- `CraftLiveMaterialTicketView`

旧シーン:

```text
Assets/Scenes/Craft.unity
```

本番Build Settingsでは無効です。

旧実装クリーンアップが完了するまで、手動削除はしないでください。

---

# 22. 推奨作業順

1. 最新10素材の名前とIDを確定する
2. 素材DefinitionのCategoryと数値を設定する
3. 武器Definitionの種類と基礎値を設定する
4. Catalogを素材10個・武器一覧へ整理する
5. Rulesで時間と必須枠を設定する
6. Requires Qr UnlockをOFFにして仮モデル通し試験をする
7. IconとWorld Prefabを設定する
8. Pad1からPad4の表示位置と大きさを調整する
9. 独自UIをUnityEventへ接続する
10. Firebase URLを設定する
11. Requires Qr UnlockをONにする
12. WebGLを再ビルドする
13. HTTPS上で4Pad・QR通し試験をする

---

# 23. 設定後のテスト項目

## 素材

- 全素材のMaterial IDが重複していない
- 全素材がCatalogに1回ずつ登録されている
- Iconが正しい
- World Prefabが正しい
- Categoryと配置枠が一致する
- テーマカラーが液体へ反映される

## 武器

- Sword、Thrust、Staffが選べる
- Workbench PrefabがPad2に表示される
- Hologram PrefabがPad4に表示される
- Preview Scaleが適切

## Pad間

- Pad3のQR登録がPad1へ反映される
- Pad1選択がPad2へ反映される
- 配置確定がPad1転送待ちへ反映される
- Pad2到着後にPad3の管が更新される
- 合成後にPad4が更新される

## セッション

- 制限時間が全Padで一致する
- 時間終了後に最終武器を選べる
- 武器コードが表示される

---

# 24. レポート

設定確認レポート:

```text
Library/CraftLiveReports/CurrentValidation_latest.md
Library/CraftLiveReports/ProductionReadiness_latest.md
Library/CraftLiveReports/EditModeTests_latest.md
Library/CraftLiveReports/WebGLBuild_latest.md
```

本番条件:

```text
Current Validation: Errors 0
Production Readiness: Errors 0
EditMode Tests: Failed 0
WebGL Build: Succeeded
```
