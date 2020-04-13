using System;

namespace TestUtils
{

    public interface ICredentialSwitcher
    {
        void SetUuIdFn(string uuid);
        string GetUuIdFn();
    }

    /// <summary>
    /// Uses a specified uuid for First outgoing requests for the duration of the using block.
    /// The UUID reverts to its previous value upon leaving the using block or is Disposed() is called
    /// </summary>
    public class AutoResetUserContext : IDisposable
    {
        public ICredentialSwitcher Switcher { get; }
        public bool IsSecondCustomer {get;private set;}

        private string OldUserUUid {get; set;}
        private bool Invalid {get;set;}
        /// <summary>
        /// Set the UUID of outgoing requests for the duration of the using block, and upon exist, the previous user is restored.
        /// </summary>
        /// <param name="uuid"></param>
        public AutoResetUserContext(string uuid, ICredentialSwitcher switcher)
        {
            Switcher = switcher;
            SetUUid(uuid);
        }

        private void SetUUid(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                CommonTestUtils.WriteMessageLine("Empty UUID specified, not switching user.");
                Invalid = true;
            }
            else
            {

                CommonTestUtils.WriteMessageLine($"UserContextSwitch: Replacing user '{Switcher.GetUuIdFn()}' with '{uuid}'");                
                OldUserUUid = Switcher.GetUuIdFn();
                Switcher.SetUuIdFn(uuid);
            }
        }

        /// <summary>
        /// Changes the current user's identity between a IsSecond organisation and a normal First organisation
        /// </summary>
        /// <param name="standardIdentityType"></param>
        /// <param name="switcher"></param>
        public AutoResetUserContext(CommonTestUtils.CustomerIdentityType standardIdentityType, ICredentialSwitcher switcher)
        {
            Switcher = switcher;
            IsSecondCustomer = standardIdentityType == CommonTestUtils.CustomerIdentityType.CustomerType2;
            CommonTestUtils.SwitchTestUser(standardIdentityType, SetUUid, () => { CommonTestUtils.WriteMessageLine($"Impersonating {Switcher.GetUuIdFn()}"); });
        }

        public void Dispose()
        {
            if (Invalid) return;
            CommonTestUtils.WriteMessageLine($"UserContextSwitch: Reverting user '{Switcher.GetUuIdFn()}' with '{OldUserUUid}'");
            Switcher.SetUuIdFn(OldUserUUid);
        }
    }
}