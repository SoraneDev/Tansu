# Tansu（箪笥）

Unity 向けの、シンプルで壊れにくいセーブライブラリです。
保存したい値を型付きのキーで定義し、値ごとに読み書きしながら、1 つの JSON ファイルにまとめて保存します。書き込み中のクラッシュや電源断に備えたアトミック書き込みと、1 世代前のバックアップからの自動復元に対応しています。

*A simple, crash-safe key-value save library for Unity.*

> 現在は v0.1.0 の開発版です。v1.0.0 になるまでは、API が変わる可能性があります。

### 名前の由来

**箪笥（たんす）** は、衣類や道具を引き出しに分けてしまう日本の伝統的な収納家具です。一つひとつの引き出しに別々のものをしまいながら、全体は 1 つの家具としてまとまっています。
値ごとにキーで出し入れしながら、1 つのファイルにまとめて保存するこのライブラリの仕組みを、その姿に重ねて名付けました。

---

## 特徴

- **型付きのキー**：保存する値を `SaveValue<T>` で、キー名・型・既定値ごと定義します。読み書きの型の取り違えはコンパイル時に分かり、同じキー名の二重定義は実行時に検出します。
- **値ごとに読み書きする**：保存したいデータを 1 つの大きなクラスにまとめる必要がありません。各システムが自分の値だけを読み書きし、ファイルには変更をまとめて書き込みます。
- **さまざまな型を保存できる**：int や string に加え、List・Dictionary・自作のクラス、Vector3 や Color などの Unity の型も保存できます。
- **アトミック書き込み**：一時ファイルに書き切ってから置き換えるため、書き込み中にクラッシュや電源断が起きても、保存ファイルが中途半端な状態になりません。
- **バックアップからの自動復元**：保存のたびに 1 つ前の内容をバックアップとして残します。保存ファイルが壊れていた場合は、自動でバックアップから読み込みます。
- **壊れたデータを上書きしない**：読み込みに失敗した場合は保存を禁止し、復旧できたかもしれないデータを既定値で上書きすることを防ぎます。
- **失敗を見逃さない**：ファイルの読み書きの失敗は例外にせず、結果を戻り値で返し、コンソールにも出力します。`await` せずに呼んでも、失敗が消えることはありません。
- **メインスレッドを止めない**：ファイルの読み書きは別スレッドで行います。メインスレッド以外からの呼び出しは、その場で検出します。
- **知らないキーを消さない**：今のコードで使っていないキーも、そのまま保持して書き戻します。

## 動作環境

- Unity 6 以降
- Newtonsoft Json（`com.unity.nuget.newtonsoft-json`。Unity 公式のパッケージです）
- UniTask などの外部ライブラリへの依存はありません

---

## 導入

### Package Manager（Git URL）

`Window > Package Manager` の `+` ボタンから `Install package from git URL...` を選び、次の URL を入力します。

```
https://github.com/SoraneDev/Tansu.git?path=/Assets/Tansu
```

`?path=` を付けることで、リポジトリ内の Tansu パッケージだけを導入します。
Newtonsoft Json は依存パッケージとして自動で導入されます。

### 手動

このリポジトリの `Assets/Tansu` フォルダを、プロジェクトの `Packages` 以下に配置します。
`Assets` 以下に配置する場合は依存パッケージが自動で導入されないため、Package Manager から `com.unity.nuget.newtonsoft-json` を導入してください。

---

## クイックスタート

### 1. 保存する値を定義する

保存したい値ごとに、キー名・型・既定値を `SaveValue<T>` で定義します。

```csharp
using System.Collections.Generic;
using Tansu;

public static class GameSaveValues
{
    public static readonly SaveValue<int> Coins = new("player.coins", 0);
    public static readonly SaveValue<float> BgmVolume = new("settings.bgmVolume", 1f);
    public static readonly SaveValue<List<string>> ClearedStages = new("progress.clearedStages", new List<string>());
}
```

キー名は `"システム名.値の名前"` の形にすると、ほかの値と重複しにくくなります。

### 2. 読み込む

