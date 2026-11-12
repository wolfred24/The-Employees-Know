using System;
using Il2CppFishNet;
using Il2CppFishNet.Managing;

namespace SmartStorage;

internal static class Net
{
    internal static bool IsServer
    {
        get
        {
            try
            {
                NetworkManager manager = InstanceFinder.NetworkManager;
                return manager != null && manager.IsServer;
            }
            catch
            {
                return false;
            }
        }
    }
}
