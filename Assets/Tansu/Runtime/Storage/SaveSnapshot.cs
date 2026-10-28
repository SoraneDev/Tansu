namespace Tansu
{
    /// <summary>
    /// ある時点の全データを、ファイルに書き込む形にしたものを表す。
    /// </summary>
    internal readonly struct SaveSnapshot
    {
        /// <summary>
        /// 書き込むテキストと通し番号から、スナップショットを作成する。
        /// </summary>
        /// <param name="text">ファイルに書き込むテキスト。</param>
        /// <param name="version">内容の新しさを表す通し番号。</param>
        public SaveSnapshot(string text, long version)
        {
            Text = text;
            Version = version;
        }


        /// <summary>ファイルに書き込むテキスト。</summary>
        public string Text { get; }


        /// <summary>内容の新しさを表す通し番号。値が大きいほど新しい。</summary>
        public long Version { get; }
    }
}
