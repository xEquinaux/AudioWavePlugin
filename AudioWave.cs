﻿using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System.Diagnostics;
using System.Drawing;
using Color = System.Drawing.Color;

namespace AudioWavePlugin
{
    /// <summary>
    /// Interaction logic for AudioWave.xaml
    /// </summary>
    public partial class AudioWave : IDisposable
    {
        public static AudioWave Instance;
        internal Wave wave;
        internal static int Seed = 1;
        public static bool RenderColor = true;
        private Process update;
        private bool init = false;
        internal int Width, Height;
        public AudioWave()
        {
            Instance = this;
            wave = new Wave();
            Wave.defaultOutput = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        public void Dispose()
        {
            if (update != null && !update.HasExited)
            {
                update?.CloseMainWindow();
                update?.Close();
            }
            wave.Stop();
		}
    }
    public class Wave
    {
        internal WaveFileReader reader;
        private float[] data;
        private AudioWave Window;
        internal WasapiOut audioOut;
        public static MMDevice defaultOutput;
        public static MMDevice defaultInput;
        internal static WasapiLoopbackCapture LoopCapture;
        public WasapiCapture capture;
        public BufferedWaveProvider buffer;
        public bool monitor, once;
        public WasapiOut monitorOut;
        internal static int width = 1;
        internal static Wave Instance;
        internal MixingWaveProvider32 mixer = new MixingWaveProvider32();
        internal Square[] meter;
        internal Square[] square;
        internal const int MeterSize = 10;
        internal const int SquareSize = 20;
        //internal Bitmap texture = (Bitmap)Bitmap.FromFile(".\\Textures\\temp90.png");
        public Wave()
        {
            Window = AudioWave.Instance;
            Instance = this;
            Display(640, 480);
            //  Generate square meter style objects
            meter = new Square[(int)Window.Width / MeterSize];
            int num = MeterSize;
            for (int i = 0; i < meter.Length; i++)
            {
                meter[i] = Square.NewSquare(num += MeterSize, (int)Window.Height / 2, MeterSize, (int)Window.Height, 1f, 1d, 1f, Color.White);
            }
            LoopCapture = new WasapiLoopbackCapture(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice());
            LoopCapture.DataAvailable += LoopCapture_DataAvailable;
            graph = new BufferedWaveProvider(LoopCapture.WaveFormat);
            graph.DiscardOnBufferOverflow = true;
            return;
            square = new Square[(int)Window.Width / SquareSize];
            int num2 = SquareSize;
            for (int i = 0; i < square.Length; i++)
            {
                square[i] = Square.NewSquare(num2 += SquareSize, (int)Window.Height / 2, SquareSize, (int)Window.Height, 1f, i * (Math.PI * 2d / square.Length), 1f, Color.White);
                square[i].texture = null;
            }
        }
        public void Stop(bool stopDeviceOut = false)
        {
            if (audioOut != null)
            {
                audioOut.Stop();
            }
            if (stopDeviceOut)
            {
                audioOut?.Dispose();
                audioOut = null;
            }
            reader?.Dispose();
            capture?.StopRecording();
            capture?.Dispose();
            record?.Dispose();
        }
        public void Init(WaveFileReader read, MMDevice output)
        {
            _Init(new WaveFileReader(read), output);
        }
        public void Init(string file, MMDevice output)
        {
            _Init(new WaveFileReader(file), output);
        }
        public void Init(Stream stream, MMDevice output)
        {
            stream.Position = 0;
            _Init(new WaveFileReader(stream), output);
        }
        public void Init(BufferedWaveProvider buff, MMDevice output)
        {
            data = _Buffer(0);
            if (audioOut != null)
            {
                audioOut.Dispose();
                audioOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
                audioOut.Init(buff);
                audioOut.Play();
            }
        }
        private void _Init(WaveFileReader read, MMDevice output)
        {
            reader = read;
            data = _Buffer(0);
            if (audioOut != null)
            {
                audioOut.Dispose();
            }
            audioOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
            audioOut.Init(reader);
            audioOut.Play();
        }
        public WaveRecorder record;
        public void InitAux(MMDevice output)
        {
            if (monitorOut != null)
                monitorOut.Dispose();
            try
            {
                monitorOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
            }
            catch
            {
                monitorOut = new WasapiOut();
            }
            if (buffer != null)
                monitorOut.Init(buffer);
        }
        public void InitCapture(MMDevice input)
        {
            if (input == null)
                input = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            capture = new WasapiCapture(input, false);

            buffer = new BufferedWaveProvider(capture.WaveFormat);
            buffer.DiscardOnBufferOverflow = true;

            graph = new BufferedWaveProvider(capture.WaveFormat);
            graph.DiscardOnBufferOverflow = true;

            filter = new BiQuadFilter[capture.WaveFormat.Channels, 8];

            capture.ShareMode = AudioClientShareMode.Shared;
            capture.StartRecording();
            capture.DataAvailable += Capture_DataAvailable;
        }

