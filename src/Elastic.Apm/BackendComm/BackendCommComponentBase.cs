// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Apm.Api;
using Elastic.Apm.Config;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;

namespace Elastic.Apm.BackendComm
{
	internal abstract class BackendCommComponentBase : IDisposable
	{
		private const string ThisClassName = nameof(BackendCommComponentBase);

		protected readonly CancellationTokenSource CtsInstance;
		protected readonly HttpClient HttpClientInstance;

		private readonly string _dbgName;
		private readonly DisposableHelper _disposableHelper;
		private readonly bool _isEnabled;
		protected readonly IApmLogger Logger;
		private readonly ManualResetEventSlim _loopCompleted;
		private readonly ManualResetEventSlim _loopStarted;
		private readonly SingleThreadTaskScheduler _singleThreadTaskScheduler;

		internal BackendCommComponentBase(bool isEnabled, IApmLogger logger, string dbgDerivedClassName, Service service
			, IConfigSnapshot config, HttpMessageHandler httpMessageHandler = null, string dbgName = null, bool useSingleThreadTaskScheduler = true
		)
		{
			_dbgName = $"{ThisClassName} ({dbgDerivedClassName})";
			Logger = logger?.Scoped(ThisClassName + (dbgName == null ? "" : $" (dbgName: `{dbgName}')"));
			_isEnabled = isEnabled;

			if (!_isEnabled)
			{
				Logger.Debug()?.Log("Disabled - exiting without initializing any members used by work loop");
				return;
			}

			CtsInstance = new CancellationTokenSource();

			_disposableHelper = new DisposableHelper();

			if (useSingleThreadTaskScheduler)
			{
				_loopStarted = new ManualResetEventSlim();
				_singleThreadTaskScheduler = new SingleThreadTaskScheduler($"ElasticApm{dbgDerivedClassName}", logger);
			}

			_loopCompleted = new ManualResetEventSlim();
			HttpClientInstance = BackendCommUtils.BuildHttpClient(logger, config, service, _dbgName, httpMessageHandler);


		}

		protected abstract Task WorkLoopIteration();

		internal bool IsRunning => _singleThreadTaskScheduler == null || _singleThreadTaskScheduler.IsRunning;

		private void PostToInternalTaskScheduler(string dbgActionDesc, Func<Task> asyncAction
			, TaskCreationOptions taskCreationOptions = TaskCreationOptions.None
		)
		{
#pragma warning disable 4014
			// We don't pass any CancellationToken on purpose because in some case (for example work loop)
			// we wait for asyncAction to start so we should never cancel it before it starts
			if(_singleThreadTaskScheduler == null)
				Task.Factory.StartNew(asyncAction, CancellationToken.None /*, taskCreationOptions, _singleThreadTaskScheduler */);
			else
				Task.Factory.StartNew(asyncAction, CancellationToken.None, taskCreationOptions, _singleThreadTaskScheduler);
#pragma warning restore 4014
			Logger.Debug()?.Log("Posted {DbgTaskDesc} to internal task scheduler", dbgActionDesc);
		}

		protected void StartWorkLoop()
		{
			PostToInternalTaskScheduler("Work loop", WorkLoop, TaskCreationOptions.LongRunning);

			Logger.Debug()?.Log("Waiting for work loop started event...");
			if(_singleThreadTaskScheduler != null)
				_loopStarted.Wait();
			Logger.Debug()?.Log("Work loop started signaled");
		}

		private async Task WorkLoop()
		{
			Logger.Debug()?.Log("Signaling work loop started event...");

			if(_singleThreadTaskScheduler != null)
				_loopStarted.Set();

			await ExceptionUtils.DoSwallowingExceptions(Logger, async () =>
				{
					while (true) await WorkLoopIteration();
					// ReSharper disable once FunctionNeverReturns
				}
				, dbgCallerMethodName: ThisClassName + "." + DbgUtils.CurrentMethodName());

			Logger.Debug()?.Log("Signaling work loop completed event...");
			_loopCompleted.Set();
		}

		public void Dispose()
		{
			if (!_isEnabled)
			{
				Logger.Debug()?.Log("Disabled - nothing to dispose, exiting");
				return;
			}

			_disposableHelper.DoOnce(Logger, _dbgName, () =>
			{
				Logger.Debug()?.Log("Posting CtsInstance.Cancel() to internal TaskScheduler...");
				Task.Run(() =>
				{
					Logger.Debug()?.Log("Calling CtsInstance.Cancel()...");
					// ReSharper disable once AccessToDisposedClosure
					CtsInstance.Cancel();
					Logger.Debug()?.Log("Called CtsInstance.Cancel()");
				});
				Logger.Debug()?.Log("Posted CtsInstance.Cancel() to default (ThreadPool) TaskScheduler");

				Logger.Debug()
					?.Log("Waiting for loop to exit... Is cancellation token signaled: {IsCancellationRequested}"
						, CtsInstance.Token.IsCancellationRequested);
				_loopCompleted.Wait();

				Logger.Debug()?.Log("Disposing _singleThreadTaskScheduler ...");
				_singleThreadTaskScheduler.Dispose();

				Logger.Debug()?.Log("Disposing HttpClientInstance...");
				HttpClientInstance.Dispose();

				Logger.Debug()?.Log("Disposing CtsInstance...");
				CtsInstance.Dispose();

				Logger.Debug()?.Log("Exiting...");
			});
		}

		protected void ThrowIfDisposed()
		{
			if (_disposableHelper.HasStarted) throw new ObjectDisposedException( /* objectName: */ _dbgName);
		}
	}
}
