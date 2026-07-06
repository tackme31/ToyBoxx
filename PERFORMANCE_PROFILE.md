# 起動パフォーマンス実測結果

`PERFORMANCE_RESEARCH.md` §6 の instrumentation（`ToyBoxx/Diagnostics/StartupProfiler.cs` ほか）を用いて、
publish 済み self-contained exe（`-c Release -r win-x64 -p:PublishReadyToRun=true`）を実機で実行した結果。

計測箇所は以下のコードに埋め込み済み:

- `ToyBoxx/Diagnostics/StartupProfiler.cs` — 計測ヘルパ本体
- `App.xaml.cs` — 静的コンストラクタ(`App static ctor enter` / `host built`)、`OnStartup`
- `Services/ApplicationHostService.cs` — `StartAsync` 内の各フェーズ
- `MainWindow.xaml.cs` — コンストラクタ内の各フェーズ
- `Controls/ControllerPanelControl.xaml.cs` — コンストラクタ内の各フェーズ

---

## 1. 生ログ

### 1回目（cold: OSファイルキャッシュなし）

```
=== ToyBoxx startup profile 2026-07-06 11:40:01.768 (pid 55000) ===
[startup] +    32.9ms  (since process start:    291.4ms)  App static ctor enter
[startup] +   516.2ms  (since process start:    774.7ms)  host built
[startup] +   718.9ms  (since process start:    977.4ms)  OnStartup enter
[startup] +   745.2ms  (since process start:   1003.7ms)  StartAsync enter
[startup] +   780.1ms  (since process start:   1038.6ms)  theme applied
[startup] +   863.5ms  (since process start:   1122.0ms)  FFmpeg path configured
[startup] +   887.6ms  (since process start:   1146.0ms)  MainWindow ctor enter
[startup] +  1185.0ms  (since process start:   1443.5ms)  ControllerPanelControl ctor enter
[startup] +  1281.7ms  (since process start:   1540.2ms)  ControllerPanelControl InitializeComponent done (BAML + preview FFME MediaElement)
[startup] +  1315.6ms  (since process start:   1574.0ms)  MainWindow InitializeComponent done (BAML + FFME MediaElement x2)
[startup] +  1316.4ms  (since process start:   1574.9ms)  InitializeMainWindow done
[startup] +  1316.9ms  (since process start:   1575.4ms)  InitializeMediaEvents done
[startup] +  1317.2ms  (since process start:   1575.7ms)  MainWindow constructed
[startup] +  2270.1ms  (since process start:   2528.6ms)  window.Show returned
[startup] +  2271.0ms  (since process start:   2529.4ms)  host started
[startup] +  2446.7ms  (since process start:   2705.2ms)  first ContentRendered
```

### 2回目（warm: OSファイルキャッシュあり）

```
=== ToyBoxx startup profile 2026-07-06 11:40:29.371 (pid 52336) ===
[startup] +    32.9ms  (since process start:    108.9ms)  App static ctor enter
[startup] +   103.1ms  (since process start:    179.1ms)  host built
[startup] +   308.3ms  (since process start:    384.3ms)  OnStartup enter
[startup] +   313.1ms  (since process start:    389.1ms)  StartAsync enter
[startup] +   347.3ms  (since process start:    423.3ms)  theme applied
[startup] +   357.4ms  (since process start:    433.4ms)  FFmpeg path configured
[startup] +   382.0ms  (since process start:    458.0ms)  MainWindow ctor enter
[startup] +   641.6ms  (since process start:    717.6ms)  ControllerPanelControl ctor enter
[startup] +   736.0ms  (since process start:    812.0ms)  ControllerPanelControl InitializeComponent done (BAML + preview FFME MediaElement)
[startup] +   773.8ms  (since process start:    849.8ms)  MainWindow InitializeComponent done (BAML + FFME MediaElement x2)
[startup] +   774.6ms  (since process start:    850.6ms)  InitializeMainWindow done
[startup] +   775.1ms  (since process start:    851.1ms)  InitializeMediaEvents done
[startup] +   775.4ms  (since process start:    851.4ms)  MainWindow constructed
[startup] +  1713.9ms  (since process start:   1789.9ms)  window.Show returned
[startup] +  1714.7ms  (since process start:   1790.7ms)  host started
[startup] +  1870.8ms  (since process start:   1946.8ms)  first ContentRendered
```

---

## 2. 区間ごとの内訳（2回目 = warm を基準、括弧内は1回目 = cold）

