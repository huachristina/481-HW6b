using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using Deceive;  // Assuming this is the namespace where StartupHandler is located

namespace Deceive.Tests
{
    [TestFixture]
    public class PermissionTests
    {
        private string testFilePath;

        [SetUp]
        public void Setup()
        {
            // Set up a common file path for all tests
            testFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Deceive", "test_permission.txt");
            if (!Directory.Exists(Path.GetDirectoryName(testFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(testFilePath));
            }
        }

        [Test]
        public void CheckAndRequestPermissions_PermissionsGranted_ReturnsTrue()
        {
            // Simulate permission granted by ensuring the file can be written and deleted
            File.WriteAllText(testFilePath, "Permission test.");
            File.Delete(testFilePath);

            // Assume CheckAndRequestPermissions handles checking this file successfully
            bool result = StartupHandler.CheckAndRequestPermissions();

            // Assert
            Assert.IsTrue(result, "Permissions should be correctly granted and checked.");
        }

        [Test]
        public void CheckAndRequestPermissions_PermissionsDenied_ReturnsFalse()
        {
            // Simulate permission denied by using a read-only attribute
            File.WriteAllText(testFilePath, "Permission test.");
            File.SetAttributes(testFilePath, FileAttributes.ReadOnly);

            bool result = StartupHandler.CheckAndRequestPermissions();

            // Clean up
            File.SetAttributes(testFilePath, FileAttributes.Normal);
            File.Delete(testFilePath);

            // Assert
            Assert.IsFalse(result, "Permissions should be denied and the function should return false.");
        }

        [Test]
        public void CheckAndRequestPermissions_FileDoesNotExist_ReturnsFalse()
        {
            // Ensure the file does not exist
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }

            bool result = StartupHandler.CheckAndRequestPermissions();

            // Assert
            Assert.IsFalse(result, "If the file does not exist, the function should return false.");
        }

        [TearDown]
        public void Cleanup()
        {
            // Ensure no test data is left over
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }
}