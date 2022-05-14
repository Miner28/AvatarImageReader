from PIL import Image


def readInformation():
    img = Image.open("output.png")

    byte_list = bytearray([])
    index = 0

    byte_length = 0
    for y in range(img.height):
        for x in reversed(range(img.width)):
            pixel = img.getpixel((x, y))


            if byte_length == 0:
                pixel = list(pixel)
                print(pixel)
                byte_length = int.from_bytes(pixel, 'big')
            else:
                byte_list.append(pixel[0])
                index = index + 1
                if index >= byte_length:
                    return byte_list

                byte_list.append(pixel[1])
                index = index + 1
                if index >= byte_length:
                    return byte_list

                byte_list.append(pixel[2])
                index = index + 1
                if index >= byte_length:
                    return byte_list

                byte_list.append(pixel[3])
                index = index + 1
                if index >= byte_length:
                    return byte_list

    return byte_list


output = readInformation()
index = 0
# for b in output:
#     print(str(index) + " : " + str(b))
#     index = index + 1

print("-----")
avi = output[0:16]
print(list(avi))
avi = avi.hex()
output = output[16:]
print(f"Found avatar-id (ENCODED): {avi}")

output = output.decode('utf-16')

print(f"Decoded string {[output]} which contains {len(output)} chars")