| 区間 | 内容 | 所要時間 | I/Oキャッシュの影響 |
|---|---|---:|---|
| プロセス開始 → `App`静的ctor到達 | apphost / CoreCLR ロード | 109ms (291ms) | 大 |
| `HostBuilder().Build()` | Hosting/DI/Configuration/Logging等のロード + config読込 | 70ms (483ms) | 大 |
| `host built` → `OnStartup enter` | `App.xaml`のリソースマージ(WPF-UI `ThemesDictionary`+`ControlsDictionary`のBAMLパース) | 205ms (203ms) | **なし** |
| `ApplicationThemeManager.Apply(theme)` | テーマ再適用 | 34ms (35ms) | **なし** |
| `Library.FFmpegDirectory = ...`（初回タッチ） | FFME/FFmpeg.AutoGenアセンブリの初回ロード | 10ms (83ms) | 大 |
| `MainWindow`ctor → `ControllerPanelControl`ctor | `MainWindow.xaml`本体BAML + メイン`Media`(FFME)生成 | 260ms (297ms) | **なし** |
| `ControllerPanelControl`ctor → `InitializeComponent`完了 | `ControllerPanelControl.xaml`BAML + プレビュー`Media`(FFME)生成 | 94ms (97ms) | **なし** |
| `MainWindow InitializeComponent`残り | TitleBar/アイコン等 | 38ms (34ms) | ほぼなし |
| **`MainWindow`構築完了 → `window.Show()`復帰** | 初回レイアウト/レンダー、FluentWindowのMica/backdrop適用 | **939ms (953ms)** | **なし** |
| `window.Show()`復帰 → 初回`ContentRendered` | 初回コンテンツ描画 | 157ms (177ms) | ほぼなし |
| **合計（プロセス開始 → `window.Show()`復帰）** | | **1790ms (2529ms)** | |

体感で2回目が速く感じたのは事実で、cold→warmで約740ms短縮されている。ただしこれは**プロセス起動・Hostビルド・FFmpegアセンブリロードがOSファイルキャッシュで軽くなった分**であり、ディスクI/Oに起因する区間にほぼ全て集約される。それ以外の区間（BAMLパース、テーマ適用、`window.Show()`）はキャッシュの温冷に関わらず一定コストであり、**純粋なCPU/GPU側のコスト**と判断できる。

---

## 3. 最大の発見: `window.Show()` が全体の約半分を占める

`MainWindow`構築完了から`window.Show()`が返るまでに **939ms(warm)/953ms(cold)** かかっている。これは:

- 実測全体（約1.8〜2.5秒）の**50%以上**を占める単一区間
- cold/warmでほぼ同じ（キャッシュ非依存）→ ディスクI/Oではなく CPU/GPU初期化コストと推定
- `PERFORMANCE_RESEARCH.md`の改善案（A-1/A-2/A-3/B-1/B-2）は**どれもこの区間を直接対象にしていない**

候補として考えられる原因:

1. **WPF描画パイプラインの初回初期化**（milcore/wpfgfx、Direct3Dデバイス生成）— WPFウィンドウの初回`Show()`に伴う既知のコールドコスト。
2. **WPF-UI `FluentWindow`が適用するMica/Acrylicバックドロップ**（`SourceInitialized`時にDWM合成APIを同期呼び出し）。

現状のログの粒度ではこの2つを切り分けられていない。追加の計測点（`SourceInitialized`/`Loaded`イベント、あるいはバックドロップを一時的に無効化しての比較実験）が次のステップとして必要。

---

## 4. その他の実測で裏取りできた点

- **A-1（テーマ二重適用）**: `ApplicationThemeManager.Apply`単体のコストは34〜35ms。cold/warmで変化なし＝実コスト。削減余地はあるが全体への影響は小さい（優先度は当初想定より低い）。
- **A-2（R2R/ビルド構成）**: 今回の計測はRelease + R2R publish で実施済み。追加確認事項なし。
- **A-3（プレビューFFMEの遅延生成）**: `ControllerPanelControl`（プレビュー`Media`含む）のBAML+FFME構築コストは94〜97ms。cold/warmで変化なし＝実コスト。遅延生成で`window.Show()`より後ろに追い出せれば、この分はそのまま短縮できる。
- **B-1（Generic Host撤去）**: `HostBuilder().Build()`はcoldで483ms、warmで70ms。cold起動（初回起動やアップデート直後）ではここが2番目に大きいボトルネック。撤去の効果はcold時に特に大きい。
- **B-2（WPF-UIリソース絞り込み）**: `App.xaml`のリソースマージ(`OnStartup enter`到達まで)は205ms前後でキャッシュ非依存。実コストであり、絞り込みの効果は見込める。
- **MainWindow本体のBAML+メインMedia生成**: 260〜297ms。キャッシュ非依存の実コストだが、メインの`Media`は起動時に必須のため単純な遅延生成は難しい（A-3のようにプレビュー側のみ切り出すのが現実的）。

