using System.Threading.Tasks;


namespace Tansu
{
    /// <summary>
    /// 保存する値の読み書きに加えて、保存ファイルの読み込みと書き込みを提供する。
    /// </summary>
    public interface ISaveStore : ISaveValues
    {
        /// <summary><see cref="LoadAsync" />が完了したかどうか。</summary>
        bool IsLoaded { get; }


        /// <summary>ファイルに保存できる状態かどうか。読み込みに失敗した場合はfalseになる。</summary>
        bool CanSave { get; }


        /// <summary>まだファイルに書き込んでいない変更があるかどうか。</summary>
        bool HasUnsavedChanges { get; }


        /// <summary>
        /// 保存ファイルを読み込む。最初に一度だけ呼ぶ。
        /// </summary>
        /// <returns>読み込み結果を返す。</returns>
        /// <remarks>
        /// 保存ファイルが壊れている場合は、バックアップから読み込む。
        /// 読み込めなかった場合（<see cref="LoadResult.Failed" />）は、壊れたファイルを上書きしないよう保存を禁止する。
        /// その状態から始め直すには、<see cref="StartFresh" />を呼ぶ。
        /// </remarks>
        Task<LoadResult> LoadAsync();


        /// <summary>
        /// 変更をファイルに書き込む。ファイルの書き込みは別スレッドで行う。
        /// </summary>
        /// <returns>書き込み結果を返す。</returns>
        /// <remarks>
        /// 書き込みに失敗しても例外は投げず、コンソールにエラーを出して<see cref="FlushResult.Failed" />を返す。
        /// そのため、awaitせずに呼び捨てにしても、失敗に気付けなくなることはない。
        /// 失敗した場合、変更は未保存のまま残り、次の呼び出しで再び書き込みを試みる。
        /// </remarks>
        Task<FlushResult> FlushAsync();


        /// <summary>
        /// 変更をファイルに書き込む。書き込みが終わるまで処理を止める。
        /// </summary>
        /// <returns>書き込み結果を返す。</returns>
        /// <remarks>
        /// アプリの終了時など、非同期処理の完了を待てない場面で使う。普段は<see cref="FlushAsync" />を使う。
        /// <see cref="FlushAsync" />の書き込みが進行中の場合は、それが終わるまで待ってから書き込む。
        /// 書き込みに失敗しても例外は投げず、コンソールにエラーを出して<see cref="FlushResult.Failed" />を返す。
        /// 失敗した場合、変更は未保存のまま残り、次の呼び出しで再び書き込みを試みる。
        /// </remarks>
        FlushResult Flush();


        /// <summary>
        /// すべての値を消し、空の状態から始め直す。読み込みに失敗して保存が禁止されている場合も、保存できる状態に戻す。
        /// </summary>
        /// <remarks>次の保存で、それまでの保存ファイルは上書きされる。読み込みに失敗したファイルを諦めるときに使う。</remarks>
        void StartFresh();
    }
}
