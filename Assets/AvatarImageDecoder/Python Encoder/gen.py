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

encodeUTF16Text("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
# Or to be used as extension of discord bot.
