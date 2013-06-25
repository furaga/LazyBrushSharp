using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.Runtime.InteropServices;

namespace LazyBrushSharp
{
    public struct Stroke
    {
        public Vector2[] Path { get; set;}
        public Color FillColor {get; set;}
        public int Radius { get; set; }
    }

    public class LazyBrush
    {
        [DllImport("GridCutLib.dll")]
        static extern IntPtr CreateGraph(int w, int h);
        [DllImport("GridCutLib.dll")]
        static extern void SetEdgeWeights(IntPtr grid, int w, int h, double[] Vv, double[] Vh);
        [DllImport("GridCutLib.dll")]
        static extern void Labelling(IntPtr grid, int labelID, int w, int h, double[] D, int[] B, int[] L);
        [DllImport("GridCutLib.dll")]
        static extern void DeleteGraph(IntPtr grid);

        // 入力・出力画像
        Texture2D srcImage;     // 元画像
        Color[] srcData;        // 元画像(色配列)
        Color[] brushedData;    // ストロークが乗った元画像
        Color[] dstData;        // 塗りつぶされた画像
        byte[] Intensities;     // 元画像の輝度マップ

        // 途中計算用
        double[] edgeHori;  // 横方向のエッジの重み。論文中のV_pqの一部
        double[] edgeVert;  // 縦方向のエッジの重み。論文中のV_pqの一部
        double[] denthMap;  // 論文中のD
        int[] strokeMap;
        int[] labelMap;     // ラベル付。論文中のL

        // パラメータ
        double K;
        const double hardness = 0.05; // softなら0.05, hardなら1

        // ストローク
        List<Stroke> strokes = new List<Stroke>();
        public int CurrentRadius { get; set; }
        public Color CurrentFillColor { get; set; }
        int maxRenderStrokeNum = 0;
        int renderStrokeNum = 0;

        public LazyBrush(Texture2D src)
        {
            this.srcImage = src;
            srcData = new Color[src.Width * src.Height];
            dstData = new Color[src.Width * src.Height];
            brushedData = new Color[src.Width * src.Height];
            Intensities = new byte[src.Width * src.Height];
            edgeHori = new double[src.Width * src.Height];
            edgeVert = new double[src.Width * src.Height];
            denthMap = new double[src.Width * src.Height];
            strokeMap = new int[src.Width * src.Height];
            labelMap = new int[src.Width * src.Height];

            src.GetData(srcData);
            srcData.CopyTo(dstData, 0);
            srcData.CopyTo(brushedData, 0);
            for (int i = 0; i < dstData.Length; i++)
            {
                Intensities[i] = dstData[i].R;
            }

            K = 2 * (src.Width + src.Height); // ?? perimeter?
            CurrentFillColor = Color.Red;
            CurrentRadius = 10;
        }

        /// 塗りつぶしてdstを更新。コンストラクタで与えた画像srcと同じサイズじゃないとだめ
        public void UpdateFilledTexture(Texture2D dst)
        {
            Fill(dstData);
            dst.SetData(dstData);
        }

        /// ストロークをのせてdstを更新。コンストラクタで与えた画像srcと同じサイズじゃないとだめ
        public void UpdateBrushedTexture(Texture2D dst, Vector2[] appendingPath = null)
        {
            RenderStrokes(brushedData, appendingPath);
            dst.SetData(brushedData);
        }

