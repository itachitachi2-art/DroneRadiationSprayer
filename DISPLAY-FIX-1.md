# 表示修正1 / Display fix 1

2026-10-07。ゲーム機能のDLLバージョンは0.1.0.1のままです。

## 変更

- `Config/Localization.txt` を、v3.3が実際に読み込む `Config/Localization.csv` に変更しました。
- ヘッダーをv3.3標準と同じ20列に合わせました。`KeepLoaded`列を含み、英語・日本語の名前と説明を対応する列へ配置しています。翻訳内容は元の候補版と同じです。
- 既存の放射能除去装置のアイコン参照をやめ、このパーツ専用の透過アイコンを追加しました。
- `CustomIcon`は `modDroneRadiationSprayer`。画像は `UIAtlases/ItemIconAtlas/modDroneRadiationSprayer.png`（160×160 RGBA）です。高解像度の生成元を `Artwork/` に保存しています。
- `LocalizationKey`は追加していません。v3.3の改造パーツ読込処理はアイテム内部名を直接ローカライズするため、既存キー `modDroneRadiationSprayer` を使用します。

## 変更していない機能

DLL、全C#ソース、ModInfo.xml、ビルドスクリプトをバイト単位で維持しました。
装着条件、25mの範囲、回復阻害、消耗品なし、停止・回収後の扱いは変更していません。

DLL SHA-256:
`4d2da710330e7d2c3fc7784127713a2f5da9da4367df26297e4caa0f02a55f9f`

## 根拠と検証

提供されたv3.3 Assembly-CSharp.dllの静的ILで次を確認しています。

- `Localization.LoadPatchDictionaries` (0x060096aa) は `/Localization.csv` を探します。旧`.txt`を読む分岐はありません。
- `Localization.loadCsv` (0x060096bc) はヘッダー名で列を対応付けます。
- `ItemModificationsFromXml.parseItem` (0x0600360f) は内部名を `Localization.Get` に渡します。
- `ItemClass.GetIconName` (0x060031a6) は明示された `CustomIcon` を優先します。
- `ModManager`は `UIAtlases` の各アトラスを読み込み、PNGファイル名から拡張子を除いた名前をスプライト名にします。

ゲームAssembly SHA-256:
`fceec27300ffd3a1f97b097e43b60f3b07597f441e7ecbc6e1b59eebb234c705`

設定整合性21項目と表示用の自動検証を実施しました。PNGの透明度・小サイズでの視認性を目視確認しました。
元の106件の機能試験は今回再実行していません。DLLと全C#が同一であることを確認し、表示関連だけを再検証しています。
`tests/validation.json`は元候補の歴史的なビルド／機能試験記録です。現在の表示関連ファイルのハッシュは `tests/display-validation.json` を参照してください。

## 次のゲーム内確認

ゲームを完全に終了し、旧フォルダを退避してから新フォルダを丸ごと配置してください。
日本語で「ドローン用放射能除去散布装置」、英語で「Drone Radiation Sprayer」の名前・説明と専用アイコンを確認します。
装着・回復阻害・回収後の挙動をもう一度確認してください。

今回も実際のUnityゲーム起動と表示確認は未実施です。再コンパイル・コンパイラ導入はしていません。
