using Game.Editor;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CiBuildArgsTests
    {
        [Test(Description = "키와 값을 읽고, 값이 없는 플래그는 빈 문자열이 된다")]
        public void Parse_ReadsKeyValuesAndFlags()
        {
            var args = CiBuildArgs.Parse(new[] { "Unity", "-batchmode", "-customBuildPath", "build/a.apk", "-quit" });

            Assert.AreEqual("build/a.apk", args["customBuildPath"]);
            Assert.AreEqual("", args["batchmode"]);
            Assert.AreEqual("", args["quit"]);
        }

        [Test(Description = "비밀번호가 '-'로 시작해도 다음 인자로 오인하지 않고 값으로 읽는다")]
        public void Parse_PasswordStartingWithDash_IsAValue()
        {
            var args = CiBuildArgs.Parse(new[] { "-androidKeystorePass", "-secret", "-androidKeyaliasName", "release" });

            Assert.AreEqual("-secret", args["androidKeystorePass"]);
            Assert.AreEqual("release", args["androidKeyaliasName"]);
        }

        [Test(Description = "비어 있는 값(-androidTargetSdkVersion 같은)은 다음 키를 먹지 않는다")]
        public void Parse_UnknownEmptyFlag_DoesNotConsumeNextKey()
        {
            var args = CiBuildArgs.Parse(new[] { "-androidTargetSdkVersion", "-androidExportType", "androidPackage" });

            Assert.AreEqual("", args["androidTargetSdkVersion"]);
            Assert.AreEqual("", args["androidExportType"]);
        }

        [Test(Description = "값을 받는 키가 맨 끝에 있어도 예외 없이 빈 값이 된다")]
        public void Parse_ValueKeyAtEnd_IsEmpty()
        {
            var args = CiBuildArgs.Parse(new[] { "-customBuildProfile" });

            Assert.AreEqual("", args["customBuildProfile"]);
        }
    }
}
