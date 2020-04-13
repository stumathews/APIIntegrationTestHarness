using System;
using System.Collections.Generic;
using Microsoft.Extensions.CommandLineUtils;

namespace TestUtils
{
    /// <summary>
    /// A Function Test package represents a single test.
    /// It holds the initial test function and holds any linked scenarios tests that will run as part of the main test.
    /// </summary>
    public class FunctionTestPackage
    {
        /// <summary>
        /// Name of the function to test
        /// </summary>
        public string TestName { get; set; }

        /// <summary>
        /// CommandOption that enables/disables the test
        /// </summary>
        public CommandOption TestOption { get; set; }

        /// <summary>
        /// The function that represents the Test for the function under test
        /// </summary>
        public Func<TestOptions, bool> TestFunction { get; set; }

        /// <summary>
        /// Linked scenario tests for this test. These will run after the <see cref="TestFunction"/>
        /// Currently no state is stored between scenario tests and main test
        /// </summary>
        public List<FunctionTestPackage> ScenariosTests { get; set; }

        /// <summary>
        /// Indicates if this test can run within dual modes (IsSecond/IsFirst) or not
        /// </summary>
        public bool IsSingular {get;set;}

        /// <summary>
        /// A Function Test package represents a single test.
        /// It holds the initial test function and holds any linked scenarios tests that will run as part of the main test.
        /// </summary>
        /// <param name="testName"></param>
        /// <param name="disableTestOption"></param>
        /// <param name="testFunction"></param>
        /// <param name="specificScenarioTests"></param>
        /// <param name="singular">Indicates if this test can only run in Single Mode (no diffirentiation between IsSecond=True/False is used)</param>
        public FunctionTestPackage(string testName, CommandOption disableTestOption, Func<TestOptions, bool> testFunction, List<FunctionTestPackage> specificScenarioTests = null, bool singular = false)
        {
            TestName = testName;
            TestOption = disableTestOption;
            TestFunction = testFunction;
            IsSingular = singular;
            ScenariosTests = specificScenarioTests ?? new List<FunctionTestPackage>();
        }
        public override string ToString()
        {
            return $"Name={TestName}, Function={TestFunction}";
        }

        public override int GetHashCode() 
            => ScenariosTests.GetHashCode() * 17 + TestFunction.GetHashCode();

        public override bool Equals(object obj)
        {
            if(obj?.GetType() != typeof(FunctionTestPackage)) return false;
            return obj is FunctionTestPackage package && (package.TestFunction.Equals(TestFunction) && package.IsSingular == IsSingular);
        }
    }
}