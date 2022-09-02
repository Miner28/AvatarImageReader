Want to see it in action ? Visit the [**Demo world**](https://vrchat.com/home/launch?worldId=wrld_b906fef2-9c90-463a-bb7b-23d187ccdffe&instanceId=0)

**Want to upload automated membership lists from your Discord server?** [**Invite to your Server**](https://discord.com/api/oauth2/authorize?client_id=938573401201721425&permissions=2147600448&scope=bot%20applications.commands)

<div>

# AvatarImageReader [![GitHub](https://img.shields.io/github/license/Miner28/AvatarImageReader?color=blue&label=License&style=flat)](https://github.com/Miner28/AvatarImageReader/blob/main/LICENSE) [![GitHub Repo stars](https://img.shields.io/github/stars/Miner28/AvatarImageReader?style=flat&label=Stars)](https://github.com/Miner28/AvatarImageReader/stargazers) [![GitHub all releases](https://img.shields.io/github/downloads/Miner28/AvatarImageReader/total?color=blue&label=Downloads&style=flat)](https://github.com/Miner28/AvatarImageReader/releases) [![GitHub tag (latest SemVer)](https://img.shields.io/github/v/tag/Miner28/AvatarImageReader?color=blue&label=Release&sort=semver&style=flat)](https://github.com/Miner28/AvatarImageReader/releases/latest)

</div>

Allows you to encode text into images and then read it in VRChat. Uses Avatar Thumbnail Images to store and load images.
Works on both PC and Quest.

![image](https://user-images.githubusercontent.com/1560327/187653313-d2637fe7-3f32-468f-8168-f8411f34843e.png)

Made by: [@GlitchyDev](https://github.com/GlitchyDev), [@Miner28](https://github.com/Miner28), [@BocuD](https://github.com/BocuD) and [@Varneon](https://github.com/Varneon)

Special help: [@lox9973](https://github.com/lox9973) for Quest support

Special Thanks: [@Merlin](https://github.com/MerlinVR) for making UdonSharp making any of this possible

# Installation

1. In the Unity toolbar, select `Window` > `Package Manager` > `[+]` > `Add package from git URL...` 
2. Paste the following link: `https://github.com/Miner28/AvatarImageReader.git`

## Update 3.0
- UTF-8 Support (upto 2x increase in maximum text storage)
- Improvements to decoding process resulting in faster decoding

## Update 2.0
- Now supports Alpha encoding which allows 33% increase in data storage
- Allows multiple avatars to be queued up for decoding. Theoretical unlimited text storage!
- Added public discord bot for linking and automatic uploading of users with specific roles. [Invite to your server](https://discord.com/api/oauth2/authorize?client_id=938573401201721425&permissions=2147600448&scope=bot%20applications.commands)

### Example use cases
- Dynamically updated calendar/events
- Any text in your world that you want to edit without re-uploading
- Automated Supporter board/Supporter access
- Persistant world settings (via custom discord bot)

## Requirements
- VRCSDK 3.1.6 or later - [Download using VRChat's Creator Companion](https://vcc.docs.vrchat.com/guides/getting-started/)
- UdonSharp 1.0 - [Download using VRChat's Creator Companion](https://vcc.docs.vrchat.com/guides/getting-started/)
- [VRChatApiTools](https://github.com/BocuD/VRChatApiTools#installation-via-unity-package-manager-git-recommended)

## Documentation
### Setup
To set up a new AvatarImageReader, use the menu item `Tools/AvatarImageReader/Create Image Reader`. For easy testing you should probably pick the option with a TMP. The next step is linking an avatar to the system. To do so, click Set Avatar. This will open up a window from where you can select an avatar to use. Keep in mind that the avatar should be uploaded at least once. To encode text for your pedestal, use the inspector tool and click Encode. Using the Upload Image to Avatar button will upload your changes to the avatar. If you now launch a local test client, your text should load in game.

### Updating text
The easiest way to update the text contents is to just use the inspector for an existing pedestal. You can change the text in here, reencode and then reupload. The menu item `Tools/Avatar Image Encoder` can also be used. Alternatively you can also use a discord bot that will provide you with avatar-id and automatically update the avatar image periodically with latest data. To create your own custom encoder system, you can use AvatarImageReader/C# Encoder/AvatarImageEncoder.cs as reference. As an alternative a python encoder is also provided.

### Getting decoded text
RuntimeDecoder contains a public variable `outputString` that will contain the encoded text once it has been decoded. To be sure that decoding has been completed you should use the option `Send Custom Event` on finish and send an event to whatever UdonBehaviour you want to access the output string with.

### Max Image Size by Platform
| Platform | Resolution | Bytes | Size | Characters (UTF-8) |
| - | - | - | - | - |
| PC | 1200 x 900 | 4,320,000 | 4.3MB | 1,080,000 - 4,320,000 |
| Android | 128 x 96 | 49,152 | 49.2KB | 12,288 - 49,152 |

> Chaining avatars has theoretically unlimited capacity, but will lead to increased CPU load upon joining the world
