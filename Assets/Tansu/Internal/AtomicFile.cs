using System.IO;
using System.Text;


namespace Tansu.Internal
{
    /// <summary>
    /// 書き込み中に中断されても、ファイルが中途半端な状態にならない書き込みを行う。
    /// </summary>
    /// <remarks>
    /// 1つのファイル（{path}）につき、次の4つのファイルを使う。
    /// <list type="bullet">
    /// <item><description>{path} … 最新のデータ</description></item>
    /// <item><description>{path}.bak … 1つ前のデータ（バックアップ）</description></item>
    /// <item><description>{path}.tmp … 書き込み途中のデータ。正常に終われば残らない。</description></item>
    /// <item><description>{path}.broken … 壊れていたファイルの退避先。</description></item>
    /// </list>
    /// </remarks>
    internal static class AtomicFile
    {
        // BOM（ファイル先頭の識別用バイト）を付けないUTF-8。
        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);


        /// <summary>
        /// バックアップファイルのパスを返す。
        /// </summary>
        public static string GetBackupPath(string path)
        {
            return path + ".bak";
        }


        /// <summary>
        /// テキストをファイルに書き込む。それまでの内容はバックアップとして残す。
        /// </summary>
        /// <exception cref="IOException">書き込みに失敗した場合。</exception>
        /// <exception cref="System.UnauthorizedAccessException">書き込み先にアクセスする権限がない場合。</exception>
        public static void Write(string path, string text)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = GetTempPath(path);
            var bytes = Utf8.GetBytes(text);

            // 本物のファイルには触らず、一時ファイルに全部書く。
            // ここで中断しても、本物のファイルは無傷のまま残る。
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);

                // OSのキャッシュ（メモリ）に残っている内容を、ディスクに書き終えるまで待つ。
                // これを省くと、電源が落ちたときに中身が空のファイルが残ることがある。
                stream.Flush(flushToDisk: true);
            }

            // 一時ファイルで本物のファイルを置き換える。
            // File.Replaceは、元のファイルをバックアップに移す処理と、一時ファイルを本物にする処理を一度に行う。
            // 途中で中断されても一時ファイルが残るだけで、次の書き込みで上書きされる。
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, GetBackupPath(path));
            }
            else
            {
                File.Move(tempPath, path);
            }
        }


        /// <summary>
        /// ファイルの内容を読み込む。
        /// </summary>
        /// <returns>ファイルの内容。ファイルがなければnull。</returns>
        /// <exception cref="IOException">読み込みに失敗した場合。</exception>
        /// <exception cref="System.UnauthorizedAccessException">ファイルにアクセスする権限がない場合。</exception>
        public static string ReadOrNull(string path)
        {
            // 存在を確認してから読むと、その間にファイルが消えた場合に例外になるため、読んでみて判断する。
            try
            {
                return File.ReadAllText(path, Utf8);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }


        /// <summary>
        /// 壊れていたファイルを別名に移す。
        /// </summary>
        /// <remarks>
        /// 壊れたファイルを残したまま<see cref="Write"/>すると、壊れたファイルがバックアップになり、正常だったバックアップが消えてしまう。
        /// それを防ぐために、先に壊れたファイルを移動させておく。削除せずに残すのは、後から原因を調べられるようにするため。
        /// </remarks>
        /// <exception cref="IOException">移動に失敗した場合。</exception>
        /// <exception cref="System.UnauthorizedAccessException">ファイルにアクセスする権限がない場合。</exception>
        public static void MoveBrokenFileAside(string path)
        {
            var brokenPath = GetBrokenPath(path);
            File.Delete(brokenPath);
            File.Move(path, brokenPath);
        }


        private static string GetTempPath(string path)
        {
            return path + ".tmp";
        }


        private static string GetBrokenPath(string path)
        {
            return path + ".broken";
        }
    }
}
