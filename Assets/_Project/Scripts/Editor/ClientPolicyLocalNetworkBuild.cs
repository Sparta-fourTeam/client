#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Game.Editor
{
    /// <summary>Local QA Gradle output only. No device/OS trust settings or release manifest changes.</summary>
    public sealed class ClientPolicyLocalNetworkBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (Environment.GetEnvironmentVariable("NOVA_POLICY_QA_LOCAL") != "1") { return; }
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var xml = new XmlDocument(); xml.Load(manifest);
            var app = xml.SelectSingleNode("/manifest/application") as XmlElement;
            if (app == null) { throw new InvalidOperationException("QA Android application manifest missing."); }
            app.SetAttribute("networkSecurityConfig", "http://schemas.android.com/apk/res/android", "@xml/nova_local_qa_network");
            xml.Save(manifest);
            string resources = Path.Combine(path, "src/main/res/xml"); Directory.CreateDirectory(resources);
            File.WriteAllText(Path.Combine(resources, "nova_local_qa_network.xml"),
                "<network-security-config><base-config cleartextTrafficPermitted=\"false\"/>"
                + "<domain-config cleartextTrafficPermitted=\"true\"><domain includeSubdomains=\"false\">127.0.0.1</domain></domain-config></network-security-config>");
        }
    }
}
#endif
