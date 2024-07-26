using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NAudio.CoreAudioApi;
using TronBonne;
using TronBonne.UI;
using System.Drawing;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AudioWavePlugin
{
	[twitchbot.api.ApiVersion(0, 1)]
	public class Class1 : ChatInterface
	{
		public override Version Version => new Version(1, 0, 14, 2);
		public override int Priority => -1;
		public override string Name => "Audio Wave";

		public override void Initialize()
		{
		}

		public override bool Load()
		{
			new AudioWave();
			new Wave();
			return true;
		}

		public override bool LoadContent()
		{
			var v2 = Game1.Consolas.MeasureString("Loopback Capture");
			Button = new Button[]
			{
				new Button("Loopback Capture", new Rectangle(0, 0, (int)v2.X, (int)v2.Y), Color.Green)
					      { active = true, drawMagicPixel = true, innactiveDrawText = true }
			};
			return true;
		}

		public override void Update()
		{
			if (Button[0].LeftClick())
			{
				if (Wave.LoopCapture.CaptureState == CaptureState.Stopped)
				{ 
					Wave.LoopCapture.StartRecording();
				}
				else Wave.LoopCapture.StopRecording();
			}
		}
		
		public override void Draw(SpriteBatch sb)
		{
			if (Wave.LoopCapture.CaptureState == CaptureState.Capturing)
			{ 
				var rect = Game1.Instance.Window.ClientBounds;
				Bitmap bmp = Wave.Instance.Display(rect.Width, rect.Height);
				var tex = Pipeline.BitmapToTex2D(bmp, Game1.Instance.GraphicsDevice);
				sb.Draw(tex, Vector2.Zero, Color.White);
				bmp.Dispose();
				tex.Dispose();
			}
		}

		public override void Dispose()
		{
		}
	}
}
