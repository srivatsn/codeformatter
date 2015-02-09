// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition.Hosting;
using System.Reflection;

namespace Microsoft.DotNet.CodeFormatting
{
    public static class FormattingEngine
    {
        public static IFormattingEngine Create(ImmutableArray<string> ruleTypes)
        {
            var configuration = new ContainerConfiguration()
                        .WithAssembly(typeof(FormattingEngine).GetTypeInfo().Assembly);

            //var catalog = new AssemblyCatalog(typeof(FormattingEngine).Assembly);

            //var ruleTypesHash = new HashSet<string>(ruleTypes, StringComparer.InvariantCultureIgnoreCase);
            //var notFoundRuleTypes = new HashSet<string>(ruleTypes, StringComparer.InvariantCultureIgnoreCase);

            //var filteredCatalog = new FilteredCatalog(catalog, cpd =>
            //{
            //    if (cpd.ExportDefinitions.Any(em =>
            //        em.ContractName == AttributedModelServices.GetContractName(typeof(DiagnosticAnalyzer))))
            //    {
            //        object ruleType;
            //        if (cpd.Metadata.TryGetValue(RuleTypeConstants.PartMetadataKey, out ruleType))
            //        {
            //            if (ruleType is string)
            //            {
            //                notFoundRuleTypes.Remove((string)ruleType);
            //                if (!ruleTypesHash.Contains((string)ruleType))
            //                {
            //                    return false;
            //                }
            //            }
            //        }
            //    }

            //    return true;
            //});

            //var container = new CompositionContainer(filteredCatalog);

            var container = configuration.CreateContainer();

            //if (filenames.Any())
            //{
            //    container.ComposeExportedValue<IFormattingFilter>(new FilenameFilter(filenames));
            //}

            var engine = container.GetExport<IFormattingEngine>();
            var consoleFormatLogger = new ConsoleFormatLogger();

            //  Need to do this after the catalog is queried, otherwise the lambda won't have been run
            //foreach (var notFoundRuleType in notFoundRuleTypes)
            //{
            //    consoleFormatLogger.WriteErrorLine("The specified rule type was not found: {0}", notFoundRuleType);
            //}

            return engine;
        }
    }
}