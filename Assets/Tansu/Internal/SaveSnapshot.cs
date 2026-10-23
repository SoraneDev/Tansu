namespace Tansu.Internal
{
    /// <summary>
    /// ある時点の全データを、ファイルに書き込む形にしたもの。
    /// </summary>
    internal readonly struct SaveSnapshot
    {
        /// <summary>ファイルに書き込むテキスト。</summary>
        public string Text { get; }

        
        /// <summary>内容の新しさを表す通し番号。値が大きいほど新しい。</summary>
        public long Version { get; }


        public SaveSnapshot(string text, long version)
        {
            Text = text;
            Version = version;
        }
    }
}
