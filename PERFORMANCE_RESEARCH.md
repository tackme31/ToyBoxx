# 起動パフォーマンス調査

対象: メディアファイルをダブルクリックしてから、ウィンドウが画面に出現するまでの時間（現状 2〜3 秒 → 目標 1 秒程度）。

> ここでの「起動完了」= `ApplicationHostService.StartAsync` 内の `window.Show()` が返り、
> ウィンドウが可視化されるまで。動画の初回フレーム描画（FFmpeg のロード）はこの後の別フェーズであり、本調査の対象外。

---

## 1. 計測条件と前提

- 配布形態は **self-contained**（`ToyBoxx.runtimeconfig.json` に `includedFrameworks` あり）。publish 出力は **280 ファイル**、主要 DLL は `Wpf.Ui.dll` 6.4MB、`ffme.win.dll` 1.2MB、`FFmpeg.AutoGen.dll` 1.1MB。
- `PublishReadyToRun=true`（R2R）を指定。
- FFmpeg 本体（`avcodec-61.dll` 84MB ほか計 ~140MB）は起動時にはロードされない。**メディアを開いた瞬間**に初めてロードされるため、ウィンドウ出現時間には効かない（初回フレーム描画時間には効く）。

### まず「事実」を測ること（最重要）

現状の 2〜3 秒がどのフェーズに割り当たっているかは、コードを読むだけでは断定できない。**先に計測して犯人を特定してから手を入れる**こと。本レポートの改善案はすべて「計測で裏取りしてから適用」を前提とする。

