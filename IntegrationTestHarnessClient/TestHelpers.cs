using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NUnit.Framework;
using TestUtils;
using static TestUtils.CommonTestUtils;

namespace Tests
{

    internal class TestHelpers
    {
        public const string FirstErrorIdentifierSuffix = "** Error:";
        private static bool MissingMandatoryArguments<T>(CommandLineApplication target, Func<T, bool> predicate,
            params T[] arguments)
        {
            var emptyArgs = arguments.Where(predicate).ToArray();
            if (emptyArgs.Any())
            {
                var missingArgList = String.Join(",", emptyArgs.Select(x => x));
                Console.WriteLine($"{FirstErrorIdentifierSuffix} Missing mandatory arguments: {missingArgList}");
                target.ShowHelp(target.Name);
                return true;
            }

            return false;
        }

        public static void Cleanup()
        {

        }

        public static void Cleanup(int[] SubjectId, TestOptions testOptions)
        {
            if (SubjectId.Length == 0) return;
            foreach(var id in SubjectId)
            {
                Cleanup();
            }
        }

        /// <summary>
        /// Run assumptions depending if the client is a Second client or a existing First client. No cleanup code is required in handlers
        /// </summary>
        /// <param name="generalAssumptionsMet">True if general assumptions handler about returned data are met</param>
        /// <param name="assumptionsMetIfNewSecondCustomer">handler function that tests if the data returned if valid in context of a Second customer. Returns true on pass , false on fail</param>
        /// <param name="assumptionsMetIfExistingFirstCustomer">Function that tests if the data returned if valid in context of an existing customer. Returns true on pass , false on fail</param>
        /// <param name="options">test options</param>
        /// <param name="SubjectId">Subject to delete at the end</param>
        /// <param name="SubjectIdsToDelete"></param>
        /// <param name="alwaysDeleteSubjectAtEnd">Always delete Subject and the end of this function</param>
        /// <param name="transactionsToDelete"></param>
        /// <param name="ignoreFailures">dont fail</param>
        /// <remarks>The appropriate validation function is called based on if the test is 'options.AreSimulatingExistingFirstClient' </remarks>
        /// <returns>True if valiation routines found no problems, false otherwise</returns>
        public static bool ValidateAssumptionsMet(Func<bool> generalAssumptionsMet,
            Func<bool> assumptionsMetIfNewSecondCustomer, Func<bool> assumptionsMetIfExistingFirstCustomer,
            TestOptions options, int[] SubjectIdsToDelete, bool alwaysDeleteSubjectAtEnd = true, IEnumerable<DateTime> transactionsToDelete = null, bool ignoreFailures = false)
        {
            var isGeneralAssumptionsMet = false;
            try
            {
                isGeneralAssumptionsMet = generalAssumptionsMet();
            }
            catch (AssertionException)
            {
                // Silently ignore the exception, but the isGeneralAssumptionsMet will be set to false so we know it has failed
            }

            if (!isGeneralAssumptionsMet)
            {
                WriteMessageLine("** General tests failed.");
                foreach (var id in SubjectIdsToDelete){ Cleanup(); }
                return false;
            }

            bool result = false;
            if(options.AreSimulatingExistingFirstClient)
            {
                try
                {
                    result = assumptionsMetIfExistingFirstCustomer();
                }
                catch (Exception e)
                {
                    // Silently ignore the exception, but the result will be set to false so we know it has failed
                    WriteFailMessage(e.Message);
                }

                if (!result) {
                    WriteMessageLine("** First specific tests failed."); }
            } 
            else 
            {
                try
                {
                    result = assumptionsMetIfNewSecondCustomer();
                }
                catch (Exception e)
                {
                    // Silently ignore the exception, but the result will be set to false so we know it has failed
                    WriteFailMessage(e.Message);
                }
                if(!result) {
                    WriteMessageLine("** Second specific tests failed."); }
            }

            if(alwaysDeleteSubjectAtEnd)
            {
                foreach(var id in SubjectIdsToDelete){ Cleanup(); }
            }
            return result;
        }
    }
}