        private void LoopCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            graph.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        public static bool update = false;
        public bool playback;
        internal static BufferedWaveProvider graph;
        public static float[] eq = new float[8];
        private BiQuadFilter[,] filter = new BiQuadFilter[,] { };
        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            float[] read = new float[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, read, 0, e.BytesRecorded);
            Update();
            for (int j = 0; j < read.Length; j++)
            {
                for (int band = 0; band < filter.GetLength(1); band++)
                {
                    int ch = j % capture.WaveFormat.Channels;
                    if (filter[ch, band] != null)
                    {
                        read[j] = filter[ch, band].Transform(read[j]);
                    }
                }
            }
            byte[] buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(read, 0, buffer, 0, e.BytesRecorded);
            graph.AddSamples(buffer, 0, e.BytesRecorded);
            Monitor(buffer, e.BytesRecorded);
        }

        private void Monitor(byte[] Buffer, int length)
        {
            if (monitor)
            {
                buffer.AddSamples(Buffer, 0, length);
                if (playback)
                {
                    if (audioOut.PlaybackState != PlaybackState.Playing)
                    {
                        if (!once)
                        {
                            InitAux(defaultOutput);
                            once = true;
                        }
                        monitorOut.Play();
                        playback = false;
                    }
                    playback = false;
                    return;
                }
            }
            else if (monitorOut != null)
            {
                monitorOut.Stop();
            }
        }
        public byte[] GetSamples(float[] samples, int sampleCount)
        {
            var pcm = new byte[sampleCount * 2];
            int sampleIndex = 0,
                pcmIndex = 0;

            while (sampleIndex < sampleCount)
            {
                var outsample = (short)(samples[sampleIndex] * short.MaxValue);
                pcm[pcmIndex] = (byte)(outsample & 0xff);
                pcm[pcmIndex + 1] = (byte)((outsample >> 8) & 0xff);

                sampleIndex++;
                pcmIndex += 2;
            }
            return pcm;
        }

