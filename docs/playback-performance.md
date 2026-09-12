# 4K 再生パフォーマンス調査と再生エンジン置き換えの検討

対象: 4K 動画の再生が重い問題（特に全画面表示時のカクつき）。

- 調査日: 2026-09-13
- 対象バージョン: FFME.Windows `7.0.361-beta.1` / FFmpeg `n7.0.2`（BtbN gpl-shared）/ WPF-UI `4.0.3` / .NET 9
- 対応コミット: `26915f0 Improve 4K playback performance`

---

## 1. 結論

- FFME の構造上、**4K のフレームを毎フレーム GPU → CPU → GPU と往復させる**ため、4K（特に全画面・60fps）は根本的に重い。
- FFME のオプション調整で「再生速度が実時間に追従する」程度までは改善した（§3）。ただし全画面 4K のカクつきは解消していない。
- 全画面 4K をなめらかにしたいなら、**デコードから表示まで GPU 上で完結する再生エンジンへの置き換え**が必要（§5 以降）。

---

## 2. 原因分析（FFME ソースで確認済み）

### 2.1 FFME のフレームパイプライン

| # | 処理 | 実行場所 | 4K での負荷 | ソース |
|---|------|----------|-------------|--------|
| 1 | デコード | GPU（HW デコード時）/ CPU | HW なら軽い | `VideoComponent.AttachHardwareDevice` |
| 2 | GPU メモリ → RAM へ転送 (`av_hwframe_transfer_data`) | GPU→CPU | **重い**（NV12 で約 12MB/frame） | `HardwareAccelerator.ExchangeFrame` |
| 3 | NV12 → BGRA 色変換 (`sws_scale`, `SWS_POINT`) | **CPU・シングルスレッド** | **重い**（出力は約 33MB/frame） | `VideoComponent.MaterializeFrame` |
| 4 | バックバッファへコピー (`Buffer.MemoryCopy`) | CPU | 重い | `InteropVideoRenderer.InteropBuffer.Write` / `VideoRenderer` |
| 5 | WPF がビットマップを GPU テクスチャへアップロードし合成 | CPU→GPU | **重い**（表示サイズに比例して合成コスト増） | WPF (milcore) |
| 6 | DWM による合成（WPF は D3D9 のリダイレクトサーフェス方式） | GPU | フリップモデルが使えず、フレームのタイミングが不安定になりやすい | — |

- 手順 1〜4 は**表示サイズと無関係**で、常に動画の元の解像度で処理される（`sws_getCachedContext` の出力サイズ = 入力サイズ）。
- 全画面で悪化するのは手順 5〜6（3840×2160 を毎フレーム描き直す）。
- ウィンドウ背景は `WindowBackdropType.None`（FluentWindow の既定値）で、Mica などの効果は使っていない。アプリ側 UI は原因ではない。

### 2.2 見つかった設定上の問題

- **デコードのスレッド数が 1**: `DecoderOptions` のコンストラクタで `Threads = "auto"` がコメントアウトされており、FFmpeg の既定値（1 スレッド）になっている。HW デコードが効かない場合（非対応コーデック、10bit 等）は 4K を CPU の 1 コアでデコードすることになる。
- **音声と映像のデコードが同じスレッド**: `UseParallelDecoding = false`（既定値）では 1 つのワーカーが音声 → 映像の順にデコードするため、映像の処理（手順 2〜3）が詰まると音声のデコードも遅れる。
- ユーザーが見つけた `e.Options.EnableHardwareAcceleration` は **FFME 7 には存在しない**。HW デコードは `MediaOptions.VideoHardwareDevices` で指定する方式で、すでに設定済みだった（CUDA → D3D11VA → DXVA2 の順に試し、最初に成功したものを使う）。

---

## 3. 実施した対策（コミット `26915f0`）

`ToyBoxx/MainWindow.xaml.cs` の `InitializeMediaEvents`（メインの `Media` のみ。プレビュー用は変更なし）:

| 設定 | 効果 | 副作用 |
|------|------|--------|
| `DecoderParams.Threads = "auto"` | CPU デコード時に全コアを使う | なし（HW デコード時は影響小） |
| `UseParallelDecoding = true` | 音声と映像を別スレッドでデコード | なし |
| `RendererOptions.VideoImageType = InteropBitmap` | 描画スレッドの CPU 負荷を削減 | FFME のドキュメントにティアリングが出る可能性ありとの記載。問題が出たら削除する |
| `MediaOpened` で `VideoHardwareDecoder` を `Debug.WriteLine` | HW デコーダを接続できたかを確認できる | Debug ビルドのみ出力 |