`SaveStore` を作り、`LoadAsync` で保存ファイルを読み込みます。読み込みが終わるまで、ほかの操作はできません。

```csharp
SaveStore store = new SaveStore();       // persistentDataPath/save.json に保存する
LoadResult result = await store.LoadAsync();
```

### 3. 値を読み書きする

```csharp
int coins = store.Get(GameSaveValues.Coins);   // まだ保存していなければ既定値の 0
store.Set(GameSaveValues.Coins, coins + 10);   // メモリ上の値が変わるだけで、ファイルには書き込まない
```

### 4. ファイルに書き込む

`FlushAsync` で、それまでの変更をまとめてファイルに書き込みます。

```csharp
FlushResult result = await store.FlushAsync();
```

### まとめ

```csharp
using Tansu;
using UnityEngine;

public sealed class CoinCounter : MonoBehaviour
{
    private SaveStore _store;


    private async void Start()
    {
        _store = new SaveStore();
        await _store.LoadAsync();

        Debug.Log($"所持コイン：{_store.Get(GameSaveValues.Coins)}");
    }


    public async void AddCoins(int amount)
    {
        int coins = _store.Get(GameSaveValues.Coins);
        _store.Set(GameSaveValues.Coins, coins + amount);

        await _store.FlushAsync();
    }
}
```

---

## SaveStore の置き方と参照方法

`SaveStore` はシングルトンではない、普通のクラスです。自動で生成されることはありません。どこで作り、どう参照するかは、プロジェクトの設計に合わせて選んでください。

