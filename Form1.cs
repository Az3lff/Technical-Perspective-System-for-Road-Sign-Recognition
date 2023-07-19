using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using System.Windows.Forms;
using static OpenCvSharp.Stitcher;

namespace Lab04_TechVis_Test03
{
	public partial class Form1 : Form
	{
		bool play, pause, filterEnabled;
		int selectIndex = 0;

		List<Label> labels = new List<Label>();

		List<string> signName = new List<string> { "Stop", "Forward", "Turn left", "Brick", "Turn Right", "U-turn", "Transition" };

		VideoCapture capture;
		Mat frame;
		Bitmap image, bmpBuffer, bitmapBlackWhiteMask; 

		OpenCvSharp.Point pointMouse;

		Color colorKeyMin, colorKeyMax;

		OpenCvSharp.Point[][] contours;
		HierarchyIndex[] hierarchy;
		OpenCvSharp.Rect rect;
		List<OpenCvSharp.Rect> rectList;

		public Form1()
		{
			InitializeComponent();

			labels.AddRange(new List<Label> {labelLB1, labelLB2, labelLB3, labelUB1, labelUB2, labelUB3});

			pictureBox1.MouseMove += onMouseMove;

			comboBox1.SelectedIndex = 0;
			comboBox2.SelectedIndex = 1;
		}

