# ステップ2 Unityセットアップ

## ステップ2で作られるもの

Unityへ戻ると、リクエストを検出して次の処理が順番に実行される。

1. `DefaultCraftLiveLaunchConfig.asset`を作成
2. BootstrapとPad1からPad4の5シーンを作成
3. Build Settingsを5シーン構成へ更新
4. 現在のプロジェクト検証を実行
5. 全EditModeテストを実行

生成器は同名シーンがすでにある場合、そのシーンを上書きしない。

## 最初に行うこと

1. Unity Editorのウィンドウをクリックして前面にする
2. 右下のコンパイル表示が消えるまで待つ
3. Consoleで赤いエラーがないことを確認する
4. Projectウィンドウで`Assets/Scenes/CraftLive`を開く
5. 5つのシーンが生成されていることを確認する

今回、生成開始時に開かれていた未保存UntitledにはUnity標準のMain Cameraと
Directional Lightが含まれていたため、念のため
`PreStep2_Untitled_Backup.unity`へ退避している。この退避シーンは
Build Settingsで無効なのでゲームへ影響しない。

自動生成されなかった場合は、Unity上部メニューから次を実行する。

```text
Tools > Craft-live > Step 2 > Create Or Update Four-Pad Skeleton
```

続いて次を実行する。

```text
Tools > Craft-live > Validate Current Project
Tools > Craft-live > Run EditMode Tests
```

レポートは次に保存される。

```text
Library/CraftLiveReports/CurrentValidation_latest.md
Library/CraftLiveReports/EditModeTests_latest.md
```

## Build Settingsの確認

`File > Build Profiles`またはBuild Settings相当の画面で、次の順序を確認する。

1. `CraftLiveBootstrap` 有効、Build Index 0
2. `Pad1_MaterialGallery` 有効
3. `Pad2_Workbench` 有効
4. `Pad3_StatusQr` 有効
5. `Pad4_Hologram` 有効

旧`Craft.unity`は削除されず、無効状態で後ろに残る。手動で削除しない。

## 起動設定

`Assets/CraftLiveData/DefaultCraftLiveLaunchConfig.asset`を選択する。

- `Editor Role`
  - Unity EditorでPlayしたときに表示するPad
- `Editor Room Id`
  - Editor内の確認用room ID。通常は`001`
- `Pad Scene Names`
  - 原則として生成された初期値を変更しない
- `Use Firebase In Editor`
  - 1台のEditor確認ではOFF
  - 複数端末との接続確認時だけON
- `Use Firebase In Web Gl`
  - 本番の複数iPadではON
- `Firebase Database Url`
  - 実際に使用するRealtime Database URLへ変更する
- `Poll Interval Seconds`
  - 初期値0.5秒。端末負荷と同期速度を見て調整する
- `Request Timeout Seconds`
  - 初期値10秒

本番前にFirebaseのURLとセキュリティルールを確定する必要がある。

## シーンごとのアセット接続先

各Padシーンを単独で開き、ルートに付いているBindingsを確認する。
完成済みモデルやUIは、対応する空GameObjectの子として配置する。
Bindingsの参照自体は生成時に設定済みなので、通常は変更しない。

### Pad1

`Pad1_MaterialGallery_Root > CraftLivePad1Bindings`

- `Power Up Wall`: 基礎ステータス素材の絵画壁
- `Skill Wall`: 固有スキル素材の絵画壁
- `Type Wall`: 属性素材の絵画壁
- `Material Preview Root`: 飛び出す素材3Dモデル
- `Hologram Info Root`: 素材説明板
- `Transfer Queue Root`: 額縁の転送待機位置
- `Spring Launcher Root`: ばね・発射機構
- `Rail Camera Anchor`: レール表示時のカメラ位置
- `UI Root`: Pad1用UI

### Pad2

`Pad2_Workbench_Root > CraftLivePad2Bindings`

- `Weapon Carousel Root`: 武器選択用モデル群
- `Center Weapon Root`: 選択中・合成中の武器
- `Hammer Root`: トンカチ
- `Upper Left Slot`: 左上の基礎素材枠
- `Middle Left Slot`: 左中の基礎素材枠
- `Upper Right Slot`: 右上の基礎素材枠
- `Middle Right Slot`: 右中の基礎素材枠
- `Lower Left Skill Slot`: 左下の固有スキル枠
- `Lower Right Attribute Slot`: 右下の属性枠
- `Transfer Arrival Root`: 素材が上から到着する開始位置
- `Liquid Flow Root`: 中央へ流れる液体
- `Result Hologram Root`: 完成後の詳細表示
- `UI Root`: Pad2用UIと合成ボタン

### Pad3

`Pad3_StatusQr_Root > CraftLivePad3Bindings`

- `Attack Tube Root`: 攻撃力管
- `Defense Tube Root`: 防御力管
- `Evasion Tube Root`: 回避率管
- `Qr Read Button Root`: 下側の読み取り開始ボタン
- `Qr Feedback Root`: 読み取り結果表示
- `UI Root`: Pad3用UI

### Pad4

`Pad4_Hologram_Root > CraftLivePad4Bindings`

- `Weapon Display Root`: 完成武器モデル
- `Effect Root`: ホログラム用効果
- `UI Root`: 必要最小限の表示
- `Calibration`: Pad4表示補正アセット

## カメラ設定

各Padルートの`CraftLivePadSceneRoot`で設定する。

- `Camera Anchor`: 共有カメラの位置と回転
- `Orthographic`: 平行投影を使う場合ON
- `Orthographic Size`: 平行投影時の大きさ
- `Field Of View`: 透視投影時の画角
- `Background Color`: Padごとの背景色

Padシーン内へ別のMain Cameraを追加しない。Bootstrapの共有カメラが
選択PadのCamera Anchorへ移動する。

## Editorでの動作確認

1. `CraftLiveBootstrap.unity`を開く
2. Launch Configの`Editor Role`を確認したいPadへ変更する
3. Playする
4. Hierarchyに選択Padシーンが追加ロードされることを確認する
5. Main CameraがPadのCamera Anchorへ移動することを確認する
6. Playを停止する
7. Editor Roleを変え、Pad1からPad4を順番に確認する

## WebGLでの起動URL

同じroom IDを全iPadで使用する。

```text
https://設置先/index.html?screen=pad1&room=001
https://設置先/index.html?screen=pad2&room=001
https://設置先/index.html?screen=pad3&room=001
https://設置先/index.html?screen=pad4&room=001
```

`screen=1`から`screen=4`も使用できる。room IDが違う端末同士は同期しない。

## この段階でできること

- 1つのWebGLビルドからPad1からPad4をURLで振り分ける
- 同じroom IDを使う端末を同一セッションとして扱う
- 各Padへ完成済みモデル、台、UIを接続する
- Editor Roleを切り替えて4画面の骨格を確認する

素材ギャラリー操作、武器カルーセル、配置確認、転送演出、QRカメラ、
液体、トンカチ、完成武器表示の実動作は次の各ステップで接続する。
