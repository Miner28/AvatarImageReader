//
// Copyright 2014-2015 Amazon.com, 
// Inc. or its affiliates. All Rights Reserved.
// 
// Licensed under the AWS Mobile SDK For Unity 
// Sample Application License Agreement (the "License"). 
// You may not use this file except in compliance with the 
// License. A copy of the License is located 
// in the "license" file accompanying this file. This file is 
// distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, express or implied. See the License 
// for the specific language governing permissions and 
// limitations under the License.
//

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.IO;
using System;
using Amazon.S3.Util;
using System.Collections.Generic;
using Amazon.CognitoIdentity;
using Amazon;

public class S3Manager : MonoBehaviour
{
	public string IdentityPoolId = "us-east-1:066cd25b-b249-4394-a267-7a49247aa8f9";   // vrchat identity pool id
	public string CognitoIdentityRegion = RegionEndpoint.USEast1.SystemName;
	private RegionEndpoint _CognitoIdentityRegion
	{
		get { return RegionEndpoint.GetBySystemName(CognitoIdentityRegion); }
	}
	public string S3Region = RegionEndpoint.USEast1.SystemName;
	private RegionEndpoint _S3Region
	{
		get { return RegionEndpoint.GetBySystemName(S3Region); }
	}
	public string S3BucketName = "vrc-uploads";   // vrchat


	private IAmazonS3 _s3Client;
	private AWSCredentials _credentials;

	private AWSCredentials Credentials
	{
		get
		{
			if (_credentials == null)
				_credentials = new CognitoAWSCredentials(IdentityPoolId, _CognitoIdentityRegion);
			return _credentials;
		}
	}

	private IAmazonS3 Client
	{
		get
		{
			if (_s3Client == null)
			{
				_s3Client = new AmazonS3Client(Credentials, _S3Region);
			}
			//test comment
			return _s3Client;
		}
	}

	void Start()
	{
		UnityInitializer.AttachToGameObject(this.gameObject);
	}

	/// <summary>
	/// Post Object to S3 Bucket. 
	/// </summary>
	public PostObjectRequest PostObject(string filePath, string s3FolderName, Action<string> onSuccess = null)
	{
		string fileName = s3FolderName + "/" + System.IO.Path.GetFileName(filePath);
		VRC.Core.Logger.Log ("uploading " + fileName, VRC.Core.DebugLevel.All);

        AWSConfigs.LoggingConfig.LogTo = LoggingOptions.None;

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

		VRC.Core.Logger.Log ("Creating request object", VRC.Core.DebugLevel.All);

        var request = new PostObjectRequest()
		{
			Bucket = S3BucketName,
			Key = fileName,
			InputStream = stream,
			CannedACL = S3CannedACL.Private
		};

		VRC.Core.Logger.Log ("Making HTTP post call", VRC.Core.DebugLevel.All);

		StartCoroutine(PostObjectRoutine(request, onSuccess));

		return request;
	}

	IEnumerator PostObjectRoutine(PostObjectRequest request, Action<string> onSuccess)
	{
		// make sure UnityInitializer call has time to initialize
		yield return null;
		yield return null;

		Client.PostObjectAsync(request, (responseObj) =>
			{
				if (responseObj.Exception == null)
				{
					VRC.Core.Logger.Log("object " + responseObj.Request.Key + " posted to bucket " + responseObj.Request.Bucket, VRC.Core.DebugLevel.All);
					string s3Url = string.Format("https://s3-us-west-2.amazonaws.com/{0}/{1}", responseObj.Request.Bucket, responseObj.Request.Key);
					if(onSuccess != null)
						onSuccess(s3Url);
				}
				else
				{
					VRC.Core.Logger.Log ("Exception while posting the result object");
					VRC.Core.Logger.Log("receieved error " + responseObj.Response.HttpStatusCode.ToString());
				}
			});
	}
}
