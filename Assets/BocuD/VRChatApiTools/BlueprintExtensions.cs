using System;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    public static class BlueprintExtensions
    {
        public static void PublishWorldToCommunityLabs(this ApiWorld world,
            Action<ApiModelContainer<ApiWorld>> successCallback,
            Action<string> errorCallback)
        {
            ApiModelContainer<ApiWorld> apiModelContainer = new ApiModelContainer<ApiWorld>
            {
                OnSuccess = c =>
                {
                    if (successCallback == null)
                        return;
                    successCallback(c as ApiModelContainer<ApiWorld>);
                },
                OnError = c =>
                {
                    if (errorCallback == null)
                        return;
                    errorCallback(c.Error);
                }
            };
            
            API.SendPutRequest("worlds/" + world.id + "/publish", apiModelContainer);
        }
        
        public static void UnPublishWorld(this ApiWorld world,
            Action<ApiModelContainer<ApiWorld>> successCallback,
            Action<string> errorCallback)
        {
            ApiModelContainer<ApiWorld> apiModelContainer = new ApiModelContainer<ApiWorld>
            {
                OnSuccess = c =>
                {
                    if (successCallback == null)
                        return;
                    successCallback(c as ApiModelContainer<ApiWorld>);
                },
                OnError = c =>
                {
                    if (errorCallback == null)
                        return;
                    errorCallback(c.Error);
                }
            };
            
            API.SendDeleteRequest("worlds/" + world.id + "/publish", apiModelContainer);
        }
    }
}