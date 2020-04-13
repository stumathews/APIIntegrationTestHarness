using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Timers;
using KellermanSoftware.CompareNetObjects;
using Microsoft.Extensions.CommandLineUtils;
using Second.Utils;
using TestUtils.ArrayExtensions;
using Utils;
using DateTime = System.DateTime;

namespace TestUtils
{
    /// <summary>
    /// All the integration tests that the client program can run
    /// </summary>
    public static class CommonTestUtils
    {
        /// <summary>
        /// Global store of all test results over the duration of the test harness run/session
        /// </summary>
        public static List<DualTestResult> TestResultStore = new List<DualTestResult>();
        public static List<ApiCallResultContainer> GlobalApiCallResultStore = new List<ApiCallResultContainer>();
        public static FunctionTestPackage CurrentlyExecutingTest = null;

        public static class DiffHelper
        {
            public static Func<TestOptions, ApiCallResultContainer> IgnoreTimeRelatedMembers = (testOptions)
                => new ApiCallResultContainer(testOptions, ignoreTimeRelatedMembers: true);
            /// <summary>
            ///  Default behavior for object diffing
            /// </summary>
            public static Func<TestOptions, ApiCallResultContainer> Default = (testOptions) 
                => new ApiCallResultContainer(testOptions, ignoreTimeRelatedMembers: DefaultObjectComparisonOptions[ObjectComparisonOption.IgnoreTimeRelatedFunctionsByDefault]);
            
        }

        public static SimpleFileLogger DefaultFileLogger = new SimpleFileLogger("TestHarness.log");
        public static NullLogger NullLogger = new NullLogger();

        public enum CustomerIdentityType
        {
            CustomerType1,
            CustomerType2
        };

        /// <summary>
        /// PrintDifferencesIfAny
        /// </summary>
        /// <param name="comparisonResult"></param>
        /// <returns>True if differences were encountered , false otherwise</returns>
        public static bool PrintDifferencesIfAny(this ComparisonResult comparisonResult, SimpleFileLogger logger = null)
        {
            if(logger == null)
                logger = DefaultFileLogger;

            var diffCount = comparisonResult.Differences.Count();
            var anyChanges = diffCount != 0;
            if(anyChanges)
            {
                const string changedDetectedMessage = "#!! Changes between original and migrated results";
                Console.WriteLine(changedDetectedMessage);
                logger?.Log(changedDetectedMessage);
                
                foreach (var diff in comparisonResult.Differences)
                {
                    var fmt = $"\t#{diffCount++} '{diff.PropertyName}' First={diff.Object1Value}, Second={diff.Object2Value}";
                    Console.WriteLine(fmt);
                    logger?.Log(fmt);
                }
            }
            else
            {
                const string noDiffsMessage = "No changes detected.";
                Console.WriteLine(noDiffsMessage);
                logger?.Log(noDiffsMessage);
            }

            
            
            return anyChanges;
        }

        public enum Status
        {
            Success,
            Info,
            Warning,
            Error
        };

        /// <summary>
        /// Global behaviors for types of checks
        /// <remarks>Used primarily to turn on/off types of check's behaviours - such as ignore failures og type X etc.</remarks>
        /// </summary>
        private static Dictionary<CheckType, CheckTypeBehavior> GlobalCheckTypeBehaviors = new Dictionary<CheckType, CheckTypeBehavior>
        {
            { CheckType.NameCheck, new CheckTypeBehavior { IgnoreFailure = false }},
            { CheckType.CodeCheck, new CheckTypeBehavior { IgnoreFailure = false }},
            { CheckType.IdCheck, new CheckTypeBehavior { IgnoreFailure = false }},
            { CheckType.Unspecified, new CheckTypeBehavior { IgnoreFailure = false }},
            { CheckType.DescCheck, new CheckTypeBehavior { IgnoreFailure = true }}, // We currently manipulate the description and so it cant be reasoned about reasonably
            { CheckType.AmountCheck, new CheckTypeBehavior { IgnoreFailure = false }},
            { CheckType.BasicSubjectChecks, new CheckTypeBehavior { IgnoreFailure = false } }
        };

        public static Dictionary<ObjectComparisonOption, bool> DefaultObjectComparisonOptions = new Dictionary<ObjectComparisonOption, bool>
        {
            // Ignore LastDateModified, CreatedDate and DateTime members in objects that we compare
            { ObjectComparisonOption.IgnoreTimeRelatedFunctionsByDefault , false}
        };

        public enum ObjectComparisonOption { IgnoreTimeRelatedFunctionsByDefault };
        
