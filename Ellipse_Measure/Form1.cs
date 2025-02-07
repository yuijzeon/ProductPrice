﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.Structure;

using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.Features2D;
using Emgu.CV.Aruco;
using ZedGraph;

namespace Oval_Measure
{
    public partial class Form1 : Form
    {  //宣告
        Image<Bgr, byte> input_Image;
        Image<Bgr, byte> ROI_Image;
        Image<Gray, byte> grayImage;
        Rectangle r_ROI = new Rectangle();
        Point Crosshair = new Point();
        bool b_ROI = new bool();
        bool b_ReferencePoint = new bool();
        int[] ROI_X = new int[2];
        int[] ROI_Y = new int[2];
        string[] Sol_Text = new string[3];

        private KeyEnterAction NextKeyEnterAction { get; set; }
        private ReferenceSetting ReferenceSetting { get; set; }
        private SquareGrid SquareGrid { get; set; }

        #region Form
        public Form1()
        {
            InitializeComponent();
            ReferenceSetting = new ReferenceSetting();
            SquareGrid = new SquareGrid(ReferenceSetting);
            SquareGrid.方形網格尺寸 = double.Parse(dTextBox.Text);
            SquareGrid.沖頭半徑 = double.Parse(pradTextBox.Text);
        }

        private void Form1_Load(object sender, EventArgs e) //載入；顯示語言中文or英文取決於語言的選擇
        {
            //init
            settingReferencePointToolStripMenuItem.Enabled = false;
            measureToolStripMenuItem.Enabled = false;
            if (menuStrip1.Text != "")//中英文介面的轉換
            {
                fileToolStripMenuItem.Text = "檔案"; //中英翻譯
                editToolStripMenuItem.Text = "編輯";
                toolToolStripMenuItem.Text = "工具";
                helpToolStripMenuItem.Text = "幫助";
                //saveToolStripMenuItem.Text = "儲存";
                openToolStripMenuItem.Text = "開啟檔案...";
                languageToolStripMenuItem.Text = "語言";
                settingReferencePointToolStripMenuItem.Text = "設定參考點";
                measureToolStripMenuItem.Text = "量測";
                versionToolStripMenuItem.Text = "版本";
                //saveAsToolStripMenuItem.Text = "另存新檔...";
                LongTextBox.Text = "長軸:";
                ShortTextBox.Text = "短軸:";
                AngleTextBox.Text = "角度:";

                chineseToolStripMenuItem1.Checked = true; //當chineseTool成立時將介面更改為中文
            }
            else
            {
                englishToolStripMenuItem1.Checked = true; //englishTool成立時將介面更改為英文
            }
            Sol_Text[0] = LongTextBox.Text; //定義橢圓長邊在Sol_Text[0]該陣列
            Sol_Text[1] = ShortTextBox.Text;//定義橢圓短邊在Sol_Text[1]該陣列
            Sol_Text[2] = AngleTextBox.Text;//定義橢圓角度在Sol_Text[2]該陣列
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();//紀錄上一次資料
        }
        #endregion

        #region Function
        private void chineseToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            menuStrip1.Text = "Ch";//Chinese:將介面更改為中文
            Application.Exit();
        }