**ユーザーによる確認結果**: 再生速度が実時間と一致するようになった。全画面 4K では依然としてかなりカクつく。

### FFME のまま試せる残りの手（効果は限定的）

- `e.Options.VideoFilter = "scale=1920:-2"`: 手順 3〜5 の処理量が約 1/4 になる。ただしズームやスクリーンショットの画質も落ちる。
- `RendererOptions.VideoRefreshRateLimit`: 描画するフレーム数に上限をつける。
- `Library.EnableWpfMultiThreadedVideo`（現在 `true`）: デバッガ接続時は無効になる実験的機能。性能比較の際は Release ビルドで計測すること。

---

## 4. 置き換え前に取っておくべき計測値

置き換えの効果を判断するため、同じ動画・同じ PC で以下を記録しておく。

- テスト動画: 解像度 / fps / コーデック / ビット深度（例: 3840×2160, 60fps, HEVC Main10）
- PC: CPU / GPU / モニタ解像度とスケーリング倍率
- 計測項目（ウィンドウ表示と全画面の両方）
  - タスクマネージャーの CPU 使用率（全体と最も高いコア）
  - GPU の「3D」と「Video Decode」の使用率
  - 体感のカクつき（可能なら PresentMon などでフレーム時間を計測）
- `[ToyBoxx] Video codec: ..., hardware decoder: ...` の出力

---

## 5. 置き換え候補の比較

| 候補 | 方式 | ライセンス | 状況（2026-09 時点） | 評価 |
|------|------|-----------|---------------------|------|
| **Flyleaf (FlyleafLib)** | FFmpeg + Direct3D 11。HW デコード → GPU 上で変換・表示（ゼロコピー） | LGPL-3.0 | v3.11.3（2026-08-21）。活発に更新中 | **第一候補** |
| libmpv（自前ラッパー / 既存 .NET バインディング） | mpv を `HwndHost` に埋め込む（`wid` オプション） | LGPL-2.1+（ビルド次第で GPL） | mpv 本体は活発 | 性能と機能は十分。WPF への組み込みと UI を重ねる部分は自前で作る必要がある |
| LibVLCSharp | libVLC。WPF の `VideoView` は HwndHost と前面ウィンドウで UI を重ねる | LGPL-2.1 | 活発 | 候補にはなるが、回転・パン・ズームなど細かい制御の自由度は要確認 |
| FFME（現状維持） | §2 のとおり CPU を経由 | Ms-PL | 最終リリース 2024-06 | 4K 全画面の根本解決は不可 |

### 5.1 Flyleaf の要点（README / ソースで確認した範囲）

- ターゲット: `net8.0-windows; net10.0-windows`（ToyBoxx の net9.0-windows からも net8 用のビルドを参照できる）
- 依存: `Flyleaf.FFmpeg.Bindings 9.0.0`、Vortice（Direct3D11 / DirectComposition / XAudio2 / MediaFoundation）
- **FFmpeg のバージョンが変わる**: README は FFmpeg v9.0 対応とあり、Flyleaf の GitHub releases で配布されている推奨ビルドの使用を推奨している。`requirements.ps1`（現状 FFmpeg 7.0.2）の更新が必要。
- 機能（README 記載。ToyBoxx での実動作は未検証）:
  - 再生 / 一時停止 / 停止 / 速度変更 / 逆再生 / フレーム単位のシーク・コマ送り
  - パン / ズーム / 回転 / 反転 / クロップ
  - スナップショット / 録画
  - HW デコード、ゼロコピー、HDR→SDR、D3D11 Video Processor、超解像（Nvidia / Intel）
  - 音量 / ミュート / 音声遅延
  - フレーム抽出（Extractor）→ シークバーのサムネイルに使える可能性あり
- WPF 統合: `FlyleafLib.Controls.WPF.FlyleafHost`（`ContentControl`）
  - 映像は **独立したウィンドウ（Surface）** に描画され、`FlyleafHost` の Content は **さらに別の Overlay ウィンドウ** に載る。
  - サンプルのコメントに「Window のリソースは Overlay 内から見えない（別ウィンドウのため）」とある。
  - 標準で次の操作に対応: ダブルクリックで全画面、ドロップで開く、Ctrl+ドラッグでパン、Ctrl+ホイールでズーム、Shift+ホイールで回転、アイドル検出（`ActivityRefresh`）。
  - プレイヤーの状態は `INotifyPropertyChanged` で公開されており、そのまま ViewModel にバインドできる。