        public static bool CanConnectToUrl(string url, bool reportStatus = true)
        {
            try
            {
                var request = WebRequest.Create(url);
                var response = (HttpWebResponse) request.GetResponse();
                
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    if (reportStatus) { Console.WriteLine($"Could not connect to URL '{url}'. Status Code= {response.StatusCode}, StatusDesc={response.StatusDescription}"); }

                    return false;
                }
                
                if (reportStatus){Console.WriteLine($"Successfully connected to URL '{url}'. Status Code= {response.StatusCode}, StatusDesc={response.StatusDescription}");}
                response.Close();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not connect to URL '{url}'. {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Useful function that logs what it does, provided you provide the logging function
        /// </summary>
        /// <param name="action">function to do it</param>
        /// <param name="log">function to log it </param>
        /// <param name="then">What to do after your original action</param>
        /// <param name="doActionIf">perform the action only if this result to true</param>
        /// <param name="onException">called if an exception occurs</param>
        /// <returns>true if everything went fine (no exceptions) false if exception occured which can be obtained via onException function</returns>
        public static bool Do(Action action, Action log = null, Action then = null, Func<bool> doActionIf = null, Action<Exception> onException = null)
        {
            try
            {
                log?.Invoke();
                bool? shouldDoAction = doActionIf?.Invoke() ?? true;
                if(shouldDoAction.Value)
                {
                    action();
                }
                then?.Invoke();
                return true;
            }
            catch(Exception e)
            {
                onException?.Invoke(e);
                return false;
            }
        }

        public static Continuation<T> Do<T>(Func<T> returningAction, Action log = null) where T : class
        {
            log?.Invoke();
            return new Continuation<T>(returningAction()); // continuation with result
        }


        /// <summary>
        /// Reports that a step is being taken and prints resulting information about the step stats(currently only time taken)
        /// Also can store the results of the operation in the global 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">name of the operation</param>
        /// <param name="operation">operation that represents the step </param>
        /// <param name="reportSummary">prints a summary of the step otherwise doesn't</param>
        /// <param name="returnDefaultTOnException">will suppress any exceptions and return default(T) instead</param>
        /// <param name="retainResponseForComparison">Set up and configure a ApiResultStore to store the results of an API Call</param>
        /// <param name="loggerFunc"></param>
        /// <returns>Will return the operations result</returns>
        public static T RunAndReturn<T>(string name, Func<T> operation, TestOptions testOptions, bool returnDefaultTOnException = false, Func<ApiCallResultContainer> retainResponseForComparison = null, bool printReturnValue = false, SimpleFileLogger logger = null, bool explicitlyIgnoreRetainingObjects = false)
        {
            try
            {
                if(logger == null)
                    logger = DefaultFileLogger;

                WriteMessageLine($"[Step] '{name}'...", true, beQuiet: testOptions.BeQuiet, fileLogger: logger);
                var ret = TimeThisAndReturn(operation, out var timeTook);

                if(testOptions.AlwaysRetainResponses && !explicitlyIgnoreRetainingObjects)
                {
                    retainResponseForComparison = new Func<ApiCallResultContainer>(()=> new ApiCallResultContainer(testOptions));
                }

                if (retainResponseForComparison != null) 
                {
                    var apiCallResultContainer = retainResponseForComparison();
                        apiCallResultContainer.TestName = name;
                        apiCallResultContainer.Object = ret;
                        apiCallResultContainer.Time = timeTook;
                        apiCallResultContainer.SpecificMethodWithinTest = operation.Method.Name;
                        apiCallResultContainer.MethodId = operation.Method.ToString();

                    if(GlobalApiCallResultStore.Contains(apiCallResultContainer))
                        GlobalApiCallResultStore.Remove(apiCallResultContainer);
                    GlobalApiCallResultStore.Add(apiCallResultContainer);
                }
                
                WritePassMessage("Done", testOptions.ReportStepSummary, beQuiet: testOptions.BeQuiet, logger: logger);
                if (testOptions.ReportStepSummary && !testOptions.BeQuiet)
                {
                    WriteMessageLine($" ({timeTook} ms)");
                } 

                if (!printReturnValue) return ret;
                var dump = typeof(T).IsInterface ? ret.DumpInterface() : XmlUtilities<T>.ObjectToXml(ret, logger);
                WriteMessageLine($"Return: { dump }", fileLogger: logger);
                return ret;
            } 
            catch(Exception ex)
            {
                if (!returnDefaultTOnException) throw;
                WriteMessageLine($"Exception occured({ex.Message}) but you told me to Ignore exception for this operation...returning default({typeof(T)})", fileLogger: logger);
                return default(T);
            }
        }

        
        /// <summary>
        /// Known type of check
        /// </summary>
        public enum CheckType { Unspecified = 0, NameCheck, CodeCheck, IdCheck, DescCheck, AmountCheck, BasicSubjectChecks };
        
        /// <summary>
        /// Encapsulates information about a check we're performing
        /// </summary>
        public class CheckTypeBehavior
        {
            public CheckType CheckType { get;set;}
            public bool IgnoreFailure { get;set;}
        }

       
        /// <summary>
        /// Returns true if Failure condition is met
        /// </summary>
        /// <param name="failurePredicate">Failure condition is met</param>
        /// <param name="failReason">Why it failed, if it failed - written to console</param>
        /// <param name="progressMessage"></param>
        /// <returns>if it failed, return false, true otherwise</returns>
        public static bool FailedIf(Func<bool> failurePredicate, string failReason = null, CheckType checkType = CheckType.Unspecified, Action doIfFailed = null, bool showValidation = true )
        {
            if(showValidation)
            {
                WriteMessageLine("[Verify]", withoutNewLine: true);
                WriteMessageLine(msg: $"'{failReason}'", withoutNewLine: true, messageStatus: Status.Info);
            }

            var failed = failurePredicate();
            var ignoreFailure = GlobalCheckTypeBehaviors[checkType].IgnoreFailure;

            if(failed) 
            {
                doIfFailed?.Invoke();
                WriteFailMessage("[Failed]");
            }else
            {
                 WriteMessageLine("[Passed]", messageStatus: Status.Success);
            }

            if (failed && !ignoreFailure)
            {
                WriteFailMessage($"{ErrorIdentifierSuffix} Test Failed. Reason: {failReason ?? "No reason supplied"}");
            }

            if(failed && ignoreFailure)
            {
                WriteMessageLine($"(IGNORED intentionally) Test Failed. Reason: {failReason ?? "No reason supplied"}");
            }

            

            return failed && !ignoreFailure;
        }

        public static string ErrorIdentifierSuffix { get; set; } = "Error!";


        /// <summary>
        /// Runs tests
        /// </summary>
        /// <param name="tests">list of test functions to run along with the cmd option that controls its inclusion or not</param>
        /// <returns>list of names of fialed tests</returns>
        public static Dictionary<DualTestResult, FunctionTestPackage> RunTests(IList<FunctionTestPackage> tests, TestOptions options)
        {
            if (!options.BeQuiet) { 
                // Print global test options are
                foreach (var option in options.GetType().GetProperties())
                {
                    if (option.GetMethod.ReturnType == typeof(IEnumerable<string>))
                    {
                        var enumerable = option.GetValue(options) as IEnumerable<string>;
                        Console.WriteLine($"{option.Name}:");
                        if (enumerable == null) continue;
                        foreach (var item in enumerable)
                        {
                            Console.WriteLine($"\t{item}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{option.Name}={option.GetValue(options)}");
                    }
                }
            }

            //Item1: name of test
            //Item2: opt-out option for test
            //item3: the test function itself
            var lastTest = tests.Last();
            if (options.RunSpecificTests.Any())
            {
                // Run only specified tests, ignore the rest
                // Add to list of tests to run if we match (case insensitive)
                    tests = tests.Where(x => options.RunSpecificTests.Contains(x.TestName, new StringCurrentCultureIgnoreCaseEqualityComparer())).ToList();
            }


        // run only tests that have not been opted-out
        var testsNotDisabled = options.RunLastTestOnly
                ? tests.Where(x => x.TestName.Equals(lastTest.TestName)).ToList()
                : tests.Where(x => !x.TestOption.HasValue()).ToList();

            if(options.RunTestsInReverseOrder)
            {
                testsNotDisabled.Reverse();
            }

            // We will be able to modify the IsSecond test Option if we're asked to run the tests using Dual mode
            var dualResults = new Dictionary<DualTestResult, FunctionTestPackage>();
            
            var testsNotDisabledDupped = Do( returningAction:()=> testsNotDisabled
                .Where(t=>!t.IsSingular)
                .SelectMany(t => Enumerable.Repeat(t, 2))
                .ToList()) // duplicate the test so that one can be run with IsSecondCustomer and with with IsFirstCustomer
                .ThenFinally(results => 
                {
                    if(results.Any())
                    {
                        WriteMessageLine("Running Dual mode tests ie. one via First and one via Second");
                    }
                    return results;
                } ); 
            
            for (var i=0; i < testsNotDisabledDupped.Count ; i++)
            {
                // Run the First Test first, then Second                   
                var modifiableTestOptions = options.Clone();
                    modifiableTestOptions.AreSimulatingExistingFirstClient = i % 2 == 0;

                    // If we've specifically asked to run as a First or Second customer, then only run as if we're simulating that scenario
                if(modifiableTestOptions.AreSimulatingExistingFirstClient != options.AreSimulatingExistingFirstClient && !options.RunDual)
                {
                    continue;
                }

                // Run the test
                DualTestResult testOutcome;
                int failCount = 0;
                var failed = false;
                bool repeatCondition; /* resilience if failure occured*/
                do  
                {
                    failed = failCount > 0;
                    repeatCondition = failed && failCount <= options.RepeatUntilSuccessCount;

                    if(repeatCondition)
                        WriteMessageLine($"** Repeating failed test attempt {failCount}/{options.RepeatUntilSuccessCount}...");
                    
                    // Run the actual test
                    testOutcome = RunTestAndIgnoreExceptions(testsNotDisabledDupped[i], modifiableTestOptions); 
                    
                    // Possible re-run required if failed
                    if(testOutcome.Result == false)
                        failCount++;
                    else
                        failCount = 0;

                    failed = failCount > 0;
                    repeatCondition = failed && failCount <= options.RepeatUntilSuccessCount;
                }
                while(repeatCondition);
                
                if ( options.CompareRetainedObjects && options.RunDual)
                { 
                    // Perform the diff is there are two results for the method
                    var TestMethods = GlobalApiCallResultStore.GroupBy(x=>x.MethodId);
                    
                    foreach(var methodGroup in TestMethods)
                    {
                        // only can compare results if there are two per method
                        if (methodGroup.Count() != 2) continue;
                        var second = methodGroup.SingleOrDefault(x => !x.TestOptions.AreSimulatingExistingFirstClient);                        
                        var first = methodGroup.SingleOrDefault(x => x.TestOptions.AreSimulatingExistingFirstClient);
                        if(second == null || first ==null)
                        {
                            WriteMessageLine($"Incomplete test data for a comparison for {methodGroup}. Skipping response comparison. If this is a mistkae, ensure your test is retaining response objects");
                            continue;
                        }
                        
                        // Don't compare results for the same calls but in diffirent methods.
                        if(second.ComparisonStatus == ApiCallResultContainer.ComparisonProgress.Finished || first.ComparisonStatus == ApiCallResultContainer.ComparisonProgress.Finished)
                            continue;
                        
                        var res = new CompareLogic(first.ComparisonConfig).Compare(first.Object, second.Object);
                        // only print out diffirences if there are any
                        if (res.AreEqual) continue;
                        WriteMessageLine($"Response differences from step:'{first.TestName}' via call to '{first.SpecificMethodWithinTest}':");
                        var diffCount = 0;
                        foreach (var diff in res.Differences)
                        {
                            WriteMessageLine($"\t#{diffCount++} '{diff.PropertyName}' First={diff.Object1Value}, Second={diff.Object2Value}");
                        }
                        // Show Speed diff
                        WriteMessageLine($"Speed diff -> First Time: {second.Time}, Second Time: {first.Time} ==  {second.Time - first.Time} ms difference");
                        
                        second.ComparisonStatus = ApiCallResultContainer.ComparisonProgress.Finished;
                        first.ComparisonStatus = ApiCallResultContainer.ComparisonProgress.Finished;
                        
                        if(options.WriteResponseDifferencesToFiles)
                        {
                            // Write the differences to file for easier comparison
                            var baseOutputPath = Path.Combine(Environment.CurrentDirectory, "ObjectDiffs");
                            if(!Directory.Exists(baseOutputPath))
                                Directory.CreateDirectory(baseOutputPath);
                            
                            var FirstLogName = $"{first.TestName}-{first.MethodId}-First.log";
                            var SecondLogName = $"{second.TestName}-{second.MethodId}-Second.log";

                            string convertToValidFileName(string filename)
                            {
                                string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                                foreach (char c in invalid)
                                {
                                    filename = filename.Replace(c.ToString(), ""); 
                                }
                                return filename;
                            }

                            var FirstLog = new SimpleFileLogger(Path.Combine(baseOutputPath,convertToValidFileName(FirstLogName)));                        
                            var SecondLog = new SimpleFileLogger(Path.Combine(baseOutputPath, convertToValidFileName(SecondLogName)));
                                                
                            FirstLog.Log(XmlUtilities<object>.ObjectToXml(first.Object), overwite: true);
                            SecondLog.Log(XmlUtilities<object>.ObjectToXml(second.Object), overwite: true);
                        }
                    }
                }

                var testPackage = testsNotDisabledDupped[i];

                if (!dualResults.ContainsKey(testOutcome))
                {
                    // Current limitation is now way to identify which result failed at the end of the all the results when compiling reports(at the very end of test runs) on 
                    // which test passed and did not - as these dual tests look the same! so for now just look at the output to see what IsSecondClient=True/False reports!
                    dualResults.Add(testOutcome, testPackage);
                }
            }
            

            // Run single mode tests
            Do( doActionIf: ()=> testsNotDisabled.Any(x=>x.IsSingular), action:()=>WriteMessageLine("Running single model tests ie. without specific to Second or First"));

            var singleResults = testsNotDisabled
                .Where(t=>t.IsSingular)
                .ToDictionary(k=>RunTestAndIgnoreExceptions(k, options), k=> k);

            // return all results
            return singleResults.Union(dualResults) // bring all the results together
                                .ToDictionary(x=>x.Key, x=>x.Value);
        }


        public static void WriteFailMessage(string msg, bool withoutNewLine = false, ISimpleFileLogger logger = null)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Red;
            PrintWithOrWithoutNewline(msg, withoutNewLine, logger: logger);
            
            Console.ResetColor();
        }

        private static void PrintWithOrWithoutNewline(string msg, bool withoutNewLine, ISimpleFileLogger logger = null)
        {
            if(logger == null)
                logger = DefaultFileLogger;

            if (!withoutNewLine)
            {
                Console.WriteLine(msg);
                logger?.Log(msg, withoutNewline: false);
            }
            else
            {
                Console.Write(msg);
                logger?.Log(msg, withoutNewline: true);
            }
        }

        public static void WriteMessageLine(string msg, bool withoutNewLine = false, bool withPrefix = false, string withCustomPrefix = null, bool beQuiet = false, Status messageStatus = Status.Warning, int indentCount = 0, ISimpleFileLogger fileLogger = null, bool doNotLog = false)
        {
            if(beQuiet) return;

            if(fileLogger == null)
                fileLogger = DefaultFileLogger;
            if(doNotLog)
                fileLogger = NullLogger;
            
            var prefix = String.Empty;
            
            if (withPrefix && withCustomPrefix != null)
            {
                prefix = withCustomPrefix;
            }

            var output = prefix + new String('\t', indentCount) + msg;

            if (messageStatus == Status.Warning)
            {
                prefix = withPrefix ? "WARNING:" : String.Empty;
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Yellow;

                NormalConsolePrint(withoutNewLine, fileLogger, output);

                Console.ResetColor();
            }
                        
            if (messageStatus == Status.Error)
            {
                prefix = withPrefix ? "ERROR:" : String.Empty;
                WriteFailMessage(msg: output, withoutNewLine: withoutNewLine);
            }

            if (messageStatus == Status.Info)
            {
                prefix = withPrefix ? "INFO:" : String.Empty;
                PrintWithOrWithoutNewline(msg: output, withoutNewLine: withoutNewLine, logger: fileLogger);
            }

            if (messageStatus == Status.Success)
            {
                prefix = withPrefix ? "INFO:" : String.Empty;
                WritePassMessage(msg: output, withoutNewLine: withoutNewLine, logger: fileLogger);
            }

        }

        private static void NormalConsolePrint(bool withoutNewLine, ISimpleFileLogger fileLogger, string output)
        {
            if (!withoutNewLine)
            {
                Console.WriteLine(output);
                fileLogger?.Log(output, withoutNewline: false);
            }
            else
            {
                Console.Write(output);
                fileLogger?.Log(output, withoutNewline: true);
            }
        }

        public static void WritePassMessage(string msg, bool withoutNewLine = false, bool beQuiet = false, ISimpleFileLogger logger = null)
        {
            if(beQuiet) return;

            if(logger == null)
                logger = DefaultFileLogger;

            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;
            if (!withoutNewLine)
            {
                Console.WriteLine(msg);
                logger?.Log(msg, withoutNewline: false);
            }
            else
            {
                Console.Write(msg);
                logger?.Log(msg, withoutNewline: true);
            }
           
            Console.ResetColor();
        }

        // Stop gap measure that allows exceptions to not go up the stack and merely resolve to a boolean false result
        private static DualTestResult RunTestAndIgnoreExceptions(FunctionTestPackage testPackage, TestOptions options)
        {
            CurrentlyExecutingTest = testPackage;
            var testResult = new DualTestResult(result: false /*assume failure until changed below*/, testName: testPackage.TestName, isSecondTest: Boolean.Parse(options.SimulateSecondClientString), isScenarioTest: testPackage.ScenariosTests.Any(x=>x.TestName.Equals(testPackage.TestName)));
            
            try
            {
                var extra = !Boolean.Parse(options.SimulateSecondClientString) ? String.Empty : "[SecondTest]";

                WriteMessageLine($"\n======= Running {testPackage.TestName} integration test {extra} =======");
                
                testResult.Result = TimeThisAndReturn(()=>
                {
                    if(options.DryRun)
                    {
                        WriteMessageLine($"Dry-Run.Skipping '{testPackage.TestName}'");
                        return false;
                    }
                    return testPackage.TestFunction(options);
                }, out var timeTook);

                printPassFailMessage(testPackage, testResult.Result);
                WriteMessageLine($" (Total duration:{timeTook} msecs)\n");
                
                return testResult;
            }
            catch (Exception e)
            {
                WriteFailMessage($"Internal Error running test: {CurrentlyExecutingTest}, Message: {e.Message}");             
                WriteFailMessage($"Inner Exception: {e.InnerException}, Stack: {e.StackTrace}");  
                testResult.Result = false; // An exception is treated as a test failure
                return testResult;
            }
            finally
            {
                // Track result globally

                // Look if we've already got a result for this test (it might have failed and we're running it again, in which case we want to replace it with our latests result)
                int foundIndex = TestResultStore.IndexOf(testResult);
                var isAlreadyRunOnce = foundIndex > 1;
                var areRerunningFailedTests = options.RepeatUntilSuccessCount > 0;
                
                if(isAlreadyRunOnce && areRerunningFailedTests && TestResultStore[foundIndex].Result == false /* false=failed */)
                {
                    // Replace the last failure result with this one
                    TestResultStore.RemoveAt(foundIndex);
                }
                
                TestResultStore.Add(testResult);
                
            }

            // Local func
            void printPassFailMessage(FunctionTestPackage test, bool result)
            {
                if (result)
                {                    
                    WritePassMessage($"{test.TestName} Passed", true);
                }
                else
                {
                    WriteFailMessage($"{test.TestName} Failed", true);
                }
            };
        }

        /// <summary>
        /// Runs some code and optionally reports how long it took. It returns the original code return type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="elapsedTimeMs"></param>
        /// <param name="reportImmediately"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static T TimeThisAndReturn<T>(Func<T> operation, out long elapsedTimeMs, bool reportImmediately = false, string message = null, SimpleFileLogger logger = null)
        {
            var stoFirstatch = new Stopwatch();
            var result = operation();
            elapsedTimeMs = stoFirstatch.ElapsedMilliseconds;
            if (reportImmediately)
            {
                WriteMessageLine("Operation SnapshotTime: " + message + $" took {elapsedTimeMs} ms", fileLogger: logger);
            }
            return result;
        }

        public class StringCurrentCultureIgnoreCaseEqualityComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return x.Equals(y, StringComparison.CurrentCultureIgnoreCase );
            }

            public int GetHashCode(string obj)
            {
                return obj.GetHashCode();
            }
        }

