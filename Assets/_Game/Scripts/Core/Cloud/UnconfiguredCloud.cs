using System.Collections.Generic;
using System.Threading.Tasks;

namespace UtsavNights.Core.Cloud
{
    /// <summary>A release build without its online service: every call says so plainly (no pretend sign-in).</summary>
    public sealed class UnconfiguredCloud : ICloud
    {
        public string Name => "none";

        public string Uid => null;

        public bool IsAnonymous => false;

        public Task<CloudResult<string>> SignInWithGoogle() => No<string>();

        public Task<CloudResult<string>> SignInAnonymously() => No<string>();

        public void SignOut()
        {
        }

        public Task<CloudResult<string>> BackupKey() => No<string>();

        public Task<CloudResult<CloudBackupInfo>> PutBackup(byte[] sealedBackup, CloudBackupInfo info) => No<CloudBackupInfo>();

        public Task<CloudResult<List<CloudBackupInfo>>> ListBackups() => No<List<CloudBackupInfo>>();

        public Task<CloudResult<byte[]>> GetBackup(string id) => No<byte[]>();

        public Task<CloudResult<bool>> DeleteBackup(string id) => No<bool>();

        public Task<CloudResult<ShareInfo>> Publish(ShareKind kind, string payload) => No<ShareInfo>();

        public Task<CloudResult<ShareInfo>> Open(string code) => No<ShareInfo>();

        public Task<CloudResult<bool>> RecordVisit(string code) => No<bool>();

        public Task<CloudResult<bool>> Report(string code, ReportReason reason) => No<bool>();

        public Task<CloudResult<bool>> Withdraw(string code) => No<bool>();

        public Task<CloudResult<List<ShareInfo>>> MyShares() => No<List<ShareInfo>>();

        public Task<CloudResult<bool>> DeleteMyData() => No<bool>();

        private static Task<CloudResult<T>> No<T>() => Task.FromResult(new CloudResult<T>(CloudError.NotConfigured));
    }
}
