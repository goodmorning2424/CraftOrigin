# Craft-liveをGitHub PagesでiPadテストする

このプロジェクトは、`main`ブランチへのpush時にUnity WebGLをビルドし、GitHub Pagesへ公開できます。

## 初回だけ必要なGitHub設定

1. リポジトリの `Settings > Pages > Build and deployment` を開き、`Source`を`GitHub Actions`にします。
2. `Settings > Secrets and variables > Actions`へ次のRepository secretsを登録します。
   - `UNITY_LICENSE`
   - `UNITY_EMAIL`
   - `UNITY_PASSWORD`
3. Unity Personalの場合は、GameCIの手順で取得・有効化したライセンスファイル全体を`UNITY_LICENSE`へ登録します。
4. `main`へpushするか、`Actions > Build and deploy Craft-live WebGL > Run workflow`を実行します。

## iPadで開くURL

公開先が `https://goodmorning2424.github.io/CraftOrigin/` の場合、同じ`room`番号を指定して各Padを開きます。

- Pad1: `https://goodmorning2424.github.io/CraftOrigin/?pad=pad1&room=001`
- Pad2: `https://goodmorning2424.github.io/CraftOrigin/?pad=pad2&room=001`
- Pad3: `https://goodmorning2424.github.io/CraftOrigin/?pad=pad3&room=001`
- Pad4: `https://goodmorning2424.github.io/CraftOrigin/?pad=pad4&room=001`

連携テストでは、各iPadで別のPad URLを開き、全端末の`room`を同じ値にします。別グループで試す場合は`room=002`などへ変更します。

Pad3でQRを読むときは、Safariのカメラ使用確認で「許可」を選びます。GitHub PagesはHTTPS配信なので、ブラウザのカメラAPIを利用できます。

## 更新方法

修正を`main`へpushするとActionsが再ビルド・再公開します。公開後に古い内容が表示される場合は、Safariでページを再読み込みします。