        /// <summary>
        /// An object to store the results of an API Call (it also stores configuration for this ResultStore)
        /// Information about how to work with the objects/times that you retain during tests.
        /// Primarily this will be for comparison purposes with other runs of the same test eg with and without Second support
        /// </summary>
        public class ApiCallResultContainer
        {
            
            /// <summary>
            /// Response object store for specific test
            /// </summary>
            public object Object = null;

            public enum ComparisonProgress { NotStarted, Finished };

            public ComparisonProgress ComparisonStatus {get;set;}

            /// <summary>
            /// Times for the specific test
            /// </summary>
            public long Time = default(long);
            /// <summary>
            /// Configuration used for comparing objects stored
            /// </summary>
            public ComparisonConfig ComparisonConfig {get; set;}
            private bool alreadyAddedIgnorePortIdNameCode = false;
            private bool alreadyAddedIgnoreTimeRelatedMembers = false;

            /// <summary>
            /// Name of the API call/method that represents this result
            /// </summary>
            public string TestName { get;set;}
            public string SpecificMethodWithinTest { get;set;}
            /// <summary>
            /// This is used to determine two objects need to be compared
            /// </summary>
            public string MethodId {get;set;}
            public TestOptions TestOptions { get; }
            public bool IsSecondTest {get; private set;}

