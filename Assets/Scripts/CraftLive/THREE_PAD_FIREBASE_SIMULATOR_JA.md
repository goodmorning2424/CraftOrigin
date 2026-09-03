# CraftOrigin 3Pad Firebaseシミュレーター

## 目的

Pad1・Pad2・Pad3を同じブラウザページ内で常時起動し、Firebaseを介したCraftOriginの連携を1台のPCで確認します。RealRPGとはFirebaseの`weaponGroups`データ以外では接続しません。

## 起動

Windowsでローカル確認する場合は、ビルド先の`Start-CraftLiveSimulator.cmd`をダブルクリックします。ローカルHTTPサーバーが自動起動し、シミュレーターが開きます。

`simulator.html`を直接ダブルクリックして`file://`で開くことはできません。Unity WebGLのデータとWebAssemblyの読み込みがブラウザに拒否され、「Failed to fetch」やダウンロード失敗になります。

サーバーへ配置する場合は、WebGLビルドをHTTPまたはHTTPSで配信して`simulator.html`を開きます。

```text
https://配信先/simulator.html
```

`index.html`を3つのiframeで開くため、ブラウザタブを切り替える必要はありません。3つのPadは同じルームIDを使い、それぞれ独立したFirebaseクライアントとして動作します。

## 操作手順

1. 画面上部の5桁デバッグ番号を確認します。使用範囲は`10000`～`99999`です。
2. 必要なら「新しい番号」を押します。
3. 「3台を起動・再読込」を押します。
4. Pad1・Pad2・Pad3を通常どおり操作し、Pad2で最終武器を決定します。
5. 「Firebase保存を確認」を押します。
6. `保存確認成功`と16項目一致が表示されることを確認します。
7. 必要なら「JSON」でFirebaseから再取得した値を確認します。

## Firebaseで確認する場所

デバッグ番号が`54321`の場合、本番と同じ次の場所へ保存されます。

```text
weaponGroups/54321
```

シミュレーターは保存直後のUnity内データを見るのではなく、Firebase REST APIからこの場所を再取得して照合します。グループ番号、送信元ルーム、武器ID・名前、攻撃・防御・回避、属性、技、モーション、合成結果が揃った場合だけ成功になります。

## 401と表示された場合

Firebase Realtime DatabaseのルールがRESTアクセスを拒否しています。CraftOriginは認証トークンを付けずに通信する現在の構成なので、テスト実施中は使用するFirebaseプロジェクトで`rooms`、`presence`、`weaponGroups`の読み書きを許可する必要があります。公開ルールは文化祭のテスト・運用時間だけ有効にし、終了後は必ず閉じてください。

## 本番との分離

- 通常起動では従来どおり2桁の本番グループ番号を発行します。
- `simulator=1`と正しい`debugGroup`がURLにある場合だけ5桁番号を使用します。
- RealRPGのコード、画面、入力システムには依存しません。
- デバッグ番号がすでに別の結果に使われている場合は上書きしません。「新しい番号」で別番号を使用してください。
