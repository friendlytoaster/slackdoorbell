using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.Devices.Gpio;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SlackDoorbell
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
		private bool blink = false;
		private bool buttonEnabled = true;
		private int ringCounter = 0;
		private const int LED_PIN = 6;
		private const int BUTTON_PIN = 5;
		private GpioPin ledPin;
		private GpioPin buttonPin;
		private DispatcherTimer lockoutTimer;
		private DispatcherTimer clockUpdateTimer;
		private DispatcherTimer animTimer;
		private DispatcherTimer blinkTimer;

		private BitmapImage hexagonImage;

		private Random random = new Random();

		public MainPage()
        {
            this.InitializeComponent();
			InitGPIO();

			lockoutTimer = new DispatcherTimer();
			lockoutTimer.Interval = TimeSpan.FromSeconds(45);
			lockoutTimer.Tick += Timer_Tick;

			blinkTimer = new DispatcherTimer();
			blinkTimer.Interval = TimeSpan.FromSeconds(0.5);
			blinkTimer.Tick += BlinkTimer_Tick;
			blinkTimer.Start();

			clockUpdateTimer = new DispatcherTimer();
			clockUpdateTimer.Interval = TimeSpan.FromSeconds(60);
			clockUpdateTimer.Tick += ClockUpdateTimer_Tick;
			clockUpdateTimer.Start();

			ClockUpdateTimer_Tick(this, EventArgs.Empty);

			hexagonImage = new BitmapImage();
			hexagonImage.UriSource = new Uri("ms-appx:///Assets/hexagon.png", UriKind.RelativeOrAbsolute);

			animTimer = new DispatcherTimer()
			{
				Interval = TimeSpan.FromSeconds(25),
			};
			animTimer.Tick += AnimTimer_Tick;
			animTimer.Start();
		}

		private void AnimTimer_Tick(object sender, object e)
		{
			CreateHexagon();
		}

		private void CreateHexagon()
		{
			Image image = new Image()
			{
				Source = hexagonImage,
				Width = RootLayout.ActualWidth * 0.25,
				Height = RootLayout.ActualWidth * 0.25,
				Opacity = 0.4f
			};

			int w = (int) (RootLayout.ActualWidth * 0.75f);
			int h = (int) (RootLayout.ActualHeight * 0.75f);
			float x = (float) (random.Next(w) - h / 2);
			float y = (float) (random.Next(w) - h / 2);
			float angle = random.Next(360);
			TransformGroup group = new TransformGroup();
			group.Children.Add(new RotateTransform()
			{
				Angle = angle,
				CenterX = image.Width / 2,
				CenterY = image.Height / 2
			});
			group.Children.Add(new TranslateTransform()
			{
				X = x,
				Y = y
			});
			image.RenderTransform = group;

			Grid layout = RootLayout;

			Storyboard s = new Storyboard();
			s.Completed += (sender, e) =>
			{
				layout.Children.Remove(image);
			};

			TimeSpan duration = TimeSpan.FromSeconds(30);

			DoubleAnimationUsingKeyFrames alphaAnim = new DoubleAnimationUsingKeyFrames();
			alphaAnim.KeyFrames.Add(new EasingDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)), Value = 0 });
			alphaAnim.KeyFrames.Add(new EasingDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration.TotalSeconds * 0.2)), Value = 0.25 });
			alphaAnim.KeyFrames.Add(new EasingDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration.TotalSeconds * 0.8)), Value = 0.25 });
			alphaAnim.KeyFrames.Add(new EasingDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration.TotalSeconds)), Value = 0 });
			Storyboard.SetTarget(alphaAnim, image);
			Storyboard.SetTargetProperty(alphaAnim, "Opacity");
			s.Children.Add(alphaAnim);


			float dist = (float) RootLayout.ActualWidth;


			DoubleAnimation xAnim = new DoubleAnimation();
			xAnim.Duration = new Duration(duration);
			xAnim.To =  x + dist * Math.Cos(angle);
			Storyboard.SetTarget(xAnim, image);
			Storyboard.SetTargetProperty(xAnim, "(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.X)");
			s.Children.Add(xAnim);

			DoubleAnimation yAnim = new DoubleAnimation();
			yAnim.Duration = new Duration(duration);
			yAnim.To = y + dist * Math.Sin(angle);
			Storyboard.SetTarget(yAnim, image);
			Storyboard.SetTargetProperty(yAnim, "(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)");
			s.Children.Add(yAnim);

			DoubleAnimation rotAnim = new DoubleAnimation();
			rotAnim.Duration = new Duration(duration);
			rotAnim.To = angle + 360 * (random.Next(100) <= 50? -1 : 1);
			Storyboard.SetTarget(rotAnim, image);
			Storyboard.SetTargetProperty(rotAnim, "(UIElement.RenderTransform).(TransformGroup.Children)[0].(RotateTransform.Angle)");
			s.Children.Add(rotAnim);

			s.Begin();


			RootLayout.Children.Add(image);
		}

		private void ClockUpdateTimer_Tick(object sender, object e)
		{
			var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				transform.X = random.Next(20) - 10;
				transform.Y = random.Next(20) - 10;
				timeLabel.Text = DateTime.Now.ToString("ddd h:mm tt");
			});
		}

		private void BlinkTimer_Tick(object sender, object e)
		{
			if (buttonEnabled)
			{
				blink = !blink;

				if (ledPin != null)
				{
					ledPin.Write(blink ? GpioPinValue.High : GpioPinValue.Low);
				}
			}
		}

		private void Timer_Tick(object sender, object e)
		{
			lockoutTimer.Stop();

			ringCounter = 0;
			buttonEnabled = true;

			var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				titleLabel.Text = "Welcome!";
				textLabel.Text = "Press the button on the counter to the left, and somebody will be here to help you shortly.";
				RootLayout.Background = Resources["DoorbellBlueBrush"] as Brush;
			});

			if (ledPin != null)
			{
				ledPin.Write(GpioPinValue.High);
			}
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			RingBell();
		}

		private async void RingBell()
		{
			// Play Sound
			try
			{
				SoundPlayer.Play();
			}
			catch (Exception x)
			{
			}

			RootLayout.Background = Resources["DoorbellGreyBrush"] as Brush;

			if (!buttonEnabled)
			{
				if (++ringCounter > 20)
				{
					titleLabel.Text = ":(";
					textLabel.Text = "The team has been notified, somebody will be here shortly.";
				}

				return;
			}

			titleLabel.Text = "Ding Dong!";
			textLabel.Text = "The team has been notified. Have a seat in the waiting area, and somebody will be out to see you shortly.";

			buttonEnabled = false;
			lockoutTimer.Start();

			if (ledPin != null)
			{
				ledPin.Write(GpioPinValue.Low);
			}

			// Post Slack Message
			HttpClient client = new HttpClient();
			String uriString = "<insert slack webhook here>";
			var cameraUrl = "<insert URL to camera>";
			var json = "{\"text\": \"Ding Dong <!here>! Somebody is at the Front Door. <"+cameraUrl+"|Click here to see who it is.>\"}";
			var body = new HttpStringContent(json);
			
			var response = await client.PostAsync(new Uri(uriString), body);
		}

		private void InitGPIO()
		{
			var gpio = GpioController.GetDefault();

			// Show an error if there is no GPIO controller
			if (gpio == null)
			{
				return;
			}

			buttonPin = gpio.OpenPin(BUTTON_PIN);
			ledPin = gpio.OpenPin(LED_PIN);

			// Initialize LED to the OFF state by first writing a HIGH value
			// We write HIGH because the LED is wired in a active LOW configuration
			ledPin.Write(GpioPinValue.High);
			ledPin.SetDriveMode(GpioPinDriveMode.Output);

			// Check if input pull-up resistors are supported
			if (buttonPin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
				buttonPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
			else
				buttonPin.SetDriveMode(GpioPinDriveMode.Input);

			// Set a debounce timeout to filter out switch bounce noise from a button press
			buttonPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);

			// Register for the ValueChanged event so our buttonPin_ValueChanged 
			// function is called when the button is pressed
			buttonPin.ValueChanged += buttonPin_ValueChanged;

			ringButton.Visibility = Visibility.Collapsed;
		}

		private void buttonPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
		{
			// need to invoke UI updates on the UI thread because this event
			// handler gets invoked on a separate thread.
			var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				if (e.Edge == GpioPinEdge.FallingEdge)
				{
					// Button Pressed
					RingBell();
				}
			});
		}

		private void SoundPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine(e);
		}

		private void SoundPlayer_MediaEnded(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine(e);
		}
	}
}
