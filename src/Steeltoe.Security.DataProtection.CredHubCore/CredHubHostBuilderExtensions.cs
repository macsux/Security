﻿// Copyright 2017 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Steeltoe.Security.DataProtection.CredHub;
using System;
using System.Threading.Tasks;

namespace Steeltoe.Security.DataProtection.CredHubCore
{
    public static class CredHubHostBuilderExtensions
    {
        /// <summary>
        /// Reach out to a CredHub server to interpolate credentials found in VCAP_SERVICES
        /// </summary>
        /// <param name="webHostBuilder">Your app's host builder</param>
        /// <param name="loggerFactory">To enable logging in the credhub client, pass in a loggerfactory</param>
        /// <returns>Your application's host builder with credentials interpolated</returns>
        public static IWebHostBuilder UseCredHubInterpolation(this IWebHostBuilder webHostBuilder, ILoggerFactory loggerFactory = null)
        {
            ILogger startupLogger = null;
            ILogger credhubLogger = null;
            if (loggerFactory != null)
            {
                startupLogger = loggerFactory.CreateLogger("Steeltoe.Security.DataProtection.CredHubCore");
                credhubLogger = loggerFactory.CreateLogger<CredHubClient>();
            }

            var vcapServices = Environment.GetEnvironmentVariable("VCAP_SERVICES");

            // don't bother interpolating if there aren't any credhub references
            if (vcapServices != null && vcapServices.Contains("credhub-ref"))
            {
                webHostBuilder.ConfigureAppConfiguration((context, config) =>
                {
                    var builtConfig = config.Build();
                    CredHubClient credHubClient = null;

                    var credHubOptions = builtConfig.GetSection("CredHubClient").Get<CredHubOptions>();
                    try
                    {
                        if (!string.IsNullOrEmpty(credHubOptions?.CredHubUser) && !string.IsNullOrEmpty(credHubOptions?.CredHubPassword))
                        {
                            startupLogger?.LogTrace("Using UAA auth for CredHub client");
                            credHubClient = CredHubClient.CreateUAAClientAsync(credHubOptions, credhubLogger).Result;
                        }
                        else
                        {
                            startupLogger?.LogTrace("Using mTLS auth for CredHub client");
                            credHubClient = CredHubClient.CreateMTLSClientAsync(credHubOptions ?? new CredHubOptions(), credhubLogger).Result;
                        }
                    }
                    catch (Exception e)
                    {
                        startupLogger?.LogCritical(e, "Failed to initialize CredHub client");
                    }

                    try
                    {
                        var interpolated = credHubClient.InterpolateServiceDataAsync(vcapServices).Result;
                        builtConfig.GetSection("vcap:services").Bind(interpolated);
                    }
                    catch (Exception e)
                    {
                        startupLogger?.LogCritical(e, "Failed to interpolate service data with CredHub");
                    }
                });
            }
            else
            {
                startupLogger?.LogInformation("No CredHub references found in VCAP_SERVICES");
            }

            return webHostBuilder;
        }
    }
}
