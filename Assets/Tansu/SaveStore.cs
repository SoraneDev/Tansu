using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tansu.Converters;
using Tansu.Internal;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// 値をキーごとに読み書きし、1つのJSONファイルにまとめて保存する。
    /// </summary>
    /// <remarks>
    /// <see cref="LoadAsync"/>で読み込みを完了させてから、<see cref="Get{T}"/>と<see cref="Set{T}"/>で値を読み書きし、
    /// <see cref="FlushAsync"/>または<see cref="Flush"/>で変更をまとめてファイルに書き込む。
    /// 生成も含め、すべての操作はメインスレッドから行う。ファイルの読み書きだけを内部で別スレッドに任せる。
    /// ファイルの読み書きの失敗は例外にせず、コンソールにエラーを出したうえで戻り値で伝える。
    /// </remarks>
    public sealed class SaveStore : ISaveStore
    {
        private const string LogPrefix = "[Tansu]";


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
        private bool _canSave;
        private bool _isDirty;
        
        // 書き込む内容に振る通し番号。古い内容で新しい内容を上書きしないために使う。
        // _snapshotVersionはメインスレッドだけが、_writtenVersionは_writeLockの中だけで読み書きする。
        private long _snapshotVersion;
        private long _writtenVersion;


        /// <summary>保存ファイルのフルパス。</summary>
        public string FilePath { get; }

        
        /// <inheritdoc cref="ISaveStore.IsLoaded"/>
        public bool IsLoaded => _loadState == LoadState.Loaded;

        
        /// <inheritdoc cref="ISaveStore.CanSave"/>
        public bool CanSave => _canSave;

        
        /// <inheritdoc cref="ISaveStore.HasUnsavedChanges"/>
        public bool HasUnsavedChanges => _isDirty;


        /// <summary>
        /// <see cref="Application.persistentDataPath"/>の下に保存するストアを作る。メインスレッドから呼ぶ。
        /// </summary>
        /// <param name="fileName">保存ファイル名。</param>
        /// <exception cref="ArgumentException"><paramref name="fileName"/>が空、または空白だけの場合。</exception>
        /// <exception cref="InvalidOperationException">メインスレッド以外から呼んだ場合。</exception>
        public SaveStore(string fileName = "save.json")
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name must not be empty.", nameof(fileName));
            }

            // Unityのメインスレッドには、awaitの後にメインスレッドへ戻すための仕組み（同期コンテキスト）が必ず設定されている。
            // これがないスレッドで作ると、awaitの後に別のスレッドで値を書き換えてしまうため、ここで止める。
            if (SynchronizationContext.Current == null)
            {
                throw new InvalidOperationException("SaveStore must be created on the Unity main thread.");
            }

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            FilePath = Path.Combine(Application.persistentDataPath, fileName);
        }


        ///<inheritdoc cref="ISaveStore.LoadAsync"/>
        /// <exception cref="InvalidOperationException">2回以上呼んだ場合、またはメインスレッド以外から呼んだ場合。</exception>
        public Task<LoadResult> LoadAsync()
        {
            // 使い方の誤りは、awaitしなくても気付けるよう、Taskを返す前にこの場で例外にする。
            // （asyncメソッドの中で投げた例外はTaskの中にしまわれ、awaitしないと表に出てこない）
            ThrowIfNotMainThread();
            if (_loadState != LoadState.NotLoaded)
            {
                throw new InvalidOperationException("LoadAsync can only be called once.");
            }

            _loadState = LoadState.Loading;
            return LoadInBackgroundAsync();
        }


        /// <inheritdoc cref="ISaveValues.Get"/>
        /// <exception cref="ArgumentNullException"><paramref name="saveValue"/>がnullの場合。</exception>
        /// <exception cref="InvalidOperationException">
        /// 読み込みが完了する前、メインスレッド以外から呼んだ場合、または同じキー名で別の定義を使った場合。
        /// </exception>
        public T Get<T>(SaveValue<T> saveValue)
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();
            Register(saveValue);

            if (!_values.TryGetValue(saveValue.Key, out var token))
            {
                return Copy(saveValue.DefaultValue);
            }

            try
            {
                return token.ToObject<T>(_serializer);
            }
            catch (Exception e) when (IsConversionError(e))
            {
                // 保存されている値の型が合わない（型を変更した、ファイルを手で編集した、など）。
                // ゲームを止めないよう、警告を出して既定値を返す。
                Debug.LogWarning($"{LogPrefix} Could not read '{saveValue.Key}' as {typeof(T).Name}. Returning default value.\n{e.Message}");
                return Copy(saveValue.DefaultValue);
            }
        }


        /// <inheritdoc cref="ISaveValues.Set"/>
        /// <exception cref="ArgumentNullException"><paramref name="saveValue"/>がnullの場合。</exception>
        /// <exception cref="InvalidOperationException">
        /// 読み込みが完了する前、メインスレッド以外から呼んだ場合、または同じキー名で別の定義を使った場合。
        /// </exception>
        public void Set<T>(SaveValue<T> saveValue, T value)
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();
            Register(saveValue);

            // JToken.FromObjectはnullを受け取ると例外を投げるため、nullは専用の値で表す。
            var token = value == null ? JValue.CreateNull() : JToken.FromObject(value, _serializer);

            // 変更がないのに「未保存の変更あり」にならないよう、同じ値なら何もしない。
            if (_values.TryGetValue(saveValue.Key, out var current) && JToken.DeepEquals(current, token))
            {
                return;
            }

            _values[saveValue.Key] = token;
            _isDirty = true;
        }


        /// <inheritdoc cref="ISaveValues.Has"/>
        /// <exception cref="ArgumentNullException"><paramref name="saveValue"/>がnullの場合。</exception>
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


        /// <inheritdoc cref="ISaveValues.Remove"/>
        /// <exception cref="ArgumentNullException"><paramref name="saveValue"/>がnullの場合。</exception>
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
                _isDirty = true;
            }
        }


        /// <inheritdoc cref="ISaveStore.FlushAsync"/>
        /// <exception cref="InvalidOperationException">読み込みが完了する前、またはメインスレッド以外から呼んだ場合。</exception>
        public Task<FlushResult> FlushAsync()
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();

            var skipResult = GetSkipResult();
            if (skipResult.HasValue)
            {
                return Task.FromResult(skipResult.Value);
            }

            // 書き込む内容はメインスレッドにいる今のうちに確定させ、別スレッドでは確定した内容を書くだけにする。
            var snapshot = TakeSnapshot();
            return WriteInBackgroundAsync(snapshot);
        }


        /// <inheritdoc cref="ISaveStore.Flush"/>
        /// <exception cref="InvalidOperationException">読み込みが完了する前、またはメインスレッド以外から呼んだ場合。</exception>
        public FlushResult Flush()
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();

            var skipResult = GetSkipResult();
            if (skipResult.HasValue)
            {
                return skipResult.Value;
            }

            var snapshot = TakeSnapshot();
            try
            {
                WriteSnapshot(snapshot);
                return FlushResult.Saved;
            }
            catch (Exception e)
            {
                return ReportWriteFailure(e);
            }
        }


        /// <inheritdoc cref="ISaveStore.StartFresh"/>
        /// <exception cref="InvalidOperationException">読み込みが完了する前、またはメインスレッド以外から呼んだ場合。</exception>
        public void StartFresh()
        {
            ThrowIfNotMainThread();
            ThrowIfNotLoaded();

            _values = new JObject();
            _canSave = true;
            _isDirty = true;
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
            catch (Exception e)
            {
                // アクセス権がないなど、ファイルを読むこと自体ができなかった。
                // 例外として外に出すと、awaitされていない場合に誰にも気付かれないため、ログに出してFailedとして扱う。
                Debug.LogError($"{LogPrefix} Failed to read save file: {FilePath}\n{e}");
                (result, values) = (LoadResult.Failed, null);
            }

            ThrowIfNotBackOnMainThread();

            _values = values ?? new JObject();
            _canSave = result != LoadResult.Failed;
            _loadState = LoadState.Loaded;

            switch (result)
            {
                case LoadResult.RestoredFromBackup:
                    Debug.LogWarning($"{LogPrefix} Save file was broken. Restored from backup: {FilePath}");
                    break;
                case LoadResult.Failed:
                    Debug.LogError($"{LogPrefix} Could not load save file. Saving is disabled until StartFresh() is called: {FilePath}");
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
            catch (Exception e)
            {
                ThrowIfNotBackOnMainThread();
                return ReportWriteFailure(e);
            }

            ThrowIfNotBackOnMainThread();
            return FlushResult.Saved;
        }


        // 書き込む必要がない場合はその理由を、書き込む必要がある場合はnullを返す。
        private FlushResult? GetSkipResult()
        {
            if (!_canSave)
            {
                Debug.LogWarning($"{LogPrefix} Saving is disabled because the save file could not be loaded. Call StartFresh() to discard it.");
                return FlushResult.Disabled;
            }

            if (!_isDirty)
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
            var text = SaveFileFormat.CreateText(_values);
            _snapshotVersion++;
            _isDirty = false;
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


        private FlushResult ReportWriteFailure(Exception exception)
        {
            // 書けなかった変更を未保存に戻し、次のFlushで再び書き込みを試みられるようにする。
            _isDirty = true;
            Debug.LogError($"{LogPrefix} Failed to write save file: {FilePath}\n{exception}");
            return FlushResult.Failed;
        }


        // 初めて使われたキーなら定義を覚え、同じキー名で別の定義が使われていたら例外にする。
        private void Register<T>(SaveValue<T> saveValue)
        {
            if (saveValue == null)
            {
                throw new ArgumentNullException(nameof(saveValue));
            }

            if (_definitions.TryGetValue(saveValue.Key, out var registered))
            {
                if (!ReferenceEquals(registered, saveValue))
                {
                    throw new InvalidOperationException(
                        $"Key '{saveValue.Key}' is defined more than once. Each key must have exactly one SaveValue definition.");
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
                throw new InvalidOperationException("SaveStore must be used from the main thread.");
            }
        }


        // awaitの後、値を書き換える前に、メインスレッドへ戻ってきたことを確かめる。
        // 通常は必ず戻ってくるが、万一戻っていなければ、別スレッドから値を書き換える前に止める。
        private void ThrowIfNotBackOnMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                var exception = new InvalidOperationException(
                    "SaveStore did not return to the main thread after a background operation. " +
                    "The caller may not have a synchronization context.");

                // この例外はTaskの中にしまわれ、awaitされていなければ表に出ないため、先にログにも出しておく。
                Debug.LogException(exception);
                throw exception;
            }
        }


        private void ThrowIfNotLoaded()
        {
            if (_loadState != LoadState.Loaded)
            {
                throw new InvalidOperationException("Call LoadAsync and wait for it to complete before using SaveStore.");
            }
        }


        // 保存ファイルを読み込む。本体が使えなければバックアップを試す。
        // 別スレッドで実行するため、インスタンスの状態に触れないようstaticにしている。
        private static (LoadResult Result, JObject Values) ReadFromDisk(string filePath)
        {
            var mainText = AtomicFile.ReadOrNull(filePath);
            if (mainText != null)
            {
                var mainState = SaveFileFormat.Parse(mainText, out var mainValues);
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

            var backupText = AtomicFile.ReadOrNull(AtomicFile.GetBackupPath(filePath));
            if (backupText != null && SaveFileFormat.Parse(backupText, out var backupValues) == SaveFileState.Valid)
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
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"{LogPrefix} Could not move broken save file aside: {filePath}\n{e}");
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


        private static bool IsConversionError(Exception e)
        {
            return e is JsonException
                || e is FormatException
                || e is InvalidCastException
                || e is OverflowException
                || e is ArgumentException;
        }
    }
}
