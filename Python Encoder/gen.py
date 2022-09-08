from PIL import Image
import codecs


version = [3, 0]
def encodeUTF8Text(input:str, avatar:str = ""):
    textbyte_list = list(bytearray(input.encode('utf-8')))

    textbyte_list = [0,0,0,0] + [0] + version + [1] + textbyte_list

    if avatar != "":
        avatar = avatar.replace("avtr_", "")
        avatar = avatar.replace("-", "")
        f = list(bytes.fromhex(avatar))
        textbyte_list = f + textbyte_list
    else:
        f = [255] * 16
        textbyte_list = f + textbyte_list

    totalBytes = len(textbyte_list).to_bytes(4, 'big')

    if len(textbyte_list) % 4 != 0:
        textbyte_list.append(16)

    if len(textbyte_list) % 4 != 0:
        textbyte_list.append(16)

    if len(textbyte_list) % 4 != 0:
        textbyte_list.append(16)

    image_width = 128
    image_height = 96

    img = Image.new("RGBA", (image_width, image_height), (255, 255, 255, 255))

    img.putpixel((img.width - 1, 0), (totalBytes[0], totalBytes[1], totalBytes[2], totalBytes[3]))

    for x in range(1, len(textbyte_list) // 4 + 1):
        img.putpixel((((img.width - 1) - x) % img.width, x // img.width),
                     (textbyte_list[(x - 1) * 4], textbyte_list[(x - 1) * 4 + 1], textbyte_list[(x - 1) * 4 + 2], textbyte_list[(x - 1) * 4 + 3]))
    img.save("img.png")



encodeUTF8Text("Test 123456789", "")
#encodeUTF16Text("Test TEst ")
# Or to be used as extension of discord bot.