		private void onMouseMove(object sender, MouseEventArgs e)
		{
			if (chBoxReadingPixels.Checked)
				pointMouse = new OpenCvSharp.Point(e.X, e.Y);
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			try
			{
				Mat frame = new Mat();

				if (comboBox2.SelectedIndex == 0)
				{
					if(play)
						capture.Read(frame);

					if (pause)
					{
						pictureBox1.Image = bmpBuffer;
						frame = BitmapConverter.ToMat(new Bitmap(pictureBox1.Image));
					}
				}
				else
					frame = BitmapConverter.ToMat(image);

				if (!frame.Empty())
				{
					if (comboBox1.SelectedIndex == 0)
					{
						if (chBoxReadingPixels.Checked && pointMouse != null && !chBImageFiltering.Checked && !chBFindObject.Checked)
						{
							Vec3b pixelValue = frame.Get<Vec3b>(pointMouse.Y, pointMouse.X);
							byte b = pixelValue.Item0;
							byte r = pixelValue.Item1;
							byte g = pixelValue.Item2;
							string str = $"B: {b.ToString()} R: {r.ToString()} G: {g.ToString()}";
							frame.PutText(str, pointMouse, new HersheyFonts(), 0.5, new Scalar(0,0,0));
						}
						else if (chBImageFiltering.Checked && !chBoxReadingPixels.Checked && !chBFindObject.Checked)
						{
							for (int i = 0; i < frame.Width; i++)
							{
								for (int j = 0; j < frame.Height;j++)
								{
									Vec3b pixelValue = frame.Get<Vec3b>(j, i);
									byte b = pixelValue.Item0;
									byte r = pixelValue.Item1;
									byte g = pixelValue.Item2;

									if (b < blueMin.Value || b > blueMax.Value || r < redMin.Value || r > redMax.Value || g < greenMin.Value || g > greenMax.Value)
										frame.Set(j, i, new Vec3b(0, 0, 0));
								}
							}
						}
						else if (chBFindObject.Checked && !chBoxReadingPixels.Checked && !chBImageFiltering.Checked)
						{
							Mat findObjectMat = new Mat();
							Cv2.Canny(frame, findObjectMat, (int)numericUpDown1.Value, (int)numericUpDown2.Value);
							
							Cv2.FindContours(findObjectMat, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxTC89KCOS);
							rectList = new List<OpenCvSharp.Rect>();
							foreach (OpenCvSharp.Point[] contour in contours)
							{
								rect = Cv2.BoundingRect(contour);

								int max = (int)numericUpDownObjectMax.Value;
								int min = (int)numericUpDownObjectMin.Value;

								if (rect.Width > min && rect.Height > min && rect.Width < max && rect.Height < max)
								{
									rectList.Add(rect);
									
									frame.PutText(rect.Height.ToString() + " " + rect.Width.ToString(), new OpenCvSharp.Point(rect.X, rect.Y - 2), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 0, 0));
									frame.Rectangle(rect,  new Scalar(0, 0, 0));
								}
							}
						}
						else
						{
							chBoxReadingPixels.Checked = false;
							chBImageFiltering.Checked = false;
							chBFindObject.Checked = false;
						}
					}

					pictureBox1.Image = BitmapConverter.ToBitmap(imageMode(frame));
					pictureBox1.Invalidate();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		Mat imageMode(in Mat frameImage)
		{
			Mat mode = new Mat();

			switch (selectIndex)
			{
				case 0: // Normal
					mode = frameImage;
					break;

				case 1: // CvtColor
					Cv2.CvtColor(frameImage, mode, ColorConversionCodes.RGB2GRAY);
					break;

				case 2: // InRange
					Scalar lowerBound = new Scalar(trackBarLB1.Value, trackBarLB2.Value, trackBarLB3.Value);
					Scalar upperBound = new Scalar(trackBarUB1.Value, trackBarUB2.Value, trackBarUB3.Value);
					Cv2.InRange(frameImage, lowerBound, upperBound, mode);
					break;

				case 3: // Canny
					Cv2.Canny(frameImage, mode, trackBarLB1.Value, trackBarUB1.Value);
					break;

				case 4: // Filter2D
					Mat kernel = new Mat(3, 3, MatType.CV_32F, new float[] { -1, -1, -1, -1, 8, -1, -1, -1, -1 });
					Cv2.Filter2D(frameImage, mode, -1, kernel);
					break;

				case 5: // Smooth
					OpenCvSharp.Size kernelSize = new OpenCvSharp.Size(trackBarBlur.Value, trackBarBlur.Value);
					Cv2.Blur(frameImage, mode, kernelSize);
					break;
			}

			return mode;
		}

		private void btnPlayOrStop_Click(object sender, EventArgs e)
		{
			play = play ? false : true;
			pause = false;

			chBoxReadingPixels.Checked = false;

			if (play)
			{
				btnPlayOrStop.Text = "Stop";
				btnPlayOrStop.BackColor = Color.Red;

				capture = new VideoCapture(0);

				if (capture.IsOpened())
				{
					timer1.Enabled = true;
					timer1.Start();
				}
			}
			else
			{
				btnPlayOrStop.Text = "Play";
				btnPlayOrStop.BackColor = Color.YellowGreen;

				timer1.Stop();
				pictureBox1.Image = null;
				capture.Release();
			}
		}

		private void btnSelectPicture_Click(object sender, EventArgs e)
		{
			OpenFileDialog openDialog = new OpenFileDialog();
			openDialog.Filter = "Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG|All files (*.*)|*.*";

			timer1.Stop();

			if (openDialog.ShowDialog() == DialogResult.OK)
			{
				try
				{
					image = new Bitmap(openDialog.FileName);

					timer1.Enabled = true;
					timer1.Start();
				}
				catch
				{
					DialogResult rezult = MessageBox.Show("The selected file cannot be opened",
					"Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			label2.Visible = false;
			label3.Visible = false;
			label4.Visible = false;
			label5.Visible = false;
			label6.Visible = false;
			label7.Visible = false;

			labelLB1.Visible = false;
			labelLB2.Visible = false;
			labelLB3.Visible = false;
			labelUB1.Visible = false;
			labelUB2.Visible = false;
			labelUB3.Visible = false;

			trackBarLB1.Visible = false;
			trackBarLB2.Visible = false;
			trackBarLB3.Visible = false;
			trackBarUB1.Visible = false;
			trackBarUB2.Visible = false;
			trackBarUB3.Visible = false;

			trackBarBlur.Visible = false;
			labelBlur.Visible = false;
			label8.Visible = false;

			if (comboBox1.SelectedIndex == 0) 
				selectIndex = 0;
			else if (comboBox1.SelectedIndex == 1) 
				selectIndex = 1;
			else if (comboBox1.SelectedIndex == 2)
			{
				selectIndex = 2;

				label2.Visible = true;
				label3.Visible = true;
				label4.Visible = true;
				label5.Visible = true;
				label6.Visible = true;
				label7.Visible = true;

				labelLB1.Visible = true;
				labelLB2.Visible = true;
				labelLB3.Visible = true;
				labelUB1.Visible = true;
				labelUB2.Visible = true;
				labelUB3.Visible = true;

				trackBarLB1.Visible = true;
				trackBarLB2.Visible = true;
				trackBarLB3.Visible = true;
				trackBarUB1.Visible = true;
				trackBarUB2.Visible = true;
				trackBarUB3.Visible = true;
			}
			else if (comboBox1.SelectedIndex == 3)
			{
				selectIndex = 3;

				label2.Visible = true;
				label5.Visible = true;

				labelLB1.Visible = true;
				labelUB1.Visible = true;

				trackBarLB1.Visible = true;
				trackBarUB1.Visible = true;
			}
			else if (comboBox1.SelectedIndex == 4) 
				selectIndex = 4;
			else if (comboBox1.SelectedIndex == 5) 
			{
				selectIndex = 5;

				trackBarBlur.Visible = true;
				labelBlur.Visible = true;
				label8.Visible = true;
			}
		}

		private void trackBar_Scroll(object sender, EventArgs e)
		{
			for (int i = 0; i < labels.Count; i++)
				if ((sender as TrackBar).Name.ToString().Substring(8) == labels[i].Name.ToString().Substring(5))
					labels[i].Text = (sender as TrackBar).Value.ToString();
		}

		private void btnDefineImage_Click(object sender, EventArgs e)
		{
			if (chBFindObject.Checked && rect != null)
			{
				string[] maskFiles = Directory.GetFiles("Mask", "*.png");
				int tagCount = 0;
				flowLayoutPanel1.Controls.Clear();

				foreach (OpenCvSharp.Rect r in rectList)
				{
					Image image = pictureBox1.Image;
					Bitmap bitmap = new Bitmap(image);
					Bitmap croppedBitmap = bitmap.Clone(new Rectangle(r.X, r.Y, r.Width, r.Height), bitmap.PixelFormat);

					double bestSimilarity = 0;

					int bestIndex = 0;
					Mat bestImage = new Mat(), bestCropped = new Mat(), bestMask = new Mat();

					for (int i = 0; i < maskFiles.Length; i++)
					{
						Mat img = new Mat();
						Mat cropped = new Mat();
						Mat mask = Cv2.ImRead(maskFiles[i]);

						Cv2.Resize(BitmapConverter.ToMat(croppedBitmap), img, new OpenCvSharp.Size(mask.Size().Width, mask.Size().Height));
						Cv2.Resize(BitmapConverter.ToMat(croppedBitmap), cropped, new OpenCvSharp.Size(mask.Size().Width, mask.Size().Height));

						Cv2.CvtColor(img, img, ColorConversionCodes.BGR2GRAY);
						Cv2.CvtColor(mask, mask, ColorConversionCodes.BGR2GRAY);
						int thresholdValue = 127;
						int maxValue = 255;

						Cv2.Threshold(img, img, thresholdValue, maxValue, ThresholdTypes.BinaryInv);
						Cv2.Threshold(mask, mask, thresholdValue, maxValue, ThresholdTypes.Binary);
						Mat imgCmp = new Mat();
						Mat maskCmp = new Mat();

						img.ConvertTo(imgCmp, MatType.CV_32F);
						mask.ConvertTo(maskCmp, MatType.CV_32F);

						double sim = Cv2.CompareHist(imgCmp, maskCmp, HistCompMethods.Correl);

						double similarity = sim * 100;

						if (similarity > bestSimilarity && similarity < 100)
						{
							bestSimilarity = similarity;
							bestIndex = i;
							bestImage = img;
							bestCropped = cropped;
							bestMask = mask;
						}
					}

					if ((int)bestSimilarity > numericUpDown3.Value && (int)bestSimilarity < 100)
					{
						bestCropped.PutText($"{signName[bestIndex]} {(int)bestSimilarity}%", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheyComplex, 0.5, new Scalar(0, 0, 0, 255), 1);
						PictureBox picture = new PictureBox();
						picture.Size = new System.Drawing.Size(150, 150);
						picture.SizeMode = PictureBoxSizeMode.StretchImage;
						flowLayoutPanel1.Controls.Add(picture);
						picture.Image = BitmapConverter.ToBitmap(bestCropped);
						tagCount++;

						PictureBox picture2 = new PictureBox();
						picture2.Size = new System.Drawing.Size(150, 150);
						picture2.SizeMode = PictureBoxSizeMode.StretchImage;
						flowLayoutPanel1.Controls.Add(picture2);
						picture2.Image = BitmapConverter.ToBitmap(bestImage);
						tagCount++;

						PictureBox picture3 = new PictureBox();
						picture3.Size = new System.Drawing.Size(150, 150);
						picture3.SizeMode = PictureBoxSizeMode.StretchImage;
						flowLayoutPanel1.Controls.Add(picture3);
						picture3.Image = BitmapConverter.ToBitmap(bestMask);
						tagCount++;
					}
				}
			}
		}

		private void trackBarBlur_Scroll(object sender, EventArgs e)
		{
			labelBlur.Text = trackBarBlur.Value.ToString();
		}

		private void chBImageFiltering_CheckedChanged(object sender, EventArgs e)
		{
			filterEnabled = ((CheckBox)sender).Checked;
		}

		private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
		{
			btnPlayOrStop.Visible = false;
			btnSelectPicture.Visible = false;
			btnPause.Visible = false;

			if (comboBox2.SelectedIndex == 0)
			{
				btnPlayOrStop.Visible = true;
				btnPause.Visible = true;
			}
			else
			{
				btnSelectPicture.Visible = true;
				if (capture != null) capture.Release();
				btnPlayOrStop.Text = "Play";
				btnPlayOrStop.BackColor = Color.YellowGreen;
				play = false;
			}
		}

		private void btnPause_Click(object sender, EventArgs e)
		{
			capture.Release();
			pause = true;
			bmpBuffer = new Bitmap(pictureBox1.Image);
		}
	}
}
