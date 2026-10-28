namespace Tansu
{
    /// <summary>
    /// 保存ファイルの解析結果を表す。
    /// </summary>
    internal enum SaveFileState
    {
        /// <summary>正しく読めた。</summary>
        Valid,

        /// <summary>壊れている。</summary>
        Broken,

        /// <summary>このアプリが対応しているより新しいファイル形式で書かれている。</summary>
        TooNew,
    }
}