        private void englishToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            menuStrip1.Text = "";//English:將介面更改為英文
            Application.Exit();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)  //載入圖片
        {
            OpenFileDialog filename = new OpenFileDialog();
            filename.Filter = "|*.jpg;*.bmp";//指定檔案類型
            if (filename.ShowDialog() == DialogResult.OK)
            {
                input_Image = new Image<Bgr, byte>(filename.FileName);
                pictureBox1.Image = input_Image.ToBitmap();
                sizeTextBox.Text = "Size: " + pictureBox1.Image.Size.Width.ToString() + ", " + pictureBox1.Image.Size.Height.ToString();

                //sizeTextBox.Text = size[0].ToString("f3");
            }
            if (input_Image != null)
            {
                settingReferencePointToolStripMenuItem.Enabled = true;
            }
        }

        private void settingReferencePointToolStripMenuItem_Click(object sender, EventArgs e) //執行校正
        {
            if (settingReferencePointToolStripMenuItem.Checked == false) //如判斷出尚未校正 則進入校正階段
            {
                settingReferencePointToolStripMenuItem.Checked = true;
                MessageBox.Show("請設定原點");
                NextKeyEnterAction = KeyEnterAction.SetReferenceOrigin;
            }
        }

        #endregion



        private void pictureBox1_MouseCaptureChanged(object sender, EventArgs e)
        {//針對滑鼠游標的捕捉來撰寫判斷式
            if (b_ROI == true)
            {
                input_Image.ROI = r_ROI;//設定ROI範圍
                ROI_Image = input_Image.Copy();//複製ROI
                //pictureBox1.Image = input_Image.ToBitmap();
                b_ROI = false;
                ContoursPoint();
                measureToolStripMenuItem.Checked = false;  //將量測功能設為不可使用
            }

        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {             //ROI:對感興趣區域進行處理可用於矩形，不規則多邊形，圓形，橢圓，手動指定形狀
            mouseTextBox.Text = e.X + ", " + e.Y;  //滑鼠選取的範圍將其定義為ROI並取得XY的世界座標
            ROI_X[0] = e.X;
            ROI_Y[0] = e.Y;
            if (b_ROI == true)
            {
                r_ROI.X = ROI_X.Min();
                r_ROI.Width = ROI_X.Max() - r_ROI.X;//X方向最大值減最小值可得到寬
                r_ROI.Y = ROI_Y.Min();
                r_ROI.Height = ROI_Y.Max() - r_ROI.Y;//Y方向最大值減最小值可得到高
                pictureBox1.Refresh();   //刷新pictureBox1
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (settingReferencePointToolStripMenuItem.Checked == true || NextKeyEnterAction != KeyEnterAction.Unknown) //針對校正功能的MouseDown
            {
                Crosshair.X = e.X; //校正時的十字線
                Crosshair.Y = e.Y;
                b_ReferencePoint = true;
                pictureBox1.Refresh(); //刷新pictureBox1
            }
            if (measureToolStripMenuItem.Checked == true) //針對量測功能的MouseDown
            {
                b_ROI = true; //當滑鼠游標捕捉的判斷式成立時定義世界座標
                ROI_X[1] = e.X;
                ROI_Y[1] = e.Y;
            }
        }

        #region DrawImage
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            SquareGrid.PaintOn(e.Graphics);

            if (b_ROI == true) //當此判斷式成立時將ROI(橢圓)以紅線選取
            {
                e.Graphics.DrawRectangle(new Pen(Brushes.Red, 2.0f), r_ROI);
            }
            if (b_ReferencePoint == true) //當此判斷式成立時顯示紅色叉叉(用於校正)
            {
                e.Graphics.DrawLine(new Pen(Brushes.Red, 2.0f), Crosshair.X - 5, Crosshair.Y - 5, Crosshair.X + 5, Crosshair.Y + 5);
                e.Graphics.DrawLine(new Pen(Brushes.Red, 2.0f), Crosshair.X - 5, Crosshair.Y + 5, Crosshair.X + 5, Crosshair.Y - 5);
            }
        }

        #endregion

        #region Vision
        void ContoursPoint()//抓輪廓
        {
            Image<Bgr, byte> output_Image = input_Image.Copy();
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            //External - 只提取最外層的輪廓
            //ChainApproxNone - 將所有點轉為點[]
            CvInvoke.FindContours(input_Image.Canny(128, 256), contours, null, RetrType.Tree, ChainApproxMethod.ChainApproxNone);//提取輪廓
            Point[][] P_contours = contours.ToArrayOfArray();//[contours.Size][0]
            double[] Sol_out = new double[5];
            double[] Sol_in = new double[5];
            bool b_in = false;
            for (int i = 0; i < P_contours.Length; i++)//迴路數量
            {
                if (CvInvoke.ContourArea(contours[i]) > 2000) //過濾輪廓面積
                {
                    double[] Contour_X = new double[P_contours[i].Length];
                    double[] Contour_Y = new double[P_contours[i].Length];
                    for (int j = 0; j < P_contours[i].Length; j++)//單迴路內所有點
                    {  //過濾完畢後傳入橢圓回歸公式之後再回傳
                        Contour_X[j] = Convert.ToDouble(P_contours[i][j].X);
                        Contour_Y[j] = Convert.ToDouble(P_contours[i][j].Y);
                        pictureBox1.CreateGraphics().FillRectangle(Brushes.Blue, P_contours[i][j].X + r_ROI.X, P_contours[i][j].Y + r_ROI.Y, 2, 2);
                    }
                    if (b_in) Sol_out = EillpseRes.EllipticRegressionAnalysis(Contour_X, Contour_Y);
                    else Sol_in = EillpseRes.EllipticRegressionAnalysis(Contour_X, Contour_Y);
                    b_in = true;                      //使用EillpseRes專案橢圓回歸公式
                }
            }
            double RTD = 180.0f / Math.PI; //角度計算
            double[] Sol = new double[5]; //實體化陣列
            for (int i = 0; i < 5; i++)
            {
                Sol[i] = (Sol_out[i] + Sol_in[i]) / 2; //因線粗關係，將橢圓外圈和內圈平均 使結果更趨近於現實
            }
            double[] rol = { Math.Cos(Sol[4]), -Math.Sin(Sol[4]), Math.Sin(Sol[4]), Math.Cos(Sol[4]) };
            LongTextBox.Text = Sol_Text[0] + " " + (Sol[2] * ReferenceSetting.XScale).ToString("f3"); //定義長軸
            ShortTextBox.Text = Sol_Text[1] + " " + (Sol[3] * ReferenceSetting.YScale).ToString("f3"); //定義短軸
            AngleTextBox.Text = Sol_Text[2] + " " + (-Sol[4] * RTD).ToString("f3");    //定義角度
        }



        #endregion

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (b_ReferencePoint == true) //設定原點 之後將X和Y的長度存入size[0]和size[1]中
            {
                if (e.KeyData == Keys.Up)
                {
                    Crosshair.Y -= 1;
                }
                if (e.KeyData == Keys.Down)
                {
                    Crosshair.Y += 1;
                }
                if (e.KeyData == Keys.Right)
                {
                    Crosshair.X += 1;
                }
                if (e.KeyData == Keys.Left)
                {
                    Crosshair.X -= 1;
                }
                if (e.KeyData == Keys.Enter)
                {
                    switch (NextKeyEnterAction)
                    {
                        case KeyEnterAction.SetReferenceOrigin:
                            ReferenceSetting.Origin = new Point(Crosshair.X, Crosshair.Y);
                            
                            MessageBox.Show("請設定X軸");
                            NextKeyEnterAction = KeyEnterAction.SetReferenceXRefer;
                            break;
                        
                        case KeyEnterAction.SetReferenceXRefer:
                            ReferenceSetting.XRefer = new Point(Crosshair.X, Crosshair.Y);
                            
                            var xLengthForm = new Form2("請輸入X軸長度");//輸入長度(中斷)
                            xLengthForm.ShowDialog();
                            MessageBox.Show("X軸長度" + xLengthForm.Value + "mm"); //顯示X軸長度
                            ReferenceSetting.XScale = xLengthForm.Value / PointHelper.Distance(ReferenceSetting.XRefer, ReferenceSetting.Origin) * 2;
                            
                            MessageBox.Show("請設定Y軸"); //↑定義陣列內容並執行點位的運算
                            NextKeyEnterAction = KeyEnterAction.SetReferenceYRefer;
                            break;
                        
                        case KeyEnterAction.SetReferenceYRefer:
                            ReferenceSetting.YRefer = new Point(Crosshair.X, Crosshair.Y);
                            
                            var yLengthForm = new Form2("請輸入Y軸長度");//輸入長度(中斷)
                            yLengthForm.ShowDialog();
                            MessageBox.Show("Y軸長度" + yLengthForm.Value + "mm"); //顯示X軸長度
                            ReferenceSetting.YScale = yLengthForm.Value / PointHelper.Distance(ReferenceSetting.YRefer, ReferenceSetting.Origin) * 2;
                            
                            
                            MessageBox.Show("參考點設定完成"); //↑定義陣列內容並執行點位的運算
                            ReferencePointTextBox.Text = ReferenceSetting.XScale.ToString("f3") + "," + ReferenceSetting.YScale.ToString("f3"); //回傳size陣列的字串
                            
                            settingReferencePointToolStripMenuItem.Checked = false;
                            measureToolStripMenuItem.Enabled = true;

                            NextKeyEnterAction = KeyEnterAction.Unknown;
                            break;
                        
                        case KeyEnterAction.SetSquareGridPoints:
                            SquareGrid.RawPoints.Add(new Point(Crosshair.X, Crosshair.Y));
                            break;
                    }
                    pictureBox1.Refresh();   //刷新pictureBox1
                }
                pictureBox1.Refresh();
            }
        }

        private void measureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            measureToolStripMenuItem.Checked = true;
        }

        private void cannyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pictureBox1.Image = input_Image.Canny(128, 256).ToBitmap();
        }

        private void thresholdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            grayImage = new Image<Gray, byte>(input_Image.Bitmap);
            CvInvoke.Threshold(grayImage, grayImage, 0, 256, ThresholdType.Otsu);
            pictureBox1.Image = grayImage.ToBitmap();
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void AngleTextBox_Click(object sender, EventArgs e)
        {

        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void measureSquareGridToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("請輸入4個點");
            NextKeyEnterAction = KeyEnterAction.SetSquareGridPoints;
        }

        private void endMeasureSquarePointSettingToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void dTextBox_TextChanged(object sender, EventArgs e)
        {
            var dTextBox = sender as ToolStripTextBox;

            if (double.TryParse(dTextBox.Text, out var d))
            {
                SquareGrid.方形網格尺寸 = d;
            }
            else
            {
                dTextBox.Text = SquareGrid.方形網格尺寸.ToString();
            }
        }

        private void pradTextBox_TextChanged(object sender, EventArgs e)
        {
            if (double.TryParse(pradTextBox.Text, out var prad))
            {
                SquareGrid.沖頭半徑 = prad;
            }
            else
            {
                pradTextBox.Text = SquareGrid.沖頭半徑.ToString();
            }
        }

        private void resetSquareGridMenuItem_Click(object sender, EventArgs e)
        {
            SquareGrid.ResetPoints();
            pictureBox1.Refresh();
        }

        private void calculateEpsoolStripMenuItem_Click(object sender, EventArgs e)
        {
            NextKeyEnterAction = KeyEnterAction.Unknown;
            PutEpsValueToLabel();
        }

        private void PutEpsValueToLabel()
        {
            var info = "";

            for (int i = 0; i < SquareGrid.RawPoints.Count; i++)
            {
                info += "x:" + SquareGrid.RefPoints[i].X + ", ";
                info += "y:" + SquareGrid.RefPoints[i].Y + ", ";
                info += "eps1:" + SquareGrid.Eps[i].Item1 + ", ";
                info += "eps2:" + SquareGrid.Eps[i].Item2 + "\n";
            }

            MessageBox.Show(info);
        }
    }

    internal class ReferenceSetting
    {
        public bool IsChecked { get; set; } = false;
        public Point Origin { get; set; }
        public Point XRefer { get; set; }
        public double XScale { get; set; }
        public Point YRefer { get; set; }
        public double YScale { get; set; }

        public PointD Convert(Point raw)
        {
            var delta = (double)(Origin.X * XRefer.Y + XRefer.X * YRefer.Y + YRefer.X * Origin.Y
                                 - (YRefer.X * XRefer.Y + Origin.X * YRefer.Y + XRefer.X * Origin.Y)) / 225;
            
            var aa = (YRefer.Y - Origin.Y) / delta;
            var bb = (Origin.X - YRefer.X) / delta;
            var cc = (YRefer.X * Origin.Y - Origin.X * YRefer.Y) / delta;
            var dd = (Origin.Y - XRefer.Y) / delta;
            var ee = (XRefer.X - Origin.X) / delta;
            var ff = (Origin.X * XRefer.Y - XRefer.X * Origin.Y) / delta;
            
            return new PointD
            {
                X = XScale * (aa * raw.X + bb * raw.Y + cc),
                Y = YScale * (dd * raw.X + ee * raw.Y + ff)
            };
        }
    }

    internal class SquareGrid
    {
        public SquareGrid(ReferenceSetting referenceSetting)
        {
            ReferenceSetting = referenceSetting;
        }

        private ReferenceSetting ReferenceSetting { get; set; }
        public List<Point> RawPoints { get; set; } = new List<Point>();
        public List<PointD> RefPoints => RawPoints.Select(p => ReferenceSetting.Convert(p)).ToList();
        
        public double 沖頭半徑 { get; set; }
        public double 方形網格尺寸 { get; set; }
        public List<(double, double)> Eps
        {
            get
            {
                var valueTuples = new List<(double, double)>();

                for (var i = 0; i < RefPoints.Count; i++)
                {
                    var pointA = i > 0 ? RefPoints[i - 1] : RefPoints[RefPoints.Count - 1];
                    var pointB = i < RefPoints.Count - 1 ? RefPoints[i + 1] : RefPoints[0];
                    valueTuples.Add(CalculateEps(RefPoints[i], pointA, pointB));
                }

                return valueTuples;
            }
        }

        internal void ResetPoints()
        {
            RawPoints.Clear();
        }

        private (double, double) CalculateEps(PointD point, PointD a, PointD b)
        {
            var deltaXa = a.X - point.X;
            var deltaYa = a.Y - point.Y;
            var deltaXb = b.X - point.X;
            var deltaYb = b.Y - point.Y;
            var d = Math.Sin(方形網格尺寸 / 2.0 / 沖頭半徑) * 沖頭半徑 * 2;
            var e11 = (Math.Pow(deltaXa, 2) + Math.Pow(deltaYa, 2) - Math.Pow(d, 2)) / (2 * Math.Pow(d, 2));
            var e22 = (Math.Pow(deltaXb, 2) + Math.Pow(deltaYb, 2) - Math.Pow(d, 2)) / (2 * Math.Pow(d, 2));
            var e12 = ((deltaXa * deltaXb) + (deltaYa * deltaYb)) / (2 * Math.Pow(d, 2));

            return (Math.Log(Math.Sqrt(1 + 2 * (e11 + e22) / 2 + Math.Sqrt(Math.Pow((e11 - e22) / 2, 2) + Math.Pow(e12, 2)))),
                Math.Log(Math.Sqrt(1 + 2 * (e11 + e22) / 2 - Math.Sqrt(Math.Pow((e11 - e22) / 2, 2) + Math.Pow(e12, 2)))));
        }

        internal void PaintOn(Graphics graphics)
        {
            foreach (var point in RawPoints)
            {
                graphics.DrawLine(new Pen(Brushes.Green, 2.0f), (float)point.X - 5, (float)point.Y - 5, (float)point.X + 5, (float)point.Y + 5);
                graphics.DrawLine(new Pen(Brushes.Green, 2.0f), (float)point.X - 5, (float)point.Y + 5, (float)point.X + 5, (float)point.Y - 5);
            }
        }
    }

    internal enum KeyEnterAction
    {
        Unknown,
        SetReferenceOrigin,
        SetReferenceXRefer,
        SetReferenceYRefer,
        SetSquareGridPoints
    }
}
