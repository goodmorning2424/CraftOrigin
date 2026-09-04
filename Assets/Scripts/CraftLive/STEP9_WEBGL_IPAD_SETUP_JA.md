# Craft-live Step 9: WebGL / iPad 本番セットアップ

更新日: 2026-07-30

## Step 9で実装したもの

- iPad縦画面向けの3:4表示とSafe Area補正
- 30fps固定、スリープ防止、バックグラウンド復帰対応
- Firebase初回同期待ち
- 通信失敗時の指数バックオフ再送
- Firebase ETagによるPad間の同時更新競合検出
- 未送信RoomStateの端末内保存と再読み込み後の復旧
- 通信状態・保留送信・Room ID・revisionの診断情報
- 3Dボタンとトンカチ操作のマルチタッチ重複防止
- QR読み取りの二重コールバック防止
- HTTPS、カメラ権限、明示キャンセルを考慮したQR画面
- iPad向けWebGLテンプレート
- WebGL本番設定、検証、ビルドを行うUnityメニュー

RoomStateはV5のままです。既存の保存形式とゲーム進行仕様は変更していません。

## 1. 最初にUnityで確認する

1. Unityでプロジェクトを開く。
2. Consoleに赤いエラーがないことを確認する。
3. Projectウィンドウで
   `Assets/CraftLiveData/DefaultCraftLiveLaunchConfig.asset`
   を選択する。
4. Inspectorで次を確認する。

| 項目 | 推奨値 | 意味 |
|---|---:|---|
| Use Firebase In Editor | OFF | Editor単体試験では端末内同期を使う |
| Use Firebase In Web Gl | ON | WebGL本番でFirebaseを使う |
| Firebase Database Url | 実際のURL | Realtime DatabaseのルートURL |
| Poll Interval Seconds | 0.5 | 他Padの状態を確認する間隔 |
| Request Timeout Seconds | 10 | 1回の通信を諦める時間 |
| Initial Retry Delay Seconds | 0.75 | 再送開始までの待ち時間 |
| Maximum Retry Delay Seconds | 8 | 再送間隔の上限 |
| Cache Pending State | ON | 未送信操作を端末内へ保留する |

5. 上部メニューから
   `Tools > Craft-live > Step 9 > Validate Production Readiness`
   を実行する。
6. Consoleへ
   `Craft-live production validation passed`
   と出ることを確認する。

## 2. Firebaseを準備する

この実装はFirebase Realtime DatabaseのREST APIを使用します。

1. Firebase ConsoleでRealtime Databaseを作成する。
2. Database URLをコピーする。
3. `DefaultCraftLiveLaunchConfig.asset`の
   `Firebase Database Url`へ入力する。
4. 本番ルールで`rooms`以下を4台のiPadから読み書きできるようにする。
5. 認証なしルールを使う場合は、文化祭期間だけに限定し、終了後に閉じる。
6. Firebase Consoleで不要な古い`rooms`を本番前に削除する。

4台は同じ`room`値を使います。別グループを同時進行させる場合は、
`001`、`002`のようにRoom IDを分けます。

## 3. WebGLをビルドする

1. 上部メニューから
   `Tools > Craft-live > Step 9 > Build WebGL`
   を実行する。
2. 初回はIL2CPPとシェーダーのコンパイルで時間がかかるため待つ。
3. 完了後、次のフォルダーを確認する。

```text
Builds/CraftLiveWebGL
```

4. 次の6ファイルが生成されていることを確認する。

```text
index.html
TemplateData/style.css
Build/CraftLiveWebGL.loader.js
Build/CraftLiveWebGL.data.unityweb
Build/CraftLiveWebGL.framework.js.unityweb
Build/CraftLiveWebGL.wasm.unityweb
```

現在の検証済みビルドは13.24 MiBです。

## 4. Webサーバーへ配置する

`index.html`をダブルクリックして直接開く方法は使用できません。
QRカメラも使用するため、本番はHTTPS配信が必須です。

1. `Builds/CraftLiveWebGL`の中身を、その構造のままWebサーバーへ配置する。
2. `.unityweb`ファイルを変更せず配信する。
3. `index.html`へHTTPSでアクセスする。
4. 404が出た場合は`Build`と`TemplateData`の配置を確認する。
5. 圧縮ヘッダーを設定できないサーバーでも動くよう、
   Decompression Fallbackは有効済み。

## 5. 4台のiPadで開くURL

同じ配信URLとRoom IDを使い、`screen`だけ変更します。

```text
https://配信先/index.html?screen=pad1&room=001
https://配信先/index.html?screen=pad2&room=001
https://配信先/index.html?screen=pad3&room=001
https://配信先/index.html?screen=pad4&room=001
```