計測用の最小コードは本書末尾 [§6](#6-計測用instrumentation) に記載。まず以下 3 区間の実測を取ってから改善に進むこと。

| 区間 | 意味 |
|---|---|
| プロセス開始 → `App` 静的初期化完了 | CLR ロード + `HostBuilder().Build()` |
| `StartAsync` 開始 → `MainWindow` 構築完了 | テーマ適用 + BAML パース + FFME 生成 |
| `MainWindow` 構築完了 → `window.Show()` 復帰 | 初回レイアウト/レンダー、DWM backdrop |

---

## 2. 起動シーケンスの分解

```
OS が ToyBoxx.exe を起動
  └─ apphost が self-contained CoreCLR をロード（ローカル DLL 群を読む＝コールドI/O）
App 型初期化子
  └─ static _host = new HostBuilder()....Build()          ← App.xaml.cs:18-34
       ・Microsoft.Extensions.{Hosting,DI,Configuration,Logging,FileProviders,Primitives} をロード
       ・appsettings.json / appsettings.user.json を読む
App.OnStartup → _host.Start() → ApplicationHostService.StartAsync   ← App.xaml.cs:71 / ApplicationHostService.cs:21
  ├─ App.xaml のリソース解決
  │    └─ ui:ThemesDictionary(Dark) + ui:ControlsDictionary をマージ  ← App.xaml:10-11
  │         ・WPF-UI 全コントロールのスタイル/テンプレート(BAML)をパース ★大
  ├─ ApplicationThemeManager.Apply(theme)                 ← ApplicationHostService.cs:31 ★
  │         ・テーマリソースの再スイープ + システムアクセントカラー取得
  ├─ Library.FFmpegDirectory = ...  (パス設定のみ、ロードはしない)  ← :35
  ├─ GetRequiredService<MainWindow>()                     ← :39
  │    └─ MainWindow ctor → InitializeComponent()          ← MainWindow.xaml.cs:39
  │         ・MainWindow.xaml + ControllerPanelControl.xaml の BAML パース ★
  │         ・FFME MediaElement を 2 個生成 (Media / PreviewMedia) → ffme.win ロード ★
  │         ・ui:Button / ui:SymbolIcon 多数、Popup、TitleBar+ImageIcon(256x256.ico)
  └─ window.Show()                                        ← :62
       ・初回レイアウト/レンダーパス、FluentWindow の Mica/backdrop 適用
```

★ = 主なコスト候補。

---

## 3. ボトルネック候補（影響度の高い順）

### 3.1 WPF-UI（Wpf.Ui）のロードとリソース展開 — 影響「大」
- `Wpf.Ui.dll` は 6.4MB。`App.xaml` で `ControlsDictionary` を丸ごとマージ（`App.xaml:11`）しており、全コントロールのスタイル/テンプレート BAML を起動時にパースする。
- さらに `ApplicationThemeManager.Apply(theme)`（`ApplicationHostService.cs:31`）が**実行時にテーマリソースを再スイープ**し、システムアクセントカラーを取得する。`App.xaml` 側で既に `Theme="Dark"` 指定済み（`App.xaml:10`）なので、**同じテーマを二重に適用**している疑いが強い。
- 素の WPF サンプルには存在しないコストで、2〜3 秒の中で最大の差分要因である可能性が高い。

### 3.2 Microsoft.Extensions.Hosting による汎用ホスト — 影響「中」
- シングルウィンドウ + シングルトン 4 個だけの構成に対し、Generic Host（`App.xaml.cs:18-34`）は過剰。ホスト構築で `Hosting / DI / Configuration.Json / FileProviders / Primitives / Logging` 等 8 個前後の追加アセンブリをロードし、設定ファイル I/O とリフレクションを行う。
- これも素の WPF サンプルには無いコスト。ウィンドウ出現前（`App` 静的初期化）に同期実行され UI スレッドをブロックする。

### 3.3 FFME MediaElement の生成 — 影響「中」
- `InitializeComponent()` 時点で FFME `MediaElement` を 2 個生成（本体 `MainWindow.xaml:47`、プレビュー `ControllerPanelControl.xaml:37`）。ここで `ffme.win.dll`（1.2MB）とその静的初期化が走る。
- プレビュー用は本来ウィンドウ出現時に不要（シークバー hover 時のみ使用）。にもかかわらず起動時に構築される。

### 3.4 ControllerPanelControl の重い BAML — 影響「中」
- コントローラーパネル（`ControllerPanelControl.xaml`）は `ui:Button` / `ui:SymbolIcon` / `Popup` / 速度メニュー 8 項目など多数の要素を持つ。`InitializeComponent()` で全てが構築される。
- ただしこのパネルは `Visibility="{Binding IsApplicationLoaded, ...}"`（`MainWindow.xaml:77`）で初期は非表示。**見えないのに起動時にフル構築**しており、ウィンドウ出現を遅らせている。

### 3.5 self-contained コールドスタートの I/O — 影響「小〜中」
- 280 ファイルを初回起動時にディスクから読む。2 回目以降は OS のファイルキャッシュに載るため軽くなる。「初回だけ遅い/毎回遅い」を計測で切り分けること。

### 3.6 その他小物 — 影響「小」
- `TitleBar` の `ui:ImageIcon Source="/256x256.ico"`（`MainWindow.xaml:85`）で 256x256 アイコンをデコード。
- R2R が実際に効いているか（配布物が Release publish か、Debug ビルドで計測していないか）の確認。Debug ビルドで測っていると数字が大きく出る。

---

## 4. 改善方針

各項目「期待効果 / リスク / 工数」を付す。効果は**要計測での確認前提の見積り**。

### 優先度 A（低リスク・高効果、まず着手）

**A-1. テーマの二重適用を解消する**
- 現状: `App.xaml` の `ThemesDictionary Theme="Dark"` と `ApplicationThemeManager.Apply(theme)` が重複。
- 対応: config のテーマがコンパイル時デフォルト（Dark）と一致する場合は実行時 `Apply` をスキップ、もしくは `App.xaml` 側の `Theme` 指定を外して実行時 `Apply` に一本化する（どちらか片方に統一）。
- 注意: WPF-UI の Mica/backdrop・アクセント連動は `ApplicationThemeManager` の初期化に依存する場合がある。`Apply` を完全に消すと外観が壊れうるので、**まず `Apply` 前後を計測**し、コストが大きければ「デフォルト一致時のみスキップ」から試す。
- 効果: 中〜大 / リスク: 低〜中 / 工数: 小

**A-2. R2R とビルド構成の確認**
- 配布・計測に使っているのが `-c Release -p:PublishReadyToRun=true` の publish 出力であることを確認（Debug ビルドでの計測は無効）。
- `runtimeconfig` に以下を検討（startup 寄せ）:
  - `TieredCompilation=true`（既定）+ `TieredCompilationQuickJit=true`（既定）は起動優先で維持。
  - `TieredPGO` は定常性能寄りなので起動最優先なら無効化も一案（要計測）。
- 効果: 小〜中 / リスク: 低 / 工数: 小

**A-3. プレビュー用 FFME とコントローラーパネルの遅延生成**
- `ControllerPanelControl`（プレビュー MediaElement 含む）は起動直後は非表示。`window.Show()` 後に `Dispatcher` の低優先度（`Background`/`ContextIdle`）で構築・アタッチするか、`x:Load` 相当の遅延読み込みにする。
- 具体案: `MainWindow.xaml` からコントローラーパネルを直接インスタンス化せず、`Loaded` 後に生成して `LayoutPanel` に差し込む。もしくはプレビュー MediaElement だけを遅延生成（初回 hover 時に生成）。
- 効果: 中（3.3+3.4 をウィンドウ出現後ろに追い出せる） / リスク: 中（XAML 参照・バインディングの張り直しが必要） / 工数: 中
- ※ `MediaElementProvider` が `MainWindow.ControllerPanel.PreviewMedia` を直接参照（`MediaElementProvider.cs:18`）しているため、遅延生成時は null 参照に注意。

### 優先度 B（中リスク・効果次第で大）

**B-1. Generic Host（Microsoft.Extensions.Hosting）を撤去**
- 4 シングルトン + config 読みだけなら、`ServiceCollection` 単体（`Microsoft.Extensions.DependencyInjection` のみ）か、手書きのコンポジションルートで十分。`Hosting/Logging/FileProviders` 等のロードとホスト構築リフレクションを削減できる。
- config は `appsettings.json` を軽量に読む（`System.Text.Json` 直読み or `ConfigurationBuilder` のみ）。
- `IHostedService`（`ApplicationHostService`）は単純な初期化メソッド呼び出しに置き換え。
- 効果: 中 / リスク: 中（起動フローの書き換え） / 工数: 中
- ※ 効果は「Hosting 系アセンブリのロード＋ホスト構築」の実測コスト次第。§6 の第1区間が大きければ着手価値が高い。

**B-2. WPF-UI リソースの絞り込み**
- `ControlsDictionary` を丸ごとマージせず、実際に使うコントロール（Button, SymbolIcon, TitleBar, Snackbar, TextBlock 等）のスタイルだけを個別マージできないか WPF-UI 4.x の構成を確認。全部盛りの BAML パースを減らせれば起動が軽くなる。
- 効果: 中（削れる量次第） / リスク: 中（未マージのリソース欠落で実行時例外） / 工数: 中〜大

### 優先度 C（効果限定・状況次第）

**C-1. アイコンデコードの軽量化** — `TitleBar` アイコンを小さめ ico/png に。効果小。

**C-2. single-file publish の検討** — ファイルオープン回数は減るが展開コストが増え、コールド以外では差が出にくい。計測してからにする。効果小・場合により逆効果。

---

## 5. 推奨実施順序

1. **§6 の計測を先に入れ**、3 区間の実数を取る（これが無いと以降の判断ができない）。
2. 区間内訳で最大の犯人を確認。
3. まず **A-1（テーマ二重適用）** と **A-2（R2R/ビルド確認）** を適用 → 再計測。
4. 区間2（MainWindow 構築）が依然重ければ **A-3（パネル/プレビュー遅延）**。
5. 区間1（App 静的初期化）が重ければ **B-1（Host 撤去）**。
6. まだ目標未達なら **B-2（WPF-UI リソース絞り込み）**。

各ステップで必ず前後比較を取り、効果の無い変更は戻す。

---

## 6. 計測用instrumentation

`Stopwatch` で主要フェーズのタイムスタンプを取り、`Trace`（DebugView で観測可）またはファイルへ出力する最小例。

```csharp
// 例: App.xaml.cs の先頭付近に静的計測ヘルパを用意
internal static class Startup
{
    // プロセス開始からの経過を測る
    private static readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
    public static void Mark(string phase)
        => System.Diagnostics.Trace.WriteLine($"[startup] +{_sw.ElapsedMilliseconds,5}ms  {phase}");
}
```

計測点の埋め込み:

```csharp
// App 静的初期化の直後（_host = ...Build() の後ろ）
//   → Startup.Mark("host built");

protected override void OnStartup(StartupEventArgs e)
{
    Startup.Mark("OnStartup enter");
    _host.Start();
    Startup.Mark("host started");
}
```

```csharp
// ApplicationHostService.StartAsync
Startup.Mark("StartAsync enter");
ApplicationThemeManager.Apply(theme);
Startup.Mark("theme applied");
var window = _serviceProvider.GetRequiredService<MainWindow>();
Startup.Mark("MainWindow constructed");
window.Show();
Startup.Mark("window.Show returned");
window.ContentRendered += (_, _) => Startup.Mark("first ContentRendered");
```

- プロセス開始の絶対起点は `Process.GetCurrentProcess().StartTime` と比較すると OS 起動オーバーヘッド込みで測れる。
- 出力は Sysinternals **DebugView** で観測、または `Trace.Listeners` にファイルリスナを足す。
- **初回起動**（コールドキャッシュ）と **2 回目以降** の両方を測り、3.5 のディスク I/O 寄与を切り分ける。
```
```

> 注: 現状のコードにはまだこの計測は入っていない。改善着手前にこのブロックを一時的に追加して実数を取り、方針決定後に削除する運用を推奨。
