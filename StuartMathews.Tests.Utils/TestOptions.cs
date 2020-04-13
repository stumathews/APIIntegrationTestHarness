using System.Collections.Generic;
using System.Linq;

namespace TestUtils
{
    /// <summary>
    /// A collection of shared options and data that can affect how any test might behave
    /// </summary>
    public class TestOptions
    {
        /// <summary>
        /// Copy me!
        /// </summary>
        /// <returns></returns>
        public TestOptions Clone()
        {
            var other = (TestOptions)MemberwiseClone();
            other.RunSpecificTests = other.RunSpecificTests.Select(x=>x); // clone
            return other;
        }

        /// <summary>
        /// Runs the last test only
        /// </summary>
        public bool RunLastTestOnly { get; set; }
        
        /// <summary>
        /// Do not add Second properties to created Subjects
        /// </summary>
        public bool AreSimulatingExistingFirstClient { get; set; }

        /// <summary>
        /// Does a diff between two of the same objects (IsSecond response vs IsFirst response)
        /// </summary>
        public bool CompareRetainedObjects { get;set;}

        /// <summary>
        /// True/False string if this is a Second customer
        /// <see cref="AreSimulatingExistingFirstClient"/>
        /// </summary>
        public string SimulateSecondClientString => (!AreSimulatingExistingFirstClient).ToString();
        
        /// <summary>
        /// Runs the tests in reverse order
        /// </summary>
        public bool RunTestsInReverseOrder { get; set; }

        /// <summary>
        /// Run the provided tests. The list of test names are case sensitive
        /// </summary>
        public IEnumerable<string> RunSpecificTests { get; set; }

        /// <summary>
        /// Report each step that is registered by tests
        /// </summary>
        public bool ReportStepSummary { get; set; }

        /// <summary>
        /// Increase detail
        /// </summary>
        public bool BeVerbose { get;set; }

        /// <summary>
        /// Tell the test runner to run tests both as existing First client and new Second client
        /// </summary>
        public bool RunDual { get; set; }

        /// <summary>
        /// Runs all scenarios tests linked to the main api test
        /// </summary>
        public bool RunAllScenarios { get; set;}

        // Don't actually call Second/First
        public bool DryRun { get;set;}

        /// <summary>
        /// Be quiet please!
        /// </summary>
        public bool BeQuiet { get;set;}

        /// <summary>
        /// After Tearing down the test, and deleting the created Subjects - ensure they were deleted by calling get Subject on hopefully now non-existant Subjects
        /// </summary>
        public bool ValidateDelete { get; set; }

        /// <summary>
        /// Specify the User to use
        /// </summary>
        public string RunAsUUid {get;set;}

        /// <summary>
        /// Dump responses
        /// </summary>
        public bool DumpResponses {get;set;}

        /// <summary>
        /// Dump requests
        /// </summary>
        public bool DumpRequests {get;set;}

        /// <summary>
        /// Wait for user interaction before continuing to the next test - basically to allow user to see the output
        /// </summary>
        public bool WaitBetweenTests { get; set; }

        /// <summary>
        /// Use the configured dbg urls - used when debugging locally
        /// </summary>
        public bool UseDebugConnectionUrls { get; set; }

        /// <summary>
        /// Diff compared objects to Xml and write to XML files for easy comparison
        /// </summary>
        public bool WriteResponseDifferencesToFiles { get; set; }
        public bool AlwaysRetainResponses { get; set; }

        /// <summary>
        /// Repeat failing tests until this count is exceeded
        /// </summary>
        public int RepeatUntilSuccessCount { get; set; }
    }
}