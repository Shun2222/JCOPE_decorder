using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace JCOPE
{
    class JCOPE_Reader
    {
        static void Main(string[] args)
        {
            for (int month = 1; month < 13; month++)
            {
                for (int day = 1; day < 32; day++)
                {
                    if (month == 2 && day > 28) continue;
                    if (month < 8 && month % 2 == 0 && day == 31) continue;
                    if (month >= 8 && month % 2 != 0 && day == 31) continue;

                    for (int time = 0; time < 24; time++)
                    {
                        string path = "./eas-data/data/2015" + month.ToString() + day.ToString() + time.ToString();
                        Directory.CreateDirectory(path);
                        using (StreamWriter sw1 = new StreamWriter(path + "/N.csv"))
                        using (StreamWriter sw2 = new StreamWriter(path + "/E.csv"))
                        using (StreamWriter sw3 = new StreamWriter(path + "/NN.csv"))
                        {


                            DateTime dt = new DateTime(2015, month, day, 0, time, 0, DateTimeKind.Utc);
                            Console.WriteLine(dt);
                            JCOPE_READER.Status status;
                            for (int ilat = JCOPE_COORD.MeshNum.ILat - 1; ilat >= 0; ilat--)
                            {
                                for (int ilon = 0; ilon < JCOPE_COORD.MeshNum.ILon; ilon++)
                                {

                                    JCOPE_COORD.LatLon LatLon = new JCOPE_COORD.LatLon(
                                        (float)(JCOPE_COORD.LatLonBase.Lat + ilat * 1.0 / JCOPE_COORD.meshPerDeg),
                                        (float)(JCOPE_COORD.LatLonBase.Lon + ilon * 1.0 / JCOPE_COORD.meshPerDeg));

                                    JCOPE_READER.Current c = JCOPE_READER.GetValue(LatLon, dt, out status);
                                    if (status != JCOPE_READER.Status.Success)
                                    {
                                        sw1.Write(",");
                                        sw2.Write(",");
                                    }
                                    else
                                    {
                                        sw1.Write("{0},", c.Nraw);
                                        sw2.Write("{0},", c.Eraw);
                                    }
                                }
                                sw1.WriteLine();
                                sw2.WriteLine();
                            }

                            JCOPE_READER.Current[,] currentmap = JCOPE_READER.GetCurrentMap(dt, out status);
                            for (int ilat = JCOPE_COORD.MeshNum.ILat - 1; ilat >= 0; ilat--)
                            {
                                for (int ilon = 0; ilon < JCOPE_COORD.MeshNum.ILon; ilon++)
                                {

                                    JCOPE_READER.Current c = currentmap[ilat, ilon];

                                    if (!c.Valid)
                                    {
                                        sw3.Write(",");

                                    }
                                    else
                                    {
                                        sw3.Write("{0},", c.Nraw);

                                    }
                                }
                                sw3.WriteLine();

                            }
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// JCOPE 座標系の定義
    /// </summary>
    class JCOPE_COORD
    {
        /// <summary>
        /// 座標のインデックスを入れる変数
        /// </summary>
        public struct ILatLon
        {
            public int ILat, ILon;
            public ILatLon(int ilat, int ilon) { ILat = ilat; ILon = ilon; }
            public bool Valid { get { return (0 <= ILat && ILat < MeshNum.ILat && 0 <= ILon && ILon < MeshNum.ILon); } }
            public int LinearPosition { get { return (ILat * JCOPE_COORD.MeshNum.ILon + ILon); } }
        }
        /// <summary>
        /// 対象領域の矩形の左下の座標
        /// </summary>
        public readonly static LatLon LatLonBase = new LatLon(19.972222f, 116.972222f); // 正しいこと確認済み
        // public readonly static LatLon LatLonBase = new LatLon(23.972222f, 124.972222f);
        /// <summary>
        /// 1°あたりメッシュ数
        /// </summary>
        public const int meshPerDeg = 36;
        /// <summary>
        /// メッシュの数
        /// </summary>
        public readonly static ILatLon MeshNum = new ILatLon(1082, 1190); // 正しいこと確認済み
        // public readonly static ILatLon MeshNum = new ILatLon(865, 831);
        /// <summary>
        /// 座標を入れる変数
        /// </summary>
        public struct LatLon
        {
            public readonly float Lat, Lon;
            public readonly int ILat, ILon;
            public readonly ILatLon ILatLon;
            public readonly bool Valid;
            public LatLon(float lat, float lon)
            {
                Lat = lat; Lon = lon;
                ILat = (int)Math.Round((Lat - LatLonBase.Lat) * meshPerDeg);
                ILon = (int)Math.Round((Lon - LatLonBase.Lon) * meshPerDeg);
                ILatLon = new ILatLon(ILat, ILon);
                Valid = ILatLon.Valid;
            }
        }
    }

    /// <summary>
    /// JCOPE easデータを参照し、配列もしくはピンポイントの値を返す。
    /// </summary>
    class JCOPE_READER
    {
        /// <summary>
        /// 海流を入れる変数 単位knot
        /// </summary>
        public struct Current
        {
            const double root10 = 3.1622776601683793319988935444327,
                         sin = 1 / root10, cos = 3 / root10;
            public readonly double Nraw, Eraw;
            public readonly double Nnorm, Enorm;
            public readonly double ND, ED;
            public readonly bool Valid;
            public Current(double Nraw, double Eraw)
            {
                this.Nraw = Nraw; this.Eraw = Eraw;

                //JCOPEの正規化
                Nnorm = (Nraw - 0.157313673) / 0.528569986 * 0.678384369 + 0.068064298;
                Enorm = (Eraw - 0.491961366) / 0.871627913 * 0.943258481 + 0.263889931;
                //dash座標系への変換
                ND = cos * Nnorm - sin * Enorm;
                ED = sin * Nnorm + cos * Enorm;
                Valid = true;
            }
            /// <summary>
            /// 無効な値を表す変数
            /// </summary>
            /// <param name="_dummy">意味なし</param>
            public Current(bool _dummy = false)
            {
                Nraw = double.NaN; Eraw = double.NaN;
                Nnorm = double.NaN; Enorm = double.NaN;
                ND = double.NaN; ED = double.NaN;
                Valid = false;
            }
        }

        // static string folder = @"C:\Users\nmri\Documents\shunsuke\JCOPE\JCOPE\bin\Debug\eas";
        static string folder = @"./eas-data/data";

        static string[] DT2FilePath(DateTime DT)
        {
            string[] FilePath = new string[2] {
                Path.Combine(folder, string.Format($"VI_{DT:yyyyMMddHH}00.dat")),
                Path.Combine(folder, string.Format($"UI_{DT:yyyyMMddHH}00.dat")) };
            //Console.WriteLine(Path.Combine(folder, string.Format($"VI_{DT:yyyyMMddHH}00.dat")));
            //Console.WriteLine(Path.Combine(folder, string.Format($"UI_{DT:yyyyMMddHH}00.dat")));
            return FilePath;

        }

        public enum Status
        {
            Success = 0,
            OutOfArea,
            FileMissing,
            FileAbnormal,
            Land
        }
        /// <summary>
        /// 海流（Real-time analyses）を取得します。
        /// </summary>
        /// <param name="LatLon">緯度、経度</param>
        /// <param name="DT">日時UTC</param>
        /// <param name="status">エラーコード</param>
        /// <returns>表層海流</returns>
        public static Current GetValue(JCOPE_COORD.LatLon LatLon, DateTime DT, out Status status)
        {
            if (LatLon.Valid == false)
            {
                status = Status.OutOfArea;
                return new Current();
            }
            else return GetValue(LatLon.ILatLon, DT, out status);
        }

        static FileStream[] fs = new FileStream[] { null, null };
        static string[] fp = new string[] { "", "" };

        /// <summary>
        /// 海流（Real-time analyses）を取得します。
        /// </summary>
        /// <param name="ILatLon">緯度、経度インデックス</param>
        /// <param name="DT">日時UTC</param>
        /// <param name="status">エラーコード</param>
        /// <returns>表層海流</returns>
        public static Current GetValue(JCOPE_COORD.ILatLon ILatLon, DateTime DT, out Status status)
        {
            status = Status.Success;
            if (!ILatLon.Valid) status = Status.OutOfArea;

            if (status == Status.Success)
            {
                string[] fpnew = DT2FilePath(DT);
                if (fp[0] != fpnew[0] || fp[1] != fpnew[1])
                {
                    try
                    {
                        fp = fpnew;
                        for (int i = 0; i < 2; i++)
                        {
                            if (fs[i] != null) fs[i].Close();
                            fs[i] = new FileStream(fp[i], FileMode.Open, FileAccess.Read);
                        }
                    }
                    catch (FileNotFoundException e) { status = Status.FileMissing; }
                }
            }
            if (status == Status.Success)
            {
                int position = ILatLon.LinearPosition * 4;
                double[] current = new double[2];

                byte[] b4 = new byte[4];
                for (int i = 0; i < 2; i++)
                {
                    fs[i].Seek(position, SeekOrigin.Begin);
                    if (fs[i].Read(b4, 0, b4.Length) != b4.Length) status = Status.FileAbnormal;
                    else
                    {
                        if (BitConverter.IsLittleEndian) b4 = b4.Reverse().ToArray();
                        current[i] = BitConverter.ToSingle(b4, 0) * 3600 / 1852;
                    }
                    if (current[i] > 1e+19) status = Status.Land;
                }

                if (status == Status.Success)
                    return new Current(current[0], current[1]);
            }
            return new Current();
        }
        public static Current[,] GetCurrentMap(DateTime DT, out Status status)
        {
            Current[,] current = new Current[JCOPE_COORD.MeshNum.ILat, JCOPE_COORD.MeshNum.ILon];
            for (int ilat = 0; ilat < JCOPE_COORD.MeshNum.ILat; ilat++)
                for (int ilon = 0; ilon < JCOPE_COORD.MeshNum.ILon; ilon++)
                {
                    Current c = GetValue(new JCOPE_COORD.ILatLon(ilat, ilon), DT, out status);
                    if (status == Status.FileAbnormal || status == Status.FileMissing) return current;
                    current[ilat, ilon] = c;
                }
            status = Status.Success;
            return current;
        }
    }
}