---

## 5. 次のアクション（提案）

1. `window.Show()`内部の分解: `window.SourceInitialized`と`MainWindow`の既存`Loaded`ハンドラにMarkを追加し、939msの内訳を特定する。
2. （実験）Micaバックドロップを一時的に無効化して再計測し、939msがバックドロップ由来かWPF自体の初期化由来か切り分ける。
3. 上記で犯人が特定でき次第、対応する改善（バックドロップ適用タイミングの変更、非同期化など）を検討する。
4. 並行して、cold起動改善のため B-1（Generic Host撤去）は着手価値が高い（cold時 483ms → 70ms程度まで削減見込み）。
5. A-3（プレビューFFME/ControllerPanel遅延生成）も94ms前後の確実な削減が見込め、リスクも中程度で着手しやすい。

---

## 6. `window.Show()`内訳の追加計測（939ms区間の犯人探し）

§5の提案に従い、`MainWindow`構築完了から`window.Show()`復帰までの939ms（§3参照）を切り分けるための追加計測を実施した。

### 6.1 追加した計測点

`MainWindow.xaml.cs`に以下を追加（`Wpf.Ui.dll` 4.0.3を実バイナリ確認し、`FluentWindow`が`OnSourceInitialized`をオーバーライドして`ApplyBackdrop`／`WindowBackdropType`によるMica/Acrylic適用を行っていることを裏取り済み）:

```csharp
protected override void OnSourceInitialized(EventArgs e)
{
    StartupProfiler.Mark("OnSourceInitialized enter (HWND created)");
    base.OnSourceInitialized(e);
    StartupProfiler.Mark("OnSourceInitialized exit (FluentWindow backdrop/Mica applied)");
}

protected override void OnActivated(EventArgs e)
{
    StartupProfiler.Mark("OnActivated");
    base.OnActivated(e);
}
```

既存の`Loaded`ハンドラ（`InitializeMainWindow`内）の先頭にも`StartupProfiler.Mark("Loaded (first layout pass complete)");`を追加。

### 6.2 実測ログ（2回連続実行）

```
=== 1回目 (pid 47856) ===
MainWindow constructed                                        : +859.2ms
OnSourceInitialized enter (HWND created)                      : +1285.2ms
Loaded (first layout pass complete)                           : +1317.9ms
OnSourceInitialized exit (FluentWindow backdrop/Mica applied)  : +1377.8ms
OnActivated                                                    : +1407.8ms
window.Show returned                                           : +1933.1ms
first ContentRendered                                          : +2163.6ms

=== 2回目 (pid 472) ===
MainWindow constructed                                        : +784.0ms
OnSourceInitialized enter (HWND created)                      : +1189.4ms
Loaded (first layout pass complete)                           : +1227.8ms
OnSourceInitialized exit (FluentWindow backdrop/Mica applied)  : +1291.2ms
OnActivated                                                    : +1318.0ms
window.Show returned                                           : +1798.4ms
first ContentRendered                                          : +2081.0ms
```

### 6.3 区間内訳

| 区間 | 1回目 | 2回目 | キャッシュ依存性 |
|---|---:|---:|---|
| `MainWindow constructed` → `OnSourceInitialized enter` | 426.0ms | 405.4ms | 不明（未調査） |
| `OnSourceInitialized enter` → `Loaded` | 32.7ms | 38.4ms | なし |
| `Loaded` → `OnSourceInitialized exit` | 59.9ms | 63.4ms | なし |
| （↑ backdrop+layout合計） | **92.6ms** | **101.8ms** | なし |
| `OnSourceInitialized exit` → `OnActivated` | 30.0ms | 26.8ms | なし |
| **`OnActivated` → `window.Show returned`** | **525.3ms** | **480.4ms** | **なし（支配的）** |
| `window.Show returned` → `first ContentRendered` | 230.5ms | 282.6ms | ほぼなし |

