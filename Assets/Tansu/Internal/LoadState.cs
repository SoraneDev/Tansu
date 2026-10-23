namespace Tansu.Internal
{
    /// <summary>
    /// 読み込み処理の進み具合を表す。
    /// </summary>
    internal enum LoadState
    {
        /// <summary>まだ読み込みを始めていない。</summary>
        NotLoaded,

        /// <summary>読み込み中。</summary>
        Loading,

        /// <summary>読み込みが完了した。</summary>
        Loaded,
    }
}