            /// <summary>
            /// Stores information about a ApiCalls results
            /// Creates a object which stores information about how to work with any retain objects
            /// </summary>
            /// <param name="testOptions"></param>
            /// <param name="ignorePortIdNameCode"></param>
            /// <param name="patternsToIgnore">such as *Name or *Code</param>
            /// <param name="ignoreTimeRelatedMembers"></param>
            public ApiCallResultContainer(TestOptions testOptions, bool ignorePortIdNameCode = true, string[] patternsToIgnore = null, bool ignoreTimeRelatedMembers = false)
            {
                ComparisonStatus = ComparisonProgress.NotStarted;
                ComparisonConfig = new ComparisonConfig();
                IsSecondTest = !testOptions.AreSimulatingExistingFirstClient;

                if(patternsToIgnore != null && patternsToIgnore.Length > 0)
                {
                    foreach (var pattern in patternsToIgnore)
                    {
                        ComparisonConfig.MembersToIgnore.Add(pattern); 
                    }
                    
                }
                ComparisonConfig.MaxDifferences = 100;
                ComparisonConfig.IgnoreCollectionOrder = true;
                ComparisonConfig.CompareChildren = true;
                TestOptions = testOptions;
                

            }

            public override bool Equals(object obj)
            {
                return obj is ApiCallResultContainer container &&
                       TestName == container.TestName &&
                       SpecificMethodWithinTest == container.SpecificMethodWithinTest &&
                       MethodId == container.MethodId &&
                       IsSecondTest == container.IsSecondTest;
            }