        //--------------------------------------------------------------------------
        // 画像imgDataを塗りつぶす
        //--------------------------------------------------------------------------
        void Fill(Color[] imgData)
        {
            var total_sw = System.Diagnostics.Stopwatch.StartNew();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            CalcEdgeWeights();
            Console.WriteLine("枝の重み計算: {0} ms", sw.ElapsedMilliseconds);
            sw.Restart();

            for (int i = 0; i < labelMap.Length; i++)
            {
                labelMap[i] = -1;
            }

            Dictionary<int, Color> labelColor = new Dictionary<int, Color>();
            CreateLabelColorMapping(denthMap, strokeMap, labelColor, labelMap, 0);
            Console.WriteLine("ラベル-色のマッピング生成(C++): {0} ms", sw.ElapsedMilliseconds);
            
            int iteration = labelColor.Count - 1;

            if (labelColor.Count < 2) return;
            
            for (int labelID = 0; labelID < iteration; labelID++)
            {
                UpdateEdgeWeights(labelMap, labelID);

                IntPtr grid = CreateGraph(srcImage.Width, srcImage.Height);
                Console.WriteLine("グラフ生成(C++): {0} ms", sw.ElapsedMilliseconds);
                sw.Restart();

                SetEdgeWeights(grid, srcImage.Width, srcImage.Height, edgeHori, edgeVert);
                Console.WriteLine("枝の重み設定(C++): {0} ms", sw.ElapsedMilliseconds);
                sw.Restart();

                labelColor.Clear();
                CreateLabelColorMapping(denthMap, strokeMap, labelColor, labelMap, labelID);
                Console.WriteLine("ストロークマップの生成(C++): {0} ms", sw.ElapsedMilliseconds);

                Labelling(grid, labelID, srcImage.Width, srcImage.Height, denthMap, strokeMap, labelMap);
                Console.WriteLine("ラベリング(C++): {0} ms", sw.ElapsedMilliseconds);
                sw.Restart();

                DeleteGraph(grid);
                Console.WriteLine("グラフ削除(C++): {0} ms", sw.ElapsedMilliseconds);
                sw.Restart();
            }

            for (int i = 0; i < labelMap.Length; i++)
            {
                if (labelMap[i] == -1)
                {
                    labelMap[i] = labelColor.Count - 1;
                }
            }

            float scale = 1 / 255.0f;
            for (int y = 0; y < srcImage.Height; y++)
            {
                for (int x = 0; x < srcImage.Width; x++)
                {
                    int i = x + y * srcImage.Width;
                    dstData[i] = new Color(labelColor[labelMap[i]].ToVector3() * Intensities[i] * scale);
                }
            }
            Console.WriteLine("塗りつぶし: {0} ms", sw.ElapsedMilliseconds);
            sw.Restart();

            Console.WriteLine("合計: {0} ms", total_sw.ElapsedMilliseconds);
        }


        void CreateLabelColorMapping(double[] D, int[] S, Dictionary<int, Color> map, int[] L, int labelID)
        {
            for (int i = 0; i < D.Length; i++)
            {
                D[i] = 0;
                S[i] = -1;
            }

            Dictionary<Color, int> inv_map = new Dictionary<Color, int>();

            for (int j = 0; j < renderStrokeNum; j++)
            {
                Stroke stroke = strokes[j];

                int strokeID = map.Count;
                if (map.ContainsValue(stroke.FillColor))
                {
                    strokeID = inv_map[stroke.FillColor];
                }

                bool flg = false;
                for (int i = 0; i < stroke.Path.Length; i++)
                {
                    Vector2 pt = stroke.Path[i];
                    int px = (int)pt.X;
                    int py = (int)pt.Y;
                    int r = stroke.Radius;
                    //int x = px; int y = py; 
                    for (int y = py - r; y <= py + r; y++)
                    {
                        for (int x = px - r; x <= px + r; x++)
                        {
                            int dx = px - x;
                            int dy = py - y;
                            if (!(x < 0 || srcImage.Width <= x) &&
                                !(y < 0 || srcImage.Height <= y))
                            {
                                if (!(dx * dx + dy * dy > r * r))
                                {
                                    // 彩色済みでない値をセット
                                    if (L[x + y * srcImage.Width] < 0)
                                    {
                                        D[x + y * srcImage.Width] = hardness * K;
                                        S[x + y * srcImage.Width] = strokeID;
                                    }
                                    flg = true;
                                }
                            }
                        }
                    }
                }

                if (flg = true && !map.ContainsValue(stroke.FillColor))
                {
                    map.Add(strokeID, stroke.FillColor);
                    inv_map.Add(stroke.FillColor, strokeID);
                }
            }
        }


