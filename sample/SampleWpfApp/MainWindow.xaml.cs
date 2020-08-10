using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Elastic.Apm;
using Elastic.Apm.DiagnosticSource;

namespace SampleWpfApp
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private int _count = 0;
		public MainWindow()
		{
			Activity.DefaultIdFormat = ActivityIdFormat.Hierarchical;
			Activity.ForceDefaultIdFormat = true;

			Agent.Setup(new RumConfig().RumAgentComponents);
			Agent.Subscribe(new HttpDiagnosticsSubscriber());
			InitializeComponent();
		}

		private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
		{
			await Agent.Tracer.CaptureTransaction("ButtonClick", "WPFSample",
				async () =>
				{
					var httpClient = new HttpClient();
					var res = await httpClient.GetAsync("http://localhost:5050/api/Values");

				});

			_count++;
			Label.Content = _count;
		}
	}
}
