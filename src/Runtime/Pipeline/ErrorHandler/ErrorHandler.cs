﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Globalization;
using Aliyun.MQ.Runtime.Internal;

namespace Aliyun.MQ.Runtime.Pipeline.ErrorHandler
{
    public class ErrorHandler : PipelineHandler
    {
        private IDictionary<Type, IExceptionHandler> _exceptionHandlers;

        public IDictionary<Type, IExceptionHandler> ExceptionHandlers
        {
            get
            {
                return _exceptionHandlers;
            }
        }

        public ErrorHandler()
        {
            _exceptionHandlers = new Dictionary<Type, IExceptionHandler>
            {
                {typeof(WebException), new WebExceptionHandler()},
                {typeof(HttpErrorResponseException), new HttpErrorResponseExceptionHandler()}
            };
        }

        public override void InvokeSync(IExecutionContext executionContext)
        {
            try
            {
                base.InvokeSync(executionContext);
                return;
            }
            catch (Exception exception)
            {
                DisposeReponse(executionContext.ResponseContext);
                bool rethrowOriginalException = ProcessException(executionContext, exception);
                if (rethrowOriginalException)
                {
                    throw;
                }
            }
        }

        protected override void InvokeAsyncCallback(IAsyncExecutionContext executionContext)
        {
            // Unmarshall the response if an exception hasn't occured
            var exception = executionContext.ResponseContext.AsyncResult.Exception;
            if (exception != null)
            {
                DisposeReponse(executionContext.ResponseContext);
                try
                {
                    bool rethrowOriginalException = ProcessException(ExecutionContext.CreateFromAsyncContext(executionContext),
                        exception);
                    if (rethrowOriginalException)
                    {
                        executionContext.ResponseContext.AsyncResult.Exception = exception;
                    }
                }
                catch (Exception ex)
                {
                    executionContext.ResponseContext.AsyncResult.Exception = ex;
                }
            }
            base.InvokeAsyncCallback(executionContext);
        }

        private static void DisposeReponse(IResponseContext responseContext)
        {
            if (responseContext.HttpResponse != null &&
                responseContext.HttpResponse.ResponseBody != null)
            {
                responseContext.HttpResponse.ResponseBody.Dispose();
            }
        }

        private bool ProcessException(IExecutionContext executionContext, Exception exception)
        {
            // Find the matching handler which can process the exception
            IExceptionHandler exceptionHandler = null;

            if (this.ExceptionHandlers.TryGetValue(exception.GetType(), out exceptionHandler))
            {
                return exceptionHandler.Handle(executionContext, exception);
            }

            // No match found, rethrow the original exception.
            var message = string.Format(CultureInfo.InvariantCulture,
                    "An unexpected exception {0} was thrown, caused by {1}", exception.GetType(), exception.Message);
            throw new AliyunServiceException(message, exception);
        }        
    }
}
