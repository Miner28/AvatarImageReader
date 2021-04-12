using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CheckHierarchyScript : UdonSharpBehaviour
{
    /**
     * This Script is meant to be attatched to the Pedistal it is intending to scan from 
     */
    
    [SerializeField] private GameObject cameraRenderingPlane;
    [SerializeField] private GameObject textureComparisonPlane;
    [SerializeField] private ReadRenderTexture readRenderTexture;

    private bool stop = false;


    public void Update()
    {
        
        if(transform.childCount == 1)
        {
            if (!stop)
            {
                for (int i = 0; i < transform.GetChild(0).childCount; i++)
                {
                    // Find the Child used for the image component
                    if (transform.GetChild(0).GetChild(i).name.Equals("Image"))
                    {
                        Texture texture = transform.GetChild(0).GetChild(i).GetComponent<MeshRenderer>().material.GetTexture("_WorldTex");

                        if(texture != null)
                        {
                            Debug.Log("CheckHierarchyScript: Retrieving Avatar Pedestal Texture");
                            Debug.Log(texture != null);
                            if (texture.name != null)
                            {
                                Debug.LogError(texture.name);
                            }

                            Texture2D texture2D = (Texture2D)texture;

                            Debug.Log("CheckHierarchyScript: Texture Size: " + texture.width + " x " + texture.height);
                            Debug.Log("CheckHierarchyScript: Texture 2D: " + texture2D != null);
                            Debug.Log("CheckHierarchyScript: Texture: " + texture2D);
                            Debug.Log("CheckHierarchyScript: Texture Filter: " + texture2D.filterMode);
                            Debug.Log("CheckHierarchyScript: Texture Format: " + texture2D.format);

                            // Assign the Texture to the Render pane and the comparison pane
                            if (cameraRenderingPlane != null)
                            {
                                cameraRenderingPlane.GetComponent<MeshRenderer>().material.SetTexture(1, transform.GetChild(0).GetChild(i).GetComponent<MeshRenderer>().material.GetTexture("_WorldTex"));
                            }
                            if (textureComparisonPlane != null)
                            {
                                textureComparisonPlane.GetComponent<MeshRenderer>().material.SetTexture(1, transform.GetChild(0).GetChild(i).GetComponent<MeshRenderer>().material.GetTexture("_WorldTex"));
                            }


                            readRenderTexture.isNotReady = false;
                            // We mark once we have retrieved the texture so we never attempt it again
                            stop = true;
                        }
                    }
                }
            }
        }
    }
}