            public override int GetHashCode()
            {
                var hashCode = 526242408;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TestName);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SpecificMethodWithinTest);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MethodId);
                hashCode = hashCode * -1521134295 + IsSecondTest.GetHashCode();
                return hashCode;
            }
        }
        public class DualTestResult
        {
            public bool Result { get; set; }
            public string TestName { get; set; }
            public bool IsSecondTest { get; set; }
            public bool IsScenarioTest { get;set;}

            public DualTestResult(bool result, string testName, bool isSecondTest, bool isScenarioTest)
            {
                Result = result;
                TestName = testName;
                IsSecondTest = isSecondTest;
                IsScenarioTest = isScenarioTest;
            }

            protected bool Equals(DualTestResult other)
            {
                return Result == other.Result && TestName == other.TestName && IsSecondTest == other.IsSecondTest;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((DualTestResult) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Result.GetHashCode();
                    hashCode = (hashCode * 397) ^ (TestName != null ? TestName.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ IsSecondTest.GetHashCode();
                    return hashCode;
                }
            }
        }

        /// <summary>
        /// Look through the Global Test result store and print the results
        /// </summary>
        /// <param name="saveToFile"></param>
        /// <param name="optPrintOnlyFailedTests"></param>
        public static void PrintSummaryOfResults(bool saveToFile, bool printOnlyFailedTests)
        {
            var testResultsByName = new Dictionary<string, PassedOrFailedResult>();
            foreach (var testResults in TestResultStore.GroupBy(x => x.TestName))
            {
                // Store results into a nicer data structure
                foreach (var item in testResults)
                {
                    var passedOrFailedResult = new PassedOrFailedResult {PassedSecond = null, PassedFirst = null};

                    if (!testResultsByName.ContainsKey(item.TestName))
                    {
                        testResultsByName.Add(item.TestName, passedOrFailedResult);
                    }
                    else
                    {
                        passedOrFailedResult = testResultsByName[item.TestName];
                    }

                    if (item.IsSecondTest)
                    {
                        passedOrFailedResult.PassedSecond = item.Result;
                    }
                    else
                    {
                        passedOrFailedResult.PassedFirst = item.Result;
                    }

                    testResultsByName[item.TestName] = passedOrFailedResult;
                }
            }
            
            // Printing...
            
            foreach (var item in testResultsByName)
            {
                var showFailures = item.Value.PassedFirst.Is(false) || item.Value.PassedSecond.Is(false);
                var failureIndicator = showFailures ? "<---" : "";
                
                if(printOnlyFailedTests && showFailures) 
                {
                    Console.Write($"{item.Key,35}\t\t FirstPassed:");
                    
                    if (item.Value.PassedFirst.HasValue)
                    {
                        ConvertToColour(item.Value.PassedFirst.Value, withoutNewLine: true);
                    }
                    else
                    {
                        WriteMessageLine("Unknown", withoutNewLine: true);
                    }

                    Console.Write("\tSecondPassed:");
                    if (item.Value.PassedSecond.HasValue)
                    {
                        ConvertToColour(item.Value.PassedSecond.Value, withoutNewLine: true);
                    }
                    else
                    {
                        WriteMessageLine("Unknown", withoutNewLine: true);
                    }

                    WriteMessageLine(msg:failureIndicator, withoutNewLine: false);
                }
                // Write to file
                
                var newLine = String.Format($"{item.Key},{BoolToStr(item.Value.PassedFirst)},{BoolToStr(item.Value.PassedSecond)}");
                var csv = new StringBuilder();
                    csv.Append("API Name,FirstPassed,SecondPassed" + Environment.NewLine);
                    csv.AppendLine(newLine);

                if (!saveToFile) continue;

                var filePath = "TestResultSummaryFor" + DateTime.Now.ToString("yyyy-dd-M--HH-mm") + ".csv";
                Console.WriteLine($"\nSaving results of test run to {filePath}");
                File.WriteAllText(filePath, csv.ToString());

                /*Local funcs: */

                // local func:  Converts A True into a Green True and  False into a Red False
                void ConvertToColour(bool result, bool withoutNewLine)
                {
                    if (result)
                    {
                        WritePassMessage(result.ToString(), withoutNewLine);
                    }
                    else
                    {
                        WriteFailMessage(result.ToString(), withoutNewLine);
                    }
                }
                // local func: 
                string BoolToStr(bool? result)
                {
                    if (!result.HasValue)
                    {
                        return "Unknown/NotRun";
                    }

                    if (result.HasValue && result.Value)
                    {
                        return Boolean.TrueString;
                    }

                    return Boolean.FalseString;
                }
            }
        }

        /// <summary>
        /// Checks a nullable has a value that you expect or not.
        /// Prevents boiler plate code to do this check all the time.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <param name="expected"></param>
        /// <returns></returns>
        public static bool Is<T>(this T? type, bool expected) where T : struct
        {
            if (type.HasValue && type.Value.Equals(expected)) { return true; }
            return false;
        }
        
        public static string SwitchTestUser(CustomerIdentityType customerIdentityType, Action<string> authenticator, Action onCompleteAction = null, string overrideUseSpecificUserUuId = null)
        {
            var username = String.Empty;
            var userOverrideProvided = !String.IsNullOrEmpty(overrideUseSpecificUserUuId);

            if (userOverrideProvided)
            {
                Do(() => username = overrideUseSpecificUserUuId,
                    log: () => WriteMessageLine($"Using a specified user '{overrideUseSpecificUserUuId}'",
                        messageStatus: Status.Info));
            }

            if (!userOverrideProvided)
            {
                switch (customerIdentityType)
                {
                    case CustomerIdentityType.CustomerType2:
                        Do(() => username = "usertype1",
                            log: () => WriteMessageLine("Using a Second customer", messageStatus: Status.Info));

                        break;
                    case CustomerIdentityType.CustomerType1:
                        Do(() => username = "usertype2",
                            log: () => WriteMessageLine("Using a non-Second Customer", messageStatus: Status.Info));

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(customerIdentityType), customerIdentityType, null);
                }
            }

            Do(() => authenticator(username),
                log: () => WriteMessageLine($"Performing log on as {username}...", withoutNewLine: true), then: onCompleteAction);
            return username;
        }

        /// <summary>
        /// Takes the list of suites and runs all the tests.
        /// </summary>
        /// <param name="optListTests">User wants to list all suites and tests</param>
        /// <param name="suitesOfTests">Dictionary  of suite names mapped to the list of functions for that test</param>
        /// <param name="optRunOnlyTheseSuites">User wants to run only these suits (and thus those tests within it)</param>
        /// <param name="optExcludeSuites">User wants to not run tests within specific provided suites</param>
        /// <param name="optExcludeTests">User wants to exclude specific tests</param>
        /// <param name="optRunOnlyTheseTests">User wants to explicitly run specific tests - trumps/overrides excluded suites exclusions</param>
        /// <param name="globalTestOptions">Global options that all tests have access to</param>
        /// <param name="optRecordResults">User wants to record the results in a csv</param>
        /// <param name="optDoNotPrintRerunCommands">User doesnt not want to see the commands required to run the failed tests</param>
        /// <param name="optPrintOnlyFailedTests">Only print failed tests in summary </param>
        /// <returns></returns>
        public static int PerformTestRuns(CommandOption optListTests, Dictionary<string, List<FunctionTestPackage>> suitesOfTests, List<string> optRunOnlyTheseSuites,
            List<string> optExcludeSuites, List<string> optExcludeTests, List<string> optRunOnlyTheseTests,
            TestOptions globalTestOptions, bool optRecordResults, bool optDoNotPrintRerunCommands, bool optPrintOnlyFailedTests)
        {
            // List tests if asked to do so
            if (optListTests.HasValue())
            {
                foreach (var suite in suitesOfTests)
                {
                    var suiteName = suite.Key;
                    var suiteTests = suite.Value;
                    Console.WriteLine($"Suite: {suiteName} :");
                    foreach (var test in suiteTests)
                    {
                        Console.WriteLine($"\t{test.TestName}\t");
                    }
                }
                
                return 0; // Printing all tests doesn't run any tests, just returns
            }

            var childScenarios = suitesOfTests.ToDictionary(x=>x.Key, x=>x.Value.SelectMany(y=>y.ScenariosTests).ToList()).Where(x=>x.Value.Count > 0);

            // filter out any specified suites
            var suitesToRun = suitesOfTests
                .Union(childScenarios) // pull in the list of child scenarios also
                .Where(x => !optRunOnlyTheseSuites.Any() || optRunOnlyTheseSuites.Contains(x.Key))
                .Where(x => !optExcludeSuites.Any() || !optExcludeSuites.Contains(x.Key));

            // filter out any specified tests from remain suites
            var allTests = suitesToRun.SelectMany(x => x.Value);
            var testToRun = allTests.Where(x => !optExcludeTests.Contains(x.TestName)).ToList();

            if (optRunOnlyTheseTests.Count > 0)
            {
                testToRun = allTests.Where(t => optRunOnlyTheseTests.Contains(t.TestName)).ToList();
            }

            // Internal run after all filtering has been done
            var failedTests = RunTests(testToRun, globalTestOptions).Where(x=>x.Key.Result == false).ToArray();

            // Print a summary of the results
            PrintSummaryOfResults(optRecordResults, optPrintOnlyFailedTests);

            // Show failed tests' re-run commands
            if (!optDoNotPrintRerunCommands)
            {
                // print re-run commands
                var reruncommands = new List<string>();
                foreach (var testResults in TestResultStore.GroupBy(x => x.TestName))
                {
                    foreach (var item in testResults)
                    {
                        if (item.Result == false && !reruncommands.Contains(item.TestName))
                        {
                            reruncommands.Add($"-o {item.TestName}");
                        }
                    }
                }
            }

            if (!Enumerable.Any(failedTests))
            {
                Console.WriteLine($"\nSuccess: All tests passed.");
            }

            return failedTests.Any() ? -1 : 0;
        }

        public static void SayDone() => WriteMessageLine(" Done", messageStatus: Status.Success);
    }
    
    public class Continuation<T> where T : class
    {
        public Continuation(T result)
        {
            Result = result;
        }

        public Continuation()
        {
            Result = null;
        }
        public bool HasResult => Result != null;

        public T Result { get; }

        /// <summary>
        /// Run an any action but ignores its result and returns prior result instead
        /// </summary>
        /// <param name="action"></param>
        /// <returns>returns the result prior to the then</returns>
        public T ThenFinally(Func<T,T> action)
        {
            action(Result); return Result;
        }

        public Continuation<T> Process(Func<T,T> action)
        {
            return new Continuation<T>(action(Result));
        }


        /// <summary>
        /// Run any function taking in T but ignores its result returning rather the prior result instead
        /// </summary>
        /// <param name="action"></param>
        /// <returns>Returns the result prior to the then</returns>
        public T ThenFinally(Action<T> action)
        {
            action(Result);
            return Result;
        }

        public T ThenFinishWith(Action action)
        {
            action();
            return Result;
        }

        //public Continuation<T> ThenFinally(Func<T,object> action) { return new Continuation<T>((T)action(Result)); }
    }

    /// <summary>
    /// A single indication of a tests result.
    /// A test can have a result for a when running the test as a Second customer or running that test a a non-Second customer.
    /// Running tests as a non-Second customer does not trigger any intercept code tin First that forwards requests to First instead of First
    /// </summary>
    public struct PassedOrFailedResult 
    {
        public bool? PassedFirst { get;set;}
        public bool? PassedSecond { get;set;}
    }

    /// <summary>
    /// Source code is released under the MIT license.
    /// 
    /// The MIT License (MIT)
    /// Copyright (c) 2014 Burtsev Alexey
    /// 
    /// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
    /// 
    /// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
    /// 
    /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
    /// </summary>
    public static class ObjectExtensions
    {
        private static readonly MethodInfo CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool IsPrimitive(this Type type)
        {
            if (type == typeof(String)) return true;
            return (type.IsValueType & type.IsPrimitive);
        }

        public static Object Copy(this Object originalObject)
        {
            return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
        }
        private static Object InternalCopy(Object originalObject, IDictionary<Object, Object> visited)
        {
            if (originalObject == null) return null;
            var typeToReflect = originalObject.GetType();
            if (IsPrimitive(typeToReflect)) return originalObject;
            if (visited.ContainsKey(originalObject)) return visited[originalObject];
            if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;
            var cloneObject = CloneMethod.Invoke(originalObject, null);
            if (typeToReflect.IsArray)
            {
                var arrayType = typeToReflect.GetElementType();
                if (IsPrimitive(arrayType) == false)
                {
                    Array clonedArray = (Array)cloneObject;
                    clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
                }

            }
            visited.Add(originalObject, cloneObject);
            CopyFields(originalObject, visited, cloneObject, typeToReflect);
            RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
            return cloneObject;
        }

        private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect)
        {
            if (typeToReflect.BaseType != null)
            {
                RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
                CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
            }
        }

        private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
        {
            foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags))
            {
                if (filter != null && filter(fieldInfo) == false) continue;
                if (IsPrimitive(fieldInfo.FieldType)) continue;
                var originalFieldValue = fieldInfo.GetValue(originalObject);
                var clonedFieldValue = InternalCopy(originalFieldValue, visited);
                fieldInfo.SetValue(cloneObject, clonedFieldValue);
            }
        }
        public static T Copy<T>(this T original)
        {
            return (T)Copy((Object)original);
        }
    }

    public class ReferenceEqualityComparer : EqualityComparer<object>
    {
        public override bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }
        public override int GetHashCode(object obj)
        {
            if (obj == null) return 0;
            return obj.GetHashCode();
        }
    }

    namespace ArrayExtensions
    {
        public static class ArrayExtensions
        {
            public static void ForEach(this Array array, Action<Array, int[]> action)
            {
                if (array.LongLength == 0) return;
                ArrayTraverse walker = new ArrayTraverse(array);
                do action(array, walker.Position);
                while (walker.Step());
            }
        }

        internal class ArrayTraverse
        {
            public int[] Position;
            private int[] maxLengths;

            public ArrayTraverse(Array array)
            {
                maxLengths = new int[array.Rank];
                for (int i = 0; i < array.Rank; ++i)
                {
                    maxLengths[i] = array.GetLength(i) - 1;
                }
                Position = new int[array.Rank];
            }

            public bool Step()
            {
                for (int i = 0; i < Position.Length; ++i)
                {
                    if (Position[i] < maxLengths[i])
                    {
                        Position[i]++;
                        for (int j = 0; j < i; j++)
                        {
                            Position[j] = 0;
                        }
                        return true;
                    }
                }
                return false;
            }
        }
    }
}

