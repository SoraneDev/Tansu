namespace Tansu
{
    /// <summary>
    /// 保存ファイルへの書き込み結果を表す。
    /// </summary>
    public enum FlushResult
    {
        /// <summary>ファイルに書き込んだ。</summary>
        Saved,

        /// <summary>前回の保存から変更がないため、何もしなかった。</summary>
        NoChanges,

        /// <summary>読み込みに失敗して保存が禁止されているため、書き込まなかった。</summary>
        Disabled,

        /// <summary>書き込みに失敗した。</summary>
        Failed,
    }
}
