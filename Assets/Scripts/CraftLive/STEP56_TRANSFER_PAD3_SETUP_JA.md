# 大ステップ5〜6 転送・Pad3セットアップ手順

## 実装済みの範囲

### Pad1

- 配置確定した額縁を転送待ちへ保持
- 待機中の額縁を仮表示
- 1個発射と全件発射の切り替え
- ばねを下へ引き、離して発射する操作
- ばね、アーム、レール方向への斜方投射
- 発射時の短いカメラ移動と元位置への復帰

### Pad2

- 配置枠ごとに異なる方向から額縁が到着
- 額縁から素材3Dモデルへの変化
- 鉱石、宝石、お守り、ヒトダマごとの着地差
- 着地音と配置エフェクトのInspector接続
- 配置完了後の次素材転送
- 1回のばね操作で複数素材を順番に転送

### Pad3

- 下側のQR読み取り開始ボタン
- WebGL上で背面カメラを使ったQR読取
- 初回登録、登録済み、失敗、キャンセル表示
- 一度登録した素材のセッション中永久利用
- 攻撃力、防御力、回避率の3本の管
- 素材変更後の値アニメーション

## 状態データ

ステップ6時点ではRoomState V4へ更新した。その後ステップ8でV5へ更新済みで、
V1からV4までの保存データは現在のV5へ自動移行する。

- `transferQueue`: 転送待ち素材と配置先
- `transferBatchRemaining`: 一括転送の残り件数
- `displayedStats`: Pad3へ公開済みの3ステータス
- `statusDisplaySerial`: 管表示の更新番号

配置済みまたは転送予約済みの枠は再選択できない。

## 今すぐ必要なアタッチ

ない。Upgraderが以下を設定済み。

- Pad1: `CraftLivePad1TransferController`
- Pad2: `CraftLivePad2TransferReceiver`
- Pad3: `CraftLivePad3Controller`
- Pad3: `CraftLiveQrScanner`
- Pad3: 3個の`CraftLiveStatusTubeView`

3Dモデル、額縁、音、エフェクトが未設定でも仮Primitiveで動作する。

## Pad1単独の確認

1. `CraftLiveBootstrap.unity`を開く
2. `DefaultCraftLiveLaunchConfig.asset`を選択
3. `Editor Role`を`Material Pad`にする
4. `Use Firebase In Editor`をOFFにする
5. Playする
6. Hierarchyで`Pad1_MaterialGallery_Root`を選ぶ
7. `CraftLivePad1TransferController`のメニューを開く
8. `Debug/Queue First Available Material`を1〜3回実行する
9. 転送待ちの額縁が増えることを確認する
10. `1個発射`または`全部発射`を選ぶ
11. `ばねを下へ引いて離す`を下方向へドラッグする
12. 額縁がレール方向へ飛び、カメラが元位置へ戻ることを確認する

Pad2が同時接続されていないため、単独確認では発射後に到着待ちで停止する。
これは正常。Playを停止すれば初期状態へ戻る。

## Pad2単独の到着確認

1. Launch Configの`Editor Role`を`Workbench Pad`にする
2. Playする
3. 武器を選んで確定する
4. `Pad2_Workbench_Root`を選ぶ
5. `CraftLivePad2PlacementController`のメニューから
   `Debug/Select First Base Material`を実行する
6. 光っている基礎枠をタップする
7. `この場所に置く`を押して転送待ちへ追加する
8. `CraftLivePad2TransferReceiver`のメニューから
   `Debug/Start First Queued Arrival`を実行する
9. 額縁が現れ、素材へ変化し、選択枠へ入ることを確認する
10. 配置済みの枠が再選択できないことを確認する

スキルと属性もPlacement Controllerのデバッグ項目から確認できる。

## Pad3単独の確認

1. Launch Configの`Editor Role`を`Qr Pad`にする
2. Playする
3. `Pad3_StatusQr_Root`を選ぶ
4. `CraftLivePad3Controller`のメニューから
   `Debug/Register First QR Material`を実行する
5. 登録成功表示を確認する
6. 同じ項目を再実行し、登録済み表示になることを確認する
7. `Debug/Preview Tube Values`を実行する
8. 攻撃力75%、防御力50%、回避率30%相当まで管が動くことを確認する

Editorでは実カメラを開かず、WebGLで確認するよう案内を表示する。

## 4Pad連携確認

