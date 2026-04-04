using System.Runtime.InteropServices;

namespace Extensions.Platforms.Windows
{

    [ComImport]
    [Guid("F864D016-2416-458F-95D2-2C641FD1853A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface INetFwRule
    {
        string Name { get; set; }
        string Description { get; set; }
        int Protocol { get; set; }
        string LocalPorts { get; set; }
        int Direction { get; set; }
        int Action { get; set; }
        bool Enabled { get; set; }
        int Profiles { get; set; }
    }

    [ComImport]
    [Guid("E2B3C97F-6BD1-41A3-BAE3-67A00B7B94B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface INetFwPolicy2
    {
        object Rules { get; } // This returns an INetFwRules collection
    }
    internal class OpenPort
    {
        public static void OpenPortViaNativeCom(int port, string appName)
        {
#if WINDOWS
            Type? policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (policyType == null) return;
            dynamic? fwPolicy2 = Activator.CreateInstance(policyType);

            Type? ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (ruleType == null) return;

            if (Activator.CreateInstance(ruleType) is not INetFwRule newRule) return;
            if (fwPolicy2 == null) return;

            newRule.Name = $"{appName} Web Host";
            newRule.Protocol = 6; // TCP
            newRule.LocalPorts = port.ToString();
            newRule.Direction = 1; // Inbound
            newRule.Action = 1;    // Allow
            newRule.Enabled = true;
            newRule.Profiles = 0x7FFFFFFF; // All profiles

            fwPolicy2.Rules.Add(newRule);
#endif
        }
    }
}
