using UnityEngine;

namespace BocuD.VRChatApiTools
{
    public class Constants
    {
        //delay in milliseconds after each write
        public const int postWriteDelay = 750;
        
        //constants from the sdk
        public const int kMultipartUploadChunkSize = 50 * 1024 * 1024; // 50 MB <- is 100MB in the SDK, modified here because 25MB makes more sense
        
        public const int SERVER_PROCESSING_WAIT_TIMEOUT_CHUNK_SIZE = 50 * 1024 * 1024;
        public const float SERVER_PROCESSING_WAIT_TIMEOUT_PER_CHUNK_SIZE = 120.0f;
        public const float SERVER_PROCESSING_MAX_WAIT_TIMEOUT = 600.0f;
        public const float SERVER_PROCESSING_INITIAL_RETRY_TIME = 2.0f;
        public const float SERVER_PROCESSING_MAX_RETRY_TIME = 10.0f;
        
        public static float GetServerProcessingWaitTimeoutForDataSize(int size)
        {
            float timeoutMultiplier = Mathf.Ceil(size / (float)SERVER_PROCESSING_WAIT_TIMEOUT_CHUNK_SIZE);
            return Mathf.Clamp(timeoutMultiplier * SERVER_PROCESSING_WAIT_TIMEOUT_PER_CHUNK_SIZE,
                SERVER_PROCESSING_WAIT_TIMEOUT_PER_CHUNK_SIZE, SERVER_PROCESSING_MAX_WAIT_TIMEOUT);
        }
    }
}