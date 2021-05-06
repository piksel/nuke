// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System.Collections.Generic;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Nuke.Common.Execution
{
    internal partial class Telemetry
    {
        public static void BuildStarted(NukeBuild build)
        {
            TrackEvent(
                eventName: nameof(BuildStarted),
                properties: GetCommonProperties()
                    .AddDictionary(GetBuildProperties(build))
                    .AddDictionary(GetRepositoryProperties(NukeBuild.RootDirectory)));
        }

        public static void TargetSucceeded(NukeBuild build, ExecutableTarget target)
        {
            if (target.Name.EqualsAnyOrdinalIgnoreCase(s_knownTargets) &&
                target.Status == ExecutionStatus.Succeeded)
            {
                TrackEvent(
                    eventName: nameof(TargetSucceeded),
                    properties: GetCommonProperties()
                        .AddDictionary(GetRepositoryProperties(NukeBuild.RootDirectory))
                        .AddDictionary(GetTargetProperties(target)));
            }
        }

        public static void BuildSetup()
        {
            TrackEvent(
                eventName: nameof(BuildSetup),
                properties: GetCommonProperties()
                    .AddDictionary(GetRepositoryProperties(EnvironmentInfo.WorkingDirectory)));
        }

        public static void CakeConvert()
        {
            TrackEvent(
                eventName: nameof(CakeConvert),
                properties: GetCommonProperties()
                    .AddDictionary(GetRepositoryProperties(EnvironmentInfo.WorkingDirectory)));
        }

        private static void TrackEvent(
            string eventName,
            IDictionary<string, string> properties = null,
            IDictionary<string, double> metrics = null)
        {
            s_client?.TrackEvent(eventName, properties, metrics);
            s_client?.Flush();
        }
    }
}
