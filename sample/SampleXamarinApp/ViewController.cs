using Foundation;
using System;
using UIKit;
using System.Net.Http;
using Elastic.Apm;

namespace SampleXamarinApp
{
	public partial class ViewController : UIViewController
	{
		private int _counter = 0;
		public ViewController(IntPtr handle) : base(handle)
		{
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			// Perform any additional setup after loading the view, typically from a nib.
		}

		public override void DidReceiveMemoryWarning()
		{
			base.DidReceiveMemoryWarning();
			// Release any cached data, images, etc that aren't in use.
		}

		partial void UIButtonquXaEFwt_TouchUpInside(UIButton sender)
		{
			Agent.Tracer.CaptureTransaction("ButtonClicked", "Xamarin", async () =>
			{
				var httpClient = new HttpClient();
				//TODO add header
				await httpClient.GetAsync("http://localhost:5050/api/Values");
				
			});

			_counter++;
			myLabel.Text = _counter.ToString();
		}
	}
}