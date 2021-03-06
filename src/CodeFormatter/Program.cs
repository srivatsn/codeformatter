﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using CommandLine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.CodeFormatting;
using Microsoft.DotNet.CodeFormatter.Analyzers;
using Microsoft.CodeAnalysis.Options;
using System.Diagnostics;

namespace CodeFormatter
{
    internal static class Program
    {
        private const int FAILED = 1;
        private const int SUCCEEDED = 0;

        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<
                ListOptions,
                ExportOptions,
                FormatOptions>(args)
            .MapResult(
                (ListOptions listOptions) => RunListCommand(listOptions),
                (ExportOptions exportOptions) => RunExportOptionsCommand(exportOptions),
                (FormatOptions formatOptions) => RunFormatCommand(formatOptions),
                errs => FAILED);
        }

        private static int RunExportOptionsCommand(ExportOptions exportOptions)
        {
            int result = FAILED;
            PropertyBag allOptions = OptionsHelper.BuildDefaultPropertyBag();

            allOptions.SaveTo(exportOptions.OutputPath, id: "codeformatter-options");
            Console.WriteLine("Options file saved to: " + Path.GetFullPath(exportOptions.OutputPath));

            result = SUCCEEDED;

            return result;
        }

        private static int RunListCommand(ListOptions options)
        {
            // If user did not explicitly reference either analyzers or
            // rules in list command, we will dump both sets.
            if (!options.Analyzers && !options.Rules)
            {
                options.Analyzers = true;
                options.Rules = true;
            }

            ListRulesAndAnalyzers(options.Analyzers, options.Rules);

            return SUCCEEDED;
        }

        private static void ListRulesAndAnalyzers(bool listAnalyzers, bool listRules)
        {
            Console.WriteLine("{0,-20} {1}", "Name", "Title");
            Console.WriteLine("==============================================");

            if (listAnalyzers)
            {
                ImmutableArray<DiagnosticDescriptor> diagnosticDescriptors = FormattingEngine.GetSupportedDiagnostics(OptionsHelper.DefaultCompositionAssemblies);
                foreach (var diagnosticDescriptor in diagnosticDescriptors)
                {
                    Console.WriteLine("{0,-20} :{1}", diagnosticDescriptor.Id, diagnosticDescriptor.Title);
                }
            }

            if (listRules)
            {
                var rules = FormattingEngine.GetFormattingRules();
                foreach (var rule in rules)
                {
                    Console.WriteLine("{0,-20} :{1}", rule.Name, rule.Description);
                }
            }
        }

        private static int RunFormatCommand(FormatOptions options)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                RunFormatAsync(options, ct).Wait(ct);
                Console.WriteLine("Completed formatting.");
                return SUCCEEDED;
            }
            catch (AggregateException ex)
            {
                var typeLoadException = ex.InnerExceptions.FirstOrDefault() as ReflectionTypeLoadException;
                if (typeLoadException == null)
                    throw;

                Console.WriteLine("ERROR: Type loading error detected. In order to run this tool you need either Visual Studio 2015 or Microsoft Build Tools 2015 tools installed.");
                var messages = typeLoadException.LoaderExceptions.Select(e => e.Message).Distinct();
                foreach (var message in messages)
                    Console.WriteLine("- {0}", message);

                return FAILED;
            }
        }

        private static async Task<int> RunFormatAsync(FormatOptions options, CancellationToken cancellationToken)
        {
            var engine = FormattingEngine.Create(OptionsHelper.DefaultCompositionAssemblies);

            var configBuilder = ImmutableArray.CreateBuilder<string[]>();
            configBuilder.Add(options.PreprocessorConfigurations.ToArray());            
            engine.PreprocessorConfigurations = configBuilder.ToImmutableArray();

            engine.FormattingOptionsFilePath = options.OptionsFilePath;
            engine.Verbose = options.Verbose;
            engine.AllowTables = options.DefineDotNetFormatter;
            engine.FileNames = options.FileFilters.ToImmutableArray();
            engine.CopyrightHeader = options.CopyrightHeaderText;


            // Analyzers will hydrate rule enabled/disabled settings
            // directly from the options referenced by file path
            // in options.OptionsFilePath
            if (!options.UseAnalyzers)
            {
                if (!SetRuleMap(engine, options.RuleMap))
                {
                    return FAILED;
                }
            }

            foreach (var item in options.FormatTargets)
            {
                await RunFormatItemAsync(engine, item, options.Language, options.UseAnalyzers, cancellationToken);
            }

            return SUCCEEDED;
        }

        private static async Task RunFormatItemAsync(
            IFormattingEngine engine,
            string item,
            string language,
            bool useAnalyzers,
            CancellationToken cancellationToken)
        {
            Console.WriteLine(Path.GetFileName(item));
            string extension = Path.GetExtension(item);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".rsp"))
            {
                using (var workspace = ResponseFileWorkspace.Create())
                {
                    Project project = workspace.OpenCommandLineProject(item, language);
                    await engine.FormatProjectAsync(project, useAnalyzers, cancellationToken);
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var solution = await workspace.OpenSolutionAsync(item, cancellationToken);
                    await engine.FormatSolutionAsync(solution, useAnalyzers, cancellationToken);
                }
            }
            else
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var project = await workspace.OpenProjectAsync(item, cancellationToken);
                    await engine.FormatProjectAsync(project, useAnalyzers, cancellationToken);
                }
            }
        }

        private static bool SetRuleMap(IFormattingEngine engine, ImmutableDictionary<string, bool> ruleMap)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            foreach (var entry in ruleMap)
            {
                var rule = engine.AllRules.Where(x => comparer.Equals(x.Name, entry.Key)).FirstOrDefault();
                if (rule == null)
                {
                    Console.WriteLine("Could not find rule with name {0}", entry.Key);
                    return false;
                }

                engine.ToggleRuleEnabled(rule, entry.Value);
            }
            Debug.Assert(ruleMap.Count == engine.AllRules.Count());

            return true;
        }
    }
}