- Pad1: 素材絵画の壁、素材選択、転送発射
- Pad2: 武器選択、6枠配置、液体、トンカチ合成、結果
- Pad3: 攻撃力・防御力・回避率の管、QR読み取り
- Pad4: 完成武器の大型ホログラム表示

## 6. iPad本体の設定

1. iPadを縦向きに固定する。
2. Safariで対象URLを開く。
3. Pad3でカメラ許可を求められたら`許可`を選ぶ。
4. Safariのサイト設定でカメラが`許可`になっていることを確認する。
5. 自動ロックを文化祭運用時間に合わせて無効化する。
6. 低電力モードをOFFにする。
7. 4台を同じ安定したWi-Fiへ接続する。
8. Guided Accessを使う場合は、QRカメラが開けることを事前確認する。

## 7. QRコードの値

最も単純な形式は次です。

```text
craftlive:material:素材ID
```

例:

```text
craftlive:material:ore_attack
```

次のURL形式も使用できます。

```text
https://配信先/read?materialId=ore_attack
```

素材IDは各`CraftLiveMaterialDefinition`の`Material Id`と完全に合わせます。

## 8. モデル完成後に行うアタッチ

### 素材

1. `Assets/CraftLiveData`内の素材Definitionを選択する。
2. `Icon`へ絵画に表示する画像を設定する。
3. `Preview Prefab`または素材表示用Prefab欄へ3DモデルPrefabを設定する。
4. `Theme Color`を設定する。
5. Categoryと配置可能枠を変更しない。

### 武器

1. 武器Definitionを選択する。
2. Pad2選択表示用Prefabを設定する。
3. Pad4完成表示用Prefabを設定する。
4. モデルの原点、向き、スケールをPrefab側で整える。
5. Pad4は`DefaultPad4Calibration.asset`で表示位置と倍率を調整する。

モデル未設定でもPrimitiveの仮表示で進行確認できます。

## 9. UIを自作した後の接続

既存の各Controllerを削除せず、自作ボタンのUnityEventから公開メソッドを呼びます。

- Pad1個別転送: `CraftLivePad1TransferController`
- Pad1一括転送: `CraftLivePad1TransferController`
- Pad2配置確定: `CraftLivePad2PlacementController.ConfirmCandidate`
- Pad2配置変更: `CraftLivePad2PlacementController.ChangeCandidate`
- Pad2キャンセル: `CraftLivePad2PlacementController.CancelPlacement`
- Pad2合成開始: `CraftLiveHammerSynthesisController.StartSynthesis`
- Pad3読み取り開始: `CraftLiveQrScanner.StartScan`

仮UIを消す場合は、各Controllerの`Create Fallback...`をOFFにします。
Controller、Bindings、Session、Transportは削除しません。

## 10. 必須の実機試験

1. 4台を同じRoom IDで開く。
2. Pad3で未登録素材のQRを読む。
3. Pad1へ絵画が追加されることを確認する。
4. Pad1で素材を選ぶ。
5. Pad2で許可された枠だけが選べることを確認する。
6. 配置確定後、Pad1の転送待ちへ追加されることを確認する。
7. 個別発射と複数一括発射を両方試す。
8. Pad2到着後、Pad3の3本の管が素材ごとに更新されることを確認する。
9. 合成し、Pad2結果とPad4武器が同時更新されることを確認する。
10. 制限時間終了後、最終武器を選びコードが出ることを確認する。

### 通信復旧試験

1. 操作途中で1台だけWi-FiをOFFにする。
2. その端末で可能な操作を1回行う。
3. Wi-FiをONに戻す。
4. 数秒後にFirebaseへ再同期されることを確認する。
5. 同時に2台を連打し、片方が古い状態を上書きしないことを確認する。

## 11. 本番当日の確認

- 4台とも充電または給電されている
- 4台とも同じWi-Fi
- 4台とも同じRoom ID
- Pad1からPad4まで正しいURL
- Pad3のSafariカメラ許可済み
- Firebaseの読み書き確認済み
- QRコード全種類の読み取り確認済み
- 1ゲーム通し試験済み
- Wi-Fi切断復旧試験済み
- iPadの自動ロック無効
- 配信先HTTPS証明書が有効

## 12. 検証レポート

Unityが生成するレポート:

```text
Library/CraftLiveReports/CurrentValidation_latest.md
Library/CraftLiveReports/ProductionReadiness_latest.md
Library/CraftLiveReports/EditModeTests_latest.md
Library/CraftLiveReports/WebGLBuild_latest.md
```

本番前はErrorsが0であることを確認します。
Icon、Prefab、ゲーム数値の未設定は警告として残ります。
