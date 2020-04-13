using System;
using System.Linq;

namespace Second.Utils
{
    public static class RandomnessGenerator
    {
        /// <summary>
        /// What do you want your randomness to look like?
        /// </summary>
        public enum FormOfRandomness { NewGuid, RandomAlphaNum }

        /// <summary>
        /// A locking object to prevent two threads getting the same unique number
        /// </summary>
        private static object lockObject = new object();   
        
        /// <summary>
        /// A pesuedo random number generator
        /// </summary>
        private static readonly Random random = new Random();
        /// <summary>
        /// Generate a string of random characters of length 32
        /// </summary>
        /// <param name="natureOfRandomness"></param>
        /// <returns></returns>
        public static string GetRandom32LenString(FormOfRandomness natureOfRandomness) 
            => natureOfRandomness == FormOfRandomness.RandomAlphaNum 
                                    ? RandomString(32) 
                                    : Guid.NewGuid().ToString();
        /// <summary>
        /// Generate a random integter from lowerbound to higherbound
        /// </summary>
        /// <param name="lowerBound"></param>
        /// <param name="higherBound"></param>
        /// <returns>Uniqueness is not gaurenteed</returns>
        public static int GetRandomInt(int lowerBound = 0, int higherBound = int.MaxValue) 
            => new Random(Guid.NewGuid().GetHashCode()).Next(lowerBound, higherBound);

        /// <summary>
        /// Produces a unique number.
        /// </summary>
        /// <returns>next unique integer</returns>
        public static int GetNextUniqueInt()
        {
            lock(lockObject)
            {
                return (int)DateTime.Now.Ticks;
            }
        }
             
        /// <summary>
        /// Generates a random string derived from another characters in another string
        /// </summary>
        /// <param name="length">length of derived string to be created</param>
        /// <param name="deriveFromAnotherString">characters to use</param>
        /// <returns></returns>
        private static string RandomString(int length, string deriveFromAnotherString = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
            => new string(Enumerable.Repeat(deriveFromAnotherString, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}