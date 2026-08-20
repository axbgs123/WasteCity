using System;
using System.IO;

namespace WasteCity.Persistence
{
    public interface IFormalSaveFileSystem
    {
        bool FileExists(string path);
        byte[] ReadAllBytes(string path);
        void CreateDirectory(string path);
        string CreateTemporarySiblingPath(string targetPath, string purpose);
        void WriteAllBytesAndFlush(string path, byte[] bytes);
        void ReplaceAtomically(string sourcePath, string destinationPath);
        void DeleteIfExists(string path);
    }

    public enum FormalSaveTransactionStage
    {
        None,
        WriteTemporary,
        ValidateTemporary,
        UpdateBackup,
        ReplacePrimary,
        ValidatePrimary,
    }

    public sealed class FormalSaveFileTransactionResult
    {
        internal FormalSaveFileTransactionResult(
            bool success,
            FormalSaveTransactionStage failedStage,
            string diagnostic)
        {
            Success = success;
            FailedStage = failedStage;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Success { get; }
        public FormalSaveTransactionStage FailedStage { get; }
        public string Diagnostic { get; }
    }

    public sealed class FormalSaveFileTransaction
    {
        private readonly IFormalSaveFileSystem fileSystem;

        public FormalSaveFileTransaction(IFormalSaveFileSystem fileSystem)
        {
            this.fileSystem = fileSystem ??
                throw new ArgumentNullException(nameof(fileSystem));
        }

        public FormalSaveFileTransactionResult Commit(
            string primaryPath,
            byte[] bytes,
            Func<byte[], bool> validator)
        {
            if (string.IsNullOrWhiteSpace(primaryPath) || bytes == null ||
                validator == null)
                return Failed(
                    FormalSaveTransactionStage.WriteTemporary,
                    "文件事务参数无效");

            string primaryTemporary = null;
            string backupTemporary = null;
            FormalSaveTransactionStage stage =
                FormalSaveTransactionStage.WriteTemporary;
            try
            {
                string fullPrimary = primaryPath;
                string directory = Path.GetDirectoryName(fullPrimary);
                if (string.IsNullOrEmpty(directory)) directory = ".";
                fileSystem.CreateDirectory(directory);
                string backupPath = fullPrimary + ".bak";
                primaryTemporary =
                    fileSystem.CreateTemporarySiblingPath(
                        fullPrimary,
                        "primary");

                fileSystem.WriteAllBytesAndFlush(primaryTemporary, bytes);
                stage = FormalSaveTransactionStage.ValidateTemporary;
                if (!validator(fileSystem.ReadAllBytes(primaryTemporary)))
                    return Failed(stage, "临时存档验证失败");

                if (fileSystem.FileExists(fullPrimary))
                {
                    stage = FormalSaveTransactionStage.UpdateBackup;
                    byte[] current = fileSystem.ReadAllBytes(fullPrimary);
                    if (validator(current))
                    {
                        backupTemporary =
                            fileSystem.CreateTemporarySiblingPath(
                                backupPath,
                                "backup");
                        fileSystem.WriteAllBytesAndFlush(
                            backupTemporary,
                            current);
                        if (!validator(
                                fileSystem.ReadAllBytes(backupTemporary)))
                            return Failed(stage, "备份临时文件验证失败");
                        fileSystem.ReplaceAtomically(
                            backupTemporary,
                            backupPath);
                        backupTemporary = null;
                    }
                }

                stage = FormalSaveTransactionStage.ReplacePrimary;
                fileSystem.ReplaceAtomically(
                    primaryTemporary,
                    fullPrimary);
                primaryTemporary = null;

                stage = FormalSaveTransactionStage.ValidatePrimary;
                if (!validator(fileSystem.ReadAllBytes(fullPrimary)))
                    return Failed(stage, "新主存档复读验证失败");
                return new FormalSaveFileTransactionResult(
                    true,
                    FormalSaveTransactionStage.None,
                    string.Empty);
            }
            catch (Exception exception)
            {
                return Failed(stage, exception.GetType().Name +
                    ": " + exception.Message);
            }
            finally
            {
                Cleanup(primaryTemporary);
                Cleanup(backupTemporary);
            }
        }

        private static FormalSaveFileTransactionResult Failed(
            FormalSaveTransactionStage stage,
            string diagnostic)
        {
            return new FormalSaveFileTransactionResult(
                false,
                stage,
                diagnostic);
        }

        private void Cleanup(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                fileSystem.DeleteIfExists(path);
            }
            catch
            {
            }
        }
    }

    public sealed class SystemFormalSaveFileSystem : IFormalSaveFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
        public void CreateDirectory(string path) =>
            Directory.CreateDirectory(path);

        public string CreateTemporarySiblingPath(
            string targetPath,
            string purpose)
        {
            return targetPath + "." + purpose + "." +
                   Guid.NewGuid().ToString("N") + ".tmp";
        }

        public void WriteAllBytesAndFlush(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void ReplaceAtomically(
            string sourcePath,
            string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Replace(sourcePath, destinationPath, null);
            else
                File.Move(sourcePath, destinationPath);
        }

        public void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
