Download: https://github.com/Miner28/AvatarImageReader/releases/

Demo world: https://vrchat.com/home/launch?worldId=wrld_b906fef2-9c90-463a-bb7b-23d187ccdffe&instanceId=0

# Warning: This documentation is for the (currently) unreleased new version of AvatarImageReader. For documentation to the current release, please look at the [old documentation](https://github.com/Miner28/AvatarImageReader/tree/96751f6b3ccd6fb95d401213f5a34c64a7a50de8).

# AvatarImageReader
Allows you to encode text into image and then read it in VRChat. Uses Avatar Images to store and load images.
Works on both PC and Quest

![image](https://user-images.githubusercontent.com/24632962/149594640-bf687e49-7c29-40b2-82d1-1d378bf50477.png)

Made by: @GlitchyDev, @Miner28 and @BocuD

Special help: @lox9973 for Quest support

Special Thanks: @Merlin for making UdonSharp making any of this possible

## Update 2.0
- Now supports Alpha encoding which allows 33% increase in data storage
- Allows multiple avatars to be queued up for decoding. Theoretical unlimited text storage!
- Added public discord bot for linking and automatic uploading of users. [Invite to your server](https://discord.com/api/oauth2/authorize?client_id=938573401201721425&permissions=2147600448&scope=bot%20applications.commands)

### Example use cases
Dynamically updated calendar

Any text in your world that you want to edit without re-uploading

## Requirements
- VRCSDK 3 2021.06.03 or later (earlier versions are untested but may work)
- Udon Sharp v0.19.12 or later (earlier versions are untested but may work) https://github.com/MerlinVR/UdonSharp

## Documentation
### Setup
To set up a new AvatarImageReader, use the menu item `Tools/AvatarImageReader/Create Image Reader`. For easy testing you should probably pick the option with a TMP. The next step is linking an avatar to the system. To do so, click Set Avatar. This will open up a window from where you can select an avatar to use. Keep in mind that the avatar should be uploaded at least once for both PC and Quest. To encode text for your pedestal, use the inspector tool and click Encode. Using the Upload Image to Avatar button will upload your changes to the avatar. If you now launch a local test client, your text should load in game.

### Updating text
The easiest way to update the text contents is to just use the inspector for an existing pedestal. You can change the text in here, reencode and then reupload. The menu item `Tools/Avatar Image Encoder` can also be used. To create your own custom encoder system, you can use AvatarImageReader/C# Encoder/AvatarImageEncoder.cs as reference. As an alternative a python encoder is also provided. To upload an encoded image VRChat Api Tools, VRCX or AvatarImageUploader (included) can be used.

### Getting decoded text
AvatarImagePrefab contains a public variable `outputString` that will contain the encoded text once it has been decoded. To be sure that decoding has been completed you should use the option `Send Custom Event` on finish and send an event to whatever UdonBehaviour you want to access the output string with.

### Max Image Size by Platform
- PC: 1200x900 -> 4,320,000 Bytes (4.3MB) or 2,160,000 UTF16 characters
- Quest: 128x96 -> 49,152 Bytes (49.2KB) or 24,576 UTF16 characters
- Chained: Unlimited
