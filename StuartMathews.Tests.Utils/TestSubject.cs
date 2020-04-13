using System;
using System.Diagnostics;
using static Second.Utils.RandomnessGenerator;

namespace TestUtils
{
    public class TestSubjectOptions
    {
        public TestSubjectOptions(bool performCopyInsteadOfMigrate = false)
        {
            PerformCopyInsteadOfMigrate = performCopyInsteadOfMigrate;
        }

        /// <summary>
        /// We'll not update the 
        /// </summary>
        public bool PerformCopyInsteadOfMigrate { get; }
    }


    public class TestSubject
    {
        public TestSubject(string name = null, string scope = null, string code = null, TestSubjectOptions options = null, bool useCallerAsSubjectName = true)
        {
            Name = name ?? (useCallerAsSubjectName ? $"{new StackTrace().GetFrame(1).GetMethod().Name}-{Guid.NewGuid().ToString()}"  : Guid.NewGuid().ToString());
            Scope = scope;
            Code = code ?? GetRandom32LenString(FormOfRandomness.NewGuid);
            Options = options ?? new TestSubjectOptions();
        }
        public string Name {get;set;} 
        public string Scope {get;set;}
        public string Code {get;set;}
        public TestSubjectOptions Options { get; }
    }
    
}