- 参考サンプル: `Samples/FlyleafPlayer (Custom - MVVM) (WPF)`、`Samples/FlyleafPlayer (WPF Control) (WPF)`

---

## 6. 置き換え時に影響する箇所（FFME 依存の一覧）

### 6.1 初期化・構成

| ファイル | 内容 |
|----------|------|
| `ToyBoxx/ToyBoxx.csproj:23` | `FFME.Windows` パッケージ参照 |
| `ToyBoxx/ToyBoxx.csproj:44` | `ffmpeg\**\*` を出力にコピー |
| `requirements.ps1` | FFmpeg 7.0.2 と SoundTouch（FFME の速度変更で使用）のダウンロード |
| `ToyBoxx/appsettings.json` | `FFMpegRootPath` |
| `ToyBoxx/Services/ApplicationHostService.cs:34-36` | `Library.FFmpegDirectory` / `Library.EnableWpfMultiThreadedVideo` |
| `ToyBoxx/App.xaml.cs:31` | `IMediaElementProvider` の DI 登録 |
| `ToyBoxx/Services/MediaElementProvider.cs` | View から `MediaElement`（main / preview）を取り出す |

### 6.2 View

| ファイル | 内容 |
|----------|------|
| `ToyBoxx/MainWindow.xaml:47-68` | `ffme:MediaElement` + `RenderTransform`（Scale / Rotate / Translate を ViewModel にバインド） |
| `ToyBoxx/MainWindow.xaml.cs:119-172` | `RendererOptions`、`MediaOpening`（HW デコード / スレッド設定）、`MediaOpened`、プレビュー用の低解像度設定 |
| `ToyBoxx/MainWindow.xaml.cs:183-229` | キー操作: `IsOpen` / `IsPlaying` / `IsSeeking` / `HasVideo` / `MediaInfo.MediaSource` / `MediaInfo.Streams[VideoStreamIndex].PixelWidth/Height` |
| `ToyBoxx/MainWindow.xaml.cs:283-363` | ズーム / パン / リセット（`Media.ActualWidth/Height` を基準に WPF の Transform を操作） |
| `ToyBoxx/Controls/ControllerPanelControl.xaml:37-41` | プレビュー用 `ffme:MediaElement` |
| `ToyBoxx/Controls/ControllerPanelControl.xaml:58-66,177-195` | バインド: `Position` / `PositionStep` / `PlaybackStartTime` / `PlaybackEndTime` / `NaturalDuration` / `IsOpen` / `IsMuted` / `Volume` / `SpeedRatio` |
| `ToyBoxx/Controls/ControllerPanelControl.xaml.cs:117-124` | シークバーにマウスを乗せたときのプレビュー: `PreviewMediaElement.IsOpen` / `IsSeekable` / `Seek` |

### 6.3 ViewModel / Commands

| ファイル | 使用 API |
|----------|----------|
| `ToyBoxx/AppCommands.cs:15-60` | `Open(Uri)` / `Close()` / `IsOpen`、プレビューの `Open` → `Stop` |
| `ToyBoxx/AppCommands.cs:63-111` | `Play` / `Pause` / `Stop` / `Seek` / `StepForward` / `HasMediaEnded` / `Position` |
| `ToyBoxx/AppCommands.cs:149-156` | `SpeedRatio` |
| `ToyBoxx/AppCommands.cs:162-197` | `CaptureBitmapAsync()` → `System.Drawing.Bitmap` を PNG で保存 |
| `ToyBoxx/ViewModels/RootViewModel.cs:27-31,66-95` | `MediaElement` / `PreviewMediaElement` プロパティ、`IsOpen` / `IsOpening` / `Source` / `MediaInfo.MediaSource` の変更監視（ウィンドウタイトル） |
| `ToyBoxx/ViewModels/ControllerViewModel.cs:45-150` | `LoopingBehavior`（`MediaPlaybackState`）、`Volume` / `IsMuted` の復元、`IsPlaying` / `IsPaused` / `IsSeeking` / `IsChanging` / `CanPause` / `IsSeekable` によるボタン状態、`PositionChanged` イベントで区間ループ（範囲外に出たら `Seek`） |
| `ToyBoxx/Services/ApplicationHostService.cs` の `Closing` | `LoopingBehavior` / `Volume` / `IsMuted` を Settings に保存 |

