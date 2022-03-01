from PIL import Image
import codecs


def encodeUTF16Text(input:str, avatar:str = ""):
    textbyte_list = list(bytearray(input.encode('utf-16')))
    textbyte_list.pop(0)
    textbyte_list.pop(0)

    if avatar != "":
        avatar = avatar.replace("avtr_", "")
        avatar = avatar.replace("-", "")
        f = list(bytes.fromhex(avatar))
        print(f)
        textbyte_list = f + textbyte_list
    else:
        f = [255] * 16
        textbyte_list = f + textbyte_list

    totalBytes = len(textbyte_list).to_bytes(3, 'big')

    if len(textbyte_list) % 4 != 0:
        textbyte_list.append(16)

    if len(textbyte_list) % 4 != 0:
        textbyte_list.append(16)

    if len(textbyte_list) % 4 != 0:
        textbyte_list.append(16)

    image_width = 128
    image_height = 96

    img = Image.new("RGBA", (image_width, image_height), (255, 255, 255, 255))

    img.putpixel((img.width - 1, 0), (totalBytes[0], totalBytes[1], totalBytes[2], 255))
    print(f"A {len(textbyte_list)} {list(totalBytes)}")
    print(f"B {(totalBytes[0], totalBytes[1], totalBytes[2])}")

    for x in range(1, len(textbyte_list) // 4 + 1):
        img.putpixel((((img.width - 1) - x) % img.width, x // img.width),
                     (textbyte_list[(x - 1) * 4], textbyte_list[(x - 1) * 4 + 1], textbyte_list[(x - 1) * 4 + 2], textbyte_list[(x - 1) * 4 + 3]))
    print([textbyte_list])
    img.save("img.png")


encodeUTF16Text("123 456 this pain is 789", "avtr_2a6ba4c8-866a-4636-bdff-de429861df43")
#encodeUTF16Text("Test TEst ")
# Or to be used as extension of discord bot.
