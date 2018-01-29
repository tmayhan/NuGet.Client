// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Utility.Signing
{
    public sealed class SigningTestServer : ISigningTestServer, IDisposable
    {
        private readonly SigningTestServerStartup _startup;
        private readonly IWebHost _webHost;

        private bool _isDisposed;

        public Uri Url => _startup.Url;

        private SigningTestServer(IWebHost webHost, SigningTestServerStartup startup)
        {
            _webHost = webHost;
            _startup = startup;
        }

        private SigningTestServer()
        {
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _webHost.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        public IDisposable RegisterResponder(IHttpResponder responder)
        {
            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }

            return _startup.RegisterResponder(responder);
        }

        public static SigningTestServer Create()
        {
            var startup = new SigningTestServerStartup();
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IStartup>(startup);
                })
                .UseSetting(WebHostDefaults.ApplicationKey, nameof(SigningTestServerStartup))
                .UseKestrel()
                .UseUrls("http://127.0.0.1:0"); // automatically pick the port
            var host = builder.Build();

            host.Start();

            return new SigningTestServer(host, startup);
        }
    }
}