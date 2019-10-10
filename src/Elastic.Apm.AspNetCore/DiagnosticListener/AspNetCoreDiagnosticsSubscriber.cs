using System;
using System.Collections.Generic;
using Elastic.Apm.DiagnosticSource;
using Elastic.Apm.Helpers;

namespace Elastic.Apm.AspNetCore.DiagnosticListener
{
	/// <summary>
	/// Activates the <see cref="AspNetCoreDiagnosticListener" /> which enables
	/// capturing errors within an ASP.NET Core application.
	/// </summary>
	public class AspNetCoreDiagnosticsSubscriber : IDiagnosticsSubscriber
	{
		private readonly List<WildcardMatcher> _matcherList;

		internal AspNetCoreDiagnosticsSubscriber(List<WildcardMatcher> matcherList) => _matcherList = matcherList;

		/// <summary>
		/// Start listening for ASP.NET Core related diagnostic source events.
		/// </summary>
		public IDisposable Subscribe(IApmAgent agent)
		{
			var retVal = new CompositeDisposable();
			var subscriber = new DiagnosticInitializer(agent.Logger, new[] { new AspNetCoreDiagnosticListener(agent, _matcherList) });
			retVal.Add(subscriber);

			retVal.Add(System.Diagnostics.DiagnosticListener
				.AllListeners
				.Subscribe(subscriber));

			return retVal;
		}
	}
}
