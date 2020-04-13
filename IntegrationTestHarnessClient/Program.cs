using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using TestUtils;
using static TestUtils.CommonTestUtils;

namespace Tests
{
    class Program
    {

        private static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            Console.CancelKeyPress += MyCancelEventHandler; // Print what you've already run...

            #region RunApiTests
            app.Command("RunApiTests", target =>
            {
                target.Name = "RunApiTests";
                target.Description = "Runs the API integration tests";
                var optRunLastTestOnly = target.Option("-l|--last", "Run only the last test", CommandOptionType.NoValue);
                var optDoNotSimulateSecondClient = target.Option("-nlc|--no-Second-client", "Dont simulate a Second client", CommandOptionType.NoValue);
                var optRunReverseOrder = target.Option("-r|--reverse", "Run tests in reverse order", CommandOptionType.NoValue);
                var optListTests = target.Option("-ll|--list", "list all tests", CommandOptionType.NoValue);
                var optRunOnlyTheseTests = target.Option("-o|--only", "Run only specified tests", CommandOptionType.MultipleValue);
                var optRunOnlyTheseSuites = target.Option("-os|--only-suite", "Run ony tests within this suite", CommandOptionType.MultipleValue);
                var optRunDualTests = target.Option("-d|--dual", "Runs each test as both a Second and First client (Second client adds Second attrs)", CommandOptionType.NoValue);
                var optBeVerbose = target.Option("-v|--verbose", "Be verbose in your reporting to the user", CommandOptionType.NoValue);
                var optCompareRetainedObjects = target.Option("-c|--compare", "Compare retained objects", CommandOptionType.NoValue);
                var optDoNotPrintRerunCommands = target.Option("-drr|--dont-print-rerun", "Dont preting rerun commands", CommandOptionType.NoValue);
                var optBeQuiet = target.Option("-q|--quiet", "Don't say alot of stuff while running", CommandOptionType.NoValue);
                var optRecordResults = target.Option("-rec|--record", "Save api run results to csv", CommandOptionType.NoValue);
                var optValidateDeletes = target.Option("-vd | --verify-deletes", "Verify that delete operations actually deleted Subjects (slower)", optionType: CommandOptionType.NoValue);
                var optExcludeSuites = target.Option(template: "-xs <SuiteKey>|--exclude-suite <SuiteKey>", description: "Excludes a suite of tests", optionType: CommandOptionType.MultipleValue);
                var optExcludeTests = target.Option(template: "-xo <TestKey>|--exclude-test <TestKey>", description: "Excludes a suite of tests", optionType: CommandOptionType.MultipleValue);
                var optPrintOnlyFailedTests = target.Option("-poft|--print-failed-only", "Prints out only the failed tests in summary", CommandOptionType.NoValue);
                var optDumpRequests = target.Option("-drequests|--dump-requests", "Dumps web services requests", CommandOptionType.NoValue);
                var optDumpResponses = target.Option("-dresponses|--dump-responses", "Dumps web services responses", CommandOptionType.NoValue);
                var optDryRun = target.Option("-dr|--dry-run", "Dont actually run the tests", CommandOptionType.NoValue);
                var optDumpDiffsToFile = target.Option("--diffs-to-files", "Writes the two compared objects responses to file", CommandOptionType.NoValue);
                var optAlwaysRetainResponses = target.Option("--always-retain-responses", "Always retain responeses for comparison", CommandOptionType.NoValue);
                var optRepeatUntilSuccessCount = target.Option("-rusc|--repeat-until-success-count", "Repeat failing test n times until success, failure after n attempts to obtain success", CommandOptionType.SingleValue);
                var optRunAsUser = target.Option("--run-as| --uuid", "Login to First as a specific UUID", CommandOptionType.SingleValue);
                
                // Main entry point for RunApiTest cmd
                target.OnExecute(() =>

                {
                    if (MissingMandatoryArguments(target))
                    {
                        return -1;
                    }

                    try
                    {
                        if (!optDoNotSimulateSecondClient.HasValue() && !CanConnectToUrl("http://www.google.com"))
                        {
                            Console.WriteLine("Cannot connect to API endpoint. Aborting tests");
                            return -1;
                        }

                        // Global test options
                        var globalTestOptions = new TestOptions
                        {
                            RunLastTestOnly = optRunLastTestOnly.HasValue(),
                            AreSimulatingExistingFirstClient = optDoNotSimulateSecondClient.HasValue(),
                            RunTestsInReverseOrder = optRunReverseOrder.HasValue(),
                            RunSpecificTests = optRunOnlyTheseTests.HasValue() ? optRunOnlyTheseTests.Values : Enumerable.Empty<string>(),
                            ReportStepSummary = true,
                            RunDual = optRunDualTests.HasValue(),
                            BeVerbose = optBeVerbose.HasValue(),
                            CompareRetainedObjects = optRunDualTests.HasValue() && (optCompareRetainedObjects.HasValue() || optAlwaysRetainResponses.HasValue()),
                            BeQuiet = optBeQuiet.HasValue(),
                            DryRun = optDryRun.HasValue(),
                            ValidateDelete = optValidateDeletes.HasValue(),
                            DumpRequests = optDumpRequests.HasValue(),
                            DumpResponses = optDumpResponses.HasValue(),
                            WriteResponseDifferencesToFiles = optDumpDiffsToFile.HasValue(),
                            AlwaysRetainResponses = optAlwaysRetainResponses.HasValue(),
                            RepeatUntilSuccessCount = optRepeatUntilSuccessCount.HasValue()
                                                        ? int.Parse(optRepeatUntilSuccessCount.Value()) 
                                                        : 0,
                            RunAsUUid = optRunAsUser.HasValue()
                            ? optRunAsUser.Value() 
                            : "default"
                        };

                        // Suite, List of tests
                        var suitesOfTests = new Dictionary<string, List<FunctionTestPackage>>
                        {
                            { "SuiteName", new List<FunctionTestPackage>
                            {
                                new FunctionTestPackage("", optBeQuiet, options => true )
                            }}
                        };

                        return PerformTestRuns(
                            optListTests, 
                            suitesOfTests, 
                            optRunOnlyTheseSuites.Values, 
                            optExcludeSuites.Values,
                            optExcludeTests.Values,
                            optRunOnlyTheseTests.Values,
                            globalTestOptions, 
                            optRecordResults.HasValue(), 
                            optDoNotPrintRerunCommands.HasValue(),
                            optPrintOnlyFailedTests.HasValue());
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"\n{TestHelpers.FirstErrorIdentifierSuffix} a test failed due to emitting an unexpected exception: {e.Message} StackTrace= {e.StackTrace}");
                        return -1;
                    }
                });
                target.HelpOption("-??");
            });
            #endregion

            app.Name = "Tests.exe";
            app.Description = "TestHarness";
            app.HelpOption("--??");

            app.OnExecute(() =>
            {
                if (args.Length != 0) return 0;
                app.ShowHelp();
                return -1;
            });

            try
            {
                var result = app.Execute(args);
                if (!Debugger.IsAttached)
                {
                    return result;
                }

                Console.ReadKey();
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (Debugger.IsAttached)
                {
                    Console.ReadKey();
                }

                return 1;
            }
        }

       
        
        /// <summary>
        /// Called when the Program is interrupted (Ctrl+C for instance)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MyCancelEventHandler(object sender, ConsoleCancelEventArgs args)
        {
            PrintSummaryOfResults(false, false);
        }

        public static bool MissingMandatoryArguments(CommandLineApplication target, params CommandArgument[] arguments)
        {
            var emptyArgs = arguments.Where(x => string.IsNullOrEmpty(x.Value)).ToArray();
            if (emptyArgs.Any())
            {
                var missingArgList = string.Join(",", emptyArgs.Select(x => x.Name));
                Console.WriteLine($"{ErrorIdentifierSuffix} Missing mandatory arguments: {missingArgList}");
                target.ShowHelp(target.Name);
                return true;
            }
            return false;
        }
    }
}
