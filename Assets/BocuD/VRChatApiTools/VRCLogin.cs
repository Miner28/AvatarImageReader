using System;
using UnityEngine;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    public static class VRCLogin
    {
        private static bool loginInProgress = false;

        public static void AttemptLogin(Action<ApiModelContainer<APIUser>> onSucces, Action<ApiModelContainer<APIUser>> onError)
        {
            if (loginInProgress) return;
            
            if (!ApiCredentials.Load())
                Debug.LogError("[<color=lime>VRChatApiTools</color>] You are currently not logged in. Please log in using the VRChat SDK Control panel.");
            else
            {
                loginInProgress = true;
                APIUser.InitialFetchCurrentUser(
                    (c) =>
                    {
                        onSucces(c);
                        loginInProgress = false;
                    },
                    (c) =>
                    {
                        onError(c);
                        loginInProgress = false;
                    }
                );
            }
        }
    }
}