        private void Update()
        {
            if (!update)
                return;
            int[] freq = new int[]
            {
                100, 200, 400, 800, 1200, 2400, 4800, 9600
            };
            var rate = capture.WaveFormat.SampleRate;
            for (int bandIndex = 0; bandIndex < filter.GetLength(1); bandIndex++)
            {
                for (int n = 0; n < capture.WaveFormat.Channels; n++)
                {
                    if (filter[n, bandIndex] == null)
                        filter[n, bandIndex] = BiQuadFilter.PeakingEQ(rate, freq[bandIndex], 0.8f, eq[bandIndex]);
                    else
                        filter[n, bandIndex].SetPeakingEq(rate, freq[bandIndex], 0.8f, eq[bandIndex]);
                }
            }
            update = false;
        }
        bool flag = false;
        EventHandler method;
        AutoResetEvent wait = new AutoResetEvent(false);
        public static int Fps = 1000 / 120;
        public static bool style = false;
        public Bitmap Display(int width, int height)
        {
            //if (flag) return Game1.MagicPixel;
            //flag = true;
            if (reader != null && audioOut.PlaybackState == PlaybackState.Playing || capture != null && capture.CaptureState == CaptureState.Capturing || LoopCapture != null && LoopCapture.CaptureState == CaptureState.Capturing)
            {
                return GenerateImage(width, height);
            }
            return new Bitmap(width, height);
        }
        PointF[] oldPoints = new PointF[] { };
        private Bitmap GenerateImage(int width, int height)
        {
            int verticalOffY = 15;    //  For moving the entire graph vertically
            int stride = width * ((/*PixelFormats.Bgr24.BitsPerPixel*/24 + 7) / 8);
            Bitmap bmp = new Bitmap(width, height);
            {
                using (Graphics graphic = Graphics.FromImage(bmp))
                {
                    graphic.FillRectangle(System.Drawing.Brushes.Black, new System.Drawing.Rectangle(0, 0, width, height));

                    data = _Buffer(width);

                    float num = data.Max();
                    float num2 = data.Min();
                    float num3 = data.Average();
                    int[] indexArray = new int[3];
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (num == data[i])
                            indexArray[0] = i;
                        if (num2 == data[i])
                            indexArray[1] = i;
                        if (num3 == data[i])
                            indexArray[2] = i;
                    }
                    int length = indexArray.Max() - indexArray.Min();
                    if (length + indexArray[2] < width)
                        length += indexArray[2];

                    PointF[] points = new PointF[width];
                    if ((capture != null && capture.CaptureState != CaptureState.Capturing) || (reader != null && audioOut.PlaybackState == PlaybackState.Playing))
                    {
                        for (int i = 0; i < points.Length; i += points.Length / Math.Max(length, 1))
                        {
                            float y = height / 2 * (float)(data[i] * (style ? Math.Sin((float)i / width * Math.PI) : 1f)) + height / 2;
                            points[i] = new PointF(Math.Min(i, points.Length), y);
                        }
                        PointF begin = new PointF();
                        bool flag = false;
                        int num4 = 0;
                        for (int i = 1; i < points.Length; i++)
                        {
                            if (points[i] == default(PointF) && !flag)
                            {
                                begin = points[i - 1];
                                num4 = i;
                                flag = true;
                            }
                            if ((points[i] != default(PointF) || i == points.Length - 2) && flag)
                            {
                                for (int j = num4; j < i; j++)
                                {
                                    points[j] = new PointF(begin.X, begin.Y);
                                }
                                flag = false;
                            }
                        }
                        for (int i = points.Length - 1; i >= 0; i--)
                        {
                            if (points[i].X == 0f)
                                points[i].X = i;
                            if (points[i].Y == 0f)
                                points[i].Y = points[i - 1].Y;
                            points[i].Y -= verticalOffY;
                        }
                        points[points.Length - 1] = points[points.Length - 2];
                    }
                    else if (reader == null)
                    {
                        //data = LiveBuffer();
                        //for (int i = 0; i < points.Length; i += points.Length / Math.Max(length, 1))
                        for (int i = 0; i < points.Length; i++)
                        {
                            float y = height / 2 * (float)(data[i] * (style ? Math.Sin((float)i / width * Math.PI) : 1f)) + height / 2;
                            points[i] = new PointF(Math.Min(i, points.Length), y);
                        }
                        //for (int i = 0; i < points.Length; i++)
                        //{
                        //    points[i] = new PointF(i, height / 2 * data[i] + height / 2);
                        //}
                    }
                    //if (AuxWindow.CircularStyle && !AuxWindow.SquareStyle)
                    //    points = CircleEffect(points);
                    //if (AuxWindow.SquareStyle && !AuxWindow.CircularStyle)
                    //{
                        for (int n = 0; n < meter.Length; n++)
                        {
                            int half = (int)Window.Height / 2;
                            int x = n * MeterSize;
                            int w = (int)(half - points[x].Y) - verticalOffY;
                            int _y = (int)Math.Min(points[x].Y, half);
                            if (_y == half)
                            {
                                w = (int)points[x].Y - half;
                            }
                            w = Math.Max(0, w);
                            meter[n].x = x;
                            if (_y == half)
                            {
                                meter[n].color = Color.DeepSkyBlue;
                            }
                            else
                            {
                                meter[n].color = Color.Orange;
                            }
                            Square.SetAmplitude(meter[n], (w / (float)half) + 0.33f);
                            Square.Update(meter[n]);
                            Square.Draw(meter[n], _y, MeterSize, w + verticalOffY + 1, graphic);
                        }
                    //}
                    //else if (AuxWindow.CircularStyle && AuxWindow.SquareStyle)
                    //    MeterCircleEffect(points, graphic);
                    if (points.Length > 1)
                    {
                        if ((AudioWave.Seed += 10) >= int.MaxValue - 10)
                            AudioWave.Seed = 1;
                        var pen = new System.Drawing.Pen(System.Drawing.Brushes.White);
                        //var pen = Style.CosineColor(System.Drawing.Color.CornflowerBlue, DateTime.Now.Second * 3f);
                        pen.Width = Math.Min(Math.Max(Wave.width, 1), 12);
                        //if (AuxWindow.SquareStyle) { }
                        //else
                        //{
                        //    if (AuxWindow.CircularStyle)
                        //        graphic.DrawLines(pen, points);
                        //    else graphic.DrawCurve(pen, points);
                        //}
                        if (oldPoints.Length > 1 && points[0].Y == height / 2)
                        {
                            graphic.DrawCurve(pen, oldPoints);
                        }
                        else
                        { 
                            graphic.DrawCurve(pen, points);
                            oldPoints = points;
                        }
                    }
                }
                //  Render to WPF ImageSource object
                if (AudioWave.RenderColor)
                {
                    return bmp;
                }
                else
                {
                    return bmp;
                }
            }
        }
        private float[] LiveBuffer()
        {
            if (graph == null)
            {
                var format = defaultOutput.AudioClient.MixFormat;
                graph = new BufferedWaveProvider(new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels));
            }
            float[] buffer = new float[graph.BufferLength];
            graph.ToSampleProvider()?.Read(buffer, 0, buffer.Length);
            return buffer;
        }
        private float[] _Buffer(int length)
        {
            if (reader != null)
            {
                try
                {
                    long position = reader == null ? 0 : reader.Position;
                    float[] buffer = new float[length];
                    reader?.ToSampleProvider()?.Read(buffer, 0, buffer.Length);
                    reader.Position = position;
                    return buffer;
                }
                catch
                {
                    return new float[] { 0f };
                }
            }
            else return LiveBuffer();
        }
        private PointF[] CircleEffect(PointF[] points)
        {
            PointF[] output = new PointF[points.Length + 1];
            float fade = 1 / 24f;
            for (int i = 0; i < points.Length; i++)
            {
                bool flagIn = false;
                bool flagOut = false;
                if (flagIn = i < 24)
                    fade += 1 / 24;
                if (flagOut = i >= points.Length - 24)
                    fade -= 1 / 24f;
                float width = (float)this.Window.Width;
                float height = (float)this.Window.Height;
                float num = Math.Min(Math.Max(fade, 0.1f), 1f);
                float centerX = (float)width / 2f;
                float centerY = (float)height / 2f;
                float radius = centerY;                          //+ 1
                float x = centerX + (float)(radius / 3f * (data[i] + 1) * (flagIn || flagOut ? num : 1f) * Math.Cos(i / width * Math.PI * 2f));
                float y = centerY + (float)(radius / 3f * (data[i] + 1) * (flagIn || flagOut ? num : 1f) * Math.Sin(i / width * Math.PI * 2f));
                points[i] = new PointF(x, y);
            }
            Array.Copy(points, output, points.Length);
            output[points.Length] = points[0];
            return output;
        }
        private void MeterCircleEffect(PointF[] points, Graphics graphic)
        {
            float fade = 1f;//1 / 24f;
            int num2 = -1;
            for (int i = 0; i < points.Length; i++)
            {
                bool flagIn = false;
                bool flagOut = false;
                if (flagIn = i < 24)
                    fade += 1 / 24;
                if (flagOut = i >= points.Length - 24)
                    fade -= 1 / 24f;
                float width = (float)this.Window.Width;
                float height = (float)this.Window.Height;
                float num = Math.Min(Math.Max(fade, 0.1f), 1f);
                float centerX = (float)width / 2f;
                float centerY = (float)height / 2f;
                float radius = centerY;                          //+ 1
                float x = centerX + (float)(radius / 3f * (data[i] + 1) * (flagIn || flagOut ? num : 1f) * Math.Cos(i / width * Math.PI * 2f));
                float y = centerY + (float)(radius / 3f * (data[i] + 1) * (flagIn || flagOut ? num : 1f) * Math.Sin(i / width * Math.PI * 2f));
                points[i] = new PointF(x, y);

                if (i % SquareSize == 0)
                {
                    num2++;
                    if (num2 < square.Length)
                    {
                        float h = radius / 3f * (data[i] + 1);
                        Square.SetAmplitude(square[num2], h / (height / 2));
                        Square.Update(square[num2]);
                        Square.Draw(square[num2], (int)x, (int)y, (int)h, i / width * (float)Math.PI * 2f, graphic);
                    }
                }
            }
        }
    }
}