### 6.4 機能ごとの移行メモ

| 機能 | 現状の実装 | 置き換え時の注意 |
|------|-----------|------------------|
| ズーム / 回転 / パン | WPF の `RenderTransform` を映像の要素にかける | Flyleaf / mpv / VLC は別ウィンドウ（HWND）に描画するため **WPF の Transform は効かない**。エンジン側の API（Flyleaf: Pan/Zoom/Rotate、mpv: `video-zoom` / `video-rotate` / `video-pan-x/y`）に置き換える。F キーの「等倍表示」の計算もやり直し。 |
| コントロールパネルを映像に重ねる | 同じウィンドウ内の DockPanel で重ねている | 別ウィンドウに描画するため、同じウィンドウ内では映像の上に UI を重ねられない（エアスペース問題）。Flyleaf なら `FlyleafHost` の Content（Overlay ウィンドウ）に移す。フェードのアニメーション（`FluentWindow.Resources` の Storyboard）は Overlay から参照できなくなる点に注意。 |
| Snackbar | MainWindow 内の `SnackbarPresenter` | 映像のウィンドウより背面になる可能性あり。Overlay 側に配置し直す必要があるか要確認。 |
| シークバーのサムネイル | 2 つ目の `MediaElement` をシークして表示 | 2 つ目の Player インスタンス、または Flyleaf の Extractor / mpv のスクリーンショットで代替。軽量化の余地あり。 |
| 区間ループ | `PositionChanged` で範囲外なら `Seek` | mpv は `ab-loop-a/b` に標準対応。Flyleaf は位置変化の通知で同じ方式を再実装。 |
| 速度変更 | `SpeedRatio`（音程維持に SoundTouch） | エンジン側の速度変更に移行。SoundTouch は不要になる見込み。 |
| スクリーンショット | `CaptureBitmapAsync` → PNG | Flyleaf: Snapshot、mpv: `screenshot-to-file`。 |
| コマ送り | `StepForward` | Flyleaf / mpv とも対応。 |
| 繰り返し再生 | `LoopingBehavior`（Settings に int で保存） | 保存形式を独立した bool などに変える。既存の Settings 値の移行に注意。 |
| 全画面 | `ToggleFullScreen`（WindowStyle / WindowState を操作） | Flyleaf は `IsFullScreen` の使用を推奨（WindowState を直接触らない）。 |
| 起動時間 | 別途調査中（`PERFORMANCE_RESEARCH.md`） | D3D11 デバイスの初期化など、新エンジンの初期化コストを計測し直す。 |

---

## 7. 置き換えの進め方（案）

1. **プロトタイプ**: 別ブランチで、最小構成の WPF ウィンドウに FlyleafHost を置き、同じ 4K 動画で §4 の計測値を比較する。
2. **抽象化**: `IMediaElementProvider` が `MediaElement` 型をそのまま返しているため、ViewModel 用に再生エンジン非依存のインターフェース（Open / Play / Pause / Seek / Position / Duration / Volume / Speed / Capture / 状態変化の通知）を切り出す。これで FFME と新エンジンを切り替えられるようにする。
3. **メイン画面の置き換え**: 再生、シーク、音量、速度、全画面。
4. **UI を重ねる部分の作り直し**: コントロールパネル、Snackbar、自動非表示。
5. **付加機能**: ズーム / 回転 / パン、区間ループ、スクリーンショット、コマ送り、シークバーのサムネイル。
6. FFME / SoundTouch / 旧 FFmpeg の削除と `requirements.ps1` の更新、`CLAUDE.md` のアーキテクチャ記述の更新。

## 8. 未確認事項

- テスト動画の fps / コーデックと、全画面時に CPU と GPU のどちらが限界になっているか（§4）
- Flyleaf を ToyBoxx（WPF-UI の FluentWindow）に組み込んだ際の Overlay ウィンドウとの相性（テーマリソース、角丸、タイトルバー）
- Flyleaf が推奨する FFmpeg ビルドのライセンス（現状は GPL ビルドを使用）と配布サイズ
- libmpv / LibVLCSharp を採用する場合の WPF 統合方法の詳細
