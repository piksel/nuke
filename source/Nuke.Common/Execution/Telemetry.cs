// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Utilities;
using static Nuke.Common.ControlFlow;

namespace Nuke.Common.Execution
{
    internal static partial class Telemetry
    {
        // https://docs.microsoft.com/en-us/dotnet/core/tools/telemetry
        // https://dotnet.microsoft.com/platform/telemetry

        public const int CurrentVersion = 1;

        private const string InstrumentationKey = "05db1169-27f5-4d0c-bd17-5df446d18762";
        private const string OptOutEnvironmentKey = "NUKE_TELEMETRY_OPTOUT";
        private const string VersionPropertyName = "NukeTelemetryVersion";

        private static readonly TelemetryClient s_client;
        private static readonly int? s_confirmedVersion;

        static Telemetry()
        {
            s_confirmedVersion = SuppressErrors(CheckAwareness, includeStackTrace: true);
            var optoutParameter = EnvironmentInfo.GetParameter<string>(OptOutEnvironmentKey) ?? string.Empty;
            if (s_confirmedVersion == null ||
                optoutParameter == "1" ||
                optoutParameter.EqualsOrdinalIgnoreCase(bool.TrueString))
                return;

            var configuration = TelemetryConfiguration.CreateDefault();
            s_client = new TelemetryClient(configuration) { InstrumentationKey = InstrumentationKey };
            s_client.Context.Session.Id = Guid.NewGuid().ToString();
            s_client.Context.Location.Ip = "N/A";
            s_client.Context.Cloud.RoleInstance = "N/A";
        }

        private static int? CheckAwareness()
        {
            string GetCookieFile(string name, int version)
                => Constants.GlobalNukeDirectory / "telemetry-awareness" / $"v{version}" / name;

            if (NukeBuild.BuildProjectFile == null)
            {
                var cookieName = Assembly.GetEntryAssembly().NotNull().GetName().Name;
                Assert(cookieName != "testhost", "cookieName != 'testhost'");

                var cookieFile = GetCookieFile(cookieName, CurrentVersion);
                if (!File.Exists(cookieFile))
                {
                    PrintDisclosure($"to create awareness cookie for {cookieName.SingleQuote()}");
                    FileSystemTasks.Touch(cookieFile);
                }

                return CurrentVersion;
            }

            var project = ProjectModelTasks.ParseProject(NukeBuild.BuildProjectFile);
            var property = project.Properties.SingleOrDefault(x => x.Name.EqualsOrdinalIgnoreCase(VersionPropertyName));
            if (property?.EvaluatedValue != CurrentVersion.ToString())
            {
                if (NukeBuild.Host is not Terminal)
                {
                    PrintDisclosure(action: null);
                    return null;
                }

                PrintDisclosure($"to set the {VersionPropertyName.SingleQuote()} property");
                project.SetProperty(VersionPropertyName, CurrentVersion.ToString());
                project.Save();
            }

            for (var version = CurrentVersion; version > 0; version--)
            {
                if (File.Exists(GetCookieFile("Nuke.GlobalTool", version)))
                    return version;
            }

            return CurrentVersion;
        }

        private static void PrintDisclosure(string action)
        {
            var disclosure =
                new[]
                {
                    $"Telemetry v{CurrentVersion}",
                    "------------",
                    "NUKE collects anonymous usage data in order to help us improve your experience.",
                    string.Empty,
                    "Read more about scope, data points, and opt-out: https://nuke.build/docs/getting-started/telemetry.html",
                }.JoinNewLine();

            if (action != null)
            {
                Logger.Info(disclosure);
                Thread.Sleep(3000);
                Logger.Info(new[] { string.Empty, $"Press <Enter> to {action}..." }.JoinNewLine());
                WaitForEnter();
            }
            else
            {
                Logger.Warn(disclosure);
            }
        }

        private static void WaitForEnter()
        {
            ConsoleKey response;
            do
            {
                response = Console.ReadKey(intercept: true).Key;
            } while (response != ConsoleKey.Enter);
        }
    }
}
