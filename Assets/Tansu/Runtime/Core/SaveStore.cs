using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// 値をキーごとに読み書きし、1つのJSONファイルにまとめて保存する。
    /// </summary>
    /// <remarks>
    /// <see cref="LoadAsync" />で読み込みを完了させてから、<see cref="Get{T}" />と<see cref="Set{T}" />で値を読み書きし、
    /// <see cref="FlushAsync" />または<see cref="Flush" />で変更をまとめてファイルに書き込む。
    /// 生成も含め、すべての操作はメインスレッドから行う。ファイルの読み書きだけを内部で別スレッドに任せる。
    /// ファイルの読み書きの失敗は例外にせず、コンソールにエラーを出したうえで戻り値で伝える。
    /// </remarks>
    public sealed class SaveStore : ISaveStore
    {
        // 同じキー名で別の定義が使われていないかを調べるため、キー名ごとに最初に使われた定義を覚えておく。
        private readonly Dictionary<string, object> _definitions = new();
        // ファイルへの書き込みを1つずつ行うためのロック。別スレッドとメインスレッドの書き込みが重ならないようにする。
        private readonly object _writeLock = new();
        private readonly JsonSerializer _serializer = CreateSerializer();
        // 生成したスレッド（メインスレッド）のID。各操作が正しいスレッドから行われたかを調べるために使う。
        private readonly int _mainThreadId;
        // すべての値を、キー名ごとにJSONの形で持つ。
        // 型を決めずに持つことで、今のコードが知らないキーも消さずに書き戻せる。
        private JObject _values = new();
        private LoadState _loadState = LoadState.NotLoaded;
        // 書き込む内容に振る通し番号。古い内容で新しい内容を上書きしないために使う。
        // _snapshotVersionはメインスレッドだけが、_writtenVersionは_writeLockの中だけで読み書きする。
        private long _snapshotVersion;
        private long _writtenVersion;


        /// <summary>
        /// <see cref="Application.persistentDataPath" />の下に保存するストアを作成する。メインスレッドから呼ぶ。
        /// </summary>
        /// <param name="fileName">保存ファイル名。</param>
        /// <exception cref="ArgumentException"><paramref name="fileName" />が空、または空白だけの場合。</exception>
        /// <exception cref="InvalidOperationException">メインスレッド以外から呼んだ場合。</exception>
        public SaveStore(string fileName = "save.json")
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("ファイル名を空にすることはできません。", nameof(fileName));
            }

            // Unityのメインスレッドには、awaitの後にメインスレッドへ戻すための仕組み（同期コンテキスト）が必ず設定されている。
            // これがないスレッドで作ると、awaitの後に別のスレッドで値を書き換えてしまうため、ここで止める。
            if (SynchronizationContext.Current == null)
            {
                throw new InvalidOperationException($"{nameof(SaveStore)} はUnityのメインスレッドで生成してください。");
            }

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            FilePath = Path.Combine(Application.persistentDataPath, fileName);
        }


        /// <summary>保存ファイルのフルパス。</summary>
        public string FilePath { get; }


        /// <inheritdoc />
        public bool IsLoaded => _loadState == LoadState.Loaded;


        /// <inheritdoc />
        public bool CanSave { get; private set; }


        /// <inheritdoc />
        public bool HasUnsavedChanges { get; private set; }


        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">2回以上呼んだ場合、またはメインスレッド以外から呼んだ場合。</exception>
        public Task<LoadResult> LoadAsync()
        {
            // 使い方の誤りは、awaitしなくても気付けるよう、Taskを返す前にこの場で例外にする。
            // asyncメソッドの中で投げた例外はTaskの中にしまわれ、awaitしないと表に出てこないため。
            ThrowIfNotMainThread();

            if (_loadState != LoadState.NotLoaded)
            {
                throw new InvalidOperationException($"{nameof(LoadAsync)} は1回だけ呼べます。");
            }

            _loadState = LoadState.Loading;

            return LoadInBackgroundAsync();
        }


        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="saveValue" />がnullの場合。</exception>
        /// <exception cref="InvalidOperationException">
        /// 読み込みが完了する前、メインスレッド以外から呼んだ場合、または同じキー名で別の定義を使った場合。
        /// </exception>
        public T Get<T>(SaveValue<T> saveValue)
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();
            Register(saveValue);

            if (!_values.TryGetValue(saveValue.Key, out JToken token))
            {
                return Copy(saveValue.DefaultValue);
            }

            try
            {
                return token.ToObject<T>(_serializer);
            }
            catch (Exception exception) when (IsConversionError(exception))
            {
                // 保存されている値の型が合わない（型を変更した、ファイルを手で編集した、など）。
                // ゲームを止めないよう、警告を出して既定値を返す。
                Debug.LogWarning($"保存する値： {saveValue.Key} を {typeof(T).Name} として読み込めないため、既定値を返します。"
                    + $"\n{exception.Message}");

                return Copy(saveValue.DefaultValue);
            }
        }


        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="saveValue" />がnullの場合。</exception>
        /// <exception cref="InvalidOperationException">
        /// 読み込みが完了する前、メインスレッド以外から呼んだ場合、または同じキー名で別の定義を使った場合。
        /// </exception>
        public void Set<T>(SaveValue<T> saveValue, T value)
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();
            Register(saveValue);

            // JToken.FromObjectはnullを受け取ると例外を投げるため、nullは専用の値で表す。
            JToken token = value == null ? JValue.CreateNull() : JToken.FromObject(value, _serializer);

            // 変更がないのに「未保存の変更あり」にならないよう、同じ値なら何もしない。
            if (_values.TryGetValue(saveValue.Key, out JToken current) && JToken.DeepEquals(current, token))
            {
                return;
            }

            _values[saveValue.Key] = token;
            HasUnsavedChanges = true;
        }


        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="saveValue" />がnullの場合。</exception>
        /// <exception cref="InvalidOperationException">
        /// 読み込みが完了する前、メインスレッド以外から呼んだ場合、または同じキー名で別の定義を使った場合。
        /// </exception>
        public bool Has<T>(SaveValue<T> saveValue)
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();
            Register(saveValue);

            return _values.ContainsKey(saveValue.Key);
        }


        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="saveValue" />がnullの場合。</exception>
        /// <exception cref="InvalidOperationException">
        /// 読み込みが完了する前、メインスレッド以外から呼んだ場合、または同じキー名で別の定義を使った場合。
        /// </exception>
        public void Remove<T>(SaveValue<T> saveValue)
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();
            Register(saveValue);

            if (_values.Remove(saveValue.Key))
            {
                HasUnsavedChanges = true;
            }
        }


        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">読み込みが完了する前、またはメインスレッド以外から呼んだ場合。</exception>
        public Task<FlushResult> FlushAsync()
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();

            FlushResult? skipResult = GetSkipResult();
            if (skipResult.HasValue)
            {
                return Task.FromResult(skipResult.Value);
            }

            // 書き込む内容はメインスレッドにいる今のうちに確定させ、別スレッドでは確定した内容を書くだけにする。
            SaveSnapshot snapshot = TakeSnapshot();

            return WriteInBackgroundAsync(snapshot);
        }


        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">読み込みが完了する前、またはメインスレッド以外から呼んだ場合。</exception>
        public FlushResult Flush()
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();

            FlushResult? skipResult = GetSkipResult();
            if (skipResult.HasValue)
            {
                return skipResult.Value;
            }

            SaveSnapshot snapshot = TakeSnapshot();

            try
            {
                WriteSnapshot(snapshot);
                return FlushResult.Saved;
            }
            catch (Exception exception)
            {
                return ReportWriteFailure(exception);
            }
        }


        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">読み込みが完了する前、またはメインスレッド以外から呼んだ場合。</exception>
        public void StartFresh()
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();

            _values = new JObject();
            CanSave = true;
            HasUnsavedChanges = true;
        }


        private async Task<LoadResult> LoadInBackgroundAsync()
        {
            LoadResult result;
            JObject values;

            try
            {
                // ファイルの読み込みと解析は時間がかかることがあるため、別スレッドで行う。
                // メインスレッドからawaitしているので、終わった後はメインスレッドに戻ってくる。
                (result, values) = await Task.Run(() => ReadFromDisk(FilePath));
            }
            catch (Exception exception)
            {
                // アクセス権がないなど、ファイルを読むこと自体ができなかった。
                // 例外として外に出すと、awaitされていない場合に誰にも気付かれないため、ログに出して失敗として扱う。
                Debug.LogError($"保存ファイル： {FilePath} を読み込めませんでした。\n{exception}");
                (result, values) = (LoadResult.Failed, null);
            }

            ThrowIfNotBackOnMainThread();

            _values = values ?? new JObject();
            CanSave = result != LoadResult.Failed;
            _loadState = LoadState.Loaded;

            switch (result)
            {
                case LoadResult.RestoredFromBackup:
                    Debug.LogWarning($"保存ファイル： {FilePath} が壊れていたため、バックアップから読み込みました。");
                    break;
                case LoadResult.Failed:
                    Debug.LogError($"保存ファイル： {FilePath} を読み込めなかったため、"
                        + $"{nameof(StartFresh)} を呼ぶまで保存を禁止します。");
                    break;
            }

            return result;
        }


        private async Task<FlushResult> WriteInBackgroundAsync(SaveSnapshot snapshot)
        {
            try
            {
                // ファイルの書き込みは時間がかかることがあるため、別スレッドで行う。
                // メインスレッドからawaitしているので、終わった後はメインスレッドに戻ってくる。
                await Task.Run(() => WriteSnapshot(snapshot));
            }
            catch (Exception exception)
            {
                ThrowIfNotBackOnMainThread();
                return ReportWriteFailure(exception);
            }

            ThrowIfNotBackOnMainThread();

            return FlushResult.Saved;
        }


        // 書き込む必要がない場合はその理由を、書き込む必要がある場合はnullを返す。
        private FlushResult? GetSkipResult()
        {
            if (!CanSave)
            {
                Debug.LogWarning("保存ファイルを読み込めなかったため、保存を禁止しています。"
                    + $"保存ファイルを破棄して始め直す場合は、{nameof(StartFresh)} を呼んでください。");

                return FlushResult.Disabled;
            }

            if (!HasUnsavedChanges)
            {
                return FlushResult.NoChanges;
            }

            return null;
        }


        // 現在の値から書き込む内容を作る。メインスレッドで呼ぶ。
        // テキストへの変換をメインスレッドで行うのは、別スレッドで変換している最中に、
        // メインスレッドがSetで値を書き換えて内容が混ざるのを防ぐため。
        private SaveSnapshot TakeSnapshot()
        {
            string text = SaveFileFormat.CreateText(_values);
            _snapshotVersion++;
            HasUnsavedChanges = false;

            return new SaveSnapshot(text, _snapshotVersion);
        }


        // スナップショットをファイルに書き込む。どのスレッドから呼んでもよい。
        private void WriteSnapshot(SaveSnapshot snapshot)
        {
            lock (_writeLock)
            {
                // 別スレッドの書き込みと順番が入れ替わり、すでに新しい内容が書かれていた場合は、古い内容で上書きしない。
                if (snapshot.Version <= _writtenVersion)
                {
                    return;
                }

                AtomicFile.Write(FilePath, snapshot.Text);
                _writtenVersion = snapshot.Version;
            }
        }


        // 書けなかった変更を未保存に戻し、次のFlushで再び書き込みを試みられるようにする。
        private FlushResult ReportWriteFailure(Exception exception)
        {
            HasUnsavedChanges = true;
            Debug.LogError($"保存ファイル： {FilePath} に書き込めませんでした。\n{exception}");

            return FlushResult.Failed;
        }


        // 初めて使われたキーなら定義を覚え、同じキー名で別の定義が使われていたら例外にする。
        private void Register<T>(SaveValue<T> saveValue)
        {
            if (saveValue == null)
            {
                throw new ArgumentNullException(nameof(saveValue));
            }

            if (_definitions.TryGetValue(saveValue.Key, out object registered))
            {
                if (!ReferenceEquals(registered, saveValue))
                {
                    throw new InvalidOperationException($"キー名： {saveValue.Key} に、別の {nameof(SaveValue<T>)} が定義されています。"
                        + "キー名ごとに、定義は1つだけにしてください。");
                }

                return;
            }

            _definitions.Add(saveValue.Key, saveValue);
        }


        // 値を作り直して返す。
        // 既定値をそのまま返すと、Listなどの参照型では、受け取った側の変更が既定値そのものを書き換えてしまうため。
        private T Copy<T>(T value)
        {
            // JToken.FromObjectはnullを受け取ると例外を投げるため、ここで返す。
            if (value == null)
            {
                return default;
            }

            return JToken.FromObject(value, _serializer).ToObject<T>(_serializer);
        }


        private void ThrowIfNotMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                throw new InvalidOperationException($"{nameof(SaveStore)} はメインスレッドから使用してください。");
            }
        }


        // awaitの後、値を書き換える前に、メインスレッドへ戻ってきたことを確かめる。
        // 通常は必ず戻ってくるが、万一戻っていなければ、別スレッドから値を書き換える前に止める。
        private void ThrowIfNotBackOnMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                return;
            }

            var exception = new InvalidOperationException(
                $"{nameof(SaveStore)} の別スレッドでの処理の後、メインスレッドに戻れませんでした。"
                + "呼び出し元に同期コンテキストがない可能性があります。");

            // この例外はTaskの中にしまわれ、awaitされていなければ表に出ないため、先にログにも出しておく。
            Debug.LogException(exception);
            throw exception;
        }


        private void ThrowIfNotLoaded()
        {
            if (_loadState != LoadState.Loaded)
            {
                throw new InvalidOperationException(
                    $"{nameof(LoadAsync)} の完了を待ってから、{nameof(SaveStore)} を使用してください。");
            }
        }


        // 保存ファイルを読み込む。本体が使えなければバックアップを試す。
        // 別スレッドで実行するため、インスタンスの状態に触れないようstaticにしている。
        private static (LoadResult Result, JObject Values) ReadFromDisk(string filePath)
        {
            string mainText = AtomicFile.ReadOrNull(filePath);
            if (mainText != null)
            {
                SaveFileState mainState = SaveFileFormat.Parse(mainText, out JObject mainValues);
                if (mainState == SaveFileState.Valid)
                {
                    return (LoadResult.Loaded, mainValues);
                }

                // 新しいバージョンのアプリで保存されたファイル。
                // バックアップは古いデータなので、読むとプレイヤーの進行が巻き戻ってしまう。読まずに失敗とする。
                if (mainState == SaveFileState.TooNew)
                {
                    return (LoadResult.Failed, null);
                }
            }

            string backupText = AtomicFile.ReadOrNull(AtomicFile.GetBackupPath(filePath));
            if (backupText != null && SaveFileFormat.Parse(backupText, out JObject backupValues) == SaveFileState.Valid)
            {
                if (mainText != null)
                {
                    TryMoveBrokenFileAside(filePath);
                }

                return (LoadResult.RestoredFromBackup, backupValues);
            }

            if (mainText == null && backupText == null)
            {
                return (LoadResult.NotFound, null);
            }

            return (LoadResult.Failed, null);
        }


        // 壊れた本体を退避する。失敗しても、バックアップの内容は読めているので読み込みは成功として扱う。
        // 退避できなかった場合は次の保存で壊れた本体がバックアップになるが、最新の内容は正しく書き込まれる。
        private static void TryMoveBrokenFileAside(string filePath)
        {
            try
            {
                AtomicFile.MoveBrokenFileAside(filePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"壊れた保存ファイル： {filePath} を退避できませんでした。\n{exception}");
            }
        }


        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(new JsonSerializerSettings
            {
                // JSONに型名を書き込まない。書き込むと、書き換えられたファイルから任意の型を生成される危険がある。
                TypeNameHandling = TypeNameHandling.None,

                // 既定の設定では、初期値の入ったListに読み込んだ要素が「追加」されてしまうため、中身を丸ごと置き換える。
                ObjectCreationHandling = ObjectCreationHandling.Replace,

                // 端末の言語設定によって、小数点が"1.5"と"1,5"で変わらないようにする。
                Culture = CultureInfo.InvariantCulture,

                // Vector3やColorなど、Unityの型を読み書きできるようにする。
                Converters = UnityJsonConverters.CreateAll(),
            });
        }


        private static bool IsConversionError(Exception exception)
        {
            return exception is JsonException
                or FormatException
                or InvalidCastException
                or OverflowException
                or ArgumentException;
        }
    }
}