本番に近い確認では、同じWebGLビルドを4つのURLで開く。

```text
?screen=pad1&room=001
?screen=pad2&room=001
?screen=pad3&room=001
?screen=pad4&room=001
```

全端末で`room`を同じ値にする。WebGLではLaunch Configの
`Use Firebase In WebGL`をONにし、Firebase URLを設定する。

確認順:

1. Pad3でQR素材を登録
2. Pad1で素材を選ぶ
3. Pad2で配置枠を確定
4. Pad1の転送待ちに額縁が追加される
5. 必要な素材分だけ2〜4を繰り返す
6. Pad1で1個または全部を選び、ばねを引いて離す
7. Pad2へ順番に素材が到着する
8. 到着後にPad3の該当管が動く

## QRコードの形式

次の3形式を読み取れる。

```text
craftlive:material:materialId
{"materialId":"materialId"}
https://example.com/?material=materialId
```

`materialId`は対象の`CraftLiveMaterialDefinition`に設定されたIDと一致させる。

## iPadでQRを使う条件

- HTTPSでWebGLを配信する
- Safariのカメラ許可をONにする
- 読み取り開始は来場者のタップから実行する
- CDNへ接続できるネットワークを用意する
- 背面カメラを塞がない設置にする

現在のWebGLブリッジは`qr-scanner 1.4.2`をCDNから読み込む。
オフライン運用が必要な場合は、ステップ9でライブラリをローカル同梱する。

## Pad1 Transfer Controllerの設定

- `Fallback Ticket Prefab`
  - 素材固有の額縁がない場合の共通額縁
- `Create Fallback Visuals`
  - 独自台とばねを接続するまではON
- `Queue Columns`
  - 待機額縁の1段あたりの数
- `Queue Spacing`
  - 待機額縁同士の間隔
- `Required Pull Pixels`
  - 発射に必要な下方向ドラッグ量
- `Launch All By Default`
  - 初期状態を全件発射にするか
- `Pulled Arm Euler`
  - 引いたときのアーム角度
- `Compressed Spring Scale`
  - 引いたときのばね縮小率
- `Load Duration`
  - 額縁を発射台へ載せる時間
- `Launch Duration`
  - 斜方投射時間
- `Launch Arc Height`
  - 軌道の高さ
- `Camera Shift Duration`
  - レール確認用カメラ移動時間

独自UIから次を呼び出せる。

- `SetSingleMode`
- `SetAllMode`
- `ToggleLaunchMode`
- `LaunchSelectedMode`

## Pad2 Transfer Receiverの設定

- `Fallback Ticket Prefab`
  - 額縁未設定時の共通Prefab
- `Fallback Material Prefab`
  - 素材モデル未設定時の共通Prefab
- `Arrival Delay`
  - Pad1発射後からPad2表示までの待機
- `Arrival Duration`
  - 額縁到着と素材着地の合計時間
- `Arrival Arc Height`
  - 到着軌道の高さ
- `Completion Hold Seconds`
  - 配置完了表示を保持する時間
- `Publish Stats After Arrival`
  - 現在はON。ステップ7で液体完了から更新するときはOFFにする
- `Status Publish Delay`
  - 管を動かすまでの仮待機時間

素材定義側では次を設定できる。

- `World Prefab`
- `Transfer Ticket Prefab`
- `Placement Effect Prefab`
- `Landing Audio Clip`
- `Material Form`
- `Effect Color`

## Pad3の独自UI接続

`CraftLivePad3Controller`のイベント:

- `On Scanning Changed`
- `On Feedback Changed`
- `On Registered Material Changed`
- `On New Registration`

QR開始ボタンから`StartQrScan`、取消ボタンから`StopQrScan`を呼ぶ。
独自UI完成後は`Create Fallback Visuals`をOFFにする。

各`CraftLiveStatusTubeView`では次を接続する。

- `Liquid Fill`
- `Liquid Renderers`
- `Full Height`
- `Bottom Local Y`
- `Fill Width`
- `Animation Seconds`
- `Liquid Color`
- `On Value Changed`
- `On Normalized Changed`

独自管を設定したら`Create Fallback Visual`をOFFにする。

## 現在の安全確認

```text
Validation Errors: 0
EditMode Tests: 92 / 92 Passed
```

残るWarning 2件は、後から設定するIcon/Prefabとゲーム数値。
