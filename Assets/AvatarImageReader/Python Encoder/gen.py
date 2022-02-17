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
        f = [255, 255] + f
        print(f)
        textbyte_list = f + textbyte_list

    totalBytes = len(textbyte_list).to_bytes(3, 'big')

    if len(textbyte_list) % 3 != 0:
        textbyte_list.append(16)

    if len(textbyte_list) % 3 != 0:
        textbyte_list.append(16)

    image_width = 128
    image_height = 96

    img = Image.new("RGB", (image_width, image_height), (255, 255, 255))

    img.putpixel((img.width - 1, 0), (totalBytes[0], totalBytes[1], totalBytes[2]))
    print(f"A {len(textbyte_list)} {totalBytes}")
    print(f"B {(totalBytes[0], totalBytes[1], totalBytes[2])}")

    for x in range(1, len(textbyte_list) // 3 + 1):
        img.putpixel((((img.width - 1) - x) % img.width, x // img.width),
                     (textbyte_list[(x - 1) * 3], textbyte_list[(x - 1) * 3 + 1], textbyte_list[(x - 1) * 3 + 2]))
    print([textbyte_list])
    img.save("img.png")


encodeUTF16Text("$ů2312řěů§¨ů¨ů..", "avtr_2a6ba4c8-866a-4636-bdff-de429861df43")
# Or to be used as extension of discord bot.