### 6.4 発見

**発見1: `Loaded`が`OnSourceInitialized exit`より先に発火する。**
`base.OnSourceInitialized(e)`（`FluentWindow`の実装）呼び出し中に`Loaded`イベントが先に発火しており、`FluentWindow`のbackdrop適用処理が内部的にレイアウトパスを強制していると考えられる。実害はないが、「backdrop適用」と「初回レイアウト」は計測上分離できず、**合算で約93〜102ms**。

**発見2: Mica/backdrop仮説は却下。真の支配区間は`OnActivated`より後。**
§3で立てた仮説（Mica/backdrop適用が939msの主因）は否定された。`OnSourceInitialized`区間（backdrop+layout込み）は合計しても100ms程度に過ぎない。代わりに、**`OnActivated`発火後から`window.Show()`復帰までの約480〜525ms**が最大かつ支配的な区間として判明した。この区間は：

- backdrop/Mica適用は既に完了済み（`OnSourceInitialized exit`より後の区間）
- cold/warmでほぼ同じ（キャッシュ非依存）
- `WindowBackdropType`の変更では改善しない可能性が高い（backdrop処理はこの区間より前に完了しているため）

**推定原因**: WPFの描画パイプライン自体の初回初期化（milcore/wpfgfxとの接続確立、Direct3Dデバイス生成、初回コンポジション確立）。これは**プロセス内で最初にWindowを`Show()`した際に一度だけ発生する固定コスト**である可能性が高い。

なお、`MainWindow constructed` → `OnSourceInitialized enter`の約400〜426msも無視できない大きさだが、原因は未特定（`RootViewModel`の生成コスト、`Show()`呼び出し自体のオーバーヘッドなど、`ApplicationHostService.StartAsync`側の処理を含む可能性がある）。

### 6.5 splash ウィンドウ案への示唆

支配的コスト（約500ms）が「FluentWindow固有」でも「MainWindowの中身固有」でもなく「**プロセスで最初に表示されるWPFウィンドウが払う固定税**」なのだとすれば、これを軽量なsplashウィンドウに肩代わりさせることで、「起動完了＝何らかのウィンドウが画面に出現する」という目標指標を大幅に前倒しできる可能性がある：

- 現状: プロセス開始 → 約1.8〜2.5秒後にウィンドウ出現
- splash案（仮説通りなら）: プロセス開始 → 最小限のsplashウィンドウ構築（ほぼ0ms）→ 約500msの税を払う → **約600〜700msでウィンドウ出現**。裏でHost構築・テーマ適用・MainWindow構築を進行し、税を払い済みの`MainWindow.Show()`は高速（backdrop+layoutの100ms程度のみ）と期待できる。

ただしこれは「税がプロセス全体で一度きり」という仮説に依存しており、まだ検証していない。

### 6.6 次のアクション（提案）

1. **決定的な実験**: `App.xaml.cs`の`OnStartup`冒頭、`HostBuilder().Build()`より前に、最小限の素の`Window`（`FluentWindow`ではなく`System.Windows.Window`、中身はほぼ空）を`Show()`するコードを一時的に追加し、その前後と、`MainWindow`側の`OnActivated`→`window.Show returned`区間の両方をMarkする。
   - ダミーWindowの`Show()`が約500msかかり、かつその後の`MainWindow.Show()`（`OnActivated`→`Show returned`）が大幅に短縮されていれば → **プロセス全体で一度きりの税**と確定、splash案は狙い通りに機能する。
   - 両方とも約500msかかるなら → 毎回のウィンドウ表示ごとに発生するコスト（`FluentWindow`固有の何か、あるいはMainWindowのビジュアルツリーの複雑さ）である可能性が高く、splash案の効果は限定的。
2. `MainWindow constructed` → `OnSourceInitialized enter`の約400〜426msの内訳も追加計測する（`RootViewModel`解決前後、`window.Show()`呼び出し直前にMarkを追加）。
3. 上記2点の結果を踏まえてsplashウィンドウ案の設計（実装するか、どこまで作り込むか）を最終判断する。

---

## 7. 常時起動型（タスクトレイ常駐）の検討

splashウィンドウ案とは別の方向性として、「アプリをタスクトレイに常駐させ、2回目以降のファイルオープンは常駐プロセスへIPCで転送する」案を検討した。まだ実装・検証はしていない机上の分析。

### 7.1 splash案との違い

