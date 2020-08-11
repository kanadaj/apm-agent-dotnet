using Foundation;
using System;
using UIKit;
using System.Net.Http;
using Elastic.Apm;
using Elastic.Apm.Api;

namespace SampleXamarinApp
{
	public partial class ViewController : UIViewController
	{
		private int _counter = 0;
		private ITransaction _viewLoadTransaction;
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

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);
			_viewLoadTransaction = Agent.Tracer.StartTransaction("Show ViewController", "ViewControllerLoad");
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);
			_viewLoadTransaction?.End();
		}

		partial void UIButtonquXaEFwt_TouchUpInside(UIButton sender)
		{
			Agent.Tracer.CaptureTransaction("ButtonClicked", "Xamarin", async (transaction) =>
			{
				await transaction.CaptureSpan("GET /api/values", ApiConstants.TypeExternal, async span =>
				{
					var uri = "http://localhost:5050/api/Values";
					span.Context.Http = new Http { Url = uri };

					var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5050/api/Values");
					request.Headers.Add("traceparent", transaction?.OutgoingDistributedTracingData.SerializeToString());
					var httpClient = new HttpClient();
					var res = await httpClient.SendAsync(request);

					span.Context.Http.StatusCode = (int)res.StatusCode;
					var stringRes = await res.Content.ReadAsStringAsync();
				});
			});

			_counter++;
			myLabel.Text = _counter.ToString();
		}
	}
}