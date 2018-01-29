// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Utility.Signing
{
    internal sealed class SigningTestServerStartup : IStartup
    {
        private readonly ConcurrentDictionary<string, IHttpResponder> _responders;
        private Lazy<Uri> _url;

        internal Uri Url => _url?.Value;

        internal SigningTestServerStartup()
        {
            _responders = new ConcurrentDictionary<string, IHttpResponder>();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            // The actual port isn't known until later so defer retrieval until it is.
            _url = new Lazy<Uri>(() =>
            {
                var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();

                if (serverAddressesFeature == null)
                {
                    throw new InvalidOperationException();
                }

                return new Uri(serverAddressesFeature.Addresses.Single());
            });

            app.MapWhen(
                context =>
                {
                    if (context.Request.Path.HasValue)
                    {
                        var path = GetBaseAbsolutePath(context.Request.Path);

                        return _responders.ContainsKey(path);
                    }

                    return false;
                },
                a => a.Run(async context =>
                {
                    var path = GetBaseAbsolutePath(context.Request.Path);

                    try
                    {
                        await _responders[path].RespondAsync(context);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }));
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            return services.BuildServiceProvider();
        }

        internal IDisposable RegisterResponder(IHttpResponder responder)
        {
            return new Responder(_responders, responder.Url.AbsolutePath, responder);
        }

        private static string GetBaseAbsolutePath(PathString path)
        {
            return path.Value.Substring(0, path.Value.IndexOf('/', 1) + 1);
        }

        private sealed class Responder : IDisposable
        {
            private readonly ConcurrentDictionary<string, IHttpResponder> _responders;
            private readonly string _key;
            private readonly IHttpResponder _responder;

            internal Responder(ConcurrentDictionary<string, IHttpResponder> responders, string key, IHttpResponder responder)
            {
                _responders = responders;
                _key = key;
                _responder = responder;
                _responders[key] = responder;
            }

            public void Dispose()
            {
                IHttpResponder value;

                _responders.TryRemove(_key, out value);
            }
        }
    }
}