- 生成も含め、`SaveStore` の操作はすべてメインスレッドから行います。
- 同じファイルに対して `SaveStore` を複数作らないでください。互いの変更を上書きし合います。
- ファイル名を分ければ、複数の `SaveStore` を使い分けられます（[保存ファイルを分ける](#保存ファイルを分ける)）。

### 方法 1：DI コンテナで渡す

VContainer の場合、`SaveStore` をインスタンスとして登録すると、使う側にコンストラクタなどで注入できます。

```csharp
public sealed class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(new SaveStore());
    }
}
```

`LoadAsync` は、起動時の処理などで一度だけ呼び、完了を待ってから値を読み書きします。
シーンをまたいで使う場合は、プロジェクト全体の親となる LifetimeScope に登録します。

### 方法 2：静的なプロパティで公開する

DI コンテナを使わない場合は、起動時に一度だけ生成し、静的なプロパティなどで公開します。

```csharp
using System.Threading.Tasks;
using Tansu;

public static class GameSave
{
    public static SaveStore Store { get; private set; }


    // タイトル画面など、最初に一度だけ通る場所から呼ぶ
    public static async Task<LoadResult> InitializeAsync()
    {
        Store = new SaveStore();
        return await Store.LoadAsync();
    }
}
```

---

## 使い方

### 読み込み結果を確認する

`LoadAsync` は読み込み結果を `LoadResult` で返します。

```csharp
LoadResult result = await store.LoadAsync();

switch (result)
{
    case LoadResult.Loaded:
    case LoadResult.NotFound:
        break;

    case LoadResult.RestoredFromBackup:
        // 直前の保存は失われたが、1 つ前の状態で続けられる
        break;

    case LoadResult.Failed:
        // 保存は禁止されている。プレイヤーに確認してから StartFresh で始め直す、など
        break;
}
```

`Failed` の場合は、壊れたファイルを上書きしないよう、保存が禁止されます。そのデータを諦めて始め直す場合は、`StartFresh` を呼びます。

```csharp
store.StartFresh();   // すべての値を消し、保存できる状態に戻す
```

### 値を読み書きする

```csharp
int coins = store.Get(GameSaveValues.Coins);
store.Set(GameSaveValues.Coins, 100);

bool hasCoins = store.Has(GameSaveValues.Coins);   // 値が設定されているかどうか
store.Remove(GameSaveValues.Coins);                // 値を消す。以降の Get は既定値を返す
```

`Get` は毎回新しく作った値を返します。List などを受け取って中身を変えた場合は、`Set` し直します。

```csharp
List<string> stages = store.Get(GameSaveValues.ClearedStages);
stages.Add("stage1");
store.Set(GameSaveValues.ClearedStages, stages);
```

### ファイルに書き込む

`Set` や `Remove` はメモリ上の値を変えるだけです。チェックポイント・シーンの切り替え・ポーズ時など、決まったタイミングで `FlushAsync` を呼んで書き込みます。
変更がなければ何もしないため、気軽に呼べます。

```csharp
FlushResult result = await store.FlushAsync();
```

`FlushAsync` は失敗しても例外を投げず、失敗はコンソールに出力されます。そのため、結果を待たずに呼び捨てにしても構いません。

```csharp
_ = store.FlushAsync();
```

### アプリの終了時に書き込む

アプリの終了時など、非同期処理の完了を待てない場面では `Flush` を使います。書き込みが終わるまで処理を止めます。
モバイルでは、アプリが終了処理を通らずに止められることがあるため、バックグラウンドに移るときにも書き込みます。

```csharp
private void OnApplicationPause(bool isPaused)
{
    if (isPaused)
    {
        _store.Flush();
    }
}


private void OnApplicationQuit()
{
    _store.Flush();
}
```

### 保存ファイルを分ける

`SaveStore` の生成時にファイル名を指定すると、保存ファイルを分けられます。フォルダを含めることもできます。

```csharp
SaveStore settings = new SaveStore("settings.json");
SaveStore slot1 = new SaveStore("slots/slot1.json");
SaveStore slot2 = new SaveStore("slots/slot2.json");
```

設定とゲームの進行度を分けたり、セーブスロットを作ったりするときに使います。

### キー名を変更する

保存済みのキー名を変える場合は、読み込んだ後に古いキーの値を新しいキーへ移します。

```csharp
public static readonly SaveValue<int> OldCoins = new("coins", 0);
public static readonly SaveValue<int> Coins = new("player.coins", 0);
```

```csharp
if (store.Has(OldCoins))
{
    store.Set(Coins, store.Get(OldCoins));
    store.Remove(OldCoins);
}
```

### 保存できる型

| 種類 | 例 |
|---|---|
| 基本的な型 | `int`、`float`、`bool`、`string`、enum など |
| コレクション | `List<T>`、配列、`Dictionary<string, T>` など |
| 自作のクラス・構造体 | public なプロパティやフィールドを持つ、普通の C# のクラス |
| Unity の型 | `Vector2`、`Vector3`、`Vector4`、`Vector2Int`、`Vector3Int`、`Quaternion`、`Color`、`Color32` |

- MonoBehaviour や ScriptableObject は保存できません。保存したい値だけを、普通のクラスにまとめてください。
- 自作のクラスのプロパティには、`[JsonProperty("name")]` で保存時の名前を付けておくと、プロパティ名を変えても保存済みのデータを読めます。

---

## 結果の一覧

### LoadResult

| 値 | 意味 | 保存 |
|---|---|---|
| `Loaded` | 保存ファイルを読み込めた | できる |
| `NotFound` | 保存ファイルがなかった（初回起動など）。すべての値が既定値の状態で始まる | できる |
| `RestoredFromBackup` | 保存ファイルが壊れていたため、1 つ前のバックアップから読み込んだ。直前の保存の内容は失われている | できる |
| `Failed` | 保存ファイルとバックアップの両方が壊れている、ファイルにアクセスできない、またはこのアプリより新しいバージョンで保存されたファイルである | `StartFresh` を呼ぶまでできない |

### FlushResult

| 値 | 意味 |
|---|---|
| `Saved` | ファイルに書き込んだ |
| `NoChanges` | 前回の保存から変更がないため、何もしなかった |
| `Disabled` | 読み込みに失敗して保存が禁止されているため、書き込まなかった |
| `Failed` | 書き込みに失敗した。変更は未保存のまま残り、次の呼び出しで再び書き込みを試みる |

---

## 保存ファイル

保存ファイルは `Application.persistentDataPath` の下に作られます。1 つの保存ファイルにつき、次のファイルを使います。

| ファイル | 内容 |
|---|---|
| `save.json` | 最新のデータ |
| `save.json.bak` | 1 つ前のデータ（バックアップ） |
| `save.json.tmp` | 書き込み途中のデータ。正常に書き込めれば残らない |
| `save.json.broken` | 壊れていたファイルの退避先。原因を調べるために残す |

中身は次のような JSON です。`version` はファイル形式のバージョンで、将来形式を変えたときに古い形式と見分けるために使います。

```json
{
  "version": 1,
  "values": {
    "player.coins": 120,
    "settings.bgmVolume": 0.8
  }
}
```

### 書き込みの流れ

1. 新しい内容を一時ファイル（`.tmp`）に書き込み、ディスクへの書き込みが終わるまで待つ
2. 一時ファイルで保存ファイルを置き換え、それまでの保存ファイルをバックアップ（`.bak`）にする

1 の途中で中断しても、保存ファイルには触れていないため無傷のままです。2 の置き換えは一度の操作で行うため、保存ファイルが中途半端な状態になりません。

### 読み込みの流れ

1. 保存ファイルを読み込む。読めればそれを使う
2. 保存ファイルがない、または壊れている場合は、バックアップを読み込む
3. バックアップから読み込めた場合は、壊れた保存ファイルを `.broken` に退避する。次の保存で、壊れたファイルがバックアップになるのを防ぐため

保存ファイルが新しいバージョンのアプリで書かれていた場合は、バックアップを読まずに `Failed` を返します。古いバックアップを読むと、プレイヤーの進行が巻き戻ってしまうためです。

---

## 仕様と注意点

- **スレッド**：生成も含め、`SaveStore` の操作はすべてメインスレッドから行います。ほかのスレッドから呼ぶと、`InvalidOperationException` が発生します。
- **読み込み前の操作**：`LoadAsync` の完了前に `Get` や `Set` などを呼ぶと、例外が発生します。`LoadAsync` を 2 回呼んだ場合も例外になります。
- **使い方の誤りと、ファイルの失敗**：引数の誤りや呼び出す順番の誤りは、その場で例外になります。ファイルの読み書きの失敗は例外にならず、戻り値とコンソールで伝えます。
- **Get の負荷**：`Get` は毎回値を作り直すため、毎フレーム呼ぶ用途には向きません。読み込んだ値は手元に持っておき、変更したときに `Set` してください。
- **同じ値の Set**：今と同じ値を `Set` した場合は、変更として扱いません。
- **型が合わない値**：保存されている値を指定した型で読めない場合（型を変更した、ファイルを手で編集した、など）は、警告を出して既定値を返します。
- **書き込みの順番**：`FlushAsync` の書き込み中に `Flush` を呼んでも、古い内容で新しい内容を上書きすることはありません。`Flush` は、進行中の書き込みが終わるまで待ってから書き込みます。
- **IL2CPP**：保存するクラスがコードの削減（Managed Stripping）で取り除かれると、読み込めなくなる場合があります。その場合は、クラスに `[Preserve]` を付けるか、`link.xml` で保持してください。
- **エディタとビルドの保存先**：`Application.persistentDataPath` を使うため、Windows や macOS では、エディタとビルドで同じ保存ファイルを使います。

---

## 既知の制限

- **WebGL**：スレッドを使えず、保存先の仕組みも異なるため、対応していません。
- **コンソール機**：プラットフォーム専用のセーブ API には対応していません。
- **暗号化・圧縮**：対応していません。保存ファイルはテキストエディタで読み書きできます。
- **マイグレーション**：保存データの構造を自動で変換する仕組みはありません。キー名の変更は、[キー名を変更する](#キー名を変更する)の方法で行います。
- **バックアップ**：残すのは 1 つ前の内容だけです。
- **ファイル全体の書き直し**：1 つの値を変えただけでも、ファイル全体を書き直します。非常に大きなデータの保存には向きません。
- **キャンセル**：`LoadAsync` と `FlushAsync` は、`CancellationToken` による中断に対応していません。

---

## ライセンス

[MIT License](LICENSE)
