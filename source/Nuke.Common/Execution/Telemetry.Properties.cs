// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nuke.Common.CI;
using Nuke.Common.Git;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Nuke.Common.Execution
{
    internal partial class Telemetry
    {
        private static readonly string[] s_knownTargets = { "Restore", "Compile", "Test" };

        private static IDictionary<string, string> GetCommonProperties()
        {
            return new Dictionary<string, string>
                   {
                       ["OS Platform"] = EnvironmentInfo.Platform.ToString(),
                   };
        }

        private static IDictionary<string, string> GetRepositoryProperties(string directory)
        {
            var repository = ControlFlow.SuppressErrors(() => GitRepository.FromLocalDirectory(directory), logWarning: false);
            if (repository == null)
                return new Dictionary<string, string>();

            var providers =
                new List<(Func<bool>, string)>
                {
                    (() => repository.Endpoint.ContainsOrdinalIgnoreCase("github.com"), "GitHub"),
                    (() => repository.Endpoint.ContainsOrdinalIgnoreCase("gitlab.com"), "GitLab"),
                    (() => repository.Endpoint.ContainsOrdinalIgnoreCase("bitbucket.org"), "Bitbucket"),
                    (() => repository.Endpoint.ContainsOrdinalIgnoreCase("jetbrains.space"), "JetBrains"),
                };

            var branches =
                new List<(Func<bool>, string)>
                {
                    (() => repository.IsOnMainOrMasterBranch(), "main"),
                    (() => repository.IsOnDevelopBranch(), "develop"),
                    (() => repository.IsOnReleaseBranch(), "release"),
                    (() => repository.IsOnHotfixBranch(), "hotfix"),
                };

            return new Dictionary<string, string>
                   {
                       ["Repository Provider"] = providers.FirstOrDefault(x => x.Item1.Invoke()).Item2,
                       ["Repository Branch"] = branches.FirstOrDefault(x => x.Item1.Invoke()).Item2,
                       ["Repository URL"] = repository.SshUrl.GetSHA256Hash().Substring(startIndex: 0, length: 6),
                       ["Repository Commit"] = repository.Commit.GetSHA256Hash().Substring(startIndex: 0, length: 6),
                   };
        }

        private static IDictionary<string, string> GetBuildProperties(NukeBuild build)
        {
            static bool IsCommonType(Type type) => type.FullName.NotNull().StartsWith("Nuke");
            static bool IsCustomType(Type type) => !IsCommonType(type);
            static string GetName(Type type) => IsCommonType(type) ? type.Name.TrimEnd(nameof(Attribute)) : "<Custom>";
            static string GetTypeName(object obj) => GetName(obj.GetType());

            var startTimeString = EnvironmentInfo.Variables.GetValueOrDefault(Constants.GlobalToolStartTimeEnvironmentKey);
            var compileTime = startTimeString != null
                ? DateTime.Now.Subtract(DateTime.Parse(startTimeString))
                : default(TimeSpan?);

            return new Dictionary<string, string>
                   {
                       ["Compile Time (seconds)"] = compileTime?.TotalSeconds.ToString("F0"),
                       ["Target Framework"] = EnvironmentInfo.Framework.ToString(),
                       ["Version of Nuke.Common"] = typeof(NukeBuild).Assembly.GetVersionText(),
                       ["Version of Nuke.GlobalTool"] = EnvironmentInfo.Variables.GetValueOrDefault(Constants.GlobalToolVersionEnvironmentKey),
                       ["Host"] = GetTypeName(NukeBuild.Host),
                       ["Build Type"] = NukeBuild.BuildProjectFile != null ? "Project" : "Global Tool",
                       ["Number of Executable Targets"] = build.ExecutableTargets.Count.ToString(),
                       ["Number of Custom Extensions"] = build.BuildExtensions.Select(x => x.GetType()).Count(IsCustomType).ToString(),
                       ["Number of Custom Components"] = build.GetType().GetInterfaces().Count(IsCustomType).ToString(),
                       ["Configuration Generators"] = build.GetType().GetCustomAttributes<ConfigurationAttributeBase>()
                           .Select(GetTypeName).Distinct().OrderBy(x => x).JoinComma(),
                       ["Build Components"] = build.GetType().GetInterfaces().Where(x => IsCommonType(x) && x != typeof(INukeBuild))
                           .Select(GetName).Distinct().OrderBy(x => x).JoinComma()
                   };
        }

        private static IDictionary<string, string> GetTargetProperties(ExecutableTarget target)
        {
            return new Dictionary<string, string>
                   {
                       ["Target Name"] = target.Name,
                       ["Target Duration"] = target.Duration.TotalSeconds.ToString("F0"),
                       // ["Target Partitions"] = ArtifactExtensions.Partitions.GetValueOrDefault(target.Definition, Partition.Single),
                   };
        }
    }
}
