from PIL import Image;


def readInformation():
    img = Image.open("img.png")

    byte_list = bytearray([])
    index = 0

    byte_length = 0
    for y in range(img.height):
        for x in reversed(range(img.width)):
            pixel = img.getpixel((x, y))

            if byte_length == 0:
                byte_length = int.from_bytes(pixel, 'big')
                print(byte_length)
                print(pixel)
            else:
                byte_list.append(pixel[0])
                index = index + 1;
                if index >= byte_length:
                    return byte_list;

                byte_list.append(pixel[1])
                index = index + 1;
                if index >= byte_length:
                    return byte_list;

                byte_list.append(pixel[2])
                index = index + 1;
                if index >= byte_length:
                    return byte_list;

    return byte_list


output = readInformation()
index = 0
for b in output:
    print(str(index) + " : " + str(b))
    index = index + 1

print("-----")
output = output.decode('utf-16')

print([output])
print(len(output))
