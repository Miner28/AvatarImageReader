using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace AvatarImageReader
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CheckHierarchy : UdonSharpBehaviour
    { 
    /*
     * This Script is meant to be attatched to the Pedestal it is intending to scan from 
     */
    
        [SerializeField] private GameObject renderQuad;
        [SerializeField] private ReadRenderTexture readRenderTexture;

        [Header("Debug")]
        [SerializeField] private GameObject textureComparisonPlane;
        [SerializeField] private bool overrideTextureEnabled = false;
        [SerializeField] private Texture2D overrideTexture;

        public void Update()
        {
            if (overrideTextureEnabled)
            {
                // Assign the Texture to the Render pane and the comparison pane
                if (renderQuad != null)
                {
                    renderQuad.GetComponent<MeshRenderer>().material.SetTexture(1, overrideTexture);
                }

                if (textureComparisonPlane != null)
                {
                    textureComparisonPlane.GetComponent<MeshRenderer>().material.SetTexture(1, overrideTexture);
                }

                readRenderTexture.pedestalReady = true;

                Destroy(this);
            }
            else if (transform.childCount == 1)
            {
                for (int i = 0; i < transform.GetChild(0).childCount; i++)
                {
                    // Find the Child used for the image component
                    if (transform.GetChild(0).GetChild(i).name.Equals("Image"))
                    {
                        Texture texture = transform.GetChild(0).GetChild(i).GetComponent<MeshRenderer>().material
                            .GetTexture("_WorldTex");

                        if (texture != null)
                        {
                            Debug.Log("CheckHierarchyScript: Retrieving Avatar Pedestal Texture");

                            // Assign the Texture to the Render pane and the comparison pane
                            if (renderQuad != null)
                            {
                                renderQuad.GetComponent<MeshRenderer>().material.SetTexture(1,
                                    transform.GetChild(0).GetChild(i).GetComponent<MeshRenderer>().material
                                        .GetTexture("_WorldTex"));
                                renderQuad.SetActive(true);
                            }

                            // Render the texture to a comparison plane if that is enabled for debugging purposes
                            if (textureComparisonPlane != null)
                            {
                                textureComparisonPlane.GetComponent<MeshRenderer>().material.SetTexture(1,
                                    transform.GetChild(0).GetChild(i).GetComponent<MeshRenderer>().material
                                        .GetTexture("_WorldTex"));
                            }

                            readRenderTexture.pedestalReady = true;
                            
                   
                            
                            Destroy(this);
                        }
                    }
                }
            }
        }
    }
}