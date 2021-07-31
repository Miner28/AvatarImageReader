from PIL import Image



def encodeUTF16Text(input):
    textbyte_list = bytearray(input.encode('utf-16'))
    totalBytes = len(textbyte_list).to_bytes(3, 'big')

    if len(textbyte_list) % 3 != 0:
        textbyte_list.append(16)

    if len(textbyte_list) % 3 != 0:
        textbyte_list.append(16)


    index = 0;
    for x in textbyte_list:
        print(str(index) + " : " + str(x))
        index = index + 1
    image_width = 128
    image_height = 96

    img = Image.new("RGB", [image_width, image_height], (255, 255, 255))

    img.putpixel((img.width - 1, 0), (totalBytes[0], totalBytes[1], totalBytes[2]))
    print("A" + str(len(textbyte_list)) + " " + str(totalBytes))
    print("B" + str((totalBytes[0], totalBytes[1], totalBytes[2])))

    for x in range(1, len(textbyte_list) // 3 + 1):
        img.putpixel((((img.width - 1) - x) % img.width, x // img.width),
                     (textbyte_list[(x - 1) * 3], textbyte_list[(x - 1) * 3 + 1], textbyte_list[(x - 1) * 3 + 2]))

    img.save("img.png")

var = """EducatedMoose
RoseRedRuby
vNightmare I
Kain9wolfy
Peellz
DraconicGamer
WildPanda
YuriThorngage
Windisprite
RCNFL
-Greaver-
panedroppa
JaceCastor
Vulpi Somniatis
TyrNyanNog
Speidy674
poic100
Kisee
Wolf_Rawr
Jailer313
DQSHoovy
LofisMess
oreoyo
Saiyahime
aurumtiger
chaoticlila
ByakkoTamashii
․Lily UwU≺3
FIEARtheWolfdog
Ky_Za
xDasone
pham1440
SilentFoxScream
AmmoZilla
ThunderPanda2
Windows98SE
Bittersweetluv
Bratzzz
VercidiaBlack
AlexanRose
Shanester511 bf8e
RabbitRobot
Paul c36a
Spagano
HMDiamonds
ericsplode124 832e
BroMan03MLG
GasmisenFeik
YoshiYMG
Spxtღ
MessySnowFox
Sans the Skeleton f3b4
BagOfAir
LilMutton
Dark --
MoonJellyFish_boy
Zucrander
KetsuiniHitori
MommyMeg
Chaos_reaper_23
FisherMan4001
DragonsouLx5356
§σlσ․
Teddybeardemon
Makoto_Renard
Matesoul
CandyUwO
L00tSl00t
Mr TrashPanda
Badusername
Majorcare
AzulsCrown
SqueaksX․O․
gutenabent
Yui Tempest
"LUBE'"
_Ignatius
squidkiller
TTV_LoneWolfPak
Moonstone 53a9
SwooSh_100 2d3a
ItzToasty
Speedsterz
Alzetic
DAYVQ
NpMaster
WinterWolf965
Junkrat_In_Vr
OMG_Its_Shoto
unicorn2778
Vixay
BarefootTea9772
Pallas_Shaizy
DaSquiggle
SaoJ05
Panapple
Gloe
Kvisten
GentleSnail
joeythegamer
Deviancy
sauneboy
kim-chiii
Guegh
Paper_Jester
W0LFH34D 8fa3
Stagger17
Pyrozen 4f04
"HaruDidn'tLike"
VikuraViolet
eli_lov.ser
BoshBosh
Xander884
discordchaos
Vampire_Kiro
MR_Robot396 5724
ActiveRecon
OcyMagi
Kaithewolf248
Rosey_Witch19
skullreaper300 62ec
BoughtenTech4 30ff
AlecInit
VGMA Asteroth
Ｔｅａ~Ｃｈａｎ
ShadowMaiden149
matthewdepoy 148b
RabidTanukiKun
ZeldaRose 980d
Draco_Wolfe c3f1
RedSpooder
xAlphaDarkWolfx
yungmarcy666
KartyMan76
PhilledGoblin
NoTloUD
°Killerswinner°
Bluesky3010
I_am_death
purplecatgames 7a09
MightySushi
TheWhiteKnight
Depression2022
Dinoangeldragon
AzKar.eXho823f
Celebrimbor651
vulpab
Lipseyftw d7a9
Dormammu67
LOUDEN13
sdasdas
crazzyfox
TheIrishBanana 26eb
belle970
I_am_X
Pallinum
Markquisador
Deantwo
Nighlock~
Dinosnake76
bfbpro866
Kyubey_Staring
TrapBunniBubles
redeyes2134
SealSAURUS
RTWalduin
DankMemeStash
shivasix1
fxxkitbeach 955d
little winona
pyack12882 778f
james22themute
Vacivus
Swedish boii
AlfieBG2020
MysticalBunny
jackiemoon
Funkmj Team YT
landonalway
Syro Moon
coolpuppy100200 8d42
Blonde Don
LichtMarv
LittleAngel_xx
Salutations!
ShadowolfCass
Swirl Kitten 
Captain-nat
Death2105
Dormammuba1b
HeyItzOreo
ENNAA
Silvermoon
Huntxter
IronDruid
coldfish_Nova17
"Luke'"
LauraGGG
Cinna_Mochi
popysnap
 Killrscorpion07
WitheredFetch1010 5877
MemeFromBeyond 
Callmecat2453
toga_himiko_1"""
encodeUTF16Text("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
# Or to be used as extension of discord bot.
