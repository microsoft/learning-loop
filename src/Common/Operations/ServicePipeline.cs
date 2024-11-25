// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer.Operations
{
    /// <summary>
    /// Derives a cancellation token linked to its caller to manage lifecycle.
    /// </summary>
    public abstract class ServicePipeline : IDisposable
    {
        public virtual bool IsStarted => Completion != null;

        public Task Completion { get; private set; }

        protected CancellationTokenSource internalCancellationTokenSource;
        protected CancellationToken internalCancellationToken;

        protected int shutDownTimeout = 3 * 60 * 1000; // 3 minutes
        protected bool isDisposed;

        private CancellationToken parentCancellationToken;

        public void Start(CancellationToken cancellationToken)
        {
            if (IsStarted)
            {
                return;
            }

            this.parentCancellationToken = cancellationToken;
            internalCancellationTokenSource = CreateLinkedCancellationTokenSource();
            internalCancellationToken = internalCancellationTokenSource.Token;

            Completion = ExecuteAsync();
        }

        // the cancellation token passed to StopAsync is to determine the timespan
        // before ungraceful shutdown, ie. passing a token with a 5 second timeout
        // will force shutdown after 5 seconds, even if Completion is not complete.
        // Default token passed by hosted service executor is 5 seconds.
        public virtual async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!IsStarted)
            {
                return;
            }
            internalCancellationTokenSource?.Cancel();

            await Task.WhenAny(Completion, Task.Delay(-1, cancellationToken));

            Completion = null;
            internalCancellationTokenSource?.Dispose();
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    internalCancellationTokenSource?.Dispose();
                }
                isDisposed = true;
            }
        }

        ~ServicePipeline()
        {
            Dispose(false);
        }

        protected abstract Task ExecuteAsync();

        /// <summary>
        /// Resets the cancellation token source from the parent cancellation token.
        /// </summary>
        protected void ResetCancellationTokenSource()
        {
            this.internalCancellationTokenSource?.Cancel();
            this.internalCancellationTokenSource?.Dispose();
            this.internalCancellationTokenSource = CreateLinkedCancellationTokenSource();
            this.internalCancellationToken = internalCancellationTokenSource.Token;
        }

        private CancellationTokenSource CreateLinkedCancellationTokenSource()
        {
            return CancellationTokenSource.CreateLinkedTokenSource(this.parentCancellationToken);
        }
    }
}
