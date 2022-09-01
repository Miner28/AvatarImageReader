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
version_data = output[0:4]
output = output[4:]
print(f"Found avatar-id (ENCODED): {avi}")
print(f"V{version_data[1]}.{version_data[2]} Encoder: {version_data[3]} Mode: {version_data[0]}")

output = output.decode('utf-8')

print(f"Decoded string {[output]} which contains {len(output)} chars")