- **splash案** = 1回の起動の中で、支配的コスト（~500msの税）を払うタイミングを前倒しし、その間にローディング画面を見せる。**税自体は毎回のプロセス起動で発生し続ける。**
- **常駐案** = プロセスをまたいで再利用することで、税を**最初の1回だけ**にする。2回目以降のファイルオープンでは新しいプロセスを起動しないため、Host構築・`App.xaml`リソースマージ・テーマ適用・WPF-UI/FFMEアセンブリロードなど、プロセス起動時にしか発生しないコスト（§2の内訳で言うcold時1秒以上の部分）も丸ごとスキップできる。

### 7.2 §6の仮説との関係

§6.4で「`OnActivated`→`window.Show returned`の約500msは、プロセス内で最初にWPFウィンドウを`Show()`した時に一度だけ発生する固定コスト（milcore/wpfgfxの初期化）」という仮説を立てた。これが正しければ：

- 常駐プロセスが一度でもウィンドウを表示していれば、レンダリングパイプラインはウォーム状態
- 2回目以降の新規ウィンドウ`Show()`は税を払わず、backdrop+layout分（§6.3実測で約93〜102ms）程度で済むと期待できる
- ユーザーの懸念（「複数ウィンドウでも結局`Window.Show()`は発生するのでは」）に対する回答: `Show()`自体は毎回発生するが、その**内訳（何にコストがかかるか）が一度きりのコストと毎回のコストに分かれる**。前者（税）が消え、後者（backdrop+layout）だけが残るなら、2回目以降は実測で1/5〜1/10程度に短縮される可能性がある。

したがって、この仮説が§6.6の「ダミーWindow実験」で確認できれば、常駐案の効果もあわせて裏付けられる。逆に「ウィンドウ毎に税が発生する」ことが判明した場合は、常駐案の恩恵は限定的になる。

### 7.3 トレードオフ

**効果（仮説が正しい場合）**:
- 2回目以降のファイルオープンは、プロセス起動コスト（cold時1秒以上）と税（~500ms）の両方をスキップでき、体感速度は現状より大幅に改善する可能性がある
- splash案より効果の上限が高い（splash案は税の支払いタイミングを変えるだけだが、常駐案は税の回数自体を減らす）

**コスト・リスク**:
- **アーキテクチャ変更が本格的**: 単一インスタンス化（Named Mutex/Pipeで既存プロセス検知）、ファイルオープン要求をIPCで常駐プロセスへ転送する仕組み、トレイアイコン＋コンテキストメニュー、ファイル関連付け（「プログラムから開く」）を常駐プロセスへ転送する軽量ランチャーに変更、が必要
- **DI設計の見直しが必須**: 現状`MainWindow`はDIコンテナから`GetRequiredService<MainWindow>()`で解決されており（`ApplicationHostService.cs:44`）、シングルトン前提の可能性が高い。複数ウィンドウを同時に開けるようにするには、`RootViewModel`/`ControllerViewModel`をウィンドウごとに分離する設計変更が必要
- **初回起動（常駐プロセスがまだ無い状態、OS起動直後など）は現状と変わらず遅い** — 恩恵を受けるのは「常駐後の2回目以降」のみ。単発利用が中心のユースケースでは恩恵が薄い
- 常駐によるメモリ常時消費、Windows起動時の自動起動設定、ウイルス対策ソフトによる常駐プロセスへの警戒など、周辺の検討事項も増える
- プロジェクトの性格（軽量な単発メディアプレイヤー）から「常駐デーモン」への性質変化を伴うため、ユーザー体験・配布方針への影響も考慮が必要

### 7.4 次のアクション（提案）

常駐案は着手コストが高いため、実装前に以下を確認してから判断するのが望ましい:

1. §6.6の「ダミーWindow実験」を先に実施し、「税がプロセス全体で一度きりか、ウィンドウ毎に発生するか」を確定させる（splash案・常駐案どちらの判断にも必要な共通の前提条件）。
2. 税が一度きりであることが確認できた場合、常駐案とsplash案の**両方を組み合わせる**（常駐プロセスの初回起動時はsplashで前倒し、2回目以降は常駐の恩恵で高速化）という選択肢も視野に入る。
3. 常駐案に着手する場合は、まず単一インスタンス化とIPCの最小プロトタイプ（新規ウィンドウを開けと伝えるだけ）で効果を実測してから、トレイアイコンや複数ウィンドウ対応など本格的な作り込みに進む。
