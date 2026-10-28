namespace Tansu
{
    /// <summary>
    /// 保存ファイルの読み込み結果を表す。
    /// </summary>
    public enum LoadResult
    {
        /// <summary>保存ファイルを読み込めた。</summary>
        Loaded,

        /// <summary>保存ファイルがなかった。</summary>
        NotFound,

        /// <summary>
        /// 保存ファイルが壊れていたため、1つ前のバックアップから読み込んだ。
        /// 直前の保存の内容は失われている。
        /// </summary>
        RestoredFromBackup,

        /// <summary>
        /// 読み込めなかった。保存ファイルとバックアップの両方が壊れている、ファイルにアクセスできない、
        /// またはこのアプリより新しいバージョンで保存されたファイルである。
        /// </summary>
        Failed,
    }
}