        void CalcEdgeWeights()
        {
            double scale = 1.0 / 255;
            const double gamma = 5;
            for (int y = 0; y < srcImage.Height; y++)
            {
                for (int x = 0; x < srcImage.Width - 1; x++)
                {
                    int ip = x + y * srcImage.Width;
                    int iq = x + 1 + y * srcImage.Width;
                    byte I_p = Intensities[ip];
                    edgeHori[x + y * srcImage.Width] = 1 + K * Math.Pow(I_p * scale, gamma);
                }
            }
            for (int x = 0; x < srcImage.Width; x++)
            {
                for (int y = 0; y < srcImage.Height - 1; y++)
                {
                    int ip = x + y * srcImage.Width;
                    int iq = x + (y + 1) * srcImage.Width;
                    byte I_p = Intensities[ip];
                    byte I_q = Intensities[iq];
                    edgeVert[x + y * srcImage.Width] = 1 + K * Math.Pow(I_p * scale, gamma);
                }
            }
        }
        void UpdateEdgeWeights(int[] L, int labelID)
        {
            for (int y = 0; y < srcImage.Height; y++)
            {
                for (int x = 0; x < srcImage.Width; x++)
                {
                    int i = x + y * srcImage.Width;
                    if (L[i] >= 0)
                    {
                        // 彩色済み。なかったコトにする。つまり重みをにする
                        edgeHori[x + y * srcImage.Width] = 0;
                        edgeVert[x + y * srcImage.Width] = 0;
                        if (x >= 1) edgeHori[x - 1 + y * srcImage.Width] = 0;
                        if (y >= 1) edgeVert[x + (y - 1) * srcImage.Width] = 0;
                    }
                }
            }
        }

        //--------------------------------------------------------------------------
        // ストロークを画像imgDataに載せる
        //--------------------------------------------------------------------------
        void RenderStrokes(Color[] imgData, Vector2[] appendingPath)
        {
            Stroke[] renderStrokes;
            if (appendingPath != null)
            {
                renderStrokes = new Stroke[renderStrokeNum + 1];
                strokes.CopyTo(0, renderStrokes, 0, renderStrokeNum);
                renderStrokes[renderStrokeNum] = new Stroke()
                {
                    FillColor = this.CurrentFillColor,
                    Path = appendingPath,
                    Radius = this.CurrentRadius
                };
            }
            else
            {
                renderStrokes = new Stroke[renderStrokeNum];
                strokes.CopyTo(0, renderStrokes, 0, renderStrokeNum);
            }

            srcData.CopyTo(imgData, 0);
            foreach (Stroke stroke in renderStrokes)
            {
                for (int i = 0; i < stroke.Path.Length; i++)
                {
                    Vector2 pt = stroke.Path[i];
                    int px = (int)pt.X;
                    int py = (int)pt.Y;
                    int r = stroke.Radius;
                    for (int y = py - r; y <= py + r; y++)
                    {
                        for (int x = px - r; x <= px + r; x++)
                        {
                            int dx = px - x;
                            int dy = py - y;
                            if (!(x < 0 || srcImage.Width <= x) &&
                                !(y < 0 || srcImage.Height <= y) &&
                                !(dx * dx + dy * dy > r * r))
                            {
                                imgData[x + y * srcImage.Width] = stroke.FillColor;
                            }
                        }
                    }
                }
            }
        }

        //--------------------------------------------------------------------------
        // ピクセル間の枝の重み（V_pq）をを可視化。デバッグ用
        //--------------------------------------------------------------------------
        void RenderEdgeWeight(Color[] data)
        {
            CalcEdgeWeights();
            for (int y = 0; y < srcImage.Height; y++)
            {
                for (int x = 0; x < srcImage.Width; x++)
                {
                    int i = x + y * srcImage.Width;
                    double weight = 0;
                    int cnt = 0;
                    if (x < srcImage.Width - 1 && y < srcImage.Height - 1)
                    {
                        weight += edgeHori[i];
                        cnt++;
                    }
                    if (x < srcImage.Width - 1 && y < srcImage.Height - 1)
                    {
                        weight += edgeVert[i];
                        cnt++;
                    }
                    weight = weight / cnt;
                    data[i].R = data[i].G = data[i].B = (byte)(weight / (1 + K) * 255);
                }
            }
        }

        //--------------------------------------------------------------------------
        // ストロークの追加・削除
        //--------------------------------------------------------------------------

        public void AddStroke(Vector2[] path)
        {
            if (strokes.Count <= renderStrokeNum)
            {
                strokes.Add(new Stroke()
                {
                    FillColor = this.CurrentFillColor,
                    Path = path,
                    Radius = this.CurrentRadius
                });
            }
            else
            {
                strokes[renderStrokeNum] = new Stroke()
                {
                    FillColor = this.CurrentFillColor,
                    Path = path,
                    Radius = this.CurrentRadius
                };
            }
            renderStrokeNum++;
            maxRenderStrokeNum = renderStrokeNum;
        }

        public void PopStroke()
        {
            renderStrokeNum = Math.Max(renderStrokeNum - 1, 0);
        }
        public void RestoreStroke()
        {
            renderStrokeNum = Math.Min(renderStrokeNum + 1, maxRenderStrokeNum);
        }
    }